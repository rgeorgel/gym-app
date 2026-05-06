using FluentAssertions;
using GymApp.Api.Core;
using GymApp.Domain.Enums;
using Xunit;

namespace GymApp.Tests.Core;

public class BillingWebhookHandlerTests
{
    [Fact]
    public void CalculateSubscriptionPeriod_WithFuturePeriodEnd_UsesPeriodEnd()
    {
        var futureEnd = DateTime.UtcNow.AddDays(10);
        var (from, to) = BillingWebhookHandler.CalculateSubscriptionPeriod(futureEnd);

        from.Should().Be(futureEnd);
        to.Should().Be(futureEnd.AddDays(30));
    }

    [Fact]
    public void CalculateSubscriptionPeriod_WithPastPeriodEnd_UsesNow()
    {
        var pastEnd = DateTime.UtcNow.AddDays(-5);
        var (from, to) = BillingWebhookHandler.CalculateSubscriptionPeriod(pastEnd);

        from.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        to.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CalculateSubscriptionPeriod_WithNull_UsesNow()
    {
        var (from, to) = BillingWebhookHandler.CalculateSubscriptionPeriod(null);

        from.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        to.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(SubscriptionStatus.Trial, true)]
    [InlineData(SubscriptionStatus.PastDue, true)]
    [InlineData(SubscriptionStatus.Canceled, true)]
    [InlineData(SubscriptionStatus.Active, false)]
    public void IsFirstPayment_ReturnsExpected(SubscriptionStatus status, bool expected)
    {
        BillingWebhookHandler.IsFirstPayment(status).Should().Be(expected);
    }

    [Theory]
    [InlineData(100, 0.10, 10.00)]
    [InlineData(4900, 0.05, 245.00)]
    [InlineData(10000, 0.15, 1500.00)]
    public void CalculateAffiliateCommission_ReturnsCorrectAmount(decimal gross, decimal rate, decimal expected)
    {
        BillingWebhookHandler.CalculateAffiliateCommission(gross, rate).Should().Be(expected);
    }

    [Fact]
    public void CalculateAffiliateBalance_WithPositiveValues_ReturnsCorrect()
    {
        var result = BillingWebhookHandler.CalculateAffiliateBalance(
            totalEarned: 1000m,
            commission: 100m,
            paidOut: 500m,
            pendingWithdrawals: 200m);

        result.Should().Be(400m);
    }

    [Fact]
    public void CalculateAffiliateBalance_WithNegativeResult_ReturnsZero()
    {
        var result = BillingWebhookHandler.CalculateAffiliateBalance(
            totalEarned: 100m,
            commission: 50m,
            paidOut: 500m,
            pendingWithdrawals: 200m);

        result.Should().Be(0m);
    }

    [Theory]
    [InlineData(true, false, true, true)]  // first payment, not claimed, has referrer
    [InlineData(false, false, true, false)] // not first payment
    [InlineData(true, true, true, false)]  // already claimed
    [InlineData(true, false, false, false)] // no referrer
    public void ShouldGrantReferralReward_ReturnsExpected(
        bool isFirst, bool claimed, bool hasReferrer, bool expected)
    {
        var referredBy = hasReferrer ? Guid.NewGuid() : (Guid?)null;
        BillingWebhookHandler.ShouldGrantReferralReward(isFirst, claimed, referredBy).Should().Be(expected);
    }

    [Theory]
    [InlineData("billing.paid", SubscriptionStatus.Active)]
    [InlineData("billing.expired", SubscriptionStatus.PastDue)]
    [InlineData("billing.failed", SubscriptionStatus.PastDue)]
    [InlineData("billing.cancelled", SubscriptionStatus.Canceled)]
    [InlineData("subscription.canceled", SubscriptionStatus.Canceled)]
    [InlineData("unknown_event", SubscriptionStatus.Trial)] // unchanged
    public void DetermineStatusFromEvent_ReturnsExpected(string eventType, SubscriptionStatus expected)
    {
        BillingWebhookHandler.DetermineStatusFromEvent(eventType, SubscriptionStatus.Trial)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData("valid-id", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("   ", false)]
    public void IsValidBillingId_ReturnsExpected(string? id, bool expected)
    {
        BillingWebhookHandler.IsValidBillingId(id).Should().Be(expected);
    }
}

public class AffiliateCommissionCalculatorTests
{
    [Fact]
    public void Calculate_WithDefaultValues_ReturnsCorrectResult()
    {
        var result = AffiliateCommissionCalculator.Calculate(4900m, 0.05m);

        result.GrossAmount.Should().Be(49.00m);
        result.Commission.Should().Be(2.45m);
        result.PaymentRef.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Calculate_WithCustomPaymentRef_UsesProvidedRef()
    {
        var result = AffiliateCommissionCalculator.Calculate(4900m, 0.05m, "custom-ref-123");

        result.PaymentRef.Should().Be("custom-ref-123");
    }

    [Theory]
    [InlineData(4900, 0.10, 49.00, 4.90)]
    [InlineData(10000, 0.15, 100.00, 15.00)]
    public void Calculate_ReturnsCorrectValues(decimal priceCents, decimal rate, decimal expectedGross, decimal expectedCommission)
    {
        var result = AffiliateCommissionCalculator.Calculate(priceCents, rate);

        result.GrossAmount.Should().Be(expectedGross);
        result.Commission.Should().Be(expectedCommission);
    }
}