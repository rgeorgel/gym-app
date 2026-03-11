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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid? DefaultPackageTemplateId { get; set; }

    public PackageTemplate? DefaultPackageTemplate { get; set; }
    public ICollection<User> Users { get; set; } = [];
    public ICollection<ClassType> ClassTypes { get; set; } = [];
    public ICollection<Schedule> Schedules { get; set; } = [];
    public ICollection<Package> Packages { get; set; } = [];
    public ICollection<Instructor> Instructors { get; set; } = [];
    public ICollection<PackageTemplate> PackageTemplates { get; set; } = [];
}
