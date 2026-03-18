using GymApp.Domain.Enums;

namespace GymApp.Domain.Entities;

public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid StudentId { get; set; }
    public Guid PackageTemplateId { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? AbacatePayBillingId { get; set; }
    public string? AbacatePayBillingUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(1);
    public DateTime? PaidAt { get; set; }

    public Guid? AssignedPackageId { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public User Student { get; set; } = null!;
    public PackageTemplate PackageTemplate { get; set; } = null!;
    public Package? AssignedPackage { get; set; }
}
