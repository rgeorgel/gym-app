namespace GymApp.Domain.Entities;

public class ProfessionalAvailability
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? InstructorId { get; set; }
    public int Weekday { get; set; } // 0=Sun, 1=Mon, ..., 6=Sat
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public Instructor? Instructor { get; set; }
}
