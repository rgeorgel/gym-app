using System.Security.Claims;
using System.Text.Json;
using GymApp.Api.DTOs;
using GymApp.Api.Helpers;
using GymApp.Api.Services;
using GymApp.Domain.Entities;
using GymApp.Domain.Enums;
using GymApp.Infra.Data;
using GymApp.Infra.Services;
using Microsoft.EntityFrameworkCore;

namespace GymApp.Api.Endpoints;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this WebApplication app)
    {
        // Student-facing store: list purchasable plans
        app.MapGet("/api/store/plans", async (AppDbContext db, TenantContext tenant) =>
        {
            var paymentsActive = await db.Tenants.AsNoTracking()
                .Where(t => t.Id == tenant.TenantId)
                .Select(t => t.PaymentsEnabled && t.PaymentsAllowedBySuperAdmin)
                .FirstOrDefaultAsync();

            if (!paymentsActive) return Results.Ok(Array.Empty<object>());

            var templates = await db.PackageTemplates.AsNoTracking()
                .Include(t => t.Items).ThenInclude(i => i.ClassType)
                .Where(t => t.TenantId == tenant.TenantId && t.IsVisibleInStore)
                .OrderBy(t => t.Name)
                .ToListAsync();

            var response = templates.Select(t =>
            {
                var totalPrice = t.Items.Sum(i => i.TotalCredits * i.PricePerCredit);
                return new StorePlanResponse(
                    t.Id, t.Name, t.DurationDays, totalPrice,
                    t.Items.Select(i => new StorePlanItem(
                        i.ClassType.Name, i.ClassType.Color,
                        i.TotalCredits, i.PricePerCredit
                    )).ToList()
                );
            });

            return Results.Ok(response);
        }).RequireAuthorization("AnyUser");

        // Student: initiate purchase — creates AbacatePay billing
        app.MapPost("/api/payments/checkout", async (
            CheckoutRequest req,
            AppDbContext db,
            TenantContext tenantCtx,
            AbacatePayService abacatePay,
            IConfiguration config,
            ClaimsPrincipal user) =>
        {
            var tenantRecord = await db.Tenants.FindAsync(tenantCtx.TenantId);

            if (tenantRecord is null || !tenantRecord.PaymentsActive)
                return Results.Problem("Payments are not enabled for this gym.", statusCode: 403);

            if (string.IsNullOrEmpty(tenantRecord.AbacatePayStudentApiKey))
                return Results.Problem("Payment gateway not configured for this gym.", statusCode: 503);

            var studentId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var template = await db.PackageTemplates.AsNoTracking()
                .Include(t => t.Items)
                .FirstOrDefaultAsync(t => t.Id == req.PackageTemplateId && t.TenantId == tenantCtx.TenantId);

            if (template is null) return Results.NotFound("Plan not found.");

            var amount = template.Items.Sum(i => i.TotalCredits * i.PricePerCredit);
            if (amount <= 0) return Results.BadRequest("Plan has no price configured.");

            var studentUser = await db.Users.FindAsync(studentId);
            if (studentUser is null) return Results.NotFound("Student not found.");

            var apiKey = tenantRecord.AbacatePayStudentApiKey;

            // Ensure the student has an AbacatePay customer in this tenant's account
            if (string.IsNullOrEmpty(studentUser.AbacatePayCustomerId))
            {
                var customer = await abacatePay.CreateStudentCustomerAsync(apiKey, studentUser.Name, studentUser.Email);
                if (customer is null)
                    return Results.Problem("Failed to create customer in payment gateway.", statusCode: 502);
                studentUser.AbacatePayCustomerId = customer.Id;
                await db.SaveChangesAsync();
            }

            var baseUrl = config["App:BaseUrl"]?.TrimEnd('/') ?? "https://agendofy.com";
            var uri = new Uri(baseUrl);
            var returnUrl = $"{uri.Scheme}://{tenantRecord.Slug}.{uri.Host}/app/index.html#my-packages";

            var priceCents = (int)Math.Round(amount * 100);
            var billing = await abacatePay.CreateStudentBillingAsync(
                apiKey, studentUser.AbacatePayCustomerId,
                template.Name, studentUser.Name, priceCents, returnUrl);

            if (billing is null)
                return Results.Problem("Failed to create payment.", statusCode: 502);

            var payment = new Payment
            {
                TenantId = tenantCtx.TenantId,
                StudentId = studentId,
                PackageTemplateId = template.Id,
                Amount = amount,
                AbacatePayBillingId = billing.Id,
                AbacatePayBillingUrl = billing.Url,
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            };

            db.Payments.Add(payment);
            await db.SaveChangesAsync();

            return Results.Ok(new CheckoutResponse(payment.Id, amount, billing.Url, payment.ExpiresAt));
        }).RequireAuthorization("AnyUser");

        // Student: poll payment status
        app.MapGet("/api/payments/{id:guid}/status", async (
            Guid id,
            AppDbContext db,
            TenantContext tenant,
            ClaimsPrincipal user) =>
        {
            var studentId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var payment = await db.Payments.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenant.TenantId && p.StudentId == studentId);

            if (payment is null) return Results.NotFound();

            // Auto-expire if past expiry and still pending
            if (payment.Status == PaymentStatus.Pending && payment.ExpiresAt < DateTime.UtcNow)
            {
                var tracked = await db.Payments.FindAsync(id);
                if (tracked is not null)
                {
                    tracked.Status = PaymentStatus.Expired;
                    await db.SaveChangesAsync();
                }
                return Results.Ok(new PaymentStatusResponse(id, PaymentStatus.Expired, null, null));
            }

            return Results.Ok(new PaymentStatusResponse(
                payment.Id, payment.Status, payment.PaidAt, payment.AssignedPackageId));
        }).RequireAuthorization("AnyUser");

        // AbacatePay webhook — called when a student payment is confirmed
        app.MapPost("/api/payments/webhook/abacatepay", async (
            HttpContext ctx,
            AppDbContext db,
            TenantContext tenantCtx,
            ILogger<AbacatePayService> logger) =>
        {
            var tenant = await db.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantCtx.TenantId);

            if (tenant is null) return Results.NotFound();

            if (!string.IsNullOrEmpty(tenant.AbacatePayStudentWebhookSecret))
            {
                var receivedSecret = ctx.Request.Query["webhookSecret"].ToString();
                if (receivedSecret != tenant.AbacatePayStudentWebhookSecret)
                {
                    logger.LogWarning("AbacatePay student webhook: invalid secret for tenant {Slug}", tenant.Slug);
                    return Results.Unauthorized();
                }
            }

            using var reader = new StreamReader(ctx.Request.Body);
            var payload = await reader.ReadToEndAsync();

            AbacatePayWebhookEvent? webhook;
            try
            {
                webhook = JsonSerializer.Deserialize<AbacatePayWebhookEvent>(payload,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                return Results.BadRequest();
            }

            if (webhook?.Event != "billing.paid") return Results.Ok();

            var billingId = webhook.Data?.Billing?.Id;
            if (string.IsNullOrEmpty(billingId)) return Results.Ok();

            var payment = await db.Payments
                .FirstOrDefaultAsync(p => p.AbacatePayBillingId == billingId && p.Status == PaymentStatus.Pending);

            if (payment is null) return Results.Ok();

            payment.Status = PaymentStatus.Paid;
            payment.PaidAt = DateTime.UtcNow;

            var package = await PackageHelper.AssignFromTemplateAsync(
                db, payment.TenantId, payment.StudentId, payment.PackageTemplateId);

            if (package is not null)
                payment.AssignedPackageId = package.Id;

            await db.SaveChangesAsync();

            logger.LogInformation("Student payment confirmed: paymentId={P}, tenant={T}", payment.Id, payment.TenantId);
            return Results.Ok();
        }).AllowAnonymous();

        // Admin: list payments for the tenant
        app.MapGet("/api/payments", async (
            AppDbContext db,
            TenantContext tenant,
            int page = 1,
            int pageSize = 50) =>
        {
            var payments = await db.Payments.AsNoTracking()
                .Include(p => p.Student)
                .Include(p => p.PackageTemplate)
                .Where(p => p.TenantId == tenant.TenantId)
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    p.Id,
                    studentName = p.Student.Name,
                    planName = p.PackageTemplate.Name,
                    p.Amount,
                    p.Status,
                    p.CreatedAt,
                    p.PaidAt
                })
                .ToListAsync();

            return Results.Ok(payments);
        }).RequireAuthorization("AdminOrAbove");
    }
}
