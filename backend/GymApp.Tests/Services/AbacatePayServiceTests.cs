using GymApp.Api.Services;
using Xunit;

namespace GymApp.Tests.Services;

public class AbacatePayServiceTests
{
    [Theory]
    [InlineData(new[] { "CREDIT_CARD" }, new[] { "CARD" })]
    [InlineData(new[] { "PIX" }, new[] { "PIX" })]
    [InlineData(new[] { "DEBIT_CARD" }, new[] { "DEBIT_CARD" })]
    [InlineData(new[] { "CREDIT_CARD", "PIX" }, new[] { "CARD", "PIX" })]
    public void NormalizeMethods_TransformsCreditCardToCard(string[] input, string[] expected)
    {
        var result = AbacatePayService.NormalizeMethods(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NormalizeMethods_WithNull_ReturnsPix()
    {
        var result = AbacatePayService.NormalizeMethods(null);
        Assert.Equal(new[] { "PIX" }, result);
    }

    [Fact]
    public void NormalizeMethods_EmptyArray_ReturnsEmptyArray()
    {
        var result = AbacatePayService.NormalizeMethods(Array.Empty<string>());
        Assert.Empty(result);
    }

    [Fact]
    public void NormalizeMethods_CaseInsensitive()
    {
        var result = AbacatePayService.NormalizeMethods(new[] { "credit_card" });
        Assert.Equal(new[] { "CARD" }, result);
    }

    [Fact]
    public void NormalizeMethods_MultipleCreditCards_TransformsAll()
    {
        var result = AbacatePayService.NormalizeMethods(new[] { "CREDIT_CARD", "CREDIT_CARD" });
        Assert.Equal(new[] { "CARD", "CARD" }, result);
    }
}