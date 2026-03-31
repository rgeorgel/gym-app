using GymApp.Domain.Enums;

namespace GymApp.Domain.Entities;

public class FinancialTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public DateOnly Date { get; set; }
    public Guid? StudentId { get; set; }
    public string? StudentName { get; set; }
    public Guid? BookingId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public decimal GrossAmount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public int Installments { get; set; } = 1;
    public decimal CardFeePercentage { get; set; }
    public decimal CardFeeAmount { get; set; }
    public decimal NetAmount { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
}
