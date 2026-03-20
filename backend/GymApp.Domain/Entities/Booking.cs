using GymApp.Domain.Enums;

namespace GymApp.Domain.Entities;

public class Booking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid? PackageItemId { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
    public DateTime? CheckedInAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    public Session Session { get; set; } = null!;
    public User Student { get; set; } = null!;
    public PackageItem? PackageItem { get; set; }
}
