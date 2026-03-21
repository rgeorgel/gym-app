using GymApp.Api.DTOs;
using GymApp.Domain.Entities;
using GymApp.Infra.Data;
using GymApp.Infra.Services;
using Microsoft.EntityFrameworkCore;

namespace GymApp.Api.Endpoints;

public static class ClassTypeEndpoints
{
    public static void MapClassTypeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/class-types").RequireAuthorization();

        group.MapGet("/", async (AppDbContext db, TenantContext tenant) =>
        {
            var types = await db.ClassTypes.AsNoTracking()
                .Where(ct => ct.TenantId == tenant.TenantId)
                .OrderBy(ct => ct.Category != null ? ct.Category.SortOrder : int.MaxValue)
                .ThenBy(ct => ct.Name)
                .Select(ct => new ClassTypeResponse(ct.Id, ct.Name, ct.Description, ct.Color, ct.ModalityType, ct.IsActive, ct.Price, ct.DurationMinutes, ct.CategoryId, ct.Category != null ? ct.Category.Name : null))
                .ToListAsync();
            return Results.Ok(types);
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, TenantContext tenant) =>
        {
            var ct = await db.ClassTypes.AsNoTracking()
                .Include(x => x.Category)
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.TenantId);
            return ct is null ? Results.NotFound() :
                Results.Ok(new ClassTypeResponse(ct.Id, ct.Name, ct.Description, ct.Color, ct.ModalityType, ct.IsActive, ct.Price, ct.DurationMinutes, ct.CategoryId, ct.Category?.Name));
        });

        group.MapPost("/", async (CreateClassTypeRequest req, AppDbContext db, TenantContext tenant) =>
        {
            // Validate category belongs to tenant if provided
            if (req.CategoryId.HasValue)
            {
                var cat = await db.ServiceCategories.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == req.CategoryId && c.TenantId == tenant.TenantId);
                if (cat is null) return Results.BadRequest("Categoria não encontrada.");
            }

            var ct = new ClassType
            {
                TenantId = tenant.TenantId,
                Name = req.Name,
                Description = req.Description,
                Color = req.Color,
                ModalityType = req.ModalityType,
                Price = req.Price,
                DurationMinutes = req.DurationMinutes,
                CategoryId = req.CategoryId
            };
            db.ClassTypes.Add(ct);
            await db.SaveChangesAsync();
            var catName = req.CategoryId.HasValue
                ? (await db.ServiceCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.CategoryId))?.Name
                : null;
            return Results.Created($"/api/class-types/{ct.Id}",
                new ClassTypeResponse(ct.Id, ct.Name, ct.Description, ct.Color, ct.ModalityType, ct.IsActive, ct.Price, ct.DurationMinutes, ct.CategoryId, catName));
        }).RequireAuthorization("AdminOrAbove");

        group.MapPut("/{id:guid}", async (Guid id, UpdateClassTypeRequest req, AppDbContext db, TenantContext tenant) =>
        {
            var ct = await db.ClassTypes.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.TenantId);
            if (ct is null) return Results.NotFound();

            // Validate category belongs to tenant if provided
            if (req.CategoryId.HasValue)
            {
                var cat = await db.ServiceCategories.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == req.CategoryId && c.TenantId == tenant.TenantId);
                if (cat is null) return Results.BadRequest("Categoria não encontrada.");
            }

            ct.Name = req.Name;
            ct.Description = req.Description;
            ct.Color = req.Color;
            ct.ModalityType = req.ModalityType;
            ct.IsActive = req.IsActive;
            ct.Price = req.Price;
            ct.DurationMinutes = req.DurationMinutes;
            ct.CategoryId = req.CategoryId;
            await db.SaveChangesAsync();
            var catName = ct.CategoryId.HasValue
                ? (await db.ServiceCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == ct.CategoryId))?.Name
                : null;
            return Results.Ok(new ClassTypeResponse(ct.Id, ct.Name, ct.Description, ct.Color, ct.ModalityType, ct.IsActive, ct.Price, ct.DurationMinutes, ct.CategoryId, catName));
        }).RequireAuthorization("AdminOrAbove");

        group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, TenantContext tenant) =>
        {
            var ct = await db.ClassTypes.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.TenantId);
            if (ct is null) return Results.NotFound();
            ct.IsActive = false;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("AdminOrAbove");

        // Public: list active services with price (no auth required — shareable catalog)
        app.MapGet("/api/public/services", async (HttpContext ctx, AppDbContext db) =>
        {
            var slug = ctx.Request.Headers["X-Tenant-Slug"].FirstOrDefault();
            if (string.IsNullOrEmpty(slug)) return Results.BadRequest("Tenant slug required.");

            var tenant = await db.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Slug == slug && t.IsActive);
            if (tenant is null) return Results.NotFound();

            var services = await db.ClassTypes.AsNoTracking()
                .Where(ct => ct.TenantId == tenant.Id && ct.IsActive)
                .OrderBy(ct => ct.Category != null ? ct.Category.SortOrder : int.MaxValue)
                .ThenBy(ct => ct.Name)
                .Select(ct => new ClassTypeResponse(ct.Id, ct.Name, ct.Description, ct.Color, ct.ModalityType, ct.IsActive, ct.Price, ct.DurationMinutes, ct.CategoryId, ct.Category != null ? ct.Category.Name : null))
                .ToListAsync();

            return Results.Ok(services);
        }).AllowAnonymous();
    }
}
