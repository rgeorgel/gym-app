using GymApp.Domain.Enums;

namespace GymApp.Domain.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Student;
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? PhotoUrl { get; set; }
    public StudentStatus Status { get; set; } = StudentStatus.Active;
    public DateOnly? BirthDate { get; set; }
    public string? HealthNotes { get; set; }
    public bool ReceivesSubscriptionReminders { get; set; } = false;
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiry { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant? Tenant { get; set; }
    public ICollection<Booking> Bookings { get; set; } = [];
    public ICollection<Package> Packages { get; set; } = [];
}
