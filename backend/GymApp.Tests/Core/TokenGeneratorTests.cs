using FluentAssertions;
using GymApp.Api.Core;
using Xunit;

namespace GymApp.Tests.Core;

public class TokenGeneratorTests
{
    [Fact]
    public void GenerateRefreshToken_ReturnsBase64UrlSafeString()
    {
        var token = TokenGenerator.GenerateRefreshToken();
        token.Should().NotContain("+");
        token.Should().NotContain("/");
        token.Should().NotContain("=");
    }

    [Fact]
    public void GenerateRefreshToken_HasMinimumLength()
    {
        var token = TokenGenerator.GenerateRefreshToken();
        token.Length.Should().BeGreaterOrEqualTo(80);
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsDifferentValues()
    {
        var t1 = TokenGenerator.GenerateRefreshToken();
        var t2 = TokenGenerator.GenerateRefreshToken();
        t1.Should().NotBe(t2);
    }

    [Fact]
    public void GeneratePasswordResetToken_ReturnsBase64UrlSafeString()
    {
        var token = TokenGenerator.GeneratePasswordResetToken();
        token.Should().NotContain("+");
        token.Should().NotContain("/");
        token.Should().NotContain("=");
    }

    [Fact]
    public void GeneratePasswordResetToken_HasMinimumLength()
    {
        var token = TokenGenerator.GeneratePasswordResetToken();
        token.Length.Should().BeGreaterOrEqualTo(40);
    }

    [Fact]
    public void GeneratePasswordResetToken_ReturnsDifferentValues()
    {
        var t1 = TokenGenerator.GeneratePasswordResetToken();
        var t2 = TokenGenerator.GeneratePasswordResetToken();
        t1.Should().NotBe(t2);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("short", false)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)] // 40+ chars
    public void IsValidPasswordResetToken_ReturnsExpected(string? token, bool expected)
    {
        TokenGenerator.IsValidPasswordResetToken(token).Should().Be(expected);
    }

    [Fact]
    public void IsTokenExpired_WithNullExpiry_ReturnsTrue()
    {
        TokenGenerator.IsTokenExpired(null).Should().BeTrue();
    }

    [Fact]
    public void IsTokenExpired_WithExpiredDate_ReturnsTrue()
    {
        var expired = DateTime.UtcNow.AddHours(-1);
        TokenGenerator.IsTokenExpired(expired).Should().BeTrue();
    }

    [Fact]
    public void IsTokenExpired_WithFutureDate_ReturnsFalse()
    {
        var future = DateTime.UtcNow.AddDays(1);
        TokenGenerator.IsTokenExpired(future).Should().BeFalse();
    }

    [Fact]
    public void GetRefreshTokenExpiry_ReturnsCorrectDays()
    {
        var days = 30;
        var expiry = TokenGenerator.GetRefreshTokenExpiry(days);
        var expectedMin = DateTime.UtcNow.AddDays(days - 1);
        var expectedMax = DateTime.UtcNow.AddDays(days + 1);
        expiry.Should().BeAfter(expectedMin);
        expiry.Should().BeBefore(expectedMax);
    }

    [Fact]
    public void GetPasswordResetTokenExpiry_ReturnsTwoHours()
    {
        var expiry = TokenGenerator.GetPasswordResetTokenExpiry();
        var expectedMin = DateTime.UtcNow.AddHours(1.5);
        var expectedMax = DateTime.UtcNow.AddHours(2.5);
        expiry.Should().BeAfter(expectedMin);
        expiry.Should().BeBefore(expectedMax);
    }
}

public class PasswordValidatorTests
{
    [Theory]
    [InlineData("password123", true)]
    [InlineData("123456", true)]
    [InlineData("sixcha", true)]
    [InlineData("pass", false)] // too short
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidPassword_ReturnsExpected(string? password, bool expected)
    {
        PasswordValidator.IsValidPassword(password).Should().Be(expected);
    }

    [Theory]
    [InlineData("test@example.com", true)]
    [InlineData("user@domain", true)]
    [InlineData("invalid", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidEmail_ReturnsExpected(string? email, bool expected)
    {
        PasswordValidator.IsValidEmail(email).Should().Be(expected);
    }

    [Theory]
    [InlineData("John", true)]
    [InlineData("Jo", true)]
    [InlineData("J", false)] // too short
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void IsValidName_ReturnsExpected(string? name, bool expected)
    {
        PasswordValidator.IsValidName(name).Should().Be(expected);
    }
}