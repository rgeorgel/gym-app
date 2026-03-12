using GymApp.Domain.Enums;

namespace GymApp.Api.DTOs;

public record CheckoutRequest(Guid PackageTemplateId);

public record CheckoutResponse(
    Guid PaymentId,
    decimal Amount,
    string PixCopyPaste,
    string QrCodeBase64,
    DateTime ExpiresAt
);

public record PaymentStatusResponse(
    Guid PaymentId,
    PaymentStatus Status,
    DateTime? PaidAt,
    Guid? AssignedPackageId
);

public record EfiWebhookPayload(EfiPixEntry[]? Pix);
public record EfiPixEntry(string Txid, string Valor, string Horario);

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
