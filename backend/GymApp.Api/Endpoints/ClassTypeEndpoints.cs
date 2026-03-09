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
                .OrderBy(ct => ct.Name)
                .Select(ct => new ClassTypeResponse(ct.Id, ct.Name, ct.Description, ct.Color, ct.ModalityType, ct.IsActive))
                .ToListAsync();
            return Results.Ok(types);
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, TenantContext tenant) =>
        {
            var ct = await db.ClassTypes.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.TenantId);
            return ct is null ? Results.NotFound() :
                Results.Ok(new ClassTypeResponse(ct.Id, ct.Name, ct.Description, ct.Color, ct.ModalityType, ct.IsActive));
        });

        group.MapPost("/", async (CreateClassTypeRequest req, AppDbContext db, TenantContext tenant) =>
        {
            var ct = new ClassType
            {
                TenantId = tenant.TenantId,
                Name = req.Name,
                Description = req.Description,
                Color = req.Color,
                ModalityType = req.ModalityType
            };
            db.ClassTypes.Add(ct);
            await db.SaveChangesAsync();
            return Results.Created($"/api/class-types/{ct.Id}",
                new ClassTypeResponse(ct.Id, ct.Name, ct.Description, ct.Color, ct.ModalityType, ct.IsActive));
        }).RequireAuthorization("AdminOrAbove");

        group.MapPut("/{id:guid}", async (Guid id, UpdateClassTypeRequest req, AppDbContext db, TenantContext tenant) =>
        {
            var ct = await db.ClassTypes.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.TenantId);
            if (ct is null) return Results.NotFound();

            ct.Name = req.Name;
            ct.Description = req.Description;
            ct.Color = req.Color;
            ct.ModalityType = req.ModalityType;
            ct.IsActive = req.IsActive;
            await db.SaveChangesAsync();
            return Results.Ok(new ClassTypeResponse(ct.Id, ct.Name, ct.Description, ct.Color, ct.ModalityType, ct.IsActive));
        }).RequireAuthorization("AdminOrAbove");

        group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, TenantContext tenant) =>
        {
            var ct = await db.ClassTypes.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.TenantId);
            if (ct is null) return Results.NotFound();
            ct.IsActive = false;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("AdminOrAbove");
    }
}
