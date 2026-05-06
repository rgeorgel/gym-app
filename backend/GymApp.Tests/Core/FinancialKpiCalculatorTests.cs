using FluentAssertions;
using GymApp.Api.Core;
using GymApp.Domain.Enums;
using Xunit;

namespace GymApp.Tests.Core;

public class FinancialKpiCalculatorTests
{
    private static FinancialKpiCalculator.FinancialTransactionRow Row(
        decimal gross, decimal fee, decimal net) =>
        new(gross, fee, net);

    [Fact]
    public void CalculateRevenueKpis_WithEmptyTransactions_ReturnsZeros()
    {
        var result = FinancialKpiCalculator.CalculateRevenueKpis(
            Enumerable.Empty<FinancialKpiCalculator.FinancialTransactionRow>());

        result.gross.Should().Be(0);
        result.fees.Should().Be(0);
        result.net.Should().Be(0);
        result.count.Should().Be(0);
    }

    [Fact]
    public void CalculateRevenueKpis_WithTransactions_ReturnsTotals()
    {
        var rows = new[]
        {
            Row(100m, 5m, 95m),
            Row(200m, 10m, 190m),
        };

        var result = FinancialKpiCalculator.CalculateRevenueKpis(rows);

        result.gross.Should().Be(300m);
        result.fees.Should().Be(15m);
        result.net.Should().Be(285m);
        result.count.Should().Be(2);
    }

    [Fact]
    public void CalculateProfit_ReturnsNetMinusExpenses()
    {
        FinancialKpiCalculator.CalculateProfit(1000m, 300m).Should().Be(700m);
    }

    [Fact]
    public void CalculateProfit_WithHigherExpenses_ReturnsNegative()
    {
        FinancialKpiCalculator.CalculateProfit(100m, 300m).Should().Be(-200m);
    }

    [Theory]
    [InlineData(1000, 10, 100.00)]
    [InlineData(500, 3, 166.67)]
    [InlineData(0, 5, 0.0)]
    [InlineData(100, 1, 100.00)]
    public void CalculateAverageTicket_ReturnsCorrectValue(decimal revenue, int count, decimal expected)
    {
        FinancialKpiCalculator.CalculateAverageTicket(revenue, count).Should().Be(expected);
    }

    [Fact]
    public void CalculateAverageTicket_WithZeroCount_ReturnsZero()
    {
        FinancialKpiCalculator.CalculateAverageTicket(1000m, 0).Should().Be(0m);
    }

    [Theory]
    [InlineData(2024, 1, 2024, 1, 1)]
    [InlineData(2024, 6, 2024, 6, 1)]
    [InlineData(2024, 12, 2024, 12, 1)]
    public void GetMonthStart_ReturnsFirstDayOfMonth(int year, int month, int expectedYear, int expectedMonth, int expectedDay)
    {
        var result = FinancialKpiCalculator.GetMonthStart(year, month);
        result.Year.Should().Be(expectedYear);
        result.Month.Should().Be(expectedMonth);
        result.Day.Should().Be(expectedDay);
    }

    [Theory]
    [InlineData(2024, 1, 2024, 1, 31)]
    [InlineData(2024, 2, 2024, 2, 29)] // leap year
    [InlineData(2024, 6, 2024, 6, 30)]
    [InlineData(2023, 2, 2023, 2, 28)] // non-leap year
    public void GetMonthEnd_ReturnsLastDayOfMonth(int year, int month, int expectedYear, int expectedMonth, int expectedDay)
    {
        var result = FinancialKpiCalculator.GetMonthEnd(year, month);
        result.Year.Should().Be(expectedYear);
        result.Month.Should().Be(expectedMonth);
        result.Day.Should().Be(expectedDay);
    }

    [Theory]
    [InlineData(PaymentMethod.Cash, 1, "Cash")]
    [InlineData(PaymentMethod.Pix, 1, "Pix")]
    [InlineData(PaymentMethod.DebitCard, 1, "DebitCard")]
    [InlineData(PaymentMethod.CreditCard, 1, "CreditCard1x")]
    [InlineData(PaymentMethod.CreditCard, 3, "CreditCard2to6x")]
    [InlineData(PaymentMethod.CreditCard, 6, "CreditCard2to6x")]
    [InlineData(PaymentMethod.CreditCard, 7, "CreditCard7to12x")]
    [InlineData(PaymentMethod.CreditCard, 12, "CreditCard7to12x")]
    public void ResolveFeeType_ReturnsCorrectCategory(PaymentMethod pm, int installments, string expected)
    {
        FinancialKpiCalculator.ResolveFeeType(pm, installments).Should().Be(expected);
    }
}