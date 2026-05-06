using FluentAssertions;
using GymApp.Api.Core;
using GymApp.Domain.Enums;
using Xunit;

namespace GymApp.Tests.Core;

public class FeeCalculatorTests
{
    [Theory]
    [InlineData(PaymentMethod.CreditCard, 1, "CreditCard1x")]
    [InlineData(PaymentMethod.CreditCard, 0, "CreditCard1x")]
    [InlineData(PaymentMethod.CreditCard, 2, "CreditCard2to6x")]
    [InlineData(PaymentMethod.CreditCard, 3, "CreditCard2to6x")]
    [InlineData(PaymentMethod.CreditCard, 6, "CreditCard2to6x")]
    [InlineData(PaymentMethod.CreditCard, 7, "CreditCard7to12x")]
    [InlineData(PaymentMethod.CreditCard, 12, "CreditCard7to12x")]
    [InlineData(PaymentMethod.DebitCard, 1, "DebitCard")]
    [InlineData(PaymentMethod.DebitCard, 5, "DebitCard")]
    [InlineData(PaymentMethod.Pix, 0, "Pix")]
    [InlineData(PaymentMethod.Pix, 10, "Pix")]
    [InlineData(PaymentMethod.Cash, 0, "Cash")]
    [InlineData(PaymentMethod.Cash, 5, "Cash")]
    public void ResolveFeeType_ReturnsCorrectFeeType(PaymentMethod method, int installments, string expected)
    {
        FeeCalculator.ResolveFeeType(method, installments).Should().Be(expected);
    }

    [Fact]
    public void CalculateFeePercentage_WithExistingKey_ReturnsFee()
    {
        var configs = new Dictionary<string, decimal>
        {
            { "CreditCard1x", 0.029m },
            { "Pix", 0m }
        };

        FeeCalculator.CalculateFeePercentage("CreditCard1x", configs).Should().Be(0.029m);
    }

    [Fact]
    public void CalculateFeePercentage_WithNonExistentKey_ReturnsZero()
    {
        var configs = new Dictionary<string, decimal>
        {
            { "CreditCard1x", 0.029m }
        };

        FeeCalculator.CalculateFeePercentage("NonExistent", configs).Should().Be(0m);
    }

    [Fact]
    public void CalculateFeePercentage_WithNullConfigs_ReturnsZero()
    {
        FeeCalculator.CalculateFeePercentage("CreditCard1x", null!).Should().Be(0m);
    }

    [Fact]
    public void CalculateFeePercentage_WithNullFeeType_ReturnsZero()
    {
        var configs = new Dictionary<string, decimal> { { "CreditCard1x", 0.029m } };
        FeeCalculator.CalculateFeePercentage(null!, configs).Should().Be(0m);
    }

    [Fact]
    public void CalculateNetAmount_WithPercentage_ReturnsNetAmount()
    {
        FeeCalculator.CalculateNetAmount(100m, 0.029m).Should().Be(97.1m);
    }

    [Fact]
    public void CalculateNetAmount_WithZeroPercentage_ReturnsFullAmount()
    {
        FeeCalculator.CalculateNetAmount(100m, 0m).Should().Be(100m);
    }

    [Fact]
    public void CalculateNetAmount_WithFullPercentage_ReturnsZero()
    {
        FeeCalculator.CalculateNetAmount(100m, 1m).Should().Be(0m);
    }

    [Fact]
    public void CalculateNetAmount_WithDecimalAmount_CalculatesCorrectly()
    {
        FeeCalculator.CalculateNetAmount(49.90m, 0.029m).Should().BeApproximately(48.45m, 0.01m);
    }
}