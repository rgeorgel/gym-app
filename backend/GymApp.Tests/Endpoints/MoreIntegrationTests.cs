using FluentAssertions;
using GymApp.Api.Core;
using GymApp.Domain.Entities;
using GymApp.Domain.Enums;
using GymApp.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymApp.Tests.Endpoints;

public class TenantEndpointIntegrationTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task TenantConfig_WithValidSlug_ReturnsCorrectConfig()
    {
        using var db = CreateInMemoryDb();

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Boxe Elite",
            Slug = "boxe-elite",
            PrimaryColor = "#1a1a2e",
            SecondaryColor = "#e94560",
            TextColor = "#ffffff",
            LogoUrl = "https://example.com/logo.png",
            Language = "pt-BR",
            TenantType = TenantType.Gym,
            IsActive = true,
            SocialInstagram = "@boxeelite",
            SocialWhatsApp = "5511999999999"
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var resolved = await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == "boxe-elite" && t.IsActive);

        resolved.Should().NotBeNull();
        resolved!.Name.Should().Be("Boxe Elite");
        resolved.PrimaryColor.Should().Be("#1a1a2e");
    }

    [Fact]
    public async Task TenantConfig_WithInvalidSlug_ReturnsNull()
    {
        using var db = CreateInMemoryDb();

        var result = await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == "non-existent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateTenant_SetsCorrectDefaults()
    {
        using var db = CreateInMemoryDb();

        var tenant = new Tenant
        {
            Name = "New Gym",
            Slug = "new-gym",
            PrimaryColor = "#1a1a2e",
            SecondaryColor = "#e94560",
            Plan = TenantPlan.Basic,
            TenantType = TenantType.Gym,
            SubscriptionPriceCents = 4900
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var saved = await db.Tenants.FirstAsync(t => t.Slug == "new-gym");
        saved.SubscriptionPriceCents.Should().Be(4900);
        saved.Plan.Should().Be(TenantPlan.Basic);
    }

    [Fact]
    public async Task UpdateTenant_ChangesColors()
    {
        using var db = CreateInMemoryDb();

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Test Gym",
            Slug = "test-gym",
            PrimaryColor = "#000000",
            SecondaryColor = "#FFFFFF"
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var toUpdate = await db.Tenants.FirstAsync(t => t.Slug == "test-gym");
        toUpdate.PrimaryColor = "#FF0000";
        toUpdate.SecondaryColor = "#00FF00";
        await db.SaveChangesAsync();

        var updated = await db.Tenants.AsNoTracking().FirstAsync(t => t.Slug == "test-gym");
        updated.PrimaryColor.Should().Be("#FF0000");
        updated.SecondaryColor.Should().Be("#00FF00");
    }

    [Fact]
    public void TenantSlugResolver_GenerateSlug_ProducesValidSlug()
    {
        var slug1 = SlugGenerator.GenerateSlug("Boxe Elite Academia");
        var slug2 = SlugGenerator.GenerateSlug("My Gym 24/7 Fitness");

        slug1.Should().Be("boxe-elite-academia");
        slug2.Should().Be("my-gym-247-fitness");
    }

    [Fact]
    public void TenantSlugResolver_GenerateSlug_HandlesSpecialChars()
    {
        var slug = SlugGenerator.GenerateSlug("Academia São Paulo");

        slug.Should().Contain("academia");
        slug.Should().NotContain(" ");
    }

    [Fact]
    public async Task TenantWithReferral_ComputesTrialDays()
    {
        var days = TenantPlanHelper.GetTrialDays(TenantType.Gym, hasReferrer: true, hasAffiliate: false);
        days.Should().Be(44);
    }

    [Fact]
    public async Task TenantWithAffiliate_ComputesTrialDays()
    {
        var days = TenantPlanHelper.GetTrialDays(TenantType.Gym, hasReferrer: false, hasAffiliate: true);
        days.Should().Be(15);
    }

    [Fact]
    public async Task TenantDefault_ComputesTrialDays()
    {
        var days = TenantPlanHelper.GetTrialDays(TenantType.Gym, hasReferrer: false, hasAffiliate: false);
        days.Should().Be(14);
    }
}

public class ScheduleEndpointIntegrationTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task ScheduleResponse_Mapper_CorrectlyMaps()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var classTypeId = Guid.NewGuid();
        var instructorId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test", Slug = "test" });
        db.ClassTypes.Add(new ClassType { Id = classTypeId, TenantId = tenantId, Name = "Yoga", Color = "#00FF00" });

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
            Id = instructorId,
            TenantId = tenantId,
            UserId = user.Id
        };
        db.Instructors.Add(instructor);

        var schedule = new Schedule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ClassTypeId = classTypeId,
            InstructorId = instructorId,
            LocationId = Guid.NewGuid(),
            Weekday = 1,
            StartTime = new TimeOnly(9, 0),
            DurationMinutes = 60,
            Capacity = 20,
            IsActive = true
        };
        db.Schedules.Add(schedule);
        await db.SaveChangesAsync();

        var mapped = ResponseMapper.ToScheduleResponse(schedule);

        mapped.ClassTypeName.Should().Be("Yoga");
        mapped.ClassTypeColor.Should().Be("#00FF00");
        mapped.Weekday.Should().Be(1);
        mapped.Capacity.Should().Be(20);
    }

    [Fact]
    public async Task ScheduleResponse_Mapper_WithNullInstructor()
    {
        var schedule = new Schedule
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ClassTypeId = Guid.NewGuid(),
            InstructorId = null,
            LocationId = Guid.NewGuid(),
            Weekday = 3,
            StartTime = new TimeOnly(14, 0),
            DurationMinutes = 90,
            Capacity = 10,
            IsActive = true,
            ClassType = new ClassType { Name = "Pilates", Color = "#FF00FF" }
        };

        var mapped = ResponseMapper.ToScheduleResponse(schedule);

        mapped.InstructorName.Should().BeNull();
        mapped.ClassTypeName.Should().Be("Pilates");
    }

    [Fact]
    public async Task GenerateSessions_FromSchedule_CreatesCorrectCount()
    {
        var schedule = new Schedule
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ClassTypeId = Guid.NewGuid(),
            LocationId = Guid.NewGuid(),
            Weekday = 1,
            StartTime = new TimeOnly(9, 0),
            DurationMinutes = 60,
            Capacity = 20,
            IsActive = true
        };

        var start = new DateOnly(2024, 6, 3); // Monday
        var end = new DateOnly(2024, 6, 10); // Two weeks

        var sessions = SessionGenerator.GenerateSessionsFromSchedules(
            new[] { schedule },
            start,
            end,
            new HashSet<(Guid, DateOnly)>()).ToList();

        // Should generate for each Monday in range (June 3, June 10)
        sessions.Should().HaveCount(2);
    }
}

public class InstructorEndpointIntegrationTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task FilterValidServiceIds_OnlyReturnsValid()
    {
        var validId = Guid.NewGuid();
        var invalidId = Guid.NewGuid();
        var existingIds = new List<Guid> { validId };

        var result = InstructorServiceManager.FilterValidServiceIds(
            new[] { validId, invalidId },
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
}

public class PackageTemplateIntegrationTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task PackageResponse_Mapper_CorrectlyMaps()
    {
        var package = new Package
        {
            Id = Guid.NewGuid(),
            Name = "Premium Package",
            ExpiresAt = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Items = new List<PackageItem>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ClassTypeId = Guid.NewGuid(),
                    TotalCredits = 20,
                    UsedCredits = 5,
                    PricePerCredit = 4m,
                    ClassType = new ClassType { Name = "Yoga", Color = "#00FF00" }
                }
            }
        };

        var mapped = ResponseMapper.ToPackageResponse(package);

        mapped.Name.Should().Be("Premium Package");
        mapped.Items.Should().HaveCount(1);
        mapped.Items[0].RemainingCredits.Should().Be(15);
    }

    [Fact]
    public async Task PackageTemplate_WithItems_CreatesCorrectTotal()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var classType1 = new ClassType { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Yoga", Color = "#00FF00" };
        var classType2 = new ClassType { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Pilates", Color = "#FF00FF" };

        var template = new PackageTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Full Plan"
        };
        template.Items.Add(new PackageTemplateItem { ClassTypeId = classType1.Id, TotalCredits = 10, PricePerCredit = 5m });
        template.Items.Add(new PackageTemplateItem { ClassTypeId = classType2.Id, TotalCredits = 20, PricePerCredit = 4m });

        db.ClassTypes.AddRange(classType1, classType2);
        db.PackageTemplates.Add(template);
        await db.SaveChangesAsync();

        var loaded = await db.PackageTemplates
            .Include(t => t.Items)
            .FirstAsync(t => t.Id == template.Id);

        var totalPrice = loaded.Items.Sum(i => i.TotalCredits * i.PricePerCredit);
        totalPrice.Should().Be(130m); // 10*5 + 20*4 = 50 + 80
    }
}

public class EntityValidationIntegrationTests
{
    [Theory]
    [InlineData("admin@gym.com", true)]
    [InlineData("user@domain.co.uk", true)]
    [InlineData("invalid", false)]
    [InlineData("", false)]
    public void IsValidEmail_WithVarious_ReturnsExpected(string? email, bool expected)
    {
        EntityValidator.IsValidEmail(email).Should().Be(expected);
    }

    [Theory]
    [InlineData("11999999999", true)]
    [InlineData("+55 11 99999-9999", true)]
    [InlineData("12345", false)]
    public void IsValidPhone_WithVarious_ReturnsExpected(string? phone, bool expected)
    {
        EntityValidator.IsValidPhone(phone).Should().Be(expected);
    }

    [Theory]
    [InlineData("https://example.com", true)]
    [InlineData("http://test.com/path", true)]
    [InlineData("not-a-url", false)]
    [InlineData("", true)] // empty is valid
    public void IsValidUrl_WithVarious_ReturnsExpected(string? url, bool expected)
    {
        EntityValidator.IsValidUrl(url).Should().Be(expected);
    }

    [Theory]
    [InlineData("password123", true)]
    [InlineData("123456", true)]
    [InlineData("short", false)]
    public void IsValidPassword_WithVarious_ReturnsExpected(string? password, bool expected)
    {
        EntityValidator.IsValidPassword(password).Should().Be(expected);
    }
}

public class StudentEndpointIntegrationTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task StudentWithPackages_ComputesRemainingCredits()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var package = new Package
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StudentId = studentId,
            Name = "Test Package",
            IsActive = true
        };
        package.Items.Add(new PackageItem
        {
            ClassTypeId = Guid.NewGuid(),
            TotalCredits = 10,
            UsedCredits = 3,
            PricePerCredit = 5m
        });
        package.Items.Add(new PackageItem
        {
            ClassTypeId = Guid.NewGuid(),
            TotalCredits = 5,
            UsedCredits = 1,
            PricePerCredit = 6m
        });

        db.Packages.Add(package);
        await db.SaveChangesAsync();

        var remaining = package.Items.Sum(i => i.TotalCredits - i.UsedCredits);
        remaining.Should().Be(11);
    }

    [Fact]
    public async Task UserValidator_CanChangeOwnStatus_PreventsSelfDeactivation()
    {
        var userId = Guid.NewGuid();

        var canChange = UserValidator.CanChangeOwnStatus(userId, userId);

        canChange.Should().BeFalse();
    }

    [Fact]
    public async Task UserValidator_CanChangeOwnStatus_AllowsOtherDeactivation()
    {
        var targetId = Guid.NewGuid();
        var callerId = Guid.NewGuid();

        var canChange = UserValidator.CanChangeOwnStatus(targetId, callerId);

        canChange.Should().BeTrue();
    }
}

public class FeeCalculationIntegrationTests
{
    [Theory]
    [InlineData(PaymentMethod.Cash, 1, "Cash")]
    [InlineData(PaymentMethod.Pix, 1, "Pix")]
    [InlineData(PaymentMethod.CreditCard, 1, "CreditCard1x")]
    [InlineData(PaymentMethod.CreditCard, 3, "CreditCard2to6x")]
    [InlineData(PaymentMethod.CreditCard, 12, "CreditCard7to12x")]
    public void ResolveFeeType_WithVarious_ReturnsExpected(PaymentMethod pm, int installments, string expected)
    {
        var result = FeeCalculator.ResolveFeeType(pm, installments);
        result.Should().Be(expected);
    }

    [Fact]
    public void CalculateNetAmount_AppliesFeeCorrectly()
    {
        var net = FeeCalculator.CalculateNetAmount(100m, 0.05m);

        net.Should().Be(95m);
    }

    [Fact]
    public void CalculateFeePercentage_WithKnownFeeType_ReturnsCorrect()
    {
        var config = new Dictionary<string, decimal>
        {
            { "CreditCard1x", 0.039m },
            { "CreditCard2to6x", 0.049m }
        };

        var fee1 = FeeCalculator.CalculateFeePercentage("CreditCard1x", config);
        var fee2 = FeeCalculator.CalculateFeePercentage("CreditCard2to6x", config);
        var fee3 = FeeCalculator.CalculateFeePercentage("Unknown", config);

        fee1.Should().Be(0.039m);
        fee2.Should().Be(0.049m);
        fee3.Should().Be(0m);
    }
}

public class WhatsAppSlotIntegrationTests
{
    [Fact]
    public void WhatsAppSlotGenerator_GenerateSlots_ForMultipleProfessors()
    {
        var prof1 = Guid.NewGuid();
        var prof2 = Guid.NewGuid();

        var blocks = new List<WhatsAppSlotGenerator.AvailabilityBlock>
        {
            new(prof1, "John", new TimeOnly(9, 0), new TimeOnly(12, 0)),
            new(prof2, "Jane", new TimeOnly(9, 0), new TimeOnly(12, 0))
        };

        var result = WhatsAppSlotGenerator.GenerateSlots(
            blocks,
            new List<WhatsAppSlotGenerator.OccupiedSession>(),
            new List<WhatsAppSlotGenerator.TimeBlockItem>(),
            60);

        // Each professor has 3 slots (9:00, 10:00, 11:00)
        // Deduplication is by (time, instructorId), so both prof1 and prof2 keep their slots
        result.Should().HaveCount(6);
        result.Select(s => s.Time).Should().Contain("09:00");
    }

    [Fact]
    public void WhatsAppSlotGenerator_AutoAssign_SelectsLeastLoaded()
    {
        var prof1 = Guid.NewGuid();
        var prof2 = Guid.NewGuid();
        var prof3 = Guid.NewGuid();

        var existingSessions = new List<WhatsAppSlotGenerator.OccupiedSession>
        {
            new(new TimeOnly(9, 0), 60, prof1),
            new(new TimeOnly(10, 0), 60, prof1),
            new(new TimeOnly(9, 0), 60, prof2)
        };

        var result = WhatsAppSlotGenerator.AutoAssignProfessional(
            new[] { prof1, prof2, prof3 },
            existingSessions);

        result.Should().Be(prof3); // prof3 has 0 sessions
    }

    [Fact]
    public void WhatsAppSlotGenerator_FindOverlapping_DetectsCorrectly()
    {
        // 9:00-10:00 overlaps with 9:30-10:30
        WhatsAppSlotGenerator.FindOverlapping(
            new TimeOnly(9, 0), new TimeOnly(10, 0),
            new TimeOnly(9, 30), new TimeOnly(10, 30)
        ).Should().BeTrue();

        // 9:00-10:00 does not overlap with 10:00-11:00 (adjacent)
        WhatsAppSlotGenerator.FindOverlapping(
            new TimeOnly(9, 0), new TimeOnly(10, 0),
            new TimeOnly(10, 0), new TimeOnly(11, 0)
        ).Should().BeFalse();
    }
}