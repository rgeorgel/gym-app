using FluentAssertions;
using GymApp.Api.Core;
using GymApp.Api.DTOs;
using GymApp.Domain.Enums;
using Xunit;

namespace GymApp.Tests.Endpoints;

public class SessionDtoTests
{
    [Fact]
    public void SessionResponse_CreatesCorrectly()
    {
        var id = Guid.NewGuid();
        var classTypeId = Guid.NewGuid();
        var locationId = Guid.NewGuid();

        var response = new SessionResponse(
            id,
            Guid.NewGuid(),
            DateOnly.FromDateTime(DateTime.Today),
            new TimeOnly(9, 0),
            60,
            classTypeId,
            "Boxing",
            "#FF0000",
            50m,
            ModalityType.Group,
            "John Instructor",
            20,
            15,
            SessionStatus.Scheduled,
            5,
            locationId
        );

        response.Id.Should().Be(id);
        response.ClassTypeName.Should().Be("Boxing");
        response.ClassTypeColor.Should().Be("#FF0000");
        response.ClassTypePrice.Should().Be(50m);
        response.Capacity.Should().Be(20);
        response.SlotsAvailable.Should().Be(15);
        response.BookingsCount.Should().Be(5);
        response.Status.Should().Be(SessionStatus.Scheduled);
    }

    [Fact]
    public void SessionResponse_ForSalon_NullScheduleId()
    {
        var response = new SessionResponse(
            Guid.NewGuid(),
            null, // ScheduleId is null for salon
            DateOnly.FromDateTime(DateTime.Today),
            new TimeOnly(10, 0),
            60,
            Guid.NewGuid(),
            "Haircut",
            "#00FF00",
            100m,
            ModalityType.Individual,
            "Jane Stylist",
            1,
            0,
            SessionStatus.Scheduled,
            0,
            Guid.NewGuid()
        );

        response.ScheduleId.Should().BeNull();
        response.ModalityType.Should().Be(ModalityType.Individual);
        response.Capacity.Should().Be(1);
    }

    [Fact]
    public void CancelSessionRequest_WithReason()
    {
        var req = new CancelSessionRequest("Client cancelled");

        req.Reason.Should().Be("Client cancelled");
    }

    [Fact]
    public void CancelSessionRequest_WithNullReason()
    {
        var req = new CancelSessionRequest(null);

        req.Reason.Should().BeNull();
    }
}

public class BookingDtoTests
{
    [Fact]
    public void CreateBookingRequest_CreatesCorrectly()
    {
        var sessionId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var packageItemId = Guid.NewGuid();

        var req = new CreateBookingRequest(sessionId, studentId, packageItemId);

        req.SessionId.Should().Be(sessionId);
        req.StudentId.Should().Be(studentId);
        req.PackageItemId.Should().Be(packageItemId);
    }

    [Fact]
    public void CreateSalonBookingRequest_CreatesCorrectly()
    {
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var time = new TimeOnly(14, 30);
        var serviceId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var professionalId = Guid.NewGuid();

        var req = new CreateSalonBookingRequest(date, time, serviceId, studentId, professionalId);

        req.Date.Should().Be(date);
        req.StartTime.Should().Be(time);
        req.ServiceId.Should().Be(serviceId);
        req.StudentId.Should().Be(studentId);
        req.ProfessionalId.Should().Be(professionalId);
    }

    [Fact]
    public void BookingResponse_CreatesCorrectly()
    {
        var id = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var startTime = new TimeOnly(9, 0);
        var now = DateTime.UtcNow;

        var response = new BookingResponse(
            id, sessionId, date, startTime,
            "Boxing",
            studentId, "John Student",
            BookingStatus.Confirmed, now, now, Guid.NewGuid()
        );

        response.Id.Should().Be(id);
        response.SessionId.Should().Be(sessionId);
        response.ClassTypeName.Should().Be("Boxing");
        response.Status.Should().Be(BookingStatus.Confirmed);
    }
}

public class TenantDtoTests
{
    [Fact]
    public void TenantConfigResponse_CreatesCorrectly()
    {
        var response = new TenantConfigResponse(
            "Test Gym",
            "https://example.com/logo.png",
            "#1a1a2e",
            "#e94560",
            "#ffffff",
            "test-gym",
            "pt-BR",
            GymApp.Domain.Enums.TenantType.Gym,
            "@testgym",
            "fb.com/test",
            "5511999999999",
            "https://test.com",
            "@testgym",
            true
        );

        response.Name.Should().Be("Test Gym");
        response.Slug.Should().Be("test-gym");
        response.Language.Should().Be("pt-BR");
        response.TenantType.Should().Be(GymApp.Domain.Enums.TenantType.Gym);
        response.AiEnabled.Should().BeTrue();
    }

    [Fact]
    public void TenantSettingsResponse_CreatesCorrectly()
    {
        var response = new TenantSettingsResponse(
            Guid.NewGuid(),
            "pt-BR",
            "payee123",
            true,
            true,
            "#1a1a2e",
            "#e94560",
            "#ffffff",
            "https://example.com/logo.png",
            true, // HasAbacatePayStudentApiKey
            true, // HasAbacatePayStudentWebhookSecret
            GymApp.Domain.Enums.TenantType.Gym,
            "@insta",
            "fb.com/page",
            "5511999999999",
            "https://site.com",
            "@tik tok",
            true,
            false,
            "instance1"
        );

        response.PaymentsEnabled.Should().BeTrue();
        response.TenantType.Should().Be(GymApp.Domain.Enums.TenantType.Gym);
        response.WhatsAppAutoServiceEnabled.Should().BeFalse();
        response.WhatsAppInstanceName.Should().Be("instance1");
    }
}

public class AuthDtoTests
{
    [Fact]
    public void LoginRequest_CreatesCorrectly()
    {
        var req = new LoginRequest("admin@test.com", "password123", "test-gym");

        req.Email.Should().Be("admin@test.com");
        req.Password.Should().Be("password123");
        req.TenantSlug.Should().Be("test-gym");
    }

    [Fact]
    public void LoginRequest_WithoutTenantSlug_IsNull()
    {
        var req = new LoginRequest("admin@test.com", "password", null);

        req.TenantSlug.Should().BeNull();
    }

    [Fact]
    public void RegisterStudentRequest_CreatesCorrectly()
    {
        var req = new RegisterStudentRequest(
            "John Doe",
            "john@example.com",
            "securepass123",
            "11999999999",
            DateOnly.FromDateTime(DateTime.Today.AddYears(-20))
        );

        req.Name.Should().Be("John Doe");
        req.Email.Should().Be("john@example.com");
        req.Password.Should().Be("securepass123");
        req.Phone.Should().Be("11999999999");
    }

    [Fact]
    public void RefreshRequest_CreatesCorrectly()
    {
        var req = new RefreshRequest("refresh-token-abc123");

        req.RefreshToken.Should().Be("refresh-token-abc123");
    }

    [Fact]
    public void ForgotPasswordRequest_CreatesCorrectly()
    {
        var req = new ForgotPasswordRequest("user@example.com");

        req.Email.Should().Be("user@example.com");
    }

    [Fact]
    public void ResetPasswordRequest_CreatesCorrectly()
    {
        var req = new ResetPasswordRequest("token-xyz", "newpassword456");

        req.Token.Should().Be("token-xyz");
        req.NewPassword.Should().Be("newpassword456");
    }

    [Fact]
    public void ChangePasswordRequest_CreatesCorrectly()
    {
        var req = new ChangePasswordRequest("current123", "new456");

        req.CurrentPassword.Should().Be("current123");
        req.NewPassword.Should().Be("new456");
    }

    [Fact]
    public void LoginResponse_CreatesCorrectly()
    {
        var response = new LoginResponse(
            "access-token-123",
            "refresh-token-456",
            "Admin",
            "Admin User",
            Guid.NewGuid(),
            "test-gym"
        );

        response.AccessToken.Should().Be("access-token-123");
        response.RefreshToken.Should().Be("refresh-token-456");
        response.Role.Should().Be("Admin");
        response.Name.Should().Be("Admin User");
        response.TenantSlug.Should().Be("test-gym");
    }
}

public class TenantSlugValidationTests
{
    [Theory]
    [InlineData("boxe-elite.gymapp.com", "boxe-elite")]
    [InlineData("my-gym.platform.com", "my-gym")]
    [InlineData("app.example.co.uk", "app")]
    [InlineData("localhost", null)]
    [InlineData("gymapp.com", null)]
    public void ExtractSlug_WithVariousHosts_ReturnsExpected(string host, string? expected)
    {
        TenantSlugResolver.ExtractSlug(host).Should().Be(expected);
    }

    [Theory]
    [InlineData("my-gym", true)]
    [InlineData("boxe-elite", true)]
    [InlineData("abc123", true)]
    [InlineData("a", false)] // too short
    [InlineData("", false)]
    [InlineData("invalid slug", false)] // contains space
    [InlineData("invalid_slug", false)] // contains underscore
    public void IsValidSlug_WithVarious_ReturnsExpected(string? slug, bool expected)
    {
        EntityValidator.IsValidSlug(slug).Should().Be(expected);
    }

    [Theory]
    [InlineData("#1a1a2e", true)]
    [InlineData("#e94560", true)]
    [InlineData("#FFFFFF", true)]
    [InlineData("#000000", true)]
    [InlineData("#12345", false)] // too short
    [InlineData("not-a-color", false)]
    public void IsValidHexColor_WithVarious_ReturnsExpected(string? color, bool expected)
    {
        TenantSlugResolver.IsValidHexColor(color).Should().Be(expected);
    }
}