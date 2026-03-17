using GymApp.Domain.Interfaces;
using GymApp.Domain.Enums;
using GymApp.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace GymApp.Api.Services;

public class SubscriptionReminderService(
    IServiceScopeFactory scopeFactory,
    ILogger<SubscriptionReminderService> logger) : BackgroundService
{
    // Days before expiry on which to send a reminder
    private static readonly int[] ReminderDays = [7, 3, 1];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Short delay on startup so migrations finish first
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SubscriptionReminderService: unhandled error");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task SendRemindersAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db    = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var today = DateTime.UtcNow.Date;

        // Tenants with active subscription expiring soon
        var activeTenants = await db.Tenants
            .Where(t => t.SubscriptionStatus == SubscriptionStatus.Active
                     && t.SubscriptionCurrentPeriodEnd.HasValue
                     && t.LastReminderSentAt == null || t.LastReminderSentAt!.Value.Date < today)
            .ToListAsync(ct);

        foreach (var tenant in activeTenants)
        {
            var daysLeft = (int)(tenant.SubscriptionCurrentPeriodEnd!.Value.Date - today).TotalDays;
            if (!ReminderDays.Contains(daysLeft)) continue;

            var paymentUrl = tenant.AbacatePayBillingUrl ?? "https://agendofy.com";
            await SendReminderToAdmins(db, email, tenant, daysLeft, paymentUrl, ct);
        }

        // Tenants in trial expiring soon
        var trialTenants = await db.Tenants
            .Where(t => t.SubscriptionStatus == SubscriptionStatus.Trial
                     && (t.LastReminderSentAt == null || t.LastReminderSentAt!.Value.Date < today))
            .ToListAsync(ct);

        foreach (var tenant in trialTenants)
        {
            var trialEnd = tenant.CreatedAt.AddDays(tenant.TrialDays).Date;
            var daysLeft = (int)(trialEnd - today).TotalDays;
            if (!ReminderDays.Contains(daysLeft)) continue;

            var paymentUrl = tenant.AbacatePayBillingUrl ?? $"https://{tenant.Slug}.agendofy.com/admin/index.html";
            await SendReminderToAdmins(db, email, tenant, daysLeft, paymentUrl, ct);
        }

        logger.LogInformation("SubscriptionReminderService: check complete at {Time}", DateTime.UtcNow);
    }

    private async Task SendReminderToAdmins(
        AppDbContext db, IEmailService email,
        Domain.Entities.Tenant tenant, int daysLeft, string paymentUrl,
        CancellationToken ct)
    {
        var admins = await db.Users
            .AsNoTracking()
            .Where(u => u.TenantId == tenant.Id
                     && (u.Role == Domain.Enums.UserRole.Admin || u.Role == Domain.Enums.UserRole.SuperAdmin))
            .Select(u => new { u.Email, u.Name })
            .ToListAsync(ct);

        var anySent = false;
        foreach (var admin in admins)
        {
            try
            {
                await email.SendSubscriptionReminderAsync(
                    admin.Email, admin.Name, tenant.Name, daysLeft, paymentUrl);

                logger.LogInformation(
                    "Reminder sent to {Email} for tenant {Tenant} — {Days} day(s) left",
                    admin.Email, tenant.Name, daysLeft);

                anySent = true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send reminder to {Email}", admin.Email);
            }
        }

        // Mark as sent today so restarts don't re-send
        if (anySent)
        {
            tenant.LastReminderSentAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }
}
