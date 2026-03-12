using GymApp.Domain.Entities;
using GymApp.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace GymApp.Api.Helpers;

public static class PackageHelper
{
    /// <summary>
    /// If the tenant has a DefaultPackageTemplateId configured, creates a Package
    /// from that template and assigns it to the newly created student.
    /// Call this after adding the user to the context but before SaveChangesAsync.
    /// </summary>
    public static async Task AssignDefaultPackageIfConfiguredAsync(AppDbContext db, Guid tenantId, Guid studentId)
    {
        var tenant = await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId);

        if (tenant?.DefaultPackageTemplateId is null)
            return;

        var template = await db.PackageTemplates.AsNoTracking()
            .Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.Id == tenant.DefaultPackageTemplateId && t.TenantId == tenantId);

        if (template is null)
            return;

        var expiresAt = template.DurationDays.HasValue
            ? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(template.DurationDays.Value))
            : (DateOnly?)null;

        var package = new Package
        {
            TenantId = tenantId,
            StudentId = studentId,
            Name = template.Name,
            ExpiresAt = expiresAt
        };

        foreach (var item in template.Items)
            package.Items.Add(new PackageItem
            {
                ClassTypeId = item.ClassTypeId,
                TotalCredits = item.TotalCredits,
                PricePerCredit = item.PricePerCredit
            });

        db.Packages.Add(package);
    }

    /// <summary>
    /// Creates a Package from a specific template and assigns it to a student.
    /// Returns the created Package (not yet saved — caller must call SaveChangesAsync).
    /// </summary>
    public static async Task<Package?> AssignFromTemplateAsync(
        AppDbContext db, Guid tenantId, Guid studentId, Guid templateId)
    {
        var template = await db.PackageTemplates.AsNoTracking()
            .Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.Id == templateId && t.TenantId == tenantId);

        if (template is null) return null;

        var expiresAt = template.DurationDays.HasValue
            ? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(template.DurationDays.Value))
            : (DateOnly?)null;

        var package = new Package
        {
            TenantId = tenantId,
            StudentId = studentId,
            Name = template.Name,
            ExpiresAt = expiresAt
        };

        foreach (var item in template.Items)
            package.Items.Add(new PackageItem
            {
                ClassTypeId = item.ClassTypeId,
                TotalCredits = item.TotalCredits,
                PricePerCredit = item.PricePerCredit
            });

        db.Packages.Add(package);
        return package;
    }
}
