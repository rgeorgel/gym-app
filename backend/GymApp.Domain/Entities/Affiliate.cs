using GymApp.Domain.Enums;

namespace GymApp.Domain.Entities;

public class Affiliate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string ReferralCode { get; set; } = string.Empty;
    public decimal CommissionRate { get; set; } = 0.20m;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public ICollection<AffiliateReferral> Referrals { get; set; } = [];
    public ICollection<AffiliateCommission> Commissions { get; set; } = [];
    public ICollection<AffiliateWithdrawalRequest> WithdrawalRequests { get; set; } = [];
}

public class AffiliateReferral
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AffiliateId { get; set; }
    public Guid TenantId { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    public Affiliate Affiliate { get; set; } = null!;
    public Tenant Tenant { get; set; } = null!;
}

public class AffiliateCommission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AffiliateId { get; set; }
    public Guid TenantId { get; set; }
    public string SubscriptionPaymentRef { get; set; } = string.Empty;
    public decimal GrossAmount { get; set; }
    public decimal Rate { get; set; }
    public decimal CommissionAmount { get; set; }
    public AffiliateCommissionStatus Status { get; set; } = AffiliateCommissionStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Affiliate Affiliate { get; set; } = null!;
    public Tenant Tenant { get; set; } = null!;
}

public class AffiliateWithdrawalRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AffiliateId { get; set; }
    public decimal RequestedAmount { get; set; }
    public AffiliateWithdrawalStatus Status { get; set; } = AffiliateWithdrawalStatus.Pending;
    public string? AdminNotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }

    public Affiliate Affiliate { get; set; } = null!;
}

public class SystemConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
