using GymApp.Domain.Enums;

namespace GymApp.Api.Core;

public static class FeeCalculator
{
    public static string ResolveFeeType(PaymentMethod pm, int installments) => pm switch
    {
        PaymentMethod.Cash => "Cash",
        PaymentMethod.Pix => "Pix",
        PaymentMethod.DebitCard => "DebitCard",
        PaymentMethod.CreditCard when installments <= 1 => "CreditCard1x",
        PaymentMethod.CreditCard when installments <= 6 => "CreditCard2to6x",
        PaymentMethod.CreditCard => "CreditCard7to12x",
        _ => "Cash"
    };

    public static decimal CalculateFeePercentage(string feeType, Dictionary<string, decimal>? cardFeeConfigs)
    {
        if (string.IsNullOrEmpty(feeType) || cardFeeConfigs is null)
            return 0m;

        return cardFeeConfigs.TryGetValue(feeType, out var fee) ? fee : 0m;
    }

    public static decimal CalculateNetAmount(decimal grossAmount, decimal feePercentage)
    {
        var fee = grossAmount * feePercentage;
        return grossAmount - fee;
    }
}