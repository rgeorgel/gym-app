using GymApp.Domain.Enums;

namespace GymApp.Domain.Entities;

public class ClassType
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#3498db";
    public ModalityType ModalityType { get; set; } = ModalityType.Group;
    public decimal? Price { get; set; }
    public int? DurationMinutes { get; set; } // Used in BeautySalon mode for slot calculation
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public ICollection<Schedule> Schedules { get; set; } = [];
    public ICollection<PackageItem> PackageItems { get; set; } = [];
}
