using System.Security.Claims;
using System.Text.Json;
using GymApp.Api.DTOs;
using GymApp.Api.Services;
using GymApp.Domain.Entities;
using GymApp.Domain.Enums;
using GymApp.Infra.Data;
using GymApp.Infra.Services;
using Microsoft.EntityFrameworkCore;

namespace GymApp.Api.Endpoints;

public static class BillingEndpoints
{
    public static void MapBillingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/billing").RequireAuthorization("AdminOrAbove");

        // GET /api/billing/status — current subscription state for this tenant
        group.MapGet("/status", async (AppDbContext db, TenantContext tenantCtx) =>
        {
            var tenant = await db.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantCtx.TenantId);

            if (tenant is null) return Results.NotFound();

            return Results.Ok(new SubscriptionStatusResponse(
                tenant.SubscriptionStatus,
                tenant.HasStudentAccess,
                tenant.IsInTrial,
                tenant.TrialDaysRemaining,
                tenant.IsInTrial ? tenant.CreatedAt.AddDays(tenant.TrialDays) : null,
                tenant.SubscriptionCurrentPeriodEnd,
                BillingUrl: null   // URL is only returned during setup
            ));
        });

        // POST /api/billing/setup — create AbacatePay customer + billing, returns payment URL
        group.MapPost("/setup", async (
            SetupBillingRequest req,
            AppDbContext db,
            TenantContext tenantCtx,
            ClaimsPrincipal principal,
            AbacatePayService abacatePay,
            IConfiguration config) =>
        {
            var tenant = await db.Tenants.FindAsync(tenantCtx.TenantId);
            if (tenant is null) return Results.NotFound();

            if (tenant.SubscriptionStatus == SubscriptionStatus.Active)
                return Results.Conflict("Assinatura já está ativa.");

            var adminName  = principal.FindFirstValue(ClaimTypes.Name)   ?? tenant.Name;
            var adminEmail = principal.FindFirstValue(ClaimTypes.Email)
                             ?? principal.FindFirstValue("email")
                             ?? string.Empty;

            // Reuse existing AbacatePay customer or create a new one
            if (string.IsNullOrEmpty(tenant.AbacatePayCustomerId))
            {
                var customer = await abacatePay.CreateCustomerAsync(
                    adminName, adminEmail, req.Phone, req.TaxId);

                if (customer is null)
                    return Results.Problem("Erro ao criar cliente no AbacatePay. Tente novamente.");

                tenant.AbacatePayCustomerId = customer.Id;
            }

            var baseUrl = config["App:BaseUrl"]?.TrimEnd('/') ?? "https://agendofy.com";
            var uri = new Uri(baseUrl);
            var panelUrl = $"{uri.Scheme}://{tenant.Slug}.{uri.Host}";

            var billing = await abacatePay.CreateBillingAsync(
                tenant.AbacatePayCustomerId, tenant.Slug, adminEmail, panelUrl);

            if (billing is null)
                return Results.Problem("Erro ao criar cobrança no AbacatePay. Tente novamente.");

            tenant.AbacatePayBillingId = billing.Id;
            await db.SaveChangesAsync();

            return Results.Ok(new { url = billing.Url });
        });

        // POST /api/billing/cancel — cancel the subscription
        group.MapPost("/cancel", async (
            AppDbContext db,
            TenantContext tenantCtx,
            AbacatePayService abacatePay) =>
        {
            var tenant = await db.Tenants.FindAsync(tenantCtx.TenantId);
            if (tenant is null) return Results.NotFound();

            if (tenant.SubscriptionStatus is SubscriptionStatus.Trial or SubscriptionStatus.Canceled)
                return Results.BadRequest("Nenhuma assinatura ativa para cancelar.");

            if (!string.IsNullOrEmpty(tenant.AbacatePayBillingId))
                await abacatePay.CancelBillingAsync(tenant.AbacatePayBillingId);

            tenant.SubscriptionStatus = SubscriptionStatus.Canceled;
            // Access remains until end of current period (already set)
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                message = "Assinatura cancelada. Acesso mantido até o fim do período atual.",
                accessUntil = tenant.SubscriptionCurrentPeriodEnd
            });
        });

        // ── Super admin: adjust trial days for a specific tenant ────────────
        var adminGroup = app.MapGroup("/api/admin/tenants").RequireAuthorization("SuperAdmin");

        adminGroup.MapPut("/{id:guid}/trial-days", async (
            Guid id, SetTrialDaysRequest req, AppDbContext db) =>
        {
            if (req.Days < 1 || req.Days > 365)
                return Results.BadRequest("Trial deve ser entre 1 e 365 dias.");

            var tenant = await db.Tenants.FindAsync(id);
            if (tenant is null) return Results.NotFound();

            tenant.TrialDays = req.Days;
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                tenantId = tenant.Id,
                trialDays = tenant.TrialDays,
                trialEndsAt = tenant.CreatedAt.AddDays(tenant.TrialDays)
            });
        });
    }

    // ── Webhook (public) ────────────────────────────────────────────────────
    public static void MapAbacatePayWebhook(this WebApplication app)
    {
        app.MapPost("/api/webhooks/abacatepay", async (
            HttpContext ctx,
            AppDbContext db,
            ILogger<AbacatePayService> logger) =>
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var payload = await reader.ReadToEndAsync();

            AbacatePayWebhookEvent? webhook;
            try
            {
                webhook = JsonSerializer.Deserialize<AbacatePayWebhookEvent>(payload,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "AbacatePay webhook: failed to deserialize payload");
                return Results.BadRequest();
            }

            if (webhook is null) return Results.BadRequest();

            var billingId  = webhook.Data?.Billing?.Id;
            var customerId = webhook.Data?.Billing?.CustomerId;

            Tenant? tenant = null;

            if (!string.IsNullOrEmpty(billingId))
                tenant = await db.Tenants.FirstOrDefaultAsync(t => t.AbacatePayBillingId == billingId);

            if (tenant is null && !string.IsNullOrEmpty(customerId))
                tenant = await db.Tenants.FirstOrDefaultAsync(t => t.AbacatePayCustomerId == customerId);

            if (tenant is null)
            {
                logger.LogWarning("AbacatePay webhook: tenant not found — billingId={B} customerId={C}",
                    billingId, customerId);
                return Results.Ok(); // 200 to avoid retries for unknown IDs
            }

            switch (webhook.Event)
            {
                case "billing.paid":
                    tenant.SubscriptionStatus = SubscriptionStatus.Active;
                    // Extend period by 1 month from now (or from last period end if in future)
                    var from = tenant.SubscriptionCurrentPeriodEnd.HasValue
                        && tenant.SubscriptionCurrentPeriodEnd.Value > DateTime.UtcNow
                            ? tenant.SubscriptionCurrentPeriodEnd.Value
                            : DateTime.UtcNow;
                    tenant.SubscriptionCurrentPeriodEnd = from.AddMonths(1);
                    if (!string.IsNullOrEmpty(billingId))
                        tenant.AbacatePayBillingId = billingId;
                    logger.LogInformation("Subscription activated for tenant {Slug}, period ends {End}",
                        tenant.Slug, tenant.SubscriptionCurrentPeriodEnd);
                    break;

                case "billing.expired":
                case "billing.failed":
                    tenant.SubscriptionStatus = SubscriptionStatus.PastDue;
                    logger.LogWarning("Subscription past due for tenant {Slug}", tenant.Slug);
                    break;

                case "billing.cancelled":
                    tenant.SubscriptionStatus = SubscriptionStatus.Canceled;
                    logger.LogInformation("Subscription cancelled for tenant {Slug}", tenant.Slug);
                    break;

                default:
                    logger.LogDebug("AbacatePay webhook: unhandled event {Event}", webhook.Event);
                    break;
            }

            await db.SaveChangesAsync();
            return Results.Ok();

        }).AllowAnonymous();
    }
}
