using GymApp.Domain.Entities;
using GymApp.Domain.Enums;
using Xunit;

namespace GymApp.Tests.Entities;

public class UserTests
{
    [Fact]
    public void User_DefaultValues()
    {
        var user = new User();
        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal(string.Empty, user.Email);
        Assert.Equal(string.Empty, user.PasswordHash);
        Assert.Equal(string.Empty, user.Name);
        Assert.Equal(UserRole.Student, user.Role);
        Assert.Equal(StudentStatus.Active, user.Status);
        Assert.False(user.ReceivesSubscriptionReminders);
    }

    [Fact]
    public void User_WithTenantId()
    {
        var tenantId = Guid.NewGuid();
        var user = new User { TenantId = tenantId };
        Assert.Equal(tenantId, user.TenantId);
    }

    [Fact]
    public void User_WithRefreshToken()
    {
        var user = new User
        {
            RefreshToken = "test-token",
            RefreshTokenExpiry = DateTime.UtcNow.AddDays(30)
        };
        Assert.Equal("test-token", user.RefreshToken);
        Assert.NotNull(user.RefreshTokenExpiry);
    }

    [Fact]
    public void User_WithPasswordResetToken()
    {
        var user = new User
        {
            PasswordResetToken = "reset-token",
            PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(2)
        };
        Assert.Equal("reset-token", user.PasswordResetToken);
        Assert.NotNull(user.PasswordResetTokenExpiry);
    }

    [Fact]
    public void User_PasswordResetToken_ExpiresIn2Hours()
    {
        var user = new User
        {
            PasswordResetToken = "reset-token",
            PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(2)
        };
        Assert.True(user.PasswordResetTokenExpiry > DateTime.UtcNow);
    }

    [Fact]
    public void User_WithAbacatePayCustomerId()
    {
        var user = new User { AbacatePayCustomerId = "cus_12345" };
        Assert.Equal("cus_12345", user.AbacatePayCustomerId);
    }
}

public class SessionTests
{
    [Fact]
    public void Session_DefaultValues()
    {
        var session = new Session();
        Assert.Equal(SessionStatus.Scheduled, session.Status);
        Assert.Equal(0, session.SlotsAvailable);
    }

    [Fact]
    public void Session_WithGymData()
    {
        var session = new Session
        {
            TenantId = Guid.NewGuid(),
            LocationId = Guid.NewGuid(),
            Date = DateOnly.FromDateTime(DateTime.Today),
            StartTime = new TimeOnly(9, 0),
            DurationMinutes = 60,
            SlotsAvailable = 20
        };
        Assert.Equal(20, session.SlotsAvailable);
        Assert.Equal(60, session.DurationMinutes);
    }
}

public class BookingTests
{
    [Fact]
    public void Booking_DefaultValues()
    {
        var booking = new Booking();
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Null(booking.CheckedInAt);
        Assert.Null(booking.CancelledAt);
    }

    [Fact]
    public void Booking_WithCheckIn()
    {
        var booking = new Booking
        {
            Status = BookingStatus.CheckedIn,
            CheckedInAt = DateTime.UtcNow
        };
        Assert.Equal(BookingStatus.CheckedIn, booking.Status);
        Assert.NotNull(booking.CheckedInAt);
    }

    [Fact]
    public void Booking_WithCancellation()
    {
        var booking = new Booking
        {
            Status = BookingStatus.Cancelled,
            CancelledAt = DateTime.UtcNow,
            CancellationReason = "Schedule conflict"
        };
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.NotNull(booking.CancelledAt);
        Assert.Equal("Schedule conflict", booking.CancellationReason);
    }
}

public class PackageTests
{
    [Fact]
    public void Package_DefaultValues()
    {
        var pkg = new Package();
        Assert.True(pkg.IsActive);
        Assert.Null(pkg.ExpiresAt);
    }

    [Fact]
    public void Package_WithExpiry()
    {
        var expiresAt = DateOnly.FromDateTime(DateTime.Today.AddDays(30));
        var pkg = new Package
        {
            Name = "Monthly",
            ExpiresAt = expiresAt
        };
        Assert.Equal(expiresAt, pkg.ExpiresAt);
    }
}