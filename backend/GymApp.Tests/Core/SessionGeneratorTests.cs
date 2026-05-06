using FluentAssertions;
using GymApp.Api.Core;
using GymApp.Domain.Entities;
using GymApp.Domain.Enums;
using Xunit;

namespace GymApp.Tests.Core;

public class SessionGeneratorTests
{
    private static Schedule CreateSchedule(Guid id, int weekday, int capacity = 20) =>
        new()
        {
            Id = id,
            TenantId = Guid.NewGuid(),
            Weekday = weekday,
            StartTime = new TimeOnly(9, 0),
            DurationMinutes = 60,
            Capacity = capacity,
            IsActive = true,
            LocationId = Guid.NewGuid()
        };

    [Fact]
    public void GenerateSessionsFromSchedules_WithMatchingSchedules_CreatesSessions()
    {
        var scheduleId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var schedule = new Schedule
        {
            Id = scheduleId,
            TenantId = tenantId,
            Weekday = 1,
            StartTime = new TimeOnly(9, 0),
            DurationMinutes = 60,
            Capacity = 20,
            LocationId = locationId
        };
        var start = new DateOnly(2024, 6, 3);
        var end = new DateOnly(2024, 6, 3);
        var existingKeys = new HashSet<(Guid, DateOnly)>();

        var sessions = SessionGenerator.GenerateSessionsFromSchedules(
            new[] { schedule }, start, end, existingKeys).ToList();

        sessions.Should().HaveCount(1);
        var session = sessions[0];
        session.ScheduleId.Should().Be(scheduleId);
        session.TenantId.Should().Be(tenantId);
        session.LocationId.Should().Be(locationId);
        session.Date.Should().Be(start);
        session.StartTime.Should().Be(new TimeOnly(9, 0));
        session.DurationMinutes.Should().Be(60);
        session.SlotsAvailable.Should().Be(20);
    }

    [Fact]
    public void GenerateSessionsFromSchedules_WithExistingKeys_SkipsExisting()
    {
        var scheduleId = Guid.NewGuid();
        var schedule = CreateSchedule(scheduleId, weekday: 1);
        var start = new DateOnly(2024, 6, 3);
        var end = new DateOnly(2024, 6, 3);
        var existingKeys = new HashSet<(Guid, DateOnly)> { { (scheduleId, start) } };

        var sessions = SessionGenerator.GenerateSessionsFromSchedules(
            new[] { schedule }, start, end, existingKeys).ToList();

        sessions.Should().BeEmpty();
    }

    [Fact]
    public void GenerateSessionsFromSchedules_WithMultipleDays_CreatesForEachDay()
    {
        var scheduleId = Guid.NewGuid();
        var schedule = CreateSchedule(scheduleId, weekday: 1);
        var start = new DateOnly(2024, 6, 3);
        var end = new DateOnly(2024, 6, 10);
        var existingKeys = new HashSet<(Guid, DateOnly)>();

        var sessions = SessionGenerator.GenerateSessionsFromSchedules(
            new[] { schedule }, start, end, existingKeys).ToList();

        sessions.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void ReactivateSession_SetsStatusToScheduled()
    {
        var session = new Session { Status = SessionStatus.Cancelled, CancellationReason = "Test reason" };

        SessionGenerator.ReactivateSession(session);

        session.Status.Should().Be(SessionStatus.Scheduled);
        session.CancellationReason.Should().BeNull();
    }

    [Fact]
    public void CancelSession_SetsStatusAndReason()
    {
        var session = new Session { Status = SessionStatus.Scheduled };

        SessionGenerator.CancelSession(session, "Instructor sick");

        session.Status.Should().Be(SessionStatus.Cancelled);
        session.CancellationReason.Should().Be("Instructor sick");
    }

    [Fact]
    public void CancelSessionBookings_CancelsConfirmedBookingsAndRestoresCredits()
    {
        var packageItem = new PackageItem { Id = Guid.NewGuid(), TotalCredits = 10, UsedCredits = 1 };
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            PackageItem = packageItem
        };
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Status = SessionStatus.Scheduled,
            Bookings = new List<Booking> { booking }
        };

        SessionGenerator.CancelSessionBookings(session);

        booking.Status.Should().Be(BookingStatus.Cancelled);
        booking.CancellationReason.Should().Be("Aula cancelada");
        booking.CancelledAt.Should().NotBeNull();
        packageItem.UsedCredits.Should().Be(0);
    }

    [Fact]
    public void CancelSessionBookings_WithNoPackageItem_DoesNotAffectCredits()
    {
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            PackageItem = null
        };
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Status = SessionStatus.Scheduled,
            Bookings = new List<Booking> { booking }
        };

        SessionGenerator.CancelSessionBookings(session);

        booking.Status.Should().Be(BookingStatus.Cancelled);
    }

    [Fact]
    public void RestoreBookingCredits_RestoresConfirmedCancelledBookings()
    {
        var packageItem = new PackageItem { Id = Guid.NewGuid(), TotalCredits = 10, UsedCredits = 0 };
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            Status = BookingStatus.Cancelled,
            CancellationReason = "Aula cancelada",
            CancelledAt = DateTime.UtcNow.AddDays(-1),
            PackageItem = packageItem
        };
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Status = SessionStatus.Cancelled,
            Bookings = new List<Booking> { booking }
        };

        SessionGenerator.RestoreBookingCredits(session);

        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.CancellationReason.Should().BeNull();
        booking.CancelledAt.Should().BeNull();
        packageItem.UsedCredits.Should().Be(1);
    }

    [Fact]
    public void RestoreBookingCredits_WithDifferentCancellationReason_DoesNotRestore()
    {
        var packageItem = new PackageItem { Id = Guid.NewGuid(), TotalCredits = 10, UsedCredits = 0 };
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            Status = BookingStatus.Cancelled,
            CancellationReason = "Student requested",
            PackageItem = packageItem
        };
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Status = SessionStatus.Cancelled,
            Bookings = new List<Booking> { booking }
        };

        SessionGenerator.RestoreBookingCredits(session);

        booking.Status.Should().Be(BookingStatus.Cancelled);
        packageItem.UsedCredits.Should().Be(0);
    }

    [Fact]
    public void CountConfirmedBookings_ReturnsCorrectCount()
    {
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Bookings = new List<Booking>
            {
                new() { Id = Guid.NewGuid(), Status = BookingStatus.Confirmed },
                new() { Id = Guid.NewGuid(), Status = BookingStatus.CheckedIn },
                new() { Id = Guid.NewGuid(), Status = BookingStatus.Cancelled }
            }
        };

        var count = SessionGenerator.CountConfirmedBookings(session);

        count.Should().Be(2);
    }

    [Fact]
    public void CalculateSessionsToGenerate_ReturnsCorrectCount()
    {
        var schedule = CreateSchedule(Guid.NewGuid(), weekday: 1);
        var start = new DateOnly(2024, 6, 3);
        var end = new DateOnly(2024, 6, 10);
        var existingKeys = new HashSet<(Guid, DateOnly)>();

        var count = SessionGenerator.CalculateSessionsToGenerate(
            new[] { schedule }, start, start.AddDays(13), existingKeys);

        count.Should().BeGreaterThan(0);
    }
}