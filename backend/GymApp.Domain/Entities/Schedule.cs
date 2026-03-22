namespace GymApp.Domain.Entities;

public class Schedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ClassTypeId { get; set; }
    public Guid? InstructorId { get; set; }
    public Guid LocationId { get; set; }
    public int Weekday { get; set; } // 0=Sun, 1=Mon, ..., 6=Sat
    public TimeOnly StartTime { get; set; }
    public int DurationMinutes { get; set; } = 60;
    public int Capacity { get; set; } = 20;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public ClassType ClassType { get; set; } = null!;
    public Instructor? Instructor { get; set; }
    public Location Location { get; set; } = null!;
    public ICollection<Session> Sessions { get; set; } = [];
}
