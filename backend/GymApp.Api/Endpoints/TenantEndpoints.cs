using GymApp.Api.DTOs;
using GymApp.Domain.Entities;
using GymApp.Domain.Interfaces;
using GymApp.Infra.Data;
using GymApp.Infra.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.RegularExpressions;

namespace GymApp.Api.Endpoints;

public static class TenantEndpoints
{
    public static void MapTenantEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tenant");

        // Public: get current tenant config (used by frontend for white label)
        group.MapGet("/config", async (HttpContext ctx, AppDbContext db) =>
        {
            var host = ctx.Request.Host.Host.ToLowerInvariant();

            // 1. Custom domain match (e.g. "app.boxeelite.com.br")
            var tenant = await db.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.CustomDomain == host && t.IsActive);

            // 2. Subdomain or header slug
            if (tenant is null)
            {
                var slug = ExtractSlug(host) ?? ctx.Request.Headers["X-Tenant-Slug"].FirstOrDefault();
                if (!string.IsNullOrEmpty(slug))
                    tenant = await db.Tenants.AsNoTracking()
                        .FirstOrDefaultAsync(t => t.Slug == slug && t.IsActive);
            }

            if (tenant is null) return Results.NotFound();

            return Results.Ok(new TenantConfigResponse(
                tenant.Name, tenant.LogoUrl, tenant.PrimaryColor, tenant.SecondaryColor, tenant.Slug, tenant.Language, tenant.TenantType,
                tenant.SocialInstagram, tenant.SocialFacebook, tenant.SocialWhatsApp, tenant.SocialWebsite, tenant.SocialTikTok));
        }).AllowAnonymous();

        // Public: self-signup from landing page
        app.MapPost("/api/public/signup", async (SelfSignupRequest req, AppDbContext db, IEmailService email, IConfiguration config) =>
        {
            if (string.IsNullOrWhiteSpace(req.AdminName) ||
                string.IsNullOrWhiteSpace(req.AcademyName) ||
                string.IsNullOrWhiteSpace(req.Email) ||
                string.IsNullOrWhiteSpace(req.Password))
                return Results.BadRequest("All fields are required.");

            var emailLower = req.Email.ToLowerInvariant().Trim();
            if (await db.Users.AnyAsync(u => u.Email == emailLower))
                return Results.Conflict("E-mail já cadastrado.");

            var baseSlug = GenerateSlug(req.AcademyName);
            var slug = baseSlug;
            var suffix = 2;
            while (await db.Tenants.AnyAsync(t => t.Slug == slug))
                slug = $"{baseSlug}-{suffix++}";

            // Resolve referral code
            Tenant? referrer = null;
            if (!string.IsNullOrWhiteSpace(req.ReferralCode))
                referrer = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == req.ReferralCode.Trim().ToLowerInvariant());

            var tenant = new Tenant
            {
                Name = req.AcademyName.Trim(),
                Slug = slug,
                PrimaryColor = "#1a1a2e",
                SecondaryColor = "#e94560",
                Plan = GymApp.Domain.Enums.TenantPlan.Basic,
                TenantType = req.TenantType,
                SubscriptionPriceCents = req.TenantType == GymApp.Domain.Enums.TenantType.BeautySalon ? 1900 : 4900,
                ReferredByCode = referrer?.Slug,
                ReferredByTenantId = referrer?.Id,
                TrialDays = referrer is not null ? 44 : 14  // +30 bonus days for referred tenants
            };
            db.Tenants.Add(tenant);

            var admin = new User
            {
                TenantId = tenant.Id,
                Name = req.AdminName.Trim(),
                Email = emailLower,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
                Phone = req.Phone,
                Role = GymApp.Domain.Enums.UserRole.Admin,
                ReceivesSubscriptionReminders = true
            };
            db.Users.Add(admin);

            db.Locations.Add(new Location
            {
                TenantId = tenant.Id,
                Name = tenant.Name,
                IsMain = true
            });

            await db.SaveChangesAsync();

            var baseUrl = config["App:BaseUrl"]?.TrimEnd('/') ?? "https://agendofy.com";
            var uri = new Uri(baseUrl);
            var panelUrl = $"{uri.Scheme}://{tenant.Slug}.{uri.Host}/admin/index.html";
            _ = email.SendWelcomeAsync(admin.Email, admin.Name, tenant.Name, panelUrl, isSalon: tenant.TenantType == GymApp.Domain.Enums.TenantType.BeautySalon);

            return Results.Created($"/api/admin/tenants/{tenant.Id}",
                new SelfSignupResponse(tenant.Id, tenant.Slug, admin.Email));
        }).AllowAnonymous();

        // Super Admin: list all tenants
        var adminGroup = app.MapGroup("/api/admin/tenants").RequireAuthorization("SuperAdmin");

        adminGroup.MapGet("/", async (AppDbContext db) =>
        {
            var demoTenantIds = await db.DemoSeedLogs
                .Select(l => l.TenantId).Distinct().ToListAsync();

            var tenants = await db.Tenants.AsNoTracking()
                .OrderBy(t => t.Name)
                .Select(t => new TenantResponse(t.Id, t.Name, t.Slug, t.LogoUrl, t.PrimaryColor, t.SecondaryColor,
                    t.Plan, t.IsActive, t.CustomDomain, t.CreatedAt, t.PaymentsAllowedBySuperAdmin, t.PaymentsEnabled, t.EfiPayeeCode, t.TenantType.ToString(), t.SubscriptionPriceCents,
                    demoTenantIds.Contains(t.Id)))
                .ToListAsync();
            return Results.Ok(tenants);
        });

        adminGroup.MapPost("/", async (CreateTenantRequest req, AppDbContext db) =>
        {
            if (await db.Tenants.AnyAsync(t => t.Slug == req.Slug))
                return Results.Conflict("Slug already in use.");

            var tenant = new Tenant
            {
                Name = req.Name,
                Slug = req.Slug.ToLowerInvariant(),
                LogoUrl = req.LogoUrl,
                PrimaryColor = req.PrimaryColor,
                SecondaryColor = req.SecondaryColor,
                Plan = req.Plan,
                TenantType = req.TenantType,
                SubscriptionPriceCents = req.TenantType == GymApp.Domain.Enums.TenantType.BeautySalon ? 1900 : 4900
            };
            db.Tenants.Add(tenant);

            var admin = new User
            {
                TenantId = tenant.Id,
                Name = req.AdminName,
                Email = req.AdminEmail.ToLowerInvariant(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.AdminPassword),
                Role = GymApp.Domain.Enums.UserRole.Admin,
                ReceivesSubscriptionReminders = true
            };
            db.Users.Add(admin);

            db.Locations.Add(new Location
            {
                TenantId = tenant.Id,
                Name = tenant.Name,
                IsMain = true
            });

            await db.SaveChangesAsync();
            return Results.Created($"/api/admin/tenants/{tenant.Id}",
                new TenantResponse(tenant.Id, tenant.Name, tenant.Slug, tenant.LogoUrl,
                    tenant.PrimaryColor, tenant.SecondaryColor, tenant.Plan, tenant.IsActive,
                    tenant.CustomDomain, tenant.CreatedAt, tenant.PaymentsAllowedBySuperAdmin, tenant.PaymentsEnabled, tenant.EfiPayeeCode, tenant.TenantType.ToString(), tenant.SubscriptionPriceCents));
        });

        // Super Admin: toggle payments allowed per tenant
        adminGroup.MapPut("/{id:guid}/payments-allowed", async (Guid id, SetPaymentsAllowedRequest req, AppDbContext db) =>
        {
            var tenant = await db.Tenants.FindAsync(id);
            if (tenant is null) return Results.NotFound();

            tenant.PaymentsAllowedBySuperAdmin = req.Allowed;
            await db.SaveChangesAsync();
            return Results.Ok(new TenantResponse(tenant.Id, tenant.Name, tenant.Slug, tenant.LogoUrl,
                tenant.PrimaryColor, tenant.SecondaryColor, tenant.Plan, tenant.IsActive,
                tenant.CustomDomain, tenant.CreatedAt, tenant.PaymentsAllowedBySuperAdmin, tenant.PaymentsEnabled, tenant.EfiPayeeCode, tenant.TenantType.ToString(), tenant.SubscriptionPriceCents));
        });

        // Admin: referral stats
        app.MapGet("/api/referral/stats", async (AppDbContext db, TenantContext tenantCtx) =>
        {
            var tenant = await db.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantCtx.TenantId);
            if (tenant is null) return Results.NotFound();

            var referrals = await db.Tenants.AsNoTracking()
                .Where(t => t.ReferredByTenantId == tenant.Id)
                .Select(t => new { t.ReferralRewardClaimed })
                .ToListAsync();

            return Results.Ok(new ReferralStatsResponse(
                ReferralCode: tenant.Slug,
                TotalReferrals: referrals.Count,
                ConvertedReferrals: referrals.Count(r => r.ReferralRewardClaimed)
            ));
        }).RequireAuthorization("AdminOrAbove");

        // Admin: tenant settings
        var settingsGroup = app.MapGroup("/api/settings").RequireAuthorization("AdminOrAbove");

        settingsGroup.MapGet("/", async (AppDbContext db, TenantContext tenantCtx) =>
        {
            var tenant = await db.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantCtx.TenantId);
            if (tenant is null) return Results.NotFound();
            return Results.Ok(ToSettingsResponse(tenant));
        });

        settingsGroup.MapPut("/default-package-template", async (SetDefaultTemplateRequest req, AppDbContext db, TenantContext tenantCtx) =>
        {
            var tenant = await db.Tenants.FindAsync(tenantCtx.TenantId);
            if (tenant is null) return Results.NotFound();

            if (req.TemplateId.HasValue)
            {
                var templateExists = await db.PackageTemplates.AnyAsync(t =>
                    t.Id == req.TemplateId.Value && t.TenantId == tenantCtx.TenantId);
                if (!templateExists) return Results.NotFound("Template not found.");
            }

            tenant.DefaultPackageTemplateId = req.TemplateId;
            await db.SaveChangesAsync();
            return Results.Ok(ToSettingsResponse(tenant));
        });

        settingsGroup.MapPut("/language", async (SetLanguageRequest req, AppDbContext db, TenantContext tenantCtx) =>
        {
            var allowed = new[] { "pt-BR", "en-US", "es-ES" };
            if (!allowed.Contains(req.Language)) return Results.BadRequest("Unsupported language.");

            var tenant = await db.Tenants.FindAsync(tenantCtx.TenantId);
            if (tenant is null) return Results.NotFound();

            tenant.Language = req.Language;
            await db.SaveChangesAsync();
            return Results.Ok(ToSettingsResponse(tenant));
        });

        settingsGroup.MapPut("/efi-payee-code", async (SetEfiPayeeCodeRequest req, AppDbContext db, TenantContext tenantCtx) =>
        {
            var tenant = await db.Tenants.FindAsync(tenantCtx.TenantId);
            if (tenant is null) return Results.NotFound();

            tenant.EfiPayeeCode = string.IsNullOrWhiteSpace(req.PayeeCode) ? null : req.PayeeCode.Trim();
            await db.SaveChangesAsync();
            return Results.Ok(ToSettingsResponse(tenant));
        });

        settingsGroup.MapPut("/payments", async (SetPaymentsEnabledRequest req, AppDbContext db, TenantContext tenantCtx) =>
        {
            var tenant = await db.Tenants.FindAsync(tenantCtx.TenantId);
            if (tenant is null) return Results.NotFound();

            // Tenant cannot enable payments if super admin has blocked it
            if (req.Enabled && !tenant.PaymentsAllowedBySuperAdmin)
                return Results.Forbid();

            tenant.PaymentsEnabled = req.Enabled;
            await db.SaveChangesAsync();
            return Results.Ok(ToSettingsResponse(tenant));
        });

        settingsGroup.MapPut("/logo", async (SetLogoUrlRequest req, AppDbContext db, TenantContext tenantCtx) =>
        {
            var tenant = await db.Tenants.FindAsync(tenantCtx.TenantId);
            if (tenant is null) return Results.NotFound();

            tenant.LogoUrl = string.IsNullOrWhiteSpace(req.LogoUrl) ? null : req.LogoUrl.Trim();
            await db.SaveChangesAsync();
            return Results.Ok(ToSettingsResponse(tenant));
        });

        settingsGroup.MapPut("/colors", async (SetColorsRequest req, AppDbContext db, TenantContext tenantCtx) =>
        {
            if (string.IsNullOrWhiteSpace(req.PrimaryColor) || string.IsNullOrWhiteSpace(req.SecondaryColor))
                return Results.BadRequest("Colors are required.");

            var tenant = await db.Tenants.FindAsync(tenantCtx.TenantId);
            if (tenant is null) return Results.NotFound();

            tenant.PrimaryColor = req.PrimaryColor.Trim();
            tenant.SecondaryColor = req.SecondaryColor.Trim();
            await db.SaveChangesAsync();
            return Results.Ok(ToSettingsResponse(tenant));
        });

        settingsGroup.MapPut("/abacatepay-student-api-key", async (SetAbacatePayStudentApiKeyRequest req, AppDbContext db, TenantContext tenantCtx) =>
        {
            var tenant = await db.Tenants.FindAsync(tenantCtx.TenantId);
            if (tenant is null) return Results.NotFound();

            tenant.AbacatePayStudentApiKey = string.IsNullOrWhiteSpace(req.ApiKey) ? null : req.ApiKey.Trim();
            await db.SaveChangesAsync();
            return Results.Ok(ToSettingsResponse(tenant));
        });

        settingsGroup.MapPut("/abacatepay-student-webhook-secret", async (SetAbacatePayStudentWebhookSecretRequest req, AppDbContext db, TenantContext tenantCtx) =>
        {
            var tenant = await db.Tenants.FindAsync(tenantCtx.TenantId);
            if (tenant is null) return Results.NotFound();

            tenant.AbacatePayStudentWebhookSecret = string.IsNullOrWhiteSpace(req.Secret) ? null : req.Secret.Trim();
            await db.SaveChangesAsync();
            return Results.Ok(ToSettingsResponse(tenant));
        });

        settingsGroup.MapPut("/tenant-type", async (SetTenantTypeRequest req, AppDbContext db, TenantContext tenantCtx) =>
        {
            var tenant = await db.Tenants.FindAsync(tenantCtx.TenantId);
            if (tenant is null) return Results.NotFound();

            tenant.TenantType = req.TenantType;
            await db.SaveChangesAsync();
            return Results.Ok(ToSettingsResponse(tenant));
        }).RequireAuthorization("SuperAdmin");

        settingsGroup.MapPut("/social-links", async (SetSocialLinksRequest req, AppDbContext db, TenantContext tenantCtx) =>
        {
            var tenant = await db.Tenants.FindAsync(tenantCtx.TenantId);
            if (tenant is null) return Results.NotFound();

            tenant.SocialInstagram = string.IsNullOrWhiteSpace(req.Instagram) ? null : req.Instagram.Trim();
            tenant.SocialFacebook  = string.IsNullOrWhiteSpace(req.Facebook)  ? null : req.Facebook.Trim();
            tenant.SocialWhatsApp  = string.IsNullOrWhiteSpace(req.WhatsApp)  ? null : req.WhatsApp.Trim();
            tenant.SocialWebsite   = string.IsNullOrWhiteSpace(req.Website)   ? null : req.Website.Trim();
            tenant.SocialTikTok    = string.IsNullOrWhiteSpace(req.TikTok)    ? null : req.TikTok.Trim();
            await db.SaveChangesAsync();
            return Results.Ok(ToSettingsResponse(tenant));
        });

        adminGroup.MapPut("/{id:guid}", async (Guid id, UpdateTenantRequest req, AppDbContext db) =>
        {
            var tenant = await db.Tenants.FindAsync(id);
            if (tenant is null) return Results.NotFound();

            tenant.Name = req.Name;
            tenant.LogoUrl = req.LogoUrl;
            tenant.PrimaryColor = req.PrimaryColor;
            tenant.SecondaryColor = req.SecondaryColor;
            tenant.CustomDomain = string.IsNullOrWhiteSpace(req.CustomDomain) ? null : req.CustomDomain.ToLowerInvariant().Trim();
            tenant.IsActive = req.IsActive;
            if (req.TenantType.HasValue) tenant.TenantType = req.TenantType.Value;

            await db.SaveChangesAsync();
            return Results.Ok(new TenantResponse(tenant.Id, tenant.Name, tenant.Slug, tenant.LogoUrl,
                tenant.PrimaryColor, tenant.SecondaryColor, tenant.Plan, tenant.IsActive,
                tenant.CustomDomain, tenant.CreatedAt, tenant.PaymentsAllowedBySuperAdmin, tenant.PaymentsEnabled, tenant.EfiPayeeCode, tenant.TenantType.ToString(), tenant.SubscriptionPriceCents));
        });
    }

    private static TenantSettingsResponse ToSettingsResponse(Tenant t) =>
        new(t.DefaultPackageTemplateId, t.Language, t.EfiPayeeCode, t.PaymentsEnabled, t.PaymentsAllowedBySuperAdmin, t.PrimaryColor, t.SecondaryColor, t.LogoUrl,
            HasAbacatePayStudentApiKey: !string.IsNullOrEmpty(t.AbacatePayStudentApiKey),
            HasAbacatePayStudentWebhookSecret: !string.IsNullOrEmpty(t.AbacatePayStudentWebhookSecret),
            TenantType: t.TenantType,
            SocialInstagram: t.SocialInstagram,
            SocialFacebook: t.SocialFacebook,
            SocialWhatsApp: t.SocialWhatsApp,
            SocialWebsite: t.SocialWebsite,
            SocialTikTok: t.SocialTikTok);

    private static string GenerateSlug(string name)
    {
        var normalized = name.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        var clean = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        clean = Regex.Replace(clean, @"[^a-z0-9\s-]", "");
        clean = Regex.Replace(clean, @"\s+", "-");
        clean = Regex.Replace(clean, @"-{2,}", "-").Trim('-');
        return string.IsNullOrEmpty(clean) ? "academia" : clean;
    }

    private static string? ExtractSlug(string host)
    {
        var parts = host.Split('.');
        return parts.Length >= 3 ? parts[0] : null;
    }
}
