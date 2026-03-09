using GymApp.Domain.Interfaces;

namespace GymApp.Infra.Services;

public class TenantContext : ITenantContext
{
    public Guid TenantId { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public bool IsResolved { get; private set; }

    public void Resolve(Guid tenantId, string slug)
    {
        TenantId = tenantId;
        Slug = slug;
        IsResolved = true;
    }
}
