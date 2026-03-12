namespace GymApp.Api.DTOs;

public record CreatePackageTemplateRequest(
    string Name,
    int? DurationDays,
    List<CreatePackageItemRequest> Items
);

public record AssignTemplateRequest(
    Guid StudentId,
    DateOnly? ExpiresAt // override expiry; null = calculate from DurationDays or no expiry
);

public record PackageTemplateResponse(
    Guid Id,
    string Name,
    int? DurationDays,
    bool IsVisibleInStore,
    DateTime CreatedAt,
    List<PackageItemResponse> Items
);

public record SetStoreVisibilityRequest(bool IsVisibleInStore);
