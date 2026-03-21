using GymApp.Api.DTOs;
using GymApp.Domain.Entities;
using GymApp.Infra.Data;
using GymApp.Infra.Services;
using Microsoft.EntityFrameworkCore;

namespace GymApp.Api.Endpoints;

public static class ServiceCategoryEndpoints
{
    public static void MapServiceCategoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/service-categories").RequireAuthorization();

        group.MapGet("/", async (AppDbContext db, TenantContext tenant) =>
        {
            var cats = await db.ServiceCategories.AsNoTracking()
                .Where(c => c.TenantId == tenant.TenantId)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .Select(c => new ServiceCategoryResponse(c.Id, c.Name, c.SortOrder, c.IsActive))
                .ToListAsync();
            return Results.Ok(cats);
        });

        group.MapPost("/", async (CreateServiceCategoryRequest req, AppDbContext db, TenantContext tenant) =>
        {
            var cat = new ServiceCategory
            {
                TenantId = tenant.TenantId,
                Name = req.Name,
                SortOrder = req.SortOrder
            };
            db.ServiceCategories.Add(cat);
            await db.SaveChangesAsync();
            return Results.Created($"/api/service-categories/{cat.Id}",
                new ServiceCategoryResponse(cat.Id, cat.Name, cat.SortOrder, cat.IsActive));
        }).RequireAuthorization("AdminOrAbove");

        group.MapPut("/{id:guid}", async (Guid id, UpdateServiceCategoryRequest req, AppDbContext db, TenantContext tenant) =>
        {
            var cat = await db.ServiceCategories.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenant.TenantId);
            if (cat is null) return Results.NotFound();

            cat.Name = req.Name;
            cat.SortOrder = req.SortOrder;
            cat.IsActive = req.IsActive;
            await db.SaveChangesAsync();
            return Results.Ok(new ServiceCategoryResponse(cat.Id, cat.Name, cat.SortOrder, cat.IsActive));
        }).RequireAuthorization("AdminOrAbove");

        group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, TenantContext tenant) =>
        {
            var cat = await db.ServiceCategories.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenant.TenantId);
            if (cat is null) return Results.NotFound();

            // Unlink services from this category before deleting
            await db.ClassTypes
                .Where(ct => ct.CategoryId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(ct => ct.CategoryId, (Guid?)null));

            db.ServiceCategories.Remove(cat);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("AdminOrAbove");
    }
}
