namespace GymApp.Domain.Entities;

public class PackageTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? DurationDays { get; set; } // null = no expiry
    public bool IsVisibleInStore { get; set; } = true;
    public string? AbacatePayProductId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public ICollection<PackageTemplateItem> Items { get; set; } = [];
}

public class PackageTemplateItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TemplateId { get; set; }
    public Guid ClassTypeId { get; set; }
    public int TotalCredits { get; set; }
    public decimal PricePerCredit { get; set; }

    public PackageTemplate Template { get; set; } = null!;
    public ClassType ClassType { get; set; } = null!;
}
