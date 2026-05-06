using FluentAssertions;
using GymApp.Api.Core;
using Xunit;

namespace GymApp.Tests.Core;

public class TenantSlugResolverTests
{
    [Theory]
    [InlineData("boxe-elite.gymapp.com", "boxe-elite")]
    [InlineData("boxe-elite.gymapp.local", "boxe-elite")]
    [InlineData("my-gym.platform.com", "my-gym")]
    [InlineData("localhost", null)]
    [InlineData("gymapp.com", null)]
    [InlineData("app.example.co.uk", "app")]
    public void ExtractSlug_WithVariousHosts_ReturnsExpectedSlug(string host, string? expected)
    {
        var result = TenantSlugResolver.ExtractSlug(host);
        result.Should().Be(expected);
    }

    [Fact]
    public void IsValidHexColor_WithValidColors_ReturnsTrue()
    {
        TenantSlugResolver.IsValidHexColor("#1a1a2e").Should().BeTrue();
        TenantSlugResolver.IsValidHexColor("#e94560").Should().BeTrue();
        TenantSlugResolver.IsValidHexColor("#FFFFFF").Should().BeTrue();
        TenantSlugResolver.IsValidHexColor("#000000").Should().BeTrue();
        TenantSlugResolver.IsValidHexColor("#abc123").Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("123456")]
    [InlineData("not-a-color")]
    [InlineData("#ghijkl")]
    public void IsValidHexColor_WithInvalidColors_ReturnsFalse(string? color)
    {
        TenantSlugResolver.IsValidHexColor(color).Should().BeFalse();
    }
}

public class PasswordGeneratorTests
{
    [Fact]
    public void GenerateTempPassword_ReturnsCorrectLength()
    {
        var password = PasswordGenerator.GenerateTempPassword();
        password.Should().HaveLength(10);
    }

    [Fact]
    public void GenerateTempPassword_ReturnsDifferentValues()
    {
        var p1 = PasswordGenerator.GenerateTempPassword();
        var p2 = PasswordGenerator.GenerateTempPassword();
        p1.Should().NotBe(p2);
    }

    [Fact]
    public void GenerateTempPassword_ContainsOnlyValidCharacters()
    {
        var password = PasswordGenerator.GenerateTempPassword();
        const string validChars = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
        password.Should().Match(p => p.All(c => validChars.Contains(c)));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(20)]
    public void GenerateTempPassword_WithCustomLength_ReturnsCorrectLength(int length)
    {
        var password = PasswordGenerator.GenerateTempPassword(length);
        password.Should().HaveLength(length);
    }

    [Fact]
    public void GenerateResetToken_ReturnsBase64UrlSafeString()
    {
        var token = PasswordGenerator.GenerateResetToken();
        token.Should().NotContain("+");
        token.Should().NotContain("/");
        token.Should().NotContain("=");
    }

    [Fact]
    public void GenerateResetToken_ReturnsDifferentValues()
    {
        var t1 = PasswordGenerator.GenerateResetToken();
        var t2 = PasswordGenerator.GenerateResetToken();
        t1.Should().NotBe(t2);
    }

    [Fact]
    public void GenerateResetToken_HasMinimumLength()
    {
        var token = PasswordGenerator.GenerateResetToken();
        token.Length.Should().BeGreaterOrEqualTo(40);
    }
}