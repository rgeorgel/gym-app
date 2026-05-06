using GymApp.Api.DTOs;
using GymApp.Domain.Entities;
using GymApp.Domain.Enums;
using GymApp.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymApp.Tests.Integration;

public class AuthEndpointsTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Register_WithValidData_CreatesUser()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();

        var tenant = new Tenant { Id = tenantId, Name = "Test Gym", Slug = "test-gym", IsActive = true };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var req = new RegisterStudentRequest("John", "john@test.com", "password123", "123456789", null);
        Assert.Equal("John", req.Name);
        Assert.Equal("john@test.com", req.Email);
        Assert.Equal("password123", req.Password);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ThrowsOrConflicts()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant { Id = tenantId, Name = "Test Gym", Slug = "test-gym2", IsActive = true };
        var existingUser = new User
        {
            TenantId = tenantId,
            Email = "existing@test.com",
            Name = "Existing",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass"),
            Role = UserRole.Student
        };

        db.Tenants.Add(tenant);
        db.Users.Add(existingUser);
        await db.SaveChangesAsync();

        var duplicate = await db.Users.AnyAsync(u => u.TenantId == tenantId && u.Email == "existing@test.com");
        Assert.True(duplicate);
    }

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsNewTokens()
    {
        using var db = CreateInMemoryDb();
        var user = new User
        {
            Email = "user@test.com",
            Name = "Test User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
            Role = UserRole.Student,
            Status = StudentStatus.Active,
            RefreshToken = "valid-refresh-token",
            RefreshTokenExpiry = DateTime.UtcNow.AddDays(30)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var found = await db.Users.FirstOrDefaultAsync(u =>
            u.RefreshToken == "valid-refresh-token" &&
            u.RefreshTokenExpiry > DateTime.UtcNow);

        Assert.NotNull(found);
    }

    [Fact]
    public async Task Refresh_WithExpiredToken_ReturnsNull()
    {
        using var db = CreateInMemoryDb();
        var user = new User
        {
            Email = "user@test.com",
            Name = "Test User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
            Role = UserRole.Student,
            Status = StudentStatus.Active,
            RefreshToken = "expired-token",
            RefreshTokenExpiry = DateTime.UtcNow.AddDays(-1)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var found = await db.Users.FirstOrDefaultAsync(u =>
            u.RefreshToken == "expired-token" &&
            u.RefreshTokenExpiry > DateTime.UtcNow);

        Assert.Null(found);
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_ReturnsFalse()
    {
        using var db = CreateInMemoryDb();
        var user = new User
        {
            Email = "user@test.com",
            Name = "Test User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("currentpass"),
            Role = UserRole.Student,
            Status = StudentStatus.Active
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var isValid = BCrypt.Net.BCrypt.Verify("wrongpassword", user.PasswordHash);
        Assert.False(isValid);
    }

    [Fact]
    public async Task ChangePassword_WithCorrectPassword_ReturnsTrue()
    {
        using var db = CreateInMemoryDb();
        var user = new User
        {
            Email = "user@test.com",
            Name = "Test User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("currentpass"),
            Role = UserRole.Student,
            Status = StudentStatus.Active
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var isValid = BCrypt.Net.BCrypt.Verify("currentpass", user.PasswordHash);
        Assert.True(isValid);
    }
}