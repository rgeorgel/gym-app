using FluentAssertions;
using GymApp.Api.Core;
using Xunit;

namespace GymApp.Tests.Core;

public class SlugGeneratorTests
{
    [Fact]
    public void GenerateSlug_WithSimpleName_ReturnsLowercase()
    {
        var result = SlugGenerator.GenerateSlug("Boxing");
        result.Should().Be("boxing");
    }

    [Fact]
    public void GenerateSlug_WithSpaces_ConvertsToHyphens()
    {
        var result = SlugGenerator.GenerateSlug("Academia São Paulo");
        result.Should().Be("academia-sao-paulo");
    }

    [Fact]
    public void GenerateSlug_WithSpecialChars_RemovesSpecialChars()
    {
        var result = SlugGenerator.GenerateSlug("Zumba® Class");
        result.Should().Be("zumba-class");
    }

    [Fact]
    public void GenerateSlug_WithNullOrEmpty_ReturnsAcademia()
    {
        SlugGenerator.GenerateSlug(null!).Should().Be("academia");
        SlugGenerator.GenerateSlug("").Should().Be("academia");
        SlugGenerator.GenerateSlug("   ").Should().Be("academia");
    }

    [Fact]
    public void GenerateSlug_WithNumbers_PreservesNumbers()
    {
        var result = SlugGenerator.GenerateSlug("Boxing123");
        result.Should().Be("boxing123");
    }

    [Fact]
    public void GenerateSlug_WithBrazilianAccents_RemovesDiacritics()
    {
        var result = SlugGenerator.GenerateSlug("José João");
        result.Should().Be("jose-joao");
    }

    [Fact]
    public void GenerateSlug_WithTrailingHyphen_TrimsHyphen()
    {
        var result = SlugGenerator.GenerateSlug("test-");
        result.Should().Be("test");
    }

    [Fact]
    public void GenerateSlug_WithLeadingHyphen_TrimsHyphen()
    {
        var result = SlugGenerator.GenerateSlug("-test");
        result.Should().Be("test");
    }

    [Fact]
    public void GenerateSlug_WithDoubleHyphens_CollapsesToOne()
    {
        var result = SlugGenerator.GenerateSlug("test--class");
        result.Should().Be("test-class");
    }

    [Fact]
    public void GenerateSlug_WithUnicodeCategory_HandlesProperly()
    {
        var result = SlugGenerator.GenerateSlug(" 测试 ");
        result.Should().Be("academia");
    }

    [Fact]
    public void GenerateSlug_WithLongName_PreservesFullLength()
    {
        var longName = new string('a', 60);
        var result = SlugGenerator.GenerateSlug(longName);
        result.Should().Be(new string('a', 60));
    }

    [Fact]
    public void GenerateSlug_WithMixedCase_ReturnsLowercase()
    {
        var result = SlugGenerator.GenerateSlug("MixedCase");
        result.Should().Be("mixedcase");
    }

    [Fact]
    public void GenerateSlug_WithUnderline_RemovesUnderline()
    {
        var result = SlugGenerator.GenerateSlug("test_class");
        result.Should().Be("testclass");
    }

    [Fact]
    public void GenerateSlug_WithCamelCase_RemovesSpacesAndKeepsCamel()
    {
        var result = SlugGenerator.GenerateSlug("My Gym Academy");
        result.Should().Be("my-gym-academy");
    }

    [Fact]
    public void GenerateSlug_WithTabAndNewline_ConvertsToHyphen()
    {
        var result = SlugGenerator.GenerateSlug("test\t\nclass");
        result.Should().Be("test-class");
    }
}