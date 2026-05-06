using FluentAssertions;
using GymApp.Api.Core;
using Xunit;

namespace GymApp.Tests.Core;

public class UserValidatorTests
{
    [Theory]
    [InlineData("test@example.com", true)]
    [InlineData("user.name@domain.co.uk", true)]
    [InlineData("invalid", false)]
    [InlineData("no-at.com", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidEmail_ReturnsExpected(string? email, bool expected)
    {
        UserValidator.IsValidEmail(email).Should().Be(expected);
    }

    [Theory]
    [InlineData("password123", true)]
    [InlineData("123456", true)]
    [InlineData("sixcha", true)]
    [InlineData("pass", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidPassword_ReturnsExpected(string? password, bool expected)
    {
        UserValidator.IsValidPassword(password).Should().Be(expected);
    }

    [Theory]
    [InlineData("John", true)]
    [InlineData("Jo", true)]
    [InlineData("J", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void IsValidName_ReturnsExpected(string? name, bool expected)
    {
        UserValidator.IsValidName(name).Should().Be(expected);
    }

    [Theory]
    [InlineData("11999999999", true)]
    [InlineData("+55 11 99999-9999", true)]
    [InlineData("", true)] // empty is valid (optional)
    [InlineData("12345", false)] // too short
    public void IsValidPhone_ReturnsExpected(string? phone, bool expected)
    {
        UserValidator.IsValidPhone(phone).Should().Be(expected);
    }

    [Theory]
    [InlineData("Test@Example.COM", "test@example.com")]
    [InlineData("  USER@DOMAIN.COM  ", "user@domain.com")]
    [InlineData("AlreadyLower@email.com", "alreadylower@email.com")]
    public void NormalizeEmail_ReturnsLowerTrimmed(string email, string expected)
    {
        UserValidator.NormalizeEmail(email).Should().Be(expected);
    }

    [Theory]
    [InlineData("  John  ", "John")]
    [InlineData("Jane", "Jane")]
    public void NormalizeName_TrimsWhitespace(string name, string expected)
    {
        UserValidator.NormalizeName(name).Should().Be(expected);
    }

    [Fact]
    public void CanChangeOwnStatus_WithDifferentIds_ReturnsTrue()
    {
        var targetId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        UserValidator.CanChangeOwnStatus(targetId, currentUserId).Should().BeTrue();
    }

    [Fact]
    public void CanChangeOwnStatus_WithSameId_ReturnsFalse()
    {
        var sameId = Guid.NewGuid();
        UserValidator.CanChangeOwnStatus(sameId, sameId).Should().BeFalse();
    }

    [Fact]
    public void IsValidUserId_WithNull_ReturnsFalse()
    {
        Guid? userId = null;
        UserValidator.IsValidUserId(userId).Should().BeFalse();
    }

    [Fact]
    public void IsValidUserId_WithEmptyGuid_ReturnsFalse()
    {
        Guid? userId = Guid.Empty;
        UserValidator.IsValidUserId(userId).Should().BeFalse();
    }

    [Fact]
    public void IsValidUserId_WithValidGuid_ReturnsTrue()
    {
        UserValidator.IsValidUserId(Guid.NewGuid()).Should().BeTrue();
    }
}

public class TenantPlanHelperTests
{
    [Theory]
    [InlineData(GymApp.Domain.Enums.TenantType.BeautySalon, 1900)]
    [InlineData(GymApp.Domain.Enums.TenantType.Gym, 4900)]
    public void GetSubscriptionPrice_ReturnsCorrectPriceForType(GymApp.Domain.Enums.TenantType type, decimal expected)
    {
        TenantPlanHelper.GetSubscriptionPrice(type).Should().Be(expected);
    }

    [Theory]
    [InlineData(GymApp.Domain.Enums.TenantPlan.Basic, 4900)]
    [InlineData(GymApp.Domain.Enums.TenantPlan.Pro, 9900)]
    [InlineData(GymApp.Domain.Enums.TenantPlan.Enterprise, 19900)]
    public void GetSubscriptionPrice_ReturnsCorrectPriceForPlan(GymApp.Domain.Enums.TenantPlan plan, decimal expected)
    {
        TenantPlanHelper.GetSubscriptionPrice(GymApp.Domain.Enums.TenantType.Gym, plan).Should().Be(expected);
    }

    [Theory]
    [InlineData(false, false, 14)]
    [InlineData(true, false, 44)]
    [InlineData(false, true, 15)]
    public void GetTrialDays_ReturnsCorrectDays(bool hasReferrer, bool hasAffiliate, int expectedDays)
    {
        TenantPlanHelper.GetTrialDays(GymApp.Domain.Enums.TenantType.Gym, hasReferrer, hasAffiliate).Should().Be(expectedDays);
    }
}