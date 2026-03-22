namespace GymApp.Domain.Interfaces;

public interface ITenantContext
{
    Guid TenantId { get; }
    string Slug { get; }
    bool IsResolved { get; }
    bool HasStudentAccess { get; }
    Guid? LocationId { get; set; }
}
