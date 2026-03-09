namespace GymApp.Api.DTOs;

public record CreatePackageRequest(
    Guid StudentId,
    string Name,
    DateOnly? ExpiresAt,
    List<CreatePackageItemRequest> Items
);

public record CreatePackageItemRequest(
    Guid ClassTypeId,
    int TotalCredits,
    decimal PricePerCredit
);

public record PackageResponse(
    Guid Id,
    string Name,
    DateOnly? ExpiresAt,
    bool IsActive,
    DateTime CreatedAt,
    List<PackageItemResponse> Items
);

public record PackageItemResponse(
    Guid Id,
    Guid ClassTypeId,
    string ClassTypeName,
    string ClassTypeColor,
    int TotalCredits,
    int UsedCredits,
    int RemainingCredits,
    decimal PricePerCredit
);
