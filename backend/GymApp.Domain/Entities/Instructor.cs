namespace GymApp.Domain.Entities;

public class Instructor
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid? PrimaryLocationId { get; set; }
    public string? Bio { get; set; }
    public string? Specialties { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public User User { get; set; } = null!;
    public Location? PrimaryLocation { get; set; }
    public ICollection<Schedule> Schedules { get; set; } = [];
}
