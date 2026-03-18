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
    public TenantPlan Plan { get; set; } = TenantPlan.Basic;
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
    public int TrialDays { get; set; } = 14;
    public SubscriptionStatus SubscriptionStatus { get; set; } = SubscriptionStatus.Trial;
    public DateTime? SubscriptionCurrentPeriodEnd { get; set; }
    public string? AbacatePayCustomerId { get; set; }
    public string? AbacatePayBillingId { get; set; }
    public string? AbacatePayBillingUrl { get; set; }
    public DateTime? LastReminderSentAt { get; set; }

    // AbacatePay key for student payments (tenant's own account)
    public string? AbacatePayStudentApiKey { get; set; }

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
}
