using GymApp.Api.DTOs;
using GymApp.Domain.Entities;
using GymApp.Infra.Data;
using GymApp.Infra.Services;
using Microsoft.EntityFrameworkCore;

namespace GymApp.Api.Endpoints;

public static class PackageTemplateEndpoints
{
    public static void MapPackageTemplateEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/package-templates").RequireAuthorization("AdminOrAbove");

        group.MapGet("/", async (AppDbContext db, TenantContext tenant) =>
        {
            var templates = await db.PackageTemplates.AsNoTracking()
                .Include(t => t.Items).ThenInclude(i => i.ClassType)
                .Where(t => t.TenantId == tenant.TenantId)
                .OrderBy(t => t.Name)
                .ToListAsync();

            return Results.Ok(templates.Select(ToResponse));
        });

        group.MapPost("/", async (CreatePackageTemplateRequest req, AppDbContext db, TenantContext tenant) =>
        {
            var template = new PackageTemplate
            {
                TenantId = tenant.TenantId,
                Name = req.Name,
                DurationDays = req.DurationDays
            };

            foreach (var item in req.Items)
                template.Items.Add(new PackageTemplateItem
                {
                    ClassTypeId = item.ClassTypeId,
                    TotalCredits = item.TotalCredits,
                    PricePerCredit = item.PricePerCredit
                });

            db.PackageTemplates.Add(template);
            await db.SaveChangesAsync();

            var created = await db.PackageTemplates.AsNoTracking()
                .Include(t => t.Items).ThenInclude(i => i.ClassType)
                .FirstAsync(t => t.Id == template.Id);

            return Results.Created($"/api/package-templates/{template.Id}", ToResponse(created));
        });

        group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, TenantContext tenant) =>
        {
            var template = await db.PackageTemplates
                .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenant.TenantId);

            if (template is null) return Results.NotFound();
            db.PackageTemplates.Remove(template);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Assign template to a student — creates a real Package from the template
        group.MapPost("/{id:guid}/assign", async (Guid id, AssignTemplateRequest req, AppDbContext db, TenantContext tenant) =>
        {
            var template = await db.PackageTemplates.AsNoTracking()
                .Include(t => t.Items)
                .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenant.TenantId);

            if (template is null) return Results.NotFound("Template not found.");

            var studentExists = await db.Users.AnyAsync(u =>
                u.Id == req.StudentId && u.TenantId == tenant.TenantId);
            if (!studentExists) return Results.NotFound("Student not found.");

            var expiresAt = req.ExpiresAt
                ?? (template.DurationDays.HasValue
                    ? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(template.DurationDays.Value))
                    : (DateOnly?)null);

            var package = new Package
            {
                TenantId = tenant.TenantId,
                StudentId = req.StudentId,
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
            await db.SaveChangesAsync();

            return Results.Created($"/api/packages/{package.Id}", package.Id);
        });
    }

    private static PackageTemplateResponse ToResponse(PackageTemplate t) => new(
        t.Id, t.Name, t.DurationDays, t.CreatedAt,
        t.Items.Select(i => new PackageItemResponse(
            i.Id, i.ClassTypeId, i.ClassType.Name, i.ClassType.Color,
            i.TotalCredits, 0, i.TotalCredits, i.PricePerCredit
        )).ToList()
    );
}
