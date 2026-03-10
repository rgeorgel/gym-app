using GymApp.Domain.Enums;

namespace GymApp.Api.DTOs;

public record TenantConfigResponse(
    string Name,
    string? LogoUrl,
    string PrimaryColor,
    string SecondaryColor,
    string Slug
);

public record CreateTenantRequest(
    string Name,
    string Slug,
    string? LogoUrl,
    string PrimaryColor,
    string SecondaryColor,
    TenantPlan Plan,
    string AdminName,
    string AdminEmail,
    string AdminPassword
);

public record TenantResponse(
    Guid Id,
    string Name,
    string Slug,
    string? LogoUrl,
    string PrimaryColor,
    string SecondaryColor,
    TenantPlan Plan,
    bool IsActive,
    string? CustomDomain,
    DateTime CreatedAt
);

public record UpdateTenantRequest(
    string Name,
    string? LogoUrl,
    string PrimaryColor,
    string SecondaryColor,
    string? CustomDomain,
    bool IsActive
);
