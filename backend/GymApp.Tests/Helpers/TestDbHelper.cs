using GymApp.Domain.Entities;
using GymApp.Domain.Enums;
using GymApp.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace GymApp.Tests.Helpers;

public static class TestDbHelper
{
    public static AppDbContext CreateInMemoryDb(string name = null!)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: name ?? Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    public static async Task<Tenant> CreateTestTenant(AppDbContext db, string slug = "test-gym")
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Test Gym",
            Slug = slug,
            PrimaryColor = "#1a1a2e",
            SecondaryColor = "#e94560",
            TenantType = TenantType.Gym,
            Plan = TenantPlan.Basic,
            SubscriptionPriceCents = 4900,
            SubscriptionStatus = SubscriptionStatus.Trial,
            IsActive = true
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant;
    }

    public static async Task<User> CreateTestUser(AppDbContext db, Guid tenantId, UserRole role = UserRole.Student, string email = null!)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Test User",
            Email = email ?? $"{Guid.NewGuid()}@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
            Role = role,
            Status = StudentStatus.Active
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public static async Task<ClassType> CreateTestService(AppDbContext db, Guid tenantId, string name = "Boxing")
    {
        var service = new ClassType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Color = "#FF0000",
            IsActive = true,
            DurationMinutes = 60,
            Price = 50m
        };
        db.ClassTypes.Add(service);
        await db.SaveChangesAsync();
        return service;
    }

    public static async Task<Session> CreateTestSession(AppDbContext db, Guid tenantId, Guid classTypeId, DateOnly date, TimeOnly startTime, int capacity = 20)
    {
        var session = new Session
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ClassTypeId = classTypeId,
            Date = date,
            StartTime = startTime,
            DurationMinutes = 60,
            Status = SessionStatus.Scheduled,
            SlotsAvailable = capacity
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    public static async Task<Package> CreateTestPackage(AppDbContext db, Guid tenantId, Guid studentId)
    {
        var package = new Package
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StudentId = studentId,
            Name = "Test Package",
            IsActive = true,
            ExpiresAt = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30))
        };
        db.Packages.Add(package);
        await db.SaveChangesAsync();
        return package;
    }

    public static async Task<PackageItem> CreateTestPackageItem(AppDbContext db, Guid packageId, Guid classTypeId)
    {
        var item = new PackageItem
        {
            Id = Guid.NewGuid(),
            PackageId = packageId,
            ClassTypeId = classTypeId,
            TotalCredits = 10,
            UsedCredits = 0,
            PricePerCredit = 5m
        };
        db.PackageItems.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    public static async Task<Booking> CreateTestBooking(AppDbContext db, Guid sessionId, Guid studentId)
    {
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            StudentId = studentId,
            Status = BookingStatus.Confirmed
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        return booking;
    }

    public static async Task<Schedule> CreateTestSchedule(AppDbContext db, Guid tenantId, Guid classTypeId, int weekday)
    {
        var schedule = new Schedule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ClassTypeId = classTypeId,
            Weekday = weekday,
            StartTime = new TimeOnly(9, 0),
            DurationMinutes = 60,
            Capacity = 20,
            IsActive = true
        };
        db.Schedules.Add(schedule);
        await db.SaveChangesAsync();
        return schedule;
    }

    public static async Task<Location> CreateTestLocation(AppDbContext db, Guid tenantId, bool isMain = true)
    {
        var location = new Location
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Main Location",
            IsMain = isMain
        };
        db.Locations.Add(location);
        await db.SaveChangesAsync();
        return location;
    }

    public static async Task<ProfessionalAvailability> CreateTestAvailability(AppDbContext db, Guid tenantId, Guid instructorId, int weekday)
    {
        var availability = new ProfessionalAvailability
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InstructorId = instructorId,
            Weekday = weekday,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(18, 0),
            IsActive = true
        };
        db.ProfessionalAvailability.Add(availability);
        await db.SaveChangesAsync();
        return availability;
    }

    public static async Task<Instructor> CreateTestInstructor(AppDbContext db, Guid tenantId, string name = "John Instructor")
    {
        var user = await CreateTestUser(db, tenantId, UserRole.Admin, $"{Guid.NewGuid()}@instructor.com");
        user.Name = name;

        var instructor = new Instructor
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = user.Id
        };
        db.Instructors.Add(instructor);
        await db.SaveChangesAsync();
        return instructor;
    }
}