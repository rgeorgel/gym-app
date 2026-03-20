using GymApp.Domain.Enums;

namespace GymApp.Api.DTOs;

public record SetupBillingRequest(
    string? TaxId,
    string? Phone
);

public record SubscriptionStatusResponse(
    SubscriptionStatus Status,
    bool HasStudentAccess,
    bool IsInTrial,
    int TrialDaysRemaining,
    DateTime? TrialEndsAt,
    DateTime? CurrentPeriodEnd,
    string? BillingUrl
);

public record SetTrialDaysRequest(int Days);
public record SetSubscriptionPriceRequest(int PriceCents);

public record RevenueOverviewResponse(
    int TotalTenants,
    int ActiveTenants,
    int TrialTenants,
    int PastDueTenants,
    int CanceledTenants,
    int SubscriptionPriceCents,
    int EstimatedMrrCents,
    IReadOnlyList<TenantBillingRow> Tenants
);

public record TenantBillingRow(
    Guid Id,
    string Name,
    string Slug,
    SubscriptionStatus Status,
    bool IsInTrial,
    int TrialDaysRemaining,
    DateTime? CurrentPeriodEnd,
    DateTime CreatedAt,
    int SubscriptionPriceCents
);
