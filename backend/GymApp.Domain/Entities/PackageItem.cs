namespace GymApp.Domain.Entities;

public class PackageItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PackageId { get; set; }
    public Guid ClassTypeId { get; set; }
    public int TotalCredits { get; set; }
    public int UsedCredits { get; set; }
    public decimal PricePerCredit { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int RemainingCredits => TotalCredits - UsedCredits;

    public Package Package { get; set; } = null!;
    public ClassType ClassType { get; set; } = null!;
    public ICollection<Booking> Bookings { get; set; } = [];
}
