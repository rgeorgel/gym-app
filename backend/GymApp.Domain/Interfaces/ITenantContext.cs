namespace GymApp.Domain.Interfaces;

public interface ITenantContext
{
    Guid TenantId { get; }
    string Slug { get; }
    bool IsResolved { get; }
}
