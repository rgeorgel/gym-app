using GymApp.Domain.Enums;

namespace GymApp.Domain.Entities;

public class Session
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ScheduleId { get; set; }        // null for BeautySalon sessions
    public Guid TenantId { get; set; }            // denormalized for filtering without Schedule join
    public Guid? ClassTypeId { get; set; }        // denormalized — always set
    public Guid LocationId { get; set; }
    public Guid? InstructorId { get; set; }    // salon: professional assigned to this appointment
    public TimeOnly StartTime { get; set; }       // denormalized — always set
    public int DurationMinutes { get; set; }      // denormalized — always set
    public DateOnly Date { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Scheduled;
    public int SlotsAvailable { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Schedule? Schedule { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public ClassType? ClassType { get; set; }
    public Location Location { get; set; } = null!;
    public Instructor? Instructor { get; set; }
    public ICollection<Booking> Bookings { get; set; } = [];
    public ICollection<WaitingListEntry> WaitingList { get; set; } = [];
}
