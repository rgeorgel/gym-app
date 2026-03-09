using GymApp.Api.DTOs;
using GymApp.Domain.Entities;
using GymApp.Infra.Data;
using GymApp.Infra.Services;
using Microsoft.EntityFrameworkCore;

namespace GymApp.Api.Endpoints;

public static class PackageEndpoints
{
    public static void MapPackageEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/packages").RequireAuthorization();

        group.MapGet("/", async (AppDbContext db, TenantContext tenant, Guid? studentId) =>
        {
            var query = db.Packages.AsNoTracking()
                .Include(p => p.Items).ThenInclude(i => i.ClassType)
                .Where(p => p.TenantId == tenant.TenantId);

            if (studentId.HasValue)
                query = query.Where(p => p.StudentId == studentId.Value);

            var packages = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();

            return Results.Ok(packages.Select(MapResponse));
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, TenantContext tenant) =>
        {
            var package = await db.Packages.AsNoTracking()
                .Include(p => p.Items).ThenInclude(i => i.ClassType)
                .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenant.TenantId);

            return package is null ? Results.NotFound() : Results.Ok(MapResponse(package));
        });

        group.MapPost("/", async (CreatePackageRequest req, AppDbContext db, TenantContext tenant) =>
        {
            var package = new Package
            {
                TenantId = tenant.TenantId,
                StudentId = req.StudentId,
                Name = req.Name,
                ExpiresAt = req.ExpiresAt
            };

            foreach (var item in req.Items)
            {
                package.Items.Add(new PackageItem
                {
                    ClassTypeId = item.ClassTypeId,
                    TotalCredits = item.TotalCredits,
                    PricePerCredit = item.PricePerCredit
                });
            }

            db.Packages.Add(package);
            await db.SaveChangesAsync();

            var created = await db.Packages.AsNoTracking()
                .Include(p => p.Items).ThenInclude(i => i.ClassType)
                .FirstAsync(p => p.Id == package.Id);

            return Results.Created($"/api/packages/{package.Id}", MapResponse(created));
        }).RequireAuthorization("AdminOrAbove");

        group.MapPost("/{id:guid}/items", async (Guid id, CreatePackageItemRequest req, AppDbContext db, TenantContext tenant) =>
        {
            var package = await db.Packages.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenant.TenantId);
            if (package is null) return Results.NotFound();

            var item = new PackageItem
            {
                PackageId = id,
                ClassTypeId = req.ClassTypeId,
                TotalCredits = req.TotalCredits,
                PricePerCredit = req.PricePerCredit
            };
            db.PackageItems.Add(item);
            await db.SaveChangesAsync();
            return Results.Created($"/api/packages/{id}", item.Id);
        }).RequireAuthorization("AdminOrAbove");

        group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, TenantContext tenant) =>
        {
            var package = await db.Packages.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenant.TenantId);
            if (package is null) return Results.NotFound();
            package.IsActive = false;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("AdminOrAbove");
    }

    private static PackageResponse MapResponse(Package p) => new(
        p.Id, p.Name, p.ExpiresAt, p.IsActive, p.CreatedAt,
        p.Items.Select(i => new PackageItemResponse(
            i.Id, i.ClassTypeId, i.ClassType.Name, i.ClassType.Color,
            i.TotalCredits, i.UsedCredits, i.TotalCredits - i.UsedCredits, i.PricePerCredit
        )).ToList()
    );
}
