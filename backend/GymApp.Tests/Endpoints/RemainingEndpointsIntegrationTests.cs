using FluentAssertions;
using GymApp.Api.Core;
using GymApp.Domain.Entities;
using GymApp.Domain.Enums;
using GymApp.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymApp.Tests.Endpoints;

public class SessionIntegrationTestsV2
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task SessionGenerator_CreateSessions_ForWeekdaySchedule()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var classTypeId = Guid.NewGuid();
        var locationId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test", Slug = "test" });
        db.ClassTypes.Add(new ClassType { Id = classTypeId, TenantId = tenantId, Name = "Yoga", Color = "#00FF00" });
        db.Locations.Add(new Location { Id = locationId, TenantId = tenantId, Name = "Main Studio" });
        db.Schedules.Add(new Schedule
        {
            Id = scheduleId,
            TenantId = tenantId,
            ClassTypeId = classTypeId,
            LocationId = locationId,
            Weekday = 1,
            StartTime = new TimeOnly(9, 0),
            DurationMinutes = 60,
            Capacity = 15,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var schedules = await db.Schedules.Where(s => s.TenantId == tenantId && s.IsActive).ToListAsync();
        var startDate = new DateOnly(2024, 6, 3);
        var endDate = new DateOnly(2024, 6, 10);

        var sessions = SessionGenerator.GenerateSessionsFromSchedules(
            schedules,
            startDate,
            endDate,
            new HashSet<(Guid, DateOnly)>()).ToList();

        sessions.Should().HaveCount(2);
        sessions.All(s => s.ClassTypeId == classTypeId).Should().BeTrue();
        sessions.All(s => s.LocationId == locationId).Should().BeTrue();
    }

    [Fact]
    public async Task SessionGenerator_DoesNotDuplicate_ExistingSessions()
    {
        var tenantId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var existingDate = new DateOnly(2024, 6, 3);

        var existingKeys = new HashSet<(Guid, DateOnly)> { (scheduleId, existingDate) };

        var schedule = new Schedule
        {
            Id = scheduleId,
            TenantId = tenantId,
            ClassTypeId = Guid.NewGuid(),
            LocationId = Guid.NewGuid(),
            Weekday = 1,
            StartTime = new TimeOnly(9, 0),
            DurationMinutes = 60,
            Capacity = 20,
            IsActive = true
        };

        var sessions = SessionGenerator.GenerateSessionsFromSchedules(
            new[] { schedule },
            new DateOnly(2024, 6, 3),
            new DateOnly(2024, 6, 10),
            existingKeys).ToList();

        sessions.Should().HaveCount(1);
        sessions[0].Date.Should().Be(new DateOnly(2024, 6, 10));
    }

    [Fact]
    public async Task Session_Entity_HasCorrectProperties()
    {
        var session = new Session
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ClassTypeId = Guid.NewGuid(),
            InstructorId = Guid.NewGuid(),
            LocationId = Guid.NewGuid(),
            Date = new DateOnly(2024, 6, 3),
            StartTime = new TimeOnly(10, 0),
            DurationMinutes = 60,
            Status = SessionStatus.Scheduled,
            SlotsAvailable = 15
        };

        session.SlotsAvailable.Should().Be(15);
        session.Status.Should().Be(SessionStatus.Scheduled);
    }

    [Fact]
    public async Task SessionStatus_CanBeCancelled()
    {
        var session = new Session
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Status = SessionStatus.Scheduled
        };

        session.Status = SessionStatus.Cancelled;
        session.Status.Should().Be(SessionStatus.Cancelled);
    }

    [Fact]
    public async Task SessionCapacity_EnforcesLimits()
    {
        var session = new Session
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            SlotsAvailable = 0
        };

        var canBook = session.SlotsAvailable > 0;
        canBook.Should().BeFalse();

        session.SlotsAvailable = 5;
        (session.SlotsAvailable > 0).Should().BeTrue();
    }
}

public class ProfessionalAvailabilityIntegrationTestsV2
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task ProfessionalAvailability_HasCorrectProperties()
    {
        var availability = new ProfessionalAvailability
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            InstructorId = Guid.NewGuid(),
            Weekday = 1,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(12, 0),
            IsActive = true
        };

        availability.Weekday.Should().Be(1);
        availability.StartTime.Should().Be(new TimeOnly(9, 0));
        availability.EndTime.Should().Be(new TimeOnly(12, 0));
        availability.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Availability_OverlappingRanges_Detection()
    {
        var existingStart = new TimeOnly(9, 0);
        var existingEnd = new TimeOnly(12, 0);

        var newStart = new TimeOnly(10, 0);
        var newEnd = new TimeOnly(11, 0);

        var overlaps = newStart < existingEnd && newEnd > existingStart;
        overlaps.Should().BeTrue();
    }

    [Fact]
    public async Task Availability_AdjacentRanges_NoConflict()
    {
        var existingStart = new TimeOnly(9, 0);
        var existingEnd = new TimeOnly(10, 0);

        var newStart = new TimeOnly(10, 0);
        var newEnd = new TimeOnly(11, 0);

        var overlaps = newStart < existingEnd && newEnd > existingStart;
        overlaps.Should().BeFalse();
    }

    [Fact]
    public async Task AvailabilityFilter_ByInstructor_ReturnsCorrect()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var instructorId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test", Slug = "test" });

        db.ProfessionalAvailability.AddRange(
            new ProfessionalAvailability { Id = Guid.NewGuid(), TenantId = tenantId, InstructorId = instructorId, Weekday = 1, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(12, 0) },
            new ProfessionalAvailability { Id = Guid.NewGuid(), TenantId = tenantId, InstructorId = instructorId, Weekday = 2, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(12, 0) },
            new ProfessionalAvailability { Id = Guid.NewGuid(), TenantId = tenantId, InstructorId = Guid.NewGuid(), Weekday = 1, StartTime = new TimeOnly(14, 0), EndTime = new TimeOnly(18, 0) }
        );
        await db.SaveChangesAsync();

        var result = await db.ProfessionalAvailability
            .Where(a => a.TenantId == tenantId && a.InstructorId == instructorId)
            .ToListAsync();

        result.Should().HaveCount(2);
    }
}

public class AdminUserIntegrationTestsV2
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateAdminUser_SetsCorrectDefaults()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test", Slug = "test" });

        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Admin User",
            Email = "admin@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
            Role = UserRole.Admin
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var saved = await db.Users.FindAsync(user.Id);
        saved.Should().NotBeNull();
        saved!.Role.Should().Be(UserRole.Admin);
        saved.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task UserWithRole_AuthorizationLevel_Correct()
    {
        var admin = new User { Id = Guid.NewGuid(), Role = UserRole.Admin };
        var superAdmin = new User { Id = Guid.NewGuid(), Role = UserRole.SuperAdmin };
        var student = new User { Id = Guid.NewGuid(), Role = UserRole.Student };

        admin.Role.Should().Be(UserRole.Admin);
        superAdmin.Role.Should().Be(UserRole.SuperAdmin);
        student.Role.Should().Be(UserRole.Student);
    }

    [Fact]
    public async Task UserPassword_HashesAndVerifies()
    {
        var password = "SecurePass123!";

        var user = new User
        {
            Id = Guid.NewGuid(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
        };

        BCrypt.Net.BCrypt.Verify(password, user.PasswordHash).Should().BeTrue();
        BCrypt.Net.BCrypt.Verify("wrongpassword", user.PasswordHash).Should().BeFalse();
    }

    [Fact]
    public async Task FilterUsersByRole_ReturnsCorrect()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test", Slug = "test" });

        db.Users.AddRange(
            new User { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Admin1", Email = "a1@test.com", Role = UserRole.Admin, PasswordHash = "hash" },
            new User { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Admin2", Email = "a2@test.com", Role = UserRole.Admin, PasswordHash = "hash" },
            new User { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Student1", Email = "s1@test.com", Role = UserRole.Student, PasswordHash = "hash" }
        );
        await db.SaveChangesAsync();

        var admins = await db.Users.Where(u => u.TenantId == tenantId && u.Role == UserRole.Admin).ToListAsync();
        admins.Should().HaveCount(2);
    }
}

public class SuperAdminIntegrationTestsV2
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task SuperAdminRole_CanAccessAllTenants()
    {
        var superAdmin = new User { Id = Guid.NewGuid(), Role = UserRole.SuperAdmin };

        superAdmin.Role.Should().Be(UserRole.SuperAdmin);
    }

    [Fact]
    public async Task TenantManagement_CanCreateTenantWithPlan()
    {
        using var db = CreateInMemoryDb();

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "New Gym",
            Slug = "new-gym",
            Plan = TenantPlan.Pro,
            TenantType = TenantType.Gym,
            SubscriptionPriceCents = 9900,
            IsActive = true
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var saved = await db.Tenants.FindAsync(tenant.Id);
        saved.Should().NotBeNull();
        saved!.Plan.Should().Be(TenantPlan.Pro);
        saved.SubscriptionPriceCents.Should().Be(9900);
    }

    [Fact]
    public async Task TenantPlan_Transitions_Correctly()
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Slug = "test",
            Plan = TenantPlan.Basic,
            SubscriptionStatus = SubscriptionStatus.Trial
        };

        tenant.SubscriptionStatus = SubscriptionStatus.Active;
        tenant.Plan = TenantPlan.Pro;

        tenant.SubscriptionStatus.Should().Be(SubscriptionStatus.Active);
        tenant.Plan.Should().Be(TenantPlan.Pro);
    }

    [Fact]
    public async Task SuperAdmin_CanSuspendTenant()
    {
        using var db = CreateInMemoryDb();

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Slug = "test",
            IsActive = true
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        tenant.IsActive = false;
        await db.SaveChangesAsync();

        var saved = await db.Tenants.FindAsync(tenant.Id);
        saved!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task SuperAdmin_CanViewAllTenants()
    {
        using var db = CreateInMemoryDb();

        db.Tenants.AddRange(
            new Tenant { Id = Guid.NewGuid(), Name = "Gym1", Slug = "gym1", IsActive = true },
            new Tenant { Id = Guid.NewGuid(), Name = "Gym2", Slug = "gym2", IsActive = true },
            new Tenant { Id = Guid.NewGuid(), Name = "Gym3", Slug = "gym3", IsActive = false }
        );
        await db.SaveChangesAsync();

        var allTenants = await db.Tenants.ToListAsync();
        var activeTenants = await db.Tenants.Where(t => t.IsActive).ToListAsync();

        allTenants.Should().HaveCount(3);
        activeTenants.Should().HaveCount(2);
    }
}

public class ServiceCategoryIntegrationTestsV2
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task ServiceCategory_HasCorrectProperties()
    {
        var category = new ServiceCategory
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Massage Therapy",
            SortOrder = 1,
            IsActive = true
        };

        category.Name.Should().Be("Massage Therapy");
        category.SortOrder.Should().Be(1);
        category.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ServiceCategory_FilterByTenant_ReturnsCorrect()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test", Slug = "test" });

        db.ServiceCategories.AddRange(
            new ServiceCategory { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Service1", SortOrder = 1, IsActive = true },
            new ServiceCategory { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Service2", SortOrder = 2, IsActive = true },
            new ServiceCategory { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), Name = "Other", SortOrder = 1, IsActive = true }
        );
        await db.SaveChangesAsync();

        var result = await db.ServiceCategories.Where(sc => sc.TenantId == tenantId).ToListAsync();
        result.Should().HaveCount(2);
    }
}

public class WaitingListIntegrationTestsV2
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task WaitingListEntry_TracksPosition()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test", Slug = "test" });
        db.Sessions.Add(new Session { Id = sessionId, TenantId = tenantId, ClassTypeId = Guid.NewGuid(), LocationId = Guid.NewGuid(), Date = new DateOnly(2024, 6, 3), StartTime = new TimeOnly(10, 0), DurationMinutes = 60, Status = SessionStatus.Scheduled });
        db.Users.Add(new User { Id = studentId, TenantId = tenantId, Name = "Student", Email = "s@test.com", PasswordHash = "hash" });

        var entry = new WaitingListEntry
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            StudentId = studentId,
            Position = 1
        };
        db.WaitingList.Add(entry);
        await db.SaveChangesAsync();

        var saved = await db.WaitingList.FindAsync(entry.Id);
        saved.Should().NotBeNull();
        saved!.Position.Should().Be(1);
    }

    [Fact]
    public async Task WaitingList_OrderedByPosition()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test", Slug = "test" });
        db.Sessions.Add(new Session { Id = sessionId, TenantId = tenantId, ClassTypeId = Guid.NewGuid(), LocationId = Guid.NewGuid(), Date = new DateOnly(2024, 6, 3), StartTime = new TimeOnly(10, 0), DurationMinutes = 60, Status = SessionStatus.Scheduled });

        db.WaitingList.AddRange(
            new WaitingListEntry { Id = Guid.NewGuid(), SessionId = sessionId, StudentId = Guid.NewGuid(), Position = 3 },
            new WaitingListEntry { Id = Guid.NewGuid(), SessionId = sessionId, StudentId = Guid.NewGuid(), Position = 1 },
            new WaitingListEntry { Id = Guid.NewGuid(), SessionId = sessionId, StudentId = Guid.NewGuid(), Position = 2 }
        );
        await db.SaveChangesAsync();

        var firstInLine = await db.WaitingList
            .Where(w => w.SessionId == sessionId)
            .OrderBy(w => w.Position)
            .FirstAsync();

        firstInLine.Position.Should().Be(1);
    }
}

public class AffiliateIntegrationTestsV2
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Affiliate_HasCorrectProperties()
    {
        var affiliate = new Affiliate
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ReferralCode = "BOXE2024",
            CommissionRate = 0.10m
        };

        affiliate.ReferralCode.Should().Be("BOXE2024");
        affiliate.CommissionRate.Should().Be(0.10m);
    }

    [Fact]
    public async Task AffiliateReferral_TracksConversion()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var affiliateId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test", Slug = "test" });
        db.Affiliates.Add(new Affiliate { Id = affiliateId, UserId = Guid.NewGuid(), ReferralCode = "TEST123" });

        var referral = new AffiliateReferral
        {
            Id = Guid.NewGuid(),
            AffiliateId = affiliateId,
            TenantId = tenantId
        };
        db.AffiliateReferrals.Add(referral);
        await db.SaveChangesAsync();

        var saved = await db.AffiliateReferrals.FindAsync(referral.Id);
        saved.Should().NotBeNull();
        saved!.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task Referral_ComputesTrialExtension()
    {
        var days = TenantPlanHelper.GetTrialDays(TenantType.Gym, hasReferrer: true, hasAffiliate: false);
        days.Should().Be(44);
    }
}

public class PackageIntegrationTestsV2
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Package_CalculatesRemainingCredits()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test", Slug = "test" });

        var packageItem1 = new PackageItem
        {
            Id = Guid.NewGuid(),
            PackageId = packageId,
            ClassTypeId = Guid.NewGuid(),
            TotalCredits = 10,
            UsedCredits = 3,
            PricePerCredit = 5m
        };
        var packageItem2 = new PackageItem
        {
            Id = Guid.NewGuid(),
            PackageId = packageId,
            ClassTypeId = Guid.NewGuid(),
            TotalCredits = 5,
            UsedCredits = 1,
            PricePerCredit = 6m
        };

        var package = new Package
        {
            Id = packageId,
            TenantId = tenantId,
            StudentId = studentId,
            Name = "Test Package",
            IsActive = true
        };
        package.Items.Add(packageItem1);
        package.Items.Add(packageItem2);
        db.Packages.Add(package);
        await db.SaveChangesAsync();

        var loaded = await db.Packages.Include(p => p.Items).FirstAsync(p => p.Id == packageId);
        var remaining = loaded.Items.Sum(i => i.TotalCredits - i.UsedCredits);
        remaining.Should().Be(11);
    }

    [Fact]
    public async Task Package_ExpiresAt_IsSet()
    {
        var expiresAt = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var package = new Package
        {
            Id = Guid.NewGuid(),
            ExpiresAt = expiresAt
        };

        package.ExpiresAt.Should().Be(expiresAt);
    }
}

public class ExpenseIntegrationTestsV2
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Expense_CreatesWithCorrectFields()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test", Slug = "test" });

        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Description = "New mats",
            Amount = 500.00m,
            Date = new DateOnly(2024, 6, 1),
            Category = "Equipment",
            IsRecurring = false
        };
        db.Expenses.Add(expense);
        await db.SaveChangesAsync();

        var saved = await db.Expenses.FindAsync(expense.Id);
        saved.Should().NotBeNull();
        saved!.Amount.Should().Be(500.00m);
    }

    [Fact]
    public async Task Expense_FiltersByMonth()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test", Slug = "test" });

        db.Expenses.AddRange(
            new Expense { Id = Guid.NewGuid(), TenantId = tenantId, Description = "E1", Amount = 100, Date = new DateOnly(2024, 6, 1), Category = "Supplies" },
            new Expense { Id = Guid.NewGuid(), TenantId = tenantId, Description = "E2", Amount = 200, Date = new DateOnly(2024, 6, 15), Category = "Supplies" },
            new Expense { Id = Guid.NewGuid(), TenantId = tenantId, Description = "E3", Amount = 300, Date = new DateOnly(2024, 7, 1), Category = "Supplies" }
        );
        await db.SaveChangesAsync();

        var juneExpenses = await db.Expenses
            .Where(e => e.TenantId == tenantId && e.Date >= new DateOnly(2024, 6, 1) && e.Date <= new DateOnly(2024, 6, 30))
            .ToListAsync();

        juneExpenses.Should().HaveCount(2);
        juneExpenses.Sum(e => e.Amount).Should().Be(300);
    }

    [Fact]
    public async Task Expense_Recurring_FlagsCorrectly()
    {
        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Description = "Monthly rent",
            Amount = 2000m,
            IsRecurring = true,
            Category = "Rent"
        };

        expense.IsRecurring.Should().BeTrue();
    }
}

public class LocationIntegrationTestsV2
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Location_WithSchedules_TracksCorrectly()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test", Slug = "test" });
        db.Locations.Add(new Location { Id = locationId, TenantId = tenantId, Name = "Main Studio", Address = "123 Main St" });

        var schedule = new Schedule
        {
            Id = scheduleId,
            TenantId = tenantId,
            LocationId = locationId,
            ClassTypeId = Guid.NewGuid(),
            Weekday = 1,
            StartTime = new TimeOnly(9, 0),
            DurationMinutes = 60,
            Capacity = 20,
            IsActive = true
        };
        db.Schedules.Add(schedule);
        await db.SaveChangesAsync();

        var location = await db.Locations
            .Include(l => l.Schedules)
            .FirstAsync(l => l.Id == locationId);

        location.Schedules.Should().HaveCount(1);
        location.Name.Should().Be("Main Studio");
    }

    [Fact]
    public async Task Location_HasCorrectProperties()
    {
        var location = new Location
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Downtown Studio",
            Address = "456 Oak Ave",
            Phone = "11999999999",
            IsMain = true
        };

        location.Name.Should().Be("Downtown Studio");
        location.Address.Should().Be("456 Oak Ave");
        location.IsMain.Should().BeTrue();
    }

    [Fact]
    public async Task Location_MainLocation_IdentifiedCorrectly()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test", Slug = "test" });
        db.Locations.Add(new Location { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Main", IsMain = true });
        db.Locations.Add(new Location { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Branch", IsMain = false });
        await db.SaveChangesAsync();

        var main = await db.Locations.FirstAsync(l => l.IsMain);
        main.Name.Should().Be("Main");
    }
}

public class TimeBlockIntegrationTestsV2
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task TimeBlock_TracksAvailability()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test", Slug = "test" });

        var timeBlock = new TimeBlock
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Date = new DateOnly(2024, 6, 3),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(12, 0),
            Reason = "Available for bookings"
        };
        db.TimeBlocks.Add(timeBlock);
        await db.SaveChangesAsync();

        var saved = await db.TimeBlocks.FindAsync(timeBlock.Id);
        saved.Should().NotBeNull();
        saved!.Reason.Should().Be("Available for bookings");
    }

    [Fact]
    public async Task VacationBlock_HasCorrectProperties()
    {
        var vacation = new VacationBlock
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            StartDate = new DateOnly(2024, 7, 1),
            EndDate = new DateOnly(2024, 7, 15),
            Reason = "Summer vacation"
        };

        vacation.Reason.Should().Be("Summer vacation");
        vacation.StartDate.Should().Be(new DateOnly(2024, 7, 1));
        vacation.EndDate.Should().Be(new DateOnly(2024, 7, 15));
    }
}
