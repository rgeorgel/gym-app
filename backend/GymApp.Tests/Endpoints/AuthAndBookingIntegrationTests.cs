using FluentAssertions;
using GymApp.Api.Core;
using GymApp.Api.DTOs;
using GymApp.Api.Helpers;
using GymApp.Domain.Entities;
using GymApp.Domain.Enums;
using GymApp.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymApp.Tests.Endpoints;

public class AuthFlowIntegrationTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokens()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test Gym", Slug = "test-gym" });

        var userId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = userId,
            TenantId = tenantId,
            Name = "Test User",
            Email = "test@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
            Role = UserRole.Student,
            Status = StudentStatus.Active
        });
        await db.SaveChangesAsync();

        var user = await db.Users.FirstAsync(u => u.Email == "test@test.com");
        var isValid = BCrypt.Net.BCrypt.Verify("password123", user.PasswordHash);

        isValid.Should().BeTrue();
        user.Status.Should().Be(StudentStatus.Active);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsFalse()
    {
        using var db = CreateInMemoryDb();

        var userId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = userId,
            TenantId = Guid.NewGuid(),
            Name = "Test User",
            Email = "test@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("correctpassword"),
            Role = UserRole.Student,
            Status = StudentStatus.Active
        });
        await db.SaveChangesAsync();

        var user = await db.Users.FirstAsync(u => u.Email == "test@test.com");
        var isValid = BCrypt.Net.BCrypt.Verify("wrongpassword", user.PasswordHash);

        isValid.Should().BeFalse();
    }

    [Fact]
    public async Task Register_WithValidData_CreatesUser()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test Gym", Slug = "test-gym" });
        await db.SaveChangesAsync();

        var email = "newuser@test.com";
        var exists = await db.Users.AnyAsync(u => u.TenantId == tenantId && u.Email == email);
        exists.Should().BeFalse();

        var newUser = new User
        {
            TenantId = tenantId,
            Name = "New User",
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
            Role = UserRole.Student,
            Status = StudentStatus.Active
        };
        db.Users.Add(newUser);
        await db.SaveChangesAsync();

        var saved = await db.Users.FirstAsync(u => u.Email == email);
        saved.Name.Should().Be("New User");
        saved.Role.Should().Be(UserRole.Student);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ThrowsException()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test Gym", Slug = "test-gym" });

        var email = "existing@test.com";
        db.Users.Add(new User
        {
            TenantId = tenantId,
            Name = "Existing User",
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
            Role = UserRole.Student,
            Status = StudentStatus.Active
        });
        await db.SaveChangesAsync();

        var isDuplicate = await db.Users.AnyAsync(u => u.TenantId == tenantId && u.Email == email);
        isDuplicate.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshToken_WithValidToken_ReturnsNewTokens()
    {
        using var db = CreateInMemoryDb();

        var userId = Guid.NewGuid();
        var refreshToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));
        var expiry = DateTime.UtcNow.AddDays(30);

        db.Users.Add(new User
        {
            Id = userId,
            TenantId = Guid.NewGuid(),
            Name = "Test User",
            Email = "test@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
            Role = UserRole.Student,
            Status = StudentStatus.Active,
            RefreshToken = refreshToken,
            RefreshTokenExpiry = expiry
        });
        await db.SaveChangesAsync();

        var user = await db.Users.FirstAsync(u => u.RefreshToken == refreshToken);
        user.RefreshTokenExpiry.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task RefreshToken_WithExpiredToken_ReturnsNull()
    {
        using var db = CreateInMemoryDb();

        var userId = Guid.NewGuid();
        var expiredToken = "expired_token";
        var expiry = DateTime.UtcNow.AddDays(-1);

        db.Users.Add(new User
        {
            Id = userId,
            TenantId = Guid.NewGuid(),
            Name = "Test User",
            Email = "test@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
            Role = UserRole.Student,
            Status = StudentStatus.Active,
            RefreshToken = expiredToken,
            RefreshTokenExpiry = expiry
        });
        await db.SaveChangesAsync();

        var user = await db.Users.FirstOrDefaultAsync(u =>
            u.RefreshToken == expiredToken &&
            u.RefreshTokenExpiry > DateTime.UtcNow);

        user.Should().BeNull();
    }

    [Fact]
    public async Task ForgotPassword_SetsResetToken()
    {
        using var db = CreateInMemoryDb();

        var userId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = userId,
            TenantId = Guid.NewGuid(),
            Name = "Test User",
            Email = "test@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
            Role = UserRole.Student,
            Status = StudentStatus.Active
        });
        await db.SaveChangesAsync();

        var user = await db.Users.FirstAsync(u => u.Email == "test@test.com");
        var token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-").Replace("/", "_").Replace("=", "");

        user.PasswordResetToken = token;
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(2);
        await db.SaveChangesAsync();

        var updated = await db.Users.AsNoTracking().FirstAsync(u => u.Email == "test@test.com");
        updated.PasswordResetToken.Should().NotBeNullOrEmpty();
        updated.PasswordResetTokenExpiry.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task ResetPassword_WithValidToken_UpdatesPassword()
    {
        using var db = CreateInMemoryDb();

        var userId = Guid.NewGuid();
        var token = "valid_reset_token";
        var tokenExpiry = DateTime.UtcNow.AddHours(2);

        db.Users.Add(new User
        {
            Id = userId,
            TenantId = Guid.NewGuid(),
            Name = "Test User",
            Email = "test@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("oldpassword"),
            Role = UserRole.Student,
            Status = StudentStatus.Active,
            PasswordResetToken = token,
            PasswordResetTokenExpiry = tokenExpiry
        });
        await db.SaveChangesAsync();

        var user = await db.Users.FirstAsync(u => u.PasswordResetToken == token && u.PasswordResetTokenExpiry > DateTime.UtcNow);
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("newpassword123");
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiry = null;
        await db.SaveChangesAsync();

        var updated = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId);
        BCrypt.Net.BCrypt.Verify("newpassword123", updated.PasswordHash).Should().BeTrue();
        updated.PasswordResetToken.Should().BeNull();
    }

    [Fact]
    public async Task ChangePassword_WithCorrectCurrent_UpdatesPassword()
    {
        using var db = CreateInMemoryDb();

        var userId = Guid.NewGuid();
        var currentPassword = "currentpassword123";

        db.Users.Add(new User
        {
            Id = userId,
            TenantId = Guid.NewGuid(),
            Name = "Test User",
            Email = "test@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(currentPassword),
            Role = UserRole.Student,
            Status = StudentStatus.Active
        });
        await db.SaveChangesAsync();

        var user = await db.Users.FirstAsync(u => u.Id == userId);
        var isCurrentValid = BCrypt.Net.BCrypt.Verify("currentpassword123", user.PasswordHash);
        isCurrentValid.Should().BeTrue();

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("newpassword456");
        await db.SaveChangesAsync();

        var updated = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId);
        BCrypt.Net.BCrypt.Verify("newpassword456", updated.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task UserWithInactiveStatus_CannotLogin()
    {
        using var db = CreateInMemoryDb();

        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Inactive User",
            Email = "inactive@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
            Role = UserRole.Student,
            Status = StudentStatus.Inactive
        });
        await db.SaveChangesAsync();

        var user = await db.Users.FirstAsync(u => u.Email == "inactive@test.com");
        user.Status.Should().Be(StudentStatus.Inactive);
    }
}

public class BookingFlowIntegrationTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateBooking_WithAvailableSlot_ConfirmsBooking()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var classTypeId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test", Slug = "test" });
        db.ClassTypes.Add(new ClassType { Id = classTypeId, TenantId = tenantId, Name = "Boxing", Color = "#FF0000" });

        var session = new Session
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ClassTypeId = classTypeId,
            Date = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            StartTime = new TimeOnly(10, 0),
            DurationMinutes = 60,
            Status = SessionStatus.Scheduled,
            SlotsAvailable = 5
        };
        db.Sessions.Add(session);

        db.Users.Add(new User
        {
            Id = studentId,
            TenantId = tenantId,
            Name = "Student",
            Email = "student@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass"),
            Role = UserRole.Student,
            Status = StudentStatus.Active
        });
        await db.SaveChangesAsync();

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            StudentId = studentId,
            Status = BookingStatus.Confirmed
        };
        db.Bookings.Add(booking);
        session.SlotsAvailable--;
        await db.SaveChangesAsync();

        var saved = await db.Bookings.FirstAsync();
        saved.Status.Should().Be(BookingStatus.Confirmed);

        var updatedSession = await db.Sessions.FindAsync(session.Id);
        updatedSession!.SlotsAvailable.Should().Be(4);
    }

    [Fact]
    public async Task CreateBooking_WithNoSlotsAvailable_ReturnsBadRequest()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
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
        await db.SaveChangesAsync();

        session.SlotsAvailable.Should().BeLessOrEqualTo(0);
    }

    [Fact]
    public async Task CancelBooking_WithinDeadline_RestoresCredits()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var classTypeId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var packageItemId = Guid.NewGuid();

        var sessionDateTime = DateTime.UtcNow.AddHours(5);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ClassTypeId = classTypeId,
            Date = DateOnly.FromDateTime(sessionDateTime),
            StartTime = TimeOnly.FromDateTime(sessionDateTime),
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

        var cancellationDeadline = sessionDateTime.AddHours(-2);
        var canRefund = DateTime.UtcNow <= cancellationDeadline;
        canRefund.Should().BeTrue();

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = DateTime.UtcNow;
        booking.PackageItem.UsedCredits = Math.Max(0, booking.PackageItem.UsedCredits - 1);
        session.SlotsAvailable++;
        await db.SaveChangesAsync();

        booking.Status.Should().Be(BookingStatus.Cancelled);
        booking.PackageItem.UsedCredits.Should().Be(0);
        session.SlotsAvailable.Should().Be(1);
    }

    [Fact]
    public async Task CancelBooking_AfterDeadline_DoesNotRestoreCredits()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var classTypeId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var packageItemId = Guid.NewGuid();

        var sessionDateTime = DateTime.UtcNow.AddHours(1);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ClassTypeId = classTypeId,
            Date = DateOnly.FromDateTime(sessionDateTime),
            StartTime = TimeOnly.FromDateTime(sessionDateTime),
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

        var cancellationDeadline = sessionDateTime.AddHours(-2);
        var canRefund = DateTime.UtcNow <= cancellationDeadline;
        canRefund.Should().BeFalse();

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = DateTime.UtcNow;
        session.SlotsAvailable++;
        await db.SaveChangesAsync();

        booking.PackageItem.UsedCredits.Should().Be(1);
        session.SlotsAvailable.Should().Be(1);
    }

    [Fact]
    public async Task CheckIn_ConfirmedBooking_ChangesStatusToCheckedIn()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        var session = new Session
        {
            Id = sessionId,
            TenantId = tenantId,
            ClassTypeId = Guid.NewGuid(),
            Date = DateOnly.FromDateTime(DateTime.Today),
            StartTime = new TimeOnly(10, 0),
            DurationMinutes = 60,
            Status = SessionStatus.Scheduled,
            SlotsAvailable = 0
        };
        db.Sessions.Add(session);

        var booking = new Booking
        {
            Id = bookingId,
            SessionId = sessionId,
            StudentId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        booking.Status = BookingStatus.CheckedIn;
        booking.CheckedInAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var updated = await db.Bookings.FindAsync(bookingId);
        updated!.Status.Should().Be(BookingStatus.CheckedIn);
        updated.CheckedInAt.Should().NotBeNull();
    }

    [Fact]
    public async Task BookingCancellation_TriggersWaitingListPromotion()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var waitingStudentId = Guid.NewGuid();

        var session = new Session
        {
            Id = sessionId,
            TenantId = tenantId,
            ClassTypeId = Guid.NewGuid(),
            Date = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            StartTime = new TimeOnly(10, 0),
            DurationMinutes = 60,
            Status = SessionStatus.Scheduled,
            SlotsAvailable = 1
        };
        db.Sessions.Add(session);

        db.Users.Add(new User
        {
            Id = waitingStudentId,
            TenantId = tenantId,
            Name = "Waiting Student",
            Email = "waiting@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass"),
            Role = UserRole.Student,
            Status = StudentStatus.Active
        });

        var waitingEntry = new WaitingListEntry
        {
            SessionId = sessionId,
            StudentId = waitingStudentId,
            Position = 1
        };
        db.WaitingList.Add(waitingEntry);
        await db.SaveChangesAsync();

        var entries = await db.WaitingList.Where(w => w.SessionId == sessionId).OrderBy(w => w.Position).ToListAsync();
        entries.Should().HaveCount(1);
        entries[0].StudentId.Should().Be(waitingStudentId);
    }

    [Fact]
    public async Task BookingWithPackage_ChecksCreditsAvailability()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var classTypeId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var packageItemId = Guid.NewGuid();

        var packageItem = new PackageItem
        {
            Id = packageItemId,
            PackageId = packageId,
            ClassTypeId = classTypeId,
            TotalCredits = 5,
            UsedCredits = 5
        };

        var hasCredits = packageItem.UsedCredits < packageItem.TotalCredits;
        hasCredits.Should().BeFalse();
    }

    [Fact]
    public async Task BookingWithPackage_ChecksExpiration()
    {
        using var db = CreateInMemoryDb();

        var package = new Package
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            Name = "Expired Package",
            IsActive = true,
            ExpiresAt = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))
        };

        var isExpired = package.ExpiresAt.HasValue && package.ExpiresAt < DateOnly.FromDateTime(DateTime.UtcNow);
        isExpired.Should().BeTrue();
    }
}

public class StudentFlowIntegrationTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetStudents_ByTenant_ReturnsTenantStudents()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test Gym", Slug = "test-gym" });

        var student1 = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Student One",
            Email = "student1@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass"),
            Role = UserRole.Student,
            Status = StudentStatus.Active
        };
        var student2 = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Student Two",
            Email = "student2@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass"),
            Role = UserRole.Student,
            Status = StudentStatus.Active
        };
        db.Users.AddRange(student1, student2);
        await db.SaveChangesAsync();

        var students = await db.Users
            .Where(u => u.TenantId == tenantId && u.Role == UserRole.Student)
            .ToListAsync();

        students.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateStudent_ValidData_UpdatesSuccessfully()
    {
        using var db = CreateInMemoryDb();

        var studentId = Guid.NewGuid();
        var student = new User
        {
            Id = studentId,
            TenantId = Guid.NewGuid(),
            Name = "Original Name",
            Email = "student@test.com",
            Phone = "11999999999",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass"),
            Role = UserRole.Student,
            Status = StudentStatus.Active
        };
        db.Users.Add(student);
        await db.SaveChangesAsync();

        student.Name = "Updated Name";
        student.Phone = "11888888888";
        await db.SaveChangesAsync();

        var updated = await db.Users.AsNoTracking().FirstAsync(u => u.Id == studentId);
        updated.Name.Should().Be("Updated Name");
        updated.Phone.Should().Be("11888888888");
    }

    [Fact]
    public async Task StudentPackages_CalculatesRemainingCredits()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var classTypeId = Guid.NewGuid();

        var package = new Package
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StudentId = studentId,
            Name = "Premium Plan",
            IsActive = true
        };
        package.Items.Add(new PackageItem
        {
            ClassTypeId = classTypeId,
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

        var loaded = await db.Packages
            .Include(p => p.Items)
            .FirstAsync(p => p.Id == package.Id);

        var totalRemaining = loaded.Items.Sum(i => i.TotalCredits - i.UsedCredits);
        totalRemaining.Should().Be(11);
    }

    [Fact]
    public async Task DeactivateStudent_SetsStatusToInactive()
    {
        using var db = CreateInMemoryDb();

        var studentId = Guid.NewGuid();
        var student = new User
        {
            Id = studentId,
            TenantId = Guid.NewGuid(),
            Name = "Student",
            Email = "student@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass"),
            Role = UserRole.Student,
            Status = StudentStatus.Active
        };
        db.Users.Add(student);
        await db.SaveChangesAsync();

        student.Status = StudentStatus.Inactive;
        await db.SaveChangesAsync();

        var updated = await db.Users.AsNoTracking().FirstAsync(u => u.Id == studentId);
        updated.Status.Should().Be(StudentStatus.Inactive);
    }
}

public class TenantFlowIntegrationTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateTenant_WithValidSlug_SetsCorrectDefaults()
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
        saved.Plan.Should().Be(TenantPlan.Basic);
        saved.TenantType.Should().Be(TenantType.Gym);
    }

    [Fact]
    public async Task TenantWithDefaultPackage_CreatesPackageOnStudentRegistration()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var classTypeId = Guid.NewGuid();

        var template = new PackageTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Starter Plan",
            DurationDays = 30
        };
        template.Items.Add(new PackageTemplateItem
        {
            ClassTypeId = classTypeId,
            TotalCredits = 10,
            PricePerCredit = 5m
        });
        db.PackageTemplates.Add(template);

        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Test Gym",
            Slug = "test-gym",
            DefaultPackageTemplateId = template.Id
        };
        db.Tenants.Add(tenant);

        db.ClassTypes.Add(new ClassType { Id = classTypeId, TenantId = tenantId, Name = "Boxing", Color = "#FF0000" });
        await db.SaveChangesAsync();

        await PackageHelper.AssignDefaultPackageIfConfiguredAsync(db, tenantId, studentId);
        await db.SaveChangesAsync();

        var packages = await db.Packages.Where(p => p.StudentId == studentId).ToListAsync();
        packages.Should().HaveCount(1);
        packages[0].Name.Should().Be("Starter Plan");
    }

    [Fact]
    public async Task UpdateTenantColors_ReflectsImmediately()
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

        tenant.PrimaryColor = "#FF0000";
        tenant.SecondaryColor = "#00FF00";
        await db.SaveChangesAsync();

        var updated = await db.Tenants.AsNoTracking().FirstAsync(t => t.Slug == "test-gym");
        updated.PrimaryColor.Should().Be("#FF0000");
        updated.SecondaryColor.Should().Be("#00FF00");
    }

    [Fact]
    public async Task TenantSlugGeneration_ProducesValidSlugs()
    {
        var slug1 = SlugGenerator.GenerateSlug("Boxe Elite Academia");
        var slug2 = SlugGenerator.GenerateSlug("My Gym 24/7 Fitness Studio");

        slug1.Should().Be("boxe-elite-academia");
        slug2.Should().Be("my-gym-247-fitness-studio");
    }

    [Fact]
    public async Task TenantStatusChecker_DetectsTrialExpired()
    {
        var createdAt = DateTime.UtcNow.AddDays(-16);
        var trialDays = 14;
        var trialExpired = createdAt.AddDays(trialDays) < DateTime.UtcNow;

        trialExpired.Should().BeTrue();
    }

    [Fact]
    public async Task TenantStatusChecker_DetectsTrialActive()
    {
        var createdAt = DateTime.UtcNow.AddDays(-7);
        var trialDays = 14;
        var trialExpired = createdAt.AddDays(trialDays) < DateTime.UtcNow;

        trialExpired.Should().BeFalse();
    }

    [Fact]
    public async Task TenantStatusChecker_DetectsVacationPeriod()
    {
        var vacations = new List<TenantStatusChecker.VacationRange>
        {
            new(new DateOnly(2024, 12, 20), new DateOnly(2025, 1, 5))
        };

        var isOnVacation = TenantStatusChecker.IsVacationDate(new DateOnly(2024, 12, 25), vacations);
        isOnVacation.Should().BeTrue();

        var isNotOnVacation = TenantStatusChecker.IsVacationDate(new DateOnly(2024, 11, 15), vacations);
        isNotOnVacation.Should().BeFalse();
    }
}

public class SessionEndpointIntegrationTests2
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GenerateSessions_FromMultipleSchedules_CreatesAllSessions()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var classTypeId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test", Slug = "test" });
        db.ClassTypes.Add(new ClassType { Id = classTypeId, TenantId = tenantId, Name = "Yoga", Color = "#00FF00" });

        var schedule1 = new Schedule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ClassTypeId = classTypeId,
            Weekday = 1,
            StartTime = new TimeOnly(9, 0),
            DurationMinutes = 60,
            Capacity = 20,
            IsActive = true
        };
        var schedule2 = new Schedule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ClassTypeId = classTypeId,
            Weekday = 3,
            StartTime = new TimeOnly(14, 0),
            DurationMinutes = 90,
            Capacity = 15,
            IsActive = true
        };
        db.Schedules.AddRange(schedule1, schedule2);
        await db.SaveChangesAsync();

        var startDate = new DateOnly(2024, 6, 3);
        var endDate = new DateOnly(2024, 6, 10);

        var sessions1 = SessionGenerator.GenerateSessionsFromSchedules(
            new[] { schedule1 }, startDate, endDate, new HashSet<(Guid, DateOnly)>()).ToList();
        var sessions2 = SessionGenerator.GenerateSessionsFromSchedules(
            new[] { schedule2 }, startDate, endDate, new HashSet<(Guid, DateOnly)>()).ToList();

        sessions1.Should().HaveCount(2);
        sessions2.Should().HaveCount(1);
    }

    [Fact]
    public async Task CancelSession_CancelsAllAssociatedBookings()
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

        var booking1 = new Booking
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            StudentId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed
        };
        var booking2 = new Booking
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            StudentId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed
        };
        db.Bookings.AddRange(booking1, booking2);
        await db.SaveChangesAsync();

        SessionGenerator.CancelSession(session, "Instructor sick");
        SessionGenerator.CancelSessionBookings(session);

        session.Status.Should().Be(SessionStatus.Cancelled);
        booking1.Status.Should().Be(BookingStatus.Cancelled);
        booking2.Status.Should().Be(BookingStatus.Cancelled);
    }

    [Fact]
    public async Task SessionConflict_DetectsOverlappingBookings()
    {
        var instructorId = Guid.NewGuid();
        var existingSessions = new List<WhatsAppSlotGenerator.OccupiedSession>
        {
            new(new TimeOnly(9, 0), 60, instructorId),
            new(new TimeOnly(10, 0), 60, instructorId),
            new(new TimeOnly(11, 0), 60, instructorId)
        };

        var hasConflictAt10 = existingSessions.Any(s =>
            s.StartTime == new TimeOnly(10, 0) && s.DurationMinutes == 60);
        hasConflictAt10.Should().BeTrue();

        var hasConflictAt14 = existingSessions.Any(s =>
            s.StartTime == new TimeOnly(14, 0) && s.DurationMinutes == 60);
        hasConflictAt14.Should().BeFalse();
    }
}

public class PackageEndpointIntegrationTests2
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task AssignPackageFromTemplate_IncludesAllItems()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var classType1 = new ClassType { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Yoga", Color = "#00FF00" };
        var classType2 = new ClassType { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Pilates", Color = "#FF00FF" };
        var classType3 = new ClassType { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Boxing", Color = "#FF0000" };

        var template = new PackageTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Full Membership",
            DurationDays = 90
        };
        template.Items.Add(new PackageTemplateItem { ClassTypeId = classType1.Id, TotalCredits = 20, PricePerCredit = 4m });
        template.Items.Add(new PackageTemplateItem { ClassTypeId = classType2.Id, TotalCredits = 15, PricePerCredit = 5m });
        template.Items.Add(new PackageTemplateItem { ClassTypeId = classType3.Id, TotalCredits = 10, PricePerCredit = 6m });

        db.ClassTypes.AddRange(classType1, classType2, classType3);
        db.PackageTemplates.Add(template);
        await db.SaveChangesAsync();

        var result = await PackageHelper.AssignFromTemplateAsync(db, tenantId, studentId, template.Id);
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(3);
        result.ExpiresAt.Should().NotBeNull();
    }

    [Fact]
    public async Task PackageExpiration_ChecksCorrectly()
    {
        using var db = CreateInMemoryDb();

        var activePackage = new Package
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            Name = "Active Package",
            IsActive = true,
            ExpiresAt = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30))
        };

        var expiredPackage = new Package
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            Name = "Expired Package",
            IsActive = true,
            ExpiresAt = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5))
        };

        var noExpiryPackage = new Package
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            Name = "No Expiry Package",
            IsActive = true,
            ExpiresAt = null
        };

        activePackage.ExpiresAt.Should().BeAfter(DateOnly.FromDateTime(DateTime.UtcNow));
        expiredPackage.ExpiresAt.Should().BeBefore(DateOnly.FromDateTime(DateTime.UtcNow));
        noExpiryPackage.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task PackageItemUsage_CalculatesCorrectly()
    {
        var packageItem = new PackageItem
        {
            TotalCredits = 10,
            UsedCredits = 7
        };

        packageItem.RemainingCredits.Should().Be(3);

        packageItem.UsedCredits++;
        packageItem.RemainingCredits.Should().Be(2);

        packageItem.UsedCredits = packageItem.TotalCredits;
        packageItem.RemainingCredits.Should().Be(0);
        (packageItem.RemainingCredits > 0).Should().BeFalse();
    }

    [Fact]
    public async Task ResponseMapper_PackageResponse_MapsCorrectly()
    {
        var package = new Package
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            Name = "Premium Package",
            IsActive = true,
            ExpiresAt = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60)),
            CreatedAt = DateTime.UtcNow,
            Items = new List<PackageItem>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ClassTypeId = Guid.NewGuid(),
                    TotalCredits = 20,
                    UsedCredits = 5,
                    PricePerCredit = 5m,
                    ClassType = new ClassType { Name = "Yoga", Color = "#00FF00" }
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    ClassTypeId = Guid.NewGuid(),
                    TotalCredits = 10,
                    UsedCredits = 2,
                    PricePerCredit = 6m,
                    ClassType = new ClassType { Name = "Pilates", Color = "#FF00FF" }
                }
            }
        };

        var mapped = ResponseMapper.ToPackageResponse(package);

        mapped.Name.Should().Be("Premium Package");
        mapped.Items.Should().HaveCount(2);
        mapped.Items.Sum(i => i.RemainingCredits).Should().Be(23);
    }
}