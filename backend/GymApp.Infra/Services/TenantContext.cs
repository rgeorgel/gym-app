using GymApp.Domain.Interfaces;

namespace GymApp.Infra.Services;

public class TenantContext : ITenantContext
{
    public Guid TenantId { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public bool IsResolved { get; private set; }
    public bool HasStudentAccess { get; private set; } = true;

    public void Resolve(Guid tenantId, string slug, bool hasStudentAccess = true)
    {
        TenantId = tenantId;
        Slug = slug;
        HasStudentAccess = hasStudentAccess;
        IsResolved = true;
    }
}
