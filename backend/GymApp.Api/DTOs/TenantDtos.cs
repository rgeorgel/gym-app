using GymApp.Domain.Enums;

namespace GymApp.Api.DTOs;

public record TenantConfigResponse(
    string Name,
    string? LogoUrl,
    string PrimaryColor,
    string SecondaryColor,
    string Slug,
    string Language
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
    DateTime CreatedAt,
    bool PaymentsAllowedBySuperAdmin,
    bool PaymentsEnabled,
    string? EfiPayeeCode
);

public record UpdateTenantRequest(
    string Name,
    string? LogoUrl,
    string PrimaryColor,
    string SecondaryColor,
    string? CustomDomain,
    bool IsActive
);

public record TenantSettingsResponse(
    Guid? DefaultPackageTemplateId,
    string Language,
    string? EfiPayeeCode,
    bool PaymentsEnabled,
    bool PaymentsAllowedBySuperAdmin
);

public record SetDefaultTemplateRequest(
    Guid? TemplateId
);

public record SetLanguageRequest(
    string Language
);

public record SetEfiPayeeCodeRequest(
    string? PayeeCode
);

public record SetPaymentsEnabledRequest(
    bool Enabled
);

public record SetPaymentsAllowedRequest(
    bool Allowed
);
