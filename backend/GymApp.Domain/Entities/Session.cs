using GymApp.Domain.Enums;

namespace GymApp.Domain.Entities;

public class Session
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScheduleId { get; set; }
    public DateOnly Date { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Scheduled;
    public int SlotsAvailable { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Schedule Schedule { get; set; } = null!;
    public ICollection<Booking> Bookings { get; set; } = [];
    public ICollection<WaitingListEntry> WaitingList { get; set; } = [];
}
