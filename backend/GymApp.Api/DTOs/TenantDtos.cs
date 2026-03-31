using GymApp.Domain.Enums;

namespace GymApp.Api.DTOs;

public record TenantConfigResponse(
    string Name,
    string? LogoUrl,
    string PrimaryColor,
    string SecondaryColor,
    string TextColor,
    string Slug,
    string Language,
    TenantType TenantType,
    string? SocialInstagram,
    string? SocialFacebook,
    string? SocialWhatsApp,
    string? SocialWebsite,
    string? SocialTikTok,
    bool AiEnabled = false
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
    string AdminPassword,
    TenantType TenantType = TenantType.Gym
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
    string? EfiPayeeCode,
    string TenantType,
    int SubscriptionPriceCents,
    bool HasDemoData = false
);

public record UpdateTenantRequest(
    string Name,
    string? LogoUrl,
    string PrimaryColor,
    string SecondaryColor,
    string? CustomDomain,
    bool IsActive,
    TenantType? TenantType = null
);

public record TenantSettingsResponse(
    Guid? DefaultPackageTemplateId,
    string Language,
    string? EfiPayeeCode,
    bool PaymentsEnabled,
    bool PaymentsAllowedBySuperAdmin,
    string PrimaryColor,
    string SecondaryColor,
    string TextColor,
    string? LogoUrl,
    bool HasAbacatePayStudentApiKey,
    bool HasAbacatePayStudentWebhookSecret,
    TenantType TenantType,
    string? SocialInstagram,
    string? SocialFacebook,
    string? SocialWhatsApp,
    string? SocialWebsite,
    string? SocialTikTok,
    bool AiEnabled = false
);

public record SetAiEnabledRequest(bool Enabled);

public record SetTenantTypeRequest(TenantType TenantType);

public record SetLogoUrlRequest(
    string? LogoUrl
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

public record SetAbacatePayStudentApiKeyRequest(
    string? ApiKey
);

public record SetAbacatePayStudentWebhookSecretRequest(
    string? Secret
);

public record SetPaymentsEnabledRequest(
    bool Enabled
);

public record SetColorsRequest(
    string PrimaryColor,
    string SecondaryColor,
    string TextColor
);

public record SetPaymentsAllowedRequest(
    bool Allowed
);

public record SetSocialLinksRequest(
    string? Instagram,
    string? Facebook,
    string? WhatsApp,
    string? Website,
    string? TikTok
);

public record SelfSignupRequest(
    string AdminName,
    string AcademyName,
    string Email,
    string Password,
    string? Phone = null,
    TenantType TenantType = TenantType.Gym,
    string? ReferralCode = null
);

public record ReferralStatsResponse(
    string ReferralCode,
    int TotalReferrals,
    int ConvertedReferrals
);

public record SelfSignupResponse(
    Guid TenantId,
    string Slug,
    string AdminEmail
);
