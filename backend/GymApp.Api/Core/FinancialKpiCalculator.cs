using GymApp.Domain.Enums;

namespace GymApp.Api.Core;

public static class FinancialKpiCalculator
{
    public static (decimal gross, decimal fees, decimal net, int count) CalculateRevenueKpis(
        IEnumerable<FinancialTransactionRow> transactions)
    {
        var rows = transactions.ToList();
        return (
            rows.Sum(r => r.GrossAmount),
            rows.Sum(r => r.CardFeeAmount),
            rows.Sum(r => r.NetAmount),
            rows.Count
        );
    }

    public static decimal CalculateProfit(decimal net, decimal expenses) => net - expenses;

    public static decimal CalculateAverageTicket(decimal totalRevenue, int transactionCount) =>
        transactionCount > 0 ? Math.Round(totalRevenue / transactionCount, 2) : 0m;

    public static DateOnly GetMonthStart(int year, int month) =>
        new(year, month, 1);

    public static DateOnly GetMonthEnd(int year, int month) =>
        GetMonthStart(year, month).AddMonths(1).AddDays(-1);

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

    public record FinancialTransactionRow(decimal GrossAmount, decimal CardFeeAmount, decimal NetAmount);
}