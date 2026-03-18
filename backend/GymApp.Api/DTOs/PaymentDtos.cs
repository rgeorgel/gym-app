using GymApp.Domain.Enums;

namespace GymApp.Api.DTOs;

public record CheckoutRequest(Guid PackageTemplateId);

public record CheckoutResponse(
    Guid PaymentId,
    decimal Amount,
    string BillingUrl,
    DateTime ExpiresAt
);

public record PaymentStatusResponse(
    Guid PaymentId,
    PaymentStatus Status,
    DateTime? PaidAt,
    Guid? AssignedPackageId
);

public record StorePlanResponse(
    Guid Id,
    string Name,
    int? DurationDays,
    decimal TotalPrice,
    List<StorePlanItem> Items
);

public record StorePlanItem(
    string ClassTypeName,
    string ClassTypeColor,
    int TotalCredits,
    decimal PricePerCredit
);
