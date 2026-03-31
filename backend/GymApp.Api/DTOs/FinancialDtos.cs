using GymApp.Domain.Enums;

namespace GymApp.Api.DTOs;

public record TransactionResponse(
    Guid Id,
    DateOnly Date,
    Guid? StudentId,
    string? StudentName,
    Guid? BookingId,
    string ServiceName,
    decimal GrossAmount,
    string PaymentMethod,
    int Installments,
    decimal CardFeePercentage,
    decimal CardFeeAmount,
    decimal NetAmount,
    string? Notes,
    DateTime CreatedAt
);

public record CreateTransactionRequest(
    DateOnly Date,
    Guid? StudentId,
    string? StudentName,
    Guid? BookingId,
    string ServiceName,
    decimal GrossAmount,
    string PaymentMethod,
    int Installments,
    string? Notes
);

public record UpdateTransactionRequest(
    DateOnly Date,
    string ServiceName,
    decimal GrossAmount,
    string PaymentMethod,
    int Installments,
    string? StudentName,
    string? Notes
);

public record ExpenseResponse(
    Guid Id,
    DateOnly Date,
    string Category,
    string Description,
    decimal Amount,
    bool IsRecurring,
    Guid? OriginalExpenseId,
    DateTime CreatedAt
);

public record CreateExpenseRequest(
    DateOnly Date,
    string Category,
    string Description,
    decimal Amount,
    bool IsRecurring
);

public record UpdateExpenseRequest(
    DateOnly Date,
    string Category,
    string Description,
    decimal Amount,
    bool IsRecurring
);

public record FinancialDashboardResponse(
    decimal GrossRevenue,
    decimal CardFees,
    decimal NetRevenue,
    decimal TotalExpenses,
    decimal Profit,
    decimal TicketAverage,
    int AppointmentsCount,
    decimal PrevGrossRevenue,
    decimal PrevNetRevenue,
    decimal PrevTotalExpenses,
    decimal PrevProfit
);

public record CardFeeConfigResponse(
    string FeeType,
    decimal FeePercentage
);

public record UpdateCardFeesRequest(
    decimal Cash,
    decimal Pix,
    decimal DebitCard,
    decimal CreditCard1x,
    decimal CreditCard2to6x,
    decimal CreditCard7to12x
);
