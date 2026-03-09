namespace GymApp.Domain.Entities;

public class Package
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid StudentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public User Student { get; set; } = null!;
    public ICollection<PackageItem> Items { get; set; } = [];
}
