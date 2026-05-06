using FluentAssertions;
using GymApp.Api.Core;
using Xunit;

namespace GymApp.Tests.Core;

public class HexColorValidatorTests
{
    [Fact]
    public void IsValidHexColor_WithValidLowercase_ReturnsTrue()
    {
        HexColorValidator.IsValidHexColor("#1a1a2e").Should().BeTrue();
    }

    [Fact]
    public void IsValidHexColor_WithValidUppercase_ReturnsTrue()
    {
        HexColorValidator.IsValidHexColor("#FFFFFF").Should().BeTrue();
    }

    [Fact]
    public void IsValidHexColor_WithValidMixedCase_ReturnsTrue()
    {
        HexColorValidator.IsValidHexColor("#AbCdEf").Should().BeTrue();
    }

    [Fact]
    public void IsValidHexColor_WithNoHash_ReturnsFalse()
    {
        HexColorValidator.IsValidHexColor("1a1a2e").Should().BeFalse();
    }

    [Fact]
    public void IsValidHexColor_WithShortValue_ReturnsFalse()
    {
        HexColorValidator.IsValidHexColor("#1a1a").Should().BeFalse();
    }

    [Fact]
    public void IsValidHexColor_WithLongValue_ReturnsFalse()
    {
        HexColorValidator.IsValidHexColor("#1a1a2e00").Should().BeFalse();
    }

    [Fact]
    public void IsValidHexColor_WithRgbFormat_ReturnsFalse()
    {
        HexColorValidator.IsValidHexColor("rgb(1,1,1)").Should().BeFalse();
    }

    [Fact]
    public void IsValidHexColor_WithNull_ReturnsFalse()
    {
        HexColorValidator.IsValidHexColor(null!).Should().BeFalse();
    }

    [Fact]
    public void IsValidHexColor_WithEmptyString_ReturnsFalse()
    {
        HexColorValidator.IsValidHexColor("").Should().BeFalse();
    }

    [Fact]
    public void IsValidHexColor_WithWhitespaceOnly_ReturnsFalse()
    {
        HexColorValidator.IsValidHexColor("   ").Should().BeFalse();
    }

    [Fact]
    public void IsValidHexColor_WithJustHash_ReturnsFalse()
    {
        HexColorValidator.IsValidHexColor("#").Should().BeFalse();
    }

    [Fact]
    public void IsValidHexColor_WithTrailingSpace_ReturnsTrue()
    {
        HexColorValidator.IsValidHexColor("#1a1a2e ").Should().BeTrue();
    }

    [Fact]
    public void IsValidHexColor_WithLeadingSpace_ReturnsTrue()
    {
        HexColorValidator.IsValidHexColor(" #1a1a2e").Should().BeTrue();
    }
}