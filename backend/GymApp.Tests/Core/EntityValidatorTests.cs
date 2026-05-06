using FluentAssertions;
using GymApp.Api.Core;
using Xunit;

namespace GymApp.Tests.Core;

public class EntityValidatorTests
{
    [Theory]
    [InlineData("test@example.com", true)]
    [InlineData("user.name@domain.co.uk", true)]
    [InlineData("invalid", false)]
    [InlineData("no-at-sign.com", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("  ", false)]
    public void IsValidEmail_ReturnsExpectedResult(string? email, bool expected)
    {
        EntityValidator.IsValidEmail(email).Should().Be(expected);
    }

    [Theory]
    [InlineData("11999999999", true)]
    [InlineData("+55 11 99999-9999", true)]
    [InlineData("1234567890", true)]
    [InlineData("12345", false)] // too short
    [InlineData("", true)] // empty is valid (optional)
    [InlineData(null, true)] // null is valid (optional)
    public void IsValidPhone_ReturnsExpectedResult(string? phone, bool expected)
    {
        EntityValidator.IsValidPhone(phone).Should().Be(expected);
    }

    [Theory]
    [InlineData("John", true)]
    [InlineData("Jo", true)]
    [InlineData("J", false)] // too short
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("   ", false)]
    public void IsValidName_ReturnsExpectedResult(string? name, bool expected)
    {
        EntityValidator.IsValidName(name).Should().Be(expected);
    }

    [Theory]
    [InlineData("password123", true)]
    [InlineData("123456", true)]
    [InlineData("pass", false)] // too short
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidPassword_ReturnsExpectedResult(string? password, bool expected)
    {
        EntityValidator.IsValidPassword(password).Should().Be(expected);
    }

    [Theory]
    [InlineData("my-gym", true)]
    [InlineData("boxe-elite", true)]
    [InlineData("abc123", true)]
    [InlineData("a", false)] // too short
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("invalid slug", false)] // contains space
    [InlineData("invalid_slug", false)] // contains underscore
    public void IsValidSlug_ReturnsExpectedResult(string? slug, bool expected)
    {
        EntityValidator.IsValidSlug(slug).Should().Be(expected);
    }

    [Theory]
    [InlineData("https://example.com", true)]
    [InlineData("http://test.com/path", true)]
    [InlineData("", true)] // empty is valid
    [InlineData(null, true)] // null is valid
    [InlineData("not-a-url", false)]
    [InlineData("ftp://invalid", true)] // valid URI scheme
    public void IsValidUrl_ReturnsExpectedResult(string? url, bool expected)
    {
        EntityValidator.IsValidUrl(url).Should().Be(expected);
    }
}