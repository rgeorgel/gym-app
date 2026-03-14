using GymApp.Api.DTOs;
using GymApp.Domain.Entities;
using GymApp.Domain.Interfaces;
using GymApp.Infra.Data;
using GymApp.Infra.Services;
using Microsoft.EntityFrameworkCore;

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
                tenant.Name, tenant.LogoUrl, tenant.PrimaryColor, tenant.SecondaryColor, tenant.Slug, tenant.Language));
        }).AllowAnonymous();

        // Super Admin: list all tenants
        var adminGroup = app.MapGroup("/api/admin/tenants").RequireAuthorization("SuperAdmin");

        adminGroup.MapGet("/", async (AppDbContext db) =>
        {
            var tenants = await db.Tenants.AsNoTracking()
                .OrderBy(t => t.Name)
                .Select(t => new TenantResponse(t.Id, t.Name, t.Slug, t.LogoUrl, t.PrimaryColor, t.SecondaryColor,
                    t.Plan, t.IsActive, t.CustomDomain, t.CreatedAt, t.PaymentsAllowedBySuperAdmin, t.PaymentsEnabled, t.EfiPayeeCode))
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
                Plan = req.Plan
            };
            db.Tenants.Add(tenant);

            var admin = new User
            {
                TenantId = tenant.Id,
                Name = req.AdminName,
                Email = req.AdminEmail.ToLowerInvariant(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.AdminPassword),
                Role = GymApp.Domain.Enums.UserRole.Admin
            };
            db.Users.Add(admin);

            await db.SaveChangesAsync();
            return Results.Created($"/api/admin/tenants/{tenant.Id}",
                new TenantResponse(tenant.Id, tenant.Name, tenant.Slug, tenant.LogoUrl,
                    tenant.PrimaryColor, tenant.SecondaryColor, tenant.Plan, tenant.IsActive,
                    tenant.CustomDomain, tenant.CreatedAt, tenant.PaymentsAllowedBySuperAdmin, tenant.PaymentsEnabled, tenant.EfiPayeeCode));
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
                tenant.CustomDomain, tenant.CreatedAt, tenant.PaymentsAllowedBySuperAdmin, tenant.PaymentsEnabled, tenant.EfiPayeeCode));
        });

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

            await db.SaveChangesAsync();
            return Results.Ok(new TenantResponse(tenant.Id, tenant.Name, tenant.Slug, tenant.LogoUrl,
                tenant.PrimaryColor, tenant.SecondaryColor, tenant.Plan, tenant.IsActive,
                tenant.CustomDomain, tenant.CreatedAt, tenant.PaymentsAllowedBySuperAdmin, tenant.PaymentsEnabled, tenant.EfiPayeeCode));
        });
    }

    private static TenantSettingsResponse ToSettingsResponse(Tenant t) =>
        new(t.DefaultPackageTemplateId, t.Language, t.EfiPayeeCode, t.PaymentsEnabled, t.PaymentsAllowedBySuperAdmin, t.PrimaryColor, t.SecondaryColor);

    private static string? ExtractSlug(string host)
    {
        var parts = host.Split('.');
        return parts.Length >= 3 ? parts[0] : null;
    }
}
