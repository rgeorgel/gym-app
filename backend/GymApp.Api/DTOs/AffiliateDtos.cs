using GymApp.Domain.Enums;

namespace GymApp.Api.DTOs;

// ── Affiliate profile ────────────────────────────────────────────────────────

public record AffiliateProfileResponse(
    Guid Id,
    Guid UserId,
    string Name,
    string Email,
    string ReferralCode,
    string ReferralLink,
    decimal CommissionRate,
    DateTime CreatedAt
);

// ── Referrals ────────────────────────────────────────────────────────────────

public record AffiliateReferralResponse(
    Guid TenantId,
    string TenantName,
    string TenantSlug,
    SubscriptionStatus SubscriptionStatus,
    bool IsActive,
    DateTime RegisteredAt,
    decimal TotalCommission
);

// ── Commissions ──────────────────────────────────────────────────────────────

public record AffiliateCommissionResponse(
    Guid Id,
    string TenantName,
    string SubscriptionPaymentRef,
    decimal GrossAmount,
    decimal Rate,
    decimal CommissionAmount,
    AffiliateCommissionStatus Status,
    DateTime CreatedAt
);

// ── Balance ──────────────────────────────────────────────────────────────────

public record AffiliateBalanceResponse(
    decimal AvailableBalance,
    decimal TotalEarned,
    decimal PendingWithdrawal,
    int MinWithdrawalCents
);

// ── Withdrawals ──────────────────────────────────────────────────────────────

public record AffiliateWithdrawalResponse(
    Guid Id,
    decimal RequestedAmount,
    AffiliateWithdrawalStatus Status,
    string? AdminNotes,
    DateTime CreatedAt,
    DateTime? ResolvedAt
);

public record CreateWithdrawalRequest(decimal Amount);

// ── Super admin: affiliates ──────────────────────────────────────────────────

public record AffiliateListResponse(
    Guid Id,
    Guid UserId,
    string Name,
    string Email,
    string ReferralCode,
    decimal CommissionRate,
    int ReferralCount,
    decimal TotalEarned,
    decimal AvailableBalance,
    DateTime CreatedAt
);

public record AffiliateDetailResponse(
    Guid Id,
    Guid UserId,
    string Name,
    string Email,
    string ReferralCode,
    string ReferralLink,
    decimal CommissionRate,
    decimal TotalEarned,
    decimal AvailableBalance,
    DateTime CreatedAt,
    List<AffiliateReferralResponse> Referrals,
    List<AffiliateCommissionResponse> Commissions,
    List<AffiliateWithdrawalResponse> Withdrawals
);

public record CreateAffiliateRequest(
    string Name,
    string Email,
    string Password,
    string ReferralCode,
    decimal CommissionRate = 0.20m
);

public record UpdateAffiliateRateRequest(decimal CommissionRate);

// ── Super admin: withdrawal management ───────────────────────────────────────

public record AdminWithdrawalResponse(
    Guid Id,
    Guid AffiliateId,
    string AffiliateName,
    string AffiliateEmail,
    decimal RequestedAmount,
    AffiliateWithdrawalStatus Status,
    string? AdminNotes,
    DateTime CreatedAt,
    DateTime? ResolvedAt
);

public record ResolveWithdrawalRequest(
    AffiliateWithdrawalStatus Status,
    string? AdminNotes = null
);

// ── System config ────────────────────────────────────────────────────────────

public record SystemConfigResponse(
    int AffiliateMinWithdrawalCents,
    decimal AffiliateDefaultCommissionRate
);

public record UpdateSystemConfigRequest(
    int? AffiliateMinWithdrawalCents,
    decimal? AffiliateDefaultCommissionRate
);
