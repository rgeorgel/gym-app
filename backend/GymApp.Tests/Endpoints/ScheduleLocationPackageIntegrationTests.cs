using FluentAssertions;
using GymApp.Api.Core;
using GymApp.Api.Helpers;
using GymApp.Domain.Entities;
using GymApp.Domain.Enums;
using GymApp.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymApp.Tests.Endpoints;

public class ScheduleFlowIntegrationTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateSchedule_WithValidData_CreatesSuccessfully()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var classTypeId = Guid.NewGuid();
        var locationId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test Gym", Slug = "test" });
        db.ClassTypes.Add(new ClassType { Id = classTypeId, TenantId = tenantId, Name = "Yoga", Color = "#00FF00" });
        db.Locations.Add(new Location { Id = locationId, TenantId = tenantId, Name = "Main Studio" });
        await db.SaveChangesAsync();

        var schedule = new Schedule
        {
            TenantId = tenantId,
            ClassTypeId = classTypeId,
            LocationId = locationId,
            Weekday = 1,
            StartTime = new TimeOnly(9, 0),
            DurationMinutes = 60,
            Capacity = 20,
            IsActive = true
        };
        db.Schedules.Add(schedule);
        await db.SaveChangesAsync();

        var saved = await db.Schedules.Include(s => s.ClassType).FirstAsync(s => s.Id == schedule.Id);
        saved.ClassType.Name.Should().Be("Yoga");
        saved.Weekday.Should().Be(1);
        saved.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateSchedule_ChangesAllFields()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var classTypeId = Guid.NewGuid();
        var newClassTypeId = Guid.NewGuid();
        var locationId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test Gym", Slug = "test" });
        db.ClassTypes.Add(new ClassType { Id = classTypeId, TenantId = tenantId, Name = "Yoga", Color = "#00FF00" });
        db.ClassTypes.Add(new ClassType { Id = newClassTypeId, TenantId = tenantId, Name = "Pilates", Color = "#FF00FF" });
        db.Locations.Add(new Location { Id = locationId, TenantId = tenantId, Name = "Studio A" });
        await db.SaveChangesAsync();

        var schedule = new Schedule
        {
            TenantId = tenantId,
            ClassTypeId = classTypeId,
            LocationId = locationId,
            Weekday = 1,
            StartTime = new TimeOnly(9, 0),
            DurationMinutes = 60,
            Capacity = 20,
            IsActive = true
        };
        db.Schedules.Add(schedule);
        await db.SaveChangesAsync();

        schedule.ClassTypeId = newClassTypeId;
        schedule.Weekday = 3;
        schedule.StartTime = new TimeOnly(14, 0);
        schedule.DurationMinutes = 90;
        schedule.Capacity = 15;
        await db.SaveChangesAsync();

        var updated = await db.Schedules.AsNoTracking().FirstAsync(s => s.Id == schedule.Id);
        updated.Weekday.Should().Be(3);
        updated.StartTime.Should().Be(new TimeOnly(14, 0));
        updated.DurationMinutes.Should().Be(90);
        updated.Capacity.Should().Be(15);
    }

    [Fact]
    public async Task DeleteSchedule_SetsIsActiveToFalse()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var schedule = new Schedule
        {
            TenantId = tenantId,
            ClassTypeId = Guid.NewGuid(),
            LocationId = Guid.NewGuid(),
            Weekday = 1,
            StartTime = new TimeOnly(9, 0),
            DurationMinutes = 60,
            Capacity = 20,
            IsActive = true
        };
        db.Schedules.Add(schedule);
        await db.SaveChangesAsync();

        schedule.IsActive = false;
        await db.SaveChangesAsync();

        var deleted = await db.Schedules.AsNoTracking().FirstAsync(s => s.Id == schedule.Id);
        deleted.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetSchedules_ByTenantAndActive_ReturnsOnlyActive()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();

        db.Schedules.Add(new Schedule { TenantId = tenantId, ClassTypeId = Guid.NewGuid(), LocationId = Guid.NewGuid(), Weekday = 1, StartTime = new TimeOnly(9, 0), IsActive = true });
        db.Schedules.Add(new Schedule { TenantId = tenantId, ClassTypeId = Guid.NewGuid(), LocationId = Guid.NewGuid(), Weekday = 2, StartTime = new TimeOnly(10, 0), IsActive = true });
        db.Schedules.Add(new Schedule { TenantId = tenantId, ClassTypeId = Guid.NewGuid(), LocationId = Guid.NewGuid(), Weekday = 3, StartTime = new TimeOnly(11, 0), IsActive = false });
        await db.SaveChangesAsync();

        var activeSchedules = await db.Schedules.Where(s => s.TenantId == tenantId && s.IsActive).ToListAsync();
        activeSchedules.Should().HaveCount(2);
    }

    [Fact]
    public async Task GroupSchedulesByWeekday_GroupsCorrectly()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();

        db.Schedules.Add(new Schedule { TenantId = tenantId, ClassTypeId = Guid.NewGuid(), LocationId = Guid.NewGuid(), Weekday = 1, StartTime = new TimeOnly(9, 0), IsActive = true });
        db.Schedules.Add(new Schedule { TenantId = tenantId, ClassTypeId = Guid.NewGuid(), LocationId = Guid.NewGuid(), Weekday = 1, StartTime = new TimeOnly(10, 0), IsActive = true });
        db.Schedules.Add(new Schedule { TenantId = tenantId, ClassTypeId = Guid.NewGuid(), LocationId = Guid.NewGuid(), Weekday = 3, StartTime = new TimeOnly(14, 0), IsActive = true });
        await db.SaveChangesAsync();

        var schedules = await db.Schedules.Where(s => s.TenantId == tenantId && s.IsActive).ToListAsync();
        var grouped = schedules
            .GroupBy(s => s.Weekday)
            .ToDictionary(g => g.Key, g => g.ToList());

        grouped.Keys.Should().Contain(1);
        grouped.Keys.Should().Contain(3);
        grouped[1].Should().HaveCount(2);
        grouped[3].Should().HaveCount(1);
    }

    [Fact]
    public async Task SessionGenerator_FromSchedule_CreatesSessionOnCorrectDate()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var classTypeId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test", Slug = "test" });
        db.ClassTypes.Add(new ClassType { Id = classTypeId, TenantId = tenantId, Name = "Yoga", Color = "#00FF00" });
        await db.SaveChangesAsync();

        var schedule = new Schedule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ClassTypeId = classTypeId,
            LocationId = Guid.NewGuid(),
            Weekday = 1,
            StartTime = new TimeOnly(9, 0),
            DurationMinutes = 60,
            Capacity = 20,
            IsActive = true
        };
        db.Schedules.Add(schedule);

        var targetDate = new DateOnly(2024, 6, 3);
        var existingKeys = new HashSet<(Guid, DateOnly)>();

        var sessions = SessionGenerator.GenerateSessionsFromSchedules(
            new[] { schedule },
            targetDate,
            targetDate,
            existingKeys).ToList();

        sessions.Should().HaveCount(1);
        sessions[0].Date.Should().Be(targetDate);
        sessions[0].ClassTypeId.Should().Be(classTypeId);
    }

    [Fact]
    public async Task ScheduleWithInstructor_IncludesInstructorInfo()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "John Instructor",
            Email = "john@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass"),
            Role = UserRole.Admin
        };
        db.Users.Add(user);

        var instructor = new Instructor
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = user.Id
        };
        db.Instructors.Add(instructor);

        var schedule = new Schedule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ClassTypeId = Guid.NewGuid(),
            LocationId = Guid.NewGuid(),
            InstructorId = instructor.Id,
            Weekday = 1,
            StartTime = new TimeOnly(9, 0),
            IsActive = true
        };
        db.Schedules.Add(schedule);
        await db.SaveChangesAsync();

        var loaded = await db.Schedules.Include(s => s.Instructor).ThenInclude(i => i!.User).FirstAsync(s => s.Id == schedule.Id);
        loaded.Instructor.Should().NotBeNull();
        loaded.Instructor!.User.Name.Should().Be("John Instructor");
    }
}

public class LocationFlowIntegrationTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateLocation_WithValidData_CreatesSuccessfully()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test Gym", Slug = "test" });
        await db.SaveChangesAsync();

        var location = new Location
        {
            TenantId = tenantId,
            Name = "Main Studio",
            Address = "123 Main St",
            Phone = "11999999999",
            IsMain = true
        };
        db.Locations.Add(location);
        await db.SaveChangesAsync();

        var saved = await db.Locations.FirstAsync(l => l.TenantId == tenantId);
        saved.Name.Should().Be("Main Studio");
        saved.IsMain.Should().BeTrue();
    }

    [Fact]
    public async Task CreateLocation_AsMain_SetsOthersToNonMain()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test Gym", Slug = "test" });

        var oldMain = new Location
        {
            TenantId = tenantId,
            Name = "Old Main",
            IsMain = true
        };
        db.Locations.Add(oldMain);
        await db.SaveChangesAsync();

        LocationValidator.EnsureSingleMainLocation(oldMain, db.Locations.Where(l => l.TenantId == tenantId));

        var newMain = new Location
        {
            TenantId = tenantId,
            Name = "New Main",
            IsMain = true
        };
        db.Locations.Add(newMain);
        await db.SaveChangesAsync();

        LocationValidator.EnsureSingleMainLocation(newMain, db.Locations.Where(l => l.TenantId == tenantId));

        var locations = await db.Locations.Where(l => l.TenantId == tenantId).ToListAsync();
        locations.Should().Contain(l => l.Name == "New Main" && l.IsMain);
        locations.Should().Contain(l => l.Name == "Old Main" && !l.IsMain);
    }

    [Fact]
    public async Task UpdateLocation_ValidatesNameRequired()
    {
        using var db = CreateInMemoryDb();

        var location = new Location
        {
            TenantId = Guid.NewGuid(),
            Name = "Valid Name",
            IsMain = false
        };
        db.Locations.Add(location);
        await db.SaveChangesAsync();

        location.Name = "Updated Name";
        await db.SaveChangesAsync();

        var updated = await db.Locations.AsNoTracking().FirstAsync(l => l.Id == location.Id);
        updated.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task DeleteLocation_WithFutureSessions_ReturnsError()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var locationId = Guid.NewGuid();

        var location = new Location
        {
            Id = locationId,
            TenantId = tenantId,
            Name = "Studio"
        };
        db.Locations.Add(location);

        var futureSession = new Session
        {
            TenantId = tenantId,
            LocationId = locationId,
            ClassTypeId = Guid.NewGuid(),
            Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            StartTime = new TimeOnly(10, 0),
            DurationMinutes = 60,
            Status = SessionStatus.Scheduled
        };
        db.Sessions.Add(futureSession);
        await db.SaveChangesAsync();

        var futureSessionCount = await db.Sessions
            .Where(s => s.LocationId == locationId && s.Date >= DateOnly.FromDateTime(DateTime.UtcNow))
            .CountAsync();

        futureSessionCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DeleteLocation_LastLocation_ReturnsError()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var location = new Location
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Only Location"
        };
        db.Locations.Add(location);
        await db.SaveChangesAsync();

        var totalLocations = await db.Locations.Where(l => l.TenantId == tenantId).CountAsync();
        totalLocations.Should().Be(1);
    }

    [Fact]
    public async Task GetLocations_OrderedByIsMainFirst()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test", Slug = "test" });

        db.Locations.Add(new Location { TenantId = tenantId, Name = "Branch", IsMain = false });
        db.Locations.Add(new Location { TenantId = tenantId, Name = "Main", IsMain = true });
        db.Locations.Add(new Location { TenantId = tenantId, Name = "Other Branch", IsMain = false });
        await db.SaveChangesAsync();

        var locations = await db.Locations
            .Where(l => l.TenantId == tenantId)
            .OrderBy(l => l.IsMain ? 0 : 1)
            .ThenBy(l => l.Name)
            .ToListAsync();

        locations[0].Name.Should().Be("Main");
    }
}

public class PackageFlowIntegrationTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreatePackage_WithMultipleItems_CreatesSuccessfully()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var classTypeId1 = Guid.NewGuid();
        var classTypeId2 = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test Gym", Slug = "test" });
        db.ClassTypes.Add(new ClassType { Id = classTypeId1, TenantId = tenantId, Name = "Yoga", Color = "#00FF00" });
        db.ClassTypes.Add(new ClassType { Id = classTypeId2, TenantId = tenantId, Name = "Pilates", Color = "#FF00FF" });
        await db.SaveChangesAsync();

        var package = new Package
        {
            TenantId = tenantId,
            StudentId = studentId,
            Name = "Full Plan",
            ExpiresAt = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(90)),
            IsActive = true
        };
        package.Items.Add(new PackageItem { ClassTypeId = classTypeId1, TotalCredits = 20, UsedCredits = 0, PricePerCredit = 4m });
        package.Items.Add(new PackageItem { ClassTypeId = classTypeId2, TotalCredits = 10, UsedCredits = 0, PricePerCredit = 5m });

        db.Packages.Add(package);
        await db.SaveChangesAsync();

        var saved = await db.Packages.Include(p => p.Items).FirstAsync(p => p.Id == package.Id);
        saved.Items.Should().HaveCount(2);
        saved.Items.Sum(i => i.TotalCredits * i.PricePerCredit).Should().Be(130m);
    }

    [Fact]
    public async Task AddPackageItem_ToExistingPackage()
    {
        using var db = CreateInMemoryDb();

        var packageId = Guid.NewGuid();
        var classTypeId = Guid.NewGuid();

        var package = new Package
        {
            Id = packageId,
            TenantId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            Name = "Basic Plan",
            IsActive = true
        };
        db.Packages.Add(package);

        var newItem = new PackageItem
        {
            PackageId = packageId,
            ClassTypeId = classTypeId,
            TotalCredits = 15,
            UsedCredits = 0,
            PricePerCredit = 5m
        };
        db.PackageItems.Add(newItem);
        await db.SaveChangesAsync();

        var loaded = await db.Packages.Include(p => p.Items).FirstAsync(p => p.Id == packageId);
        loaded.Items.Should().HaveCount(1);
        loaded.Items.First().TotalCredits.Should().Be(15);
    }

    [Fact]
    public async Task SoftDeletePackage_SetsIsActiveFalse()
    {
        using var db = CreateInMemoryDb();

        var packageId = Guid.NewGuid();
        var package = new Package
        {
            Id = packageId,
            TenantId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            Name = "To Delete",
            IsActive = true
        };
        db.Packages.Add(package);
        await db.SaveChangesAsync();

        package.IsActive = false;
        await db.SaveChangesAsync();

        var loaded = await db.Packages.AsNoTracking().FirstAsync(p => p.Id == packageId);
        loaded.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetPackages_ByStudentId_FiltersCorrectly()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var studentId1 = Guid.NewGuid();
        var studentId2 = Guid.NewGuid();

        db.Packages.Add(new Package { TenantId = tenantId, StudentId = studentId1, Name = "Student 1 Package", IsActive = true });
        db.Packages.Add(new Package { TenantId = tenantId, StudentId = studentId2, Name = "Student 2 Package", IsActive = true });
        await db.SaveChangesAsync();

        var student1Packages = await db.Packages.Where(p => p.StudentId == studentId1 && p.IsActive).ToListAsync();
        student1Packages.Should().HaveCount(1);
        student1Packages[0].Name.Should().Be("Student 1 Package");
    }

    [Fact]
    public async Task PackageExpiration_CalculatesFromDurationDays()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var classTypeId = Guid.NewGuid();

        var template = new PackageTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "30 Day Plan",
            DurationDays = 30
        };
        template.Items.Add(new PackageTemplateItem { ClassTypeId = classTypeId, TotalCredits = 10, PricePerCredit = 5m });
        db.PackageTemplates.Add(template);
        await db.SaveChangesAsync();

        var createdAt = DateTime.UtcNow;
        var expiresAt = DateOnly.FromDateTime(createdAt.AddDays(template.DurationDays.Value));

        expiresAt.Should().Be(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)));
    }

    [Fact]
    public async Task PackageItemRemainingCredits_CalculatesCorrectly()
    {
        var item = new PackageItem
        {
            TotalCredits = 20,
            UsedCredits = 7
        };

        (item.TotalCredits - item.UsedCredits).Should().Be(13);

        item.UsedCredits++;
        (item.TotalCredits - item.UsedCredits).Should().Be(12);
    }

    [Fact]
    public async Task PackageTotalValue_CalculatesFromItems()
    {
        using var db = CreateInMemoryDb();

        var package = new Package
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            Name = "Premium",
            IsActive = true
        };
        package.Items.Add(new PackageItem { ClassTypeId = Guid.NewGuid(), TotalCredits = 10, UsedCredits = 0, PricePerCredit = 5m });
        package.Items.Add(new PackageItem { ClassTypeId = Guid.NewGuid(), TotalCredits = 5, UsedCredits = 0, PricePerCredit = 10m });

        var totalValue = package.Items.Sum(i => i.TotalCredits * i.PricePerCredit);
        totalValue.Should().Be(100m);
    }

    [Fact]
    public async Task ResponseMapper_PackageResponse_MapsAllFields()
    {
        var package = new Package
        {
            Id = Guid.NewGuid(),
            Name = "Test Package",
            ExpiresAt = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Items = new List<PackageItem>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ClassTypeId = Guid.NewGuid(),
                    TotalCredits = 10,
                    UsedCredits = 2,
                    PricePerCredit = 5m,
                    ClassType = new ClassType { Name = "Yoga", Color = "#00FF00" }
                }
            }
        };

        var response = ResponseMapper.ToPackageResponse(package);

        response.Name.Should().Be("Test Package");
        response.Items.Should().HaveCount(1);
        response.Items[0].RemainingCredits.Should().Be(8);
    }
}

public class ClassTypeFlowIntegrationTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateClassType_WithValidData_CreatesSuccessfully()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test Gym", Slug = "test" });
        await db.SaveChangesAsync();

        var classType = new ClassType
        {
            TenantId = tenantId,
            Name = "Boxing",
            Color = "#FF0000",
            IsActive = true,
            DurationMinutes = 60
        };
        db.ClassTypes.Add(classType);
        await db.SaveChangesAsync();

        var saved = await db.ClassTypes.FirstAsync(c => c.TenantId == tenantId);
        saved.Name.Should().Be("Boxing");
        saved.Color.Should().Be("#FF0000");
    }

    [Fact]
    public async Task UpdateClassType_ChangesColorAndName()
    {
        using var db = CreateInMemoryDb();

        var classTypeId = Guid.NewGuid();
        var classType = new ClassType
        {
            Id = classTypeId,
            TenantId = Guid.NewGuid(),
            Name = "Old Name",
            Color = "#000000",
            IsActive = true
        };
        db.ClassTypes.Add(classType);
        await db.SaveChangesAsync();

        classType.Name = "New Name";
        classType.Color = "#FF0000";
        await db.SaveChangesAsync();

        var updated = await db.ClassTypes.AsNoTracking().FirstAsync(c => c.Id == classTypeId);
        updated.Name.Should().Be("New Name");
        updated.Color.Should().Be("#FF0000");
    }

    [Fact]
    public async Task HexColorValidator_ValidatesCorrectly()
    {
        HexColorValidator.IsValidHexColor("#FF0000").Should().BeTrue();
        HexColorValidator.IsValidHexColor("#00FF00").Should().BeTrue();
        HexColorValidator.IsValidHexColor("#123456").Should().BeTrue();
        HexColorValidator.IsValidHexColor("#ABCDEF").Should().BeTrue();
        HexColorValidator.IsValidHexColor("FF0000").Should().BeFalse();
        HexColorValidator.IsValidHexColor("#FG0000").Should().BeFalse();
        HexColorValidator.IsValidHexColor("#00").Should().BeFalse();
    }

    [Fact]
    public async Task EntityValidator_UrlValidation_ValidatesCorrectly()
    {
        EntityValidator.IsValidUrl("https://example.com").Should().BeTrue();
        EntityValidator.IsValidUrl("http://example.com/path").Should().BeTrue();
        EntityValidator.IsValidUrl("").Should().BeTrue();
        EntityValidator.IsValidUrl("not-a-url").Should().BeFalse();
        EntityValidator.IsValidUrl("example.com").Should().BeFalse();
    }

    [Fact]
    public async Task GetClassTypes_ByTenant_ReturnsAll()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();

        db.ClassTypes.Add(new ClassType { TenantId = tenantId, Name = "Yoga", Color = "#00FF00", IsActive = true });
        db.ClassTypes.Add(new ClassType { TenantId = tenantId, Name = "Pilates", Color = "#FF00FF", IsActive = true });
        db.ClassTypes.Add(new ClassType { TenantId = Guid.NewGuid(), Name = "Other Gym Class", Color = "#0000FF", IsActive = true });
        await db.SaveChangesAsync();

        var classTypes = await db.ClassTypes.Where(c => c.TenantId == tenantId && c.IsActive).ToListAsync();
        classTypes.Should().HaveCount(2);
    }
}

public class InstructorFlowIntegrationTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateInstructor_WithValidUser_CreatesSuccessfully()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            TenantId = tenantId,
            Name = "John Instructor",
            Email = "john@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass"),
            Role = UserRole.Admin
        };
        db.Users.Add(user);

        var instructor = new Instructor
        {
            TenantId = tenantId,
            UserId = userId
        };
        db.Instructors.Add(instructor);
        await db.SaveChangesAsync();

        var saved = await db.Instructors.Include(i => i.User).FirstAsync(i => i.Id == instructor.Id);
        saved.User.Name.Should().Be("John Instructor");
    }

    [Fact]
    public async Task AssignServicesToInstructor_UpdatesServicesList()
    {
        using var db = CreateInMemoryDb();

        var instructorId = Guid.NewGuid();
        var classTypeId = Guid.NewGuid();

        var instructor = new Instructor
        {
            Id = instructorId,
            TenantId = Guid.NewGuid(),
            Services = new List<InstructorService>()
        };
        db.Instructors.Add(instructor);

        var newServiceId = Guid.NewGuid();
        InstructorServiceManager.SyncInstructorServices(
            instructor,
            new[] { newServiceId },
            id => id == newServiceId);

        instructor.Services.Should().HaveCount(1);
        instructor.Services.First().ClassTypeId.Should().Be(newServiceId);
    }

    [Fact]
    public async Task FilterValidServiceIds_ReturnsOnlyExisting()
    {
        var validId = Guid.NewGuid();
        var invalidId1 = Guid.NewGuid();
        var invalidId2 = Guid.NewGuid();

        var existingIds = new List<Guid> { validId };

        var result = InstructorServiceManager.FilterValidServiceIds(
            new[] { validId, invalidId1, invalidId2 },
            existingIds,
            id => existingIds.Contains(id));

        result.Should().HaveCount(1);
        result.Should().Contain(validId);
    }

    [Fact]
    public async Task SyncInstructorServices_ClearsOldAddsNew()
    {
        var instructor = new Instructor
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Services = new List<InstructorService>
            {
                new() { ClassTypeId = Guid.NewGuid() },
                new() { ClassTypeId = Guid.NewGuid() }
            }
        };

        var newServiceId = Guid.NewGuid();

        InstructorServiceManager.SyncInstructorServices(
            instructor,
            new[] { newServiceId },
            id => id == newServiceId);

        instructor.Services.Should().HaveCount(1);
        instructor.Services.First().ClassTypeId.Should().Be(newServiceId);
    }

    [Fact]
    public async Task GetInstructors_ByTenant_ReturnsAll()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();

        var user1 = new User { Id = Guid.NewGuid(), TenantId = tenantId, Name = "John", Email = "j@test.com", PasswordHash = "hash", Role = UserRole.Admin };
        var user2 = new User { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Jane", Email = "ja@test.com", PasswordHash = "hash", Role = UserRole.Admin };

        db.Users.AddRange(user1, user2);
        db.Instructors.Add(new Instructor { TenantId = tenantId, UserId = user1.Id });
        db.Instructors.Add(new Instructor { TenantId = tenantId, UserId = user2.Id });
        db.Instructors.Add(new Instructor { TenantId = Guid.NewGuid(), UserId = Guid.NewGuid() });
        await db.SaveChangesAsync();

        var instructors = await db.Instructors.Where(i => i.TenantId == tenantId).ToListAsync();
        instructors.Should().HaveCount(2);
    }
}

public class WaitingListFlowIntegrationTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task AddToWaitingList_SetsCorrectPosition()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var studentId1 = Guid.NewGuid();
        var studentId2 = Guid.NewGuid();
        var studentId3 = Guid.NewGuid();

        db.Users.Add(new User { Id = studentId1, TenantId = tenantId, Name = "S1", Email = "s1@test.com", PasswordHash = "hash", Role = UserRole.Student });
        db.Users.Add(new User { Id = studentId2, TenantId = tenantId, Name = "S2", Email = "s2@test.com", PasswordHash = "hash", Role = UserRole.Student });
        db.Users.Add(new User { Id = studentId3, TenantId = tenantId, Name = "S3", Email = "s3@test.com", PasswordHash = "hash", Role = UserRole.Student });

        db.WaitingList.Add(new WaitingListEntry { SessionId = sessionId, StudentId = studentId1, Position = 1 });
        db.WaitingList.Add(new WaitingListEntry { SessionId = sessionId, StudentId = studentId2, Position = 2 });
        await db.SaveChangesAsync();

        var position = await db.WaitingList.CountAsync(w => w.SessionId == sessionId) + 1;

        var entry = new WaitingListEntry { SessionId = sessionId, StudentId = studentId3, Position = position };
        db.WaitingList.Add(entry);
        await db.SaveChangesAsync();

        entry.Position.Should().Be(3);
    }

    [Fact]
    public async Task GetWaitingList_OrderedByPosition()
    {
        using var db = CreateInMemoryDb();

        var sessionId = Guid.NewGuid();

        db.WaitingList.Add(new WaitingListEntry { SessionId = sessionId, StudentId = Guid.NewGuid(), Position = 2 });
        db.WaitingList.Add(new WaitingListEntry { SessionId = sessionId, StudentId = Guid.NewGuid(), Position = 1 });
        db.WaitingList.Add(new WaitingListEntry { SessionId = sessionId, StudentId = Guid.NewGuid(), Position = 3 });
        await db.SaveChangesAsync();

        var entries = await db.WaitingList
            .Where(w => w.SessionId == sessionId)
            .OrderBy(w => w.Position)
            .ToListAsync();

        entries[0].Position.Should().Be(1);
        entries[1].Position.Should().Be(2);
        entries[2].Position.Should().Be(3);
    }

    [Fact]
    public async Task RemoveFromWaitingList_RemovesEntry()
    {
        using var db = CreateInMemoryDb();

        var entryId = Guid.NewGuid();
        var entry = new WaitingListEntry
        {
            Id = entryId,
            SessionId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            Position = 1
        };
        db.WaitingList.Add(entry);
        await db.SaveChangesAsync();

        db.WaitingList.Remove(entry);
        await db.SaveChangesAsync();

        var exists = await db.WaitingList.AnyAsync(w => w.Id == entryId);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task PromoteFromWaitingList_RemovesFirstInLine()
    {
        using var db = CreateInMemoryDb();

        var sessionId = Guid.NewGuid();
        var firstStudentId = Guid.NewGuid();

        db.WaitingList.Add(new WaitingListEntry { SessionId = sessionId, StudentId = firstStudentId, Position = 1 });
        db.WaitingList.Add(new WaitingListEntry { SessionId = sessionId, StudentId = Guid.NewGuid(), Position = 2 });
        await db.SaveChangesAsync();

        var firstInLine = await db.WaitingList
            .Where(w => w.SessionId == sessionId)
            .OrderBy(w => w.Position)
            .FirstAsync();

        db.WaitingList.Remove(firstInLine);
        await db.SaveChangesAsync();

        var remaining = await db.WaitingList.Where(w => w.SessionId == sessionId).ToListAsync();
        remaining.Should().HaveCount(1);
        remaining[0].Position.Should().Be(2);
    }
}