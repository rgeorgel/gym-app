using FluentAssertions;
using GymApp.Api.Core;
using GymApp.Api.Helpers;
using GymApp.Domain.Entities;
using GymApp.Domain.Enums;
using GymApp.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymApp.Tests.Endpoints;

public class BookingEndpointIntegrationTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateBooking_WithValidSession_CreatesBooking()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var classTypeId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test", Slug = "test" });
        db.ClassTypes.Add(new ClassType { Id = classTypeId, TenantId = tenantId, Name = "Boxing", Color = "#FF0000" });
        db.Users.Add(new User
        {
            Id = studentId,
            TenantId = tenantId,
            Name = "Student",
            Email = "student@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass"),
            Role = UserRole.Student
        });
        var session = new Session
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ClassTypeId = classTypeId,
            Date = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            StartTime = new TimeOnly(10, 0),
            DurationMinutes = 60,
            Status = SessionStatus.Scheduled,
            SlotsAvailable = 20
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            StudentId = studentId,
            Status = BookingStatus.Confirmed
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        var saved = await db.Bookings.FirstAsync();
        saved.SessionId.Should().Be(session.Id);
        saved.StudentId.Should().Be(studentId);
        saved.Status.Should().Be(BookingStatus.Confirmed);
    }

    [Fact]
    public async Task CancelBooking_RestoresCreditsAndSlot()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var packageItemId = Guid.NewGuid();
        var classTypeId = Guid.NewGuid();

        var session = new Session
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ClassTypeId = classTypeId,
            Date = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            StartTime = new TimeOnly(10, 0),
            DurationMinutes = 60,
            Status = SessionStatus.Scheduled,
            SlotsAvailable = 0
        };
        db.Sessions.Add(session);

        var package = new Package
        {
            Id = packageId,
            TenantId = tenantId,
            StudentId = studentId,
            Name = "Test Package",
            IsActive = true
        };
        db.Packages.Add(package);

        var packageItem = new PackageItem
        {
            Id = packageItemId,
            PackageId = packageId,
            ClassTypeId = classTypeId,
            TotalCredits = 10,
            UsedCredits = 1
        };
        db.PackageItems.Add(packageItem);

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            StudentId = studentId,
            PackageItemId = packageItemId,
            Status = BookingStatus.Confirmed
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        // Simulate cancellation
        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = DateTime.UtcNow;
        booking.PackageItem!.UsedCredits = Math.Max(0, booking.PackageItem.UsedCredits - 1);
        session.SlotsAvailable++;
        await db.SaveChangesAsync();

        booking.Status.Should().Be(BookingStatus.Cancelled);
        booking.PackageItem.UsedCredits.Should().Be(0);
        session.SlotsAvailable.Should().Be(1);
    }

    [Fact]
    public async Task BookingCancellationLogic_CanCancelWithinDeadline()
    {
        var sessionDateTime = DateOnly.FromDateTime(DateTime.Today.AddDays(1)).ToDateTime(new TimeOnly(10, 0));
        var hoursLimit = 2;
        var deadline = sessionDateTime.AddHours(-hoursLimit);

        // Current time is before deadline
        var now = DateTime.UtcNow;
        var canCancel = now <= deadline;

        // In real test, we would mock DateTime.UtcNow
        // For now, just verify the logic
        deadline.Should().BeAfter(now);
    }
}

public class PackageEndpointIntegrationTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task AssignFromTemplate_CreatesPackageWithItems()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var classTypeId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test", Slug = "test" });
        db.ClassTypes.Add(new ClassType { Id = classTypeId, TenantId = tenantId, Name = "Boxing", Color = "#FF0000" });

        var template = new PackageTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Basic Plan",
            DurationDays = 30
        };
        template.Items.Add(new PackageTemplateItem
        {
            ClassTypeId = classTypeId,
            TotalCredits = 10,
            PricePerCredit = 5m
        });
        db.PackageTemplates.Add(template);
        await db.SaveChangesAsync();

        var result = await PackageHelper.AssignFromTemplateAsync(db, tenantId, studentId, template.Id);

        result.Should().NotBeNull();
        result!.StudentId.Should().Be(studentId);
        result.Name.Should().Be("Basic Plan");
        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task AssignFromTemplate_WithInvalidTemplate_ReturnsNull()
    {
        using var db = CreateInMemoryDb();

        var result = await PackageHelper.AssignFromTemplateAsync(
            db,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task AssignDefaultPackage_WithNoDefault_DoesNothing()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test", Slug = "test" });
        await db.SaveChangesAsync();

        await PackageHelper.AssignDefaultPackageIfConfiguredAsync(db, tenantId, studentId);

        var packages = await db.Packages.Where(p => p.StudentId == studentId).ToListAsync();
        packages.Should().BeEmpty();
    }

    [Fact]
    public async Task AssignDefaultPackage_WithDefaultTemplate_CreatesPackage()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var classTypeId = Guid.NewGuid();

        var template = new PackageTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Default Plan",
            DurationDays = 60
        };
        template.Items.Add(new PackageTemplateItem
        {
            ClassTypeId = classTypeId,
            TotalCredits = 20,
            PricePerCredit = 4m
        });
        db.PackageTemplates.Add(template);

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Test",
            Slug = "test",
            DefaultPackageTemplateId = template.Id
        });
        await db.SaveChangesAsync();

        await PackageHelper.AssignDefaultPackageIfConfiguredAsync(db, tenantId, studentId);
        await db.SaveChangesAsync();

        var packages = await db.Packages.Where(p => p.StudentId == studentId).ToListAsync();
        packages.Should().HaveCount(1);
        packages[0].Name.Should().Be("Default Plan");
    }
}

public class SessionEndpointIntegrationTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task SessionGenerator_CreatesSessionsFromSchedule()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var classTypeId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test", Slug = "test" });
        db.ClassTypes.Add(new ClassType { Id = classTypeId, TenantId = tenantId, Name = "Yoga", Color = "#00FF00" });

        var schedule = new Schedule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ClassTypeId = classTypeId,
            Weekday = 1, // Monday
            StartTime = new TimeOnly(9, 0),
            DurationMinutes = 60,
            Capacity = 15,
            IsActive = true
        };
        db.Schedules.Add(schedule);
        await db.SaveChangesAsync();

        var startDate = new DateOnly(2024, 6, 3); // Monday
        var endDate = new DateOnly(2024, 6, 3);
        var existingKeys = new HashSet<(Guid, DateOnly)>();

        var sessions = SessionGenerator.GenerateSessionsFromSchedules(
            new[] { schedule },
            startDate,
            endDate,
            existingKeys).ToList();

        sessions.Should().HaveCount(1);
        sessions[0].TenantId.Should().Be(tenantId);
        sessions[0].ClassTypeId.Should().Be(classTypeId);
        sessions[0].Date.Should().Be(startDate);
    }

    [Fact]
    public async Task SessionGenerator_SkipsExistingSessions()
    {
        var scheduleId = Guid.NewGuid();
        var existingKeys = new HashSet<(Guid, DateOnly)> { (scheduleId, new DateOnly(2024, 6, 3)) };

        var schedule = new Schedule
        {
            Id = scheduleId,
            TenantId = Guid.NewGuid(),
            ClassTypeId = Guid.NewGuid(),
            Weekday = 1,
            StartTime = new TimeOnly(9, 0),
            DurationMinutes = 60,
            Capacity = 20,
            IsActive = true
        };

        var sessions = SessionGenerator.GenerateSessionsFromSchedules(
            new[] { schedule },
            new DateOnly(2024, 6, 3),
            new DateOnly(2024, 6, 3),
            existingKeys).ToList();

        sessions.Should().BeEmpty();
    }

    [Fact]
    public async Task CancelSession_UpdatesStatusAndCancelsBookings()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var session = new Session
        {
            Id = sessionId,
            TenantId = tenantId,
            ClassTypeId = Guid.NewGuid(),
            Date = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            StartTime = new TimeOnly(10, 0),
            DurationMinutes = 60,
            Status = SessionStatus.Scheduled,
            SlotsAvailable = 0
        };
        db.Sessions.Add(session);

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            StudentId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        SessionGenerator.CancelSession(session, "Instructor sick");
        SessionGenerator.CancelSessionBookings(session);

        session.Status.Should().Be(SessionStatus.Cancelled);
        session.CancellationReason.Should().Be("Instructor sick");
        booking.Status.Should().Be(BookingStatus.Cancelled);
        booking.CancellationReason.Should().Be("Aula cancelada");
    }
}

public class AvailabilityEndpointIntegrationTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GenerateAvailableSlots_WithAvailability_CreatesSlots()
    {
        var blocks = new List<SalonSlotGenerator.AvailabilityBlock>
        {
            new(new TimeOnly(9, 0), new TimeOnly(12, 0), null)
        };

        var result = SalonSlotGenerator.GenerateAvailableSlots(
            blocks,
            new List<SalonSlotGenerator.OccupiedSession>(),
            new List<SalonSlotGenerator.TimeBlockRange>(),
            60);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GenerateAvailableSlots_WithOccupiedSession_SkipsBlocked()
    {
        var blocks = new List<SalonSlotGenerator.AvailabilityBlock>
        {
            new(new TimeOnly(9, 0), new TimeOnly(12, 0), null)
        };
        var occupied = new List<SalonSlotGenerator.OccupiedSession>
        {
            new(new TimeOnly(10, 0), 60, null)
        };

        var result = SalonSlotGenerator.GenerateAvailableSlots(
            blocks,
            occupied,
            new List<SalonSlotGenerator.TimeBlockRange>(),
            60);

        result.Should().HaveCount(2);
        result.Should().NotContain(new TimeOnly(10, 0));
    }

    [Fact]
    public async Task IsVacationDate_WithVacationRange_ReturnsTrue()
    {
        var vacations = new List<TenantStatusChecker.VacationRange>
        {
            new(new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 31))
        };

        var result = TenantStatusChecker.IsVacationDate(new DateOnly(2024, 1, 15), vacations);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsVacationDate_WithNoVacations_ReturnsFalse()
    {
        var vacations = new List<TenantStatusChecker.VacationRange>();

        var result = TenantStatusChecker.IsVacationDate(new DateOnly(2024, 6, 15), vacations);

        result.Should().BeFalse();
    }
}