using GymApp.Api.DTOs;
using Xunit;

namespace GymApp.Tests.DTOs;

public class AuthDtoTests
{
    [Fact]
    public void LoginRequest_CanBeConstructed()
    {
        var req = new LoginRequest("test@example.com", "password123");
        Assert.Equal("test@example.com", req.Email);
        Assert.Equal("password123", req.Password);
        Assert.Null(req.TenantSlug);
    }

    [Fact]
    public void LoginRequest_WithTenantSlug()
    {
        var req = new LoginRequest("test@example.com", "password123", "my-gym");
        Assert.Equal("test@example.com", req.Email);
        Assert.Equal("password123", req.Password);
        Assert.Equal("my-gym", req.TenantSlug);
    }

    [Fact]
    public void LoginResponse_CanBeConstructed()
    {
        var userId = Guid.NewGuid();
        var response = new LoginResponse("access-token", "refresh-token", "Admin", "John Doe", userId);
        Assert.Equal("access-token", response.AccessToken);
        Assert.Equal("refresh-token", response.RefreshToken);
        Assert.Equal("Admin", response.Role);
        Assert.Equal("John Doe", response.Name);
        Assert.Equal(userId, response.UserId);
        Assert.Null(response.TenantSlug);
    }

    [Fact]
    public void LoginResponse_WithTenantSlug()
    {
        var userId = Guid.NewGuid();
        var response = new LoginResponse("access-token", "refresh-token", "Student", "Jane Doe", userId, "gym-slug");
        Assert.Equal("gym-slug", response.TenantSlug);
    }

    [Fact]
    public void RegisterStudentRequest_CanBeConstructed()
    {
        var req = new RegisterStudentRequest("John", "john@example.com", "secret123", "123456789", DateOnly.FromDateTime(DateTime.Today.AddYears(-20)));
        Assert.Equal("John", req.Name);
        Assert.Equal("john@example.com", req.Email);
        Assert.Equal("secret123", req.Password);
        Assert.Equal("123456789", req.Phone);
    }

    [Fact]
    public void RefreshRequest_CanBeConstructed()
    {
        var req = new RefreshRequest("some-refresh-token");
        Assert.Equal("some-refresh-token", req.RefreshToken);
    }

    [Fact]
    public void ChangePasswordRequest_CanBeConstructed()
    {
        var req = new ChangePasswordRequest("oldPass", "newPass123");
        Assert.Equal("oldPass", req.CurrentPassword);
        Assert.Equal("newPass123", req.NewPassword);
    }

    [Fact]
    public void ForgotPasswordRequest_CanBeConstructed()
    {
        var req = new ForgotPasswordRequest("user@example.com");
        Assert.Equal("user@example.com", req.Email);
    }

    [Fact]
    public void ResetPasswordRequest_CanBeConstructed()
    {
        var req = new ResetPasswordRequest("token123", "newPass456");
        Assert.Equal("token123", req.Token);
        Assert.Equal("newPass456", req.NewPassword);
    }
}