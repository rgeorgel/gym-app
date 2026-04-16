using GymApp.Domain.Enums;

namespace GymApp.Domain.Entities;

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string PrimaryColor { get; set; } = "#1a1a2e";
    public string SecondaryColor { get; set; } = "#e94560";
    public string TextColor { get; set; } = "#ffffff";
    public TenantPlan Plan { get; set; } = TenantPlan.Basic;
    public TenantType TenantType { get; set; } = TenantType.Gym;
    public bool IsActive { get; set; } = true;
    public string? CustomDomain { get; set; }
    public int CancellationHoursLimit { get; set; } = 2;
    public string Language { get; set; } = "pt-BR";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid? DefaultPackageTemplateId { get; set; }
    public string? EfiPayeeCode { get; set; }
    public bool PaymentsEnabled { get; set; } = false;
    public bool PaymentsAllowedBySuperAdmin { get; set; } = true;

    public bool PaymentsActive => PaymentsEnabled && PaymentsAllowedBySuperAdmin;

    // ── Subscription / billing ──────────────────────────────────────────────
    public int SubscriptionPriceCents { get; set; } = 4900;
    public int TrialDays { get; set; } = 14;
    public SubscriptionStatus SubscriptionStatus { get; set; } = SubscriptionStatus.Trial;
    public DateTime? SubscriptionCurrentPeriodEnd { get; set; }
    public string? AbacatePayCustomerId { get; set; }
    /// <summary>ID da cobrança avulsa mais recente (PIX). Usado para cancelar cobranças manuais.</summary>
    public string? AbacatePayBillingId { get; set; }
    public string? AbacatePayBillingUrl { get; set; }
    public DateTime? LastReminderSentAt { get; set; }

    // AbacatePay subscription product ID (V2 — created once per tenant, reused)
    public string? AbacatePaySubscriptionProductId { get; set; }

    /// <summary>ID da assinatura recorrente via cartão (V2 /subscriptions/create). Separado do billing avulso.</summary>
    public string? AbacatePaySubscriptionId { get; set; }

    // AbacatePay key for student payments (tenant's own account)
    public string? AbacatePayStudentApiKey { get; set; }
    public string? AbacatePayStudentWebhookSecret { get; set; }

    // ── Social links ────────────────────────────────────────────────────────
    public string? SocialInstagram { get; set; }
    public string? SocialFacebook { get; set; }
    public string? SocialWhatsApp { get; set; }
    public string? SocialWebsite { get; set; }
    public string? SocialTikTok { get; set; }

    // ── AI Assistant ────────────────────────────────────────────────────────
    public bool AiEnabled { get; set; } = false;
    public string? AiSystemPrompt { get; set; }

    // ── Referral program (tenant-to-tenant) ─────────────────────────────────
    public string? ReferredByCode { get; set; }
    public Guid? ReferredByTenantId { get; set; }
    public bool ReferralRewardClaimed { get; set; } = false;

    // ── Affiliate program ────────────────────────────────────────────────────
    /// <summary>Affiliate referral code used when this tenant registered.</summary>
    public string? AffiliateReferralCode { get; set; }

    // ── WhatsApp Auto-Service (Evolution API) ─────────────────────────────
    /// <summary>Evolution API instance name linked to this tenant's WhatsApp number.</summary>
    public string? WhatsAppInstanceName { get; set; }
    public bool WhatsAppAutoServiceEnabled { get; set; } = false;

    /// <summary>True while in trial, while paid and active, or during the
    /// remaining paid period after a cancellation.</summary>
    public bool HasStudentAccess => IsActive && SubscriptionStatus switch
    {
        SubscriptionStatus.Trial    => DateTime.UtcNow < CreatedAt.AddDays(TrialDays),
        SubscriptionStatus.Active   => true,
        SubscriptionStatus.Canceled => SubscriptionCurrentPeriodEnd.HasValue
                                       && DateTime.UtcNow < SubscriptionCurrentPeriodEnd.Value,
        _                           => false   // PastDue, Suspended
    };

    public bool IsInTrial => SubscriptionStatus == SubscriptionStatus.Trial
                             && DateTime.UtcNow < CreatedAt.AddDays(TrialDays);

    public int TrialDaysRemaining => IsInTrial
        ? Math.Max(0, (int)(CreatedAt.AddDays(TrialDays) - DateTime.UtcNow).TotalDays)
        : 0;

    public PackageTemplate? DefaultPackageTemplate { get; set; }
    public ICollection<User> Users { get; set; } = [];
    public ICollection<ClassType> ClassTypes { get; set; } = [];
    public ICollection<Schedule> Schedules { get; set; } = [];
    public ICollection<Package> Packages { get; set; } = [];
    public ICollection<Instructor> Instructors { get; set; } = [];
    public ICollection<PackageTemplate> PackageTemplates { get; set; } = [];
    public ICollection<Location> Locations { get; set; } = [];
}
