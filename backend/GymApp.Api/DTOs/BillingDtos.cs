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
