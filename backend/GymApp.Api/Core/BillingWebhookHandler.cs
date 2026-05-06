using GymApp.Domain.Enums;

namespace GymApp.Api.Core;

public static class BillingWebhookHandler
{
    public static (DateTime from, DateTime to) CalculateSubscriptionPeriod(
        DateTime? currentPeriodEnd,
        int additionalDays = 30)
    {
        var from = currentPeriodEnd.HasValue && currentPeriodEnd.Value > DateTime.UtcNow
            ? currentPeriodEnd.Value
            : DateTime.UtcNow;
        return (from, from.AddDays(additionalDays));
    }

    public static bool IsFirstPayment(SubscriptionStatus currentStatus) =>
        currentStatus != SubscriptionStatus.Active;

    public static decimal CalculateAffiliateCommission(decimal grossAmount, decimal commissionRate) =>
        Math.Round(grossAmount * commissionRate, 2);

    public static decimal CalculateAffiliateBalance(
        decimal totalEarned,
        decimal commission,
        decimal paidOut,
        decimal pendingWithdrawals) =>
        Math.Max(0m, totalEarned + commission - paidOut - pendingWithdrawals);

    public static bool ShouldGrantReferralReward(
        bool isFirstPayment,
        bool referralRewardClaimed,
        Guid? referredByTenantId) =>
        isFirstPayment && !referralRewardClaimed && referredByTenantId.HasValue;

    public static (DateTime from, DateTime to) CalculateReferralRewardPeriod(DateTime? currentPeriodEnd) =>
        CalculateSubscriptionPeriod(currentPeriodEnd, additionalDays: 30);

    public static bool IsValidBillingId(string? billingId) =>
        !string.IsNullOrWhiteSpace(billingId);

    public static bool IsValidCustomerId(string? customerId) =>
        !string.IsNullOrWhiteSpace(customerId);

    public static SubscriptionStatus DetermineStatusFromEvent(string eventType, SubscriptionStatus currentStatus) =>
        eventType switch
        {
            "billing.paid" => SubscriptionStatus.Active,
            "billing.expired" or "billing.failed" => SubscriptionStatus.PastDue,
            "billing.cancelled" or "subscription.canceled" => SubscriptionStatus.Canceled,
            _ => currentStatus
        };
}

public static class AffiliateCommissionCalculator
{
    public static AffiliateCommissionResult Calculate(
        decimal subscriptionPriceCents,
        decimal commissionRate,
        string? paymentRef = null)
    {
        var grossAmount = subscriptionPriceCents / 100m;
        var commission = Math.Round(grossAmount * commissionRate, 2);
        return new AffiliateCommissionResult(grossAmount, commission, paymentRef ?? Guid.NewGuid().ToString());
    }

    public record AffiliateCommissionResult(decimal GrossAmount, decimal Commission, string PaymentRef);
}