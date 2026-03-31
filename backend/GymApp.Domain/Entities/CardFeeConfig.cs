namespace GymApp.Domain.Entities;

public class CardFeeConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string FeeType { get; set; } = string.Empty; // Cash, Pix, DebitCard, CreditCard1x, CreditCard2to6x, CreditCard7to12x
    public decimal FeePercentage { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
