namespace GymApp.Domain.Entities;

public class Location
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public bool IsMain { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public ICollection<Schedule> Schedules { get; set; } = [];
    public ICollection<Session> Sessions { get; set; } = [];
    public ICollection<Instructor> Instructors { get; set; } = [];
}
