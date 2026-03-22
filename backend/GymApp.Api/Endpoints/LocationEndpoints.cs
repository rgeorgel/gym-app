using GymApp.Api.DTOs;
using GymApp.Domain.Entities;
using GymApp.Infra.Data;
using GymApp.Infra.Services;
using Microsoft.EntityFrameworkCore;

namespace GymApp.Api.Endpoints;

public static class LocationEndpoints
{
    public static void MapLocationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/locations").RequireAuthorization();

        group.MapGet("/", async (AppDbContext db, TenantContext tenant) =>
        {
            var locations = await db.Locations.AsNoTracking()
                .Where(l => l.TenantId == tenant.TenantId)
                .OrderBy(l => l.IsMain ? 0 : 1)
                .ThenBy(l => l.Name)
                .Select(l => new LocationResponse(l.Id, l.Name, l.Address, l.Phone, l.IsMain, l.CreatedAt))
                .ToListAsync();
            return Results.Ok(locations);
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, TenantContext tenant) =>
        {
            var location = await db.Locations.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == id && l.TenantId == tenant.TenantId);
            return location is null ? Results.NotFound() :
                Results.Ok(new LocationResponse(location.Id, location.Name, location.Address, location.Phone, location.IsMain, location.CreatedAt));
        });

        group.MapPost("/", async (CreateLocationRequest req, AppDbContext db, TenantContext tenant) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest("Nome é obrigatório.");

            if (req.IsMain)
            {
                var existingMain = await db.Locations
                    .Where(l => l.TenantId == tenant.TenantId && l.IsMain)
                    .FirstOrDefaultAsync();
                if (existingMain != null)
                    existingMain.IsMain = false;
            }

            var location = new Location
            {
                TenantId = tenant.TenantId,
                Name = req.Name,
                Address = req.Address,
                Phone = req.Phone,
                IsMain = req.IsMain
            };
            db.Locations.Add(location);
            await db.SaveChangesAsync();
            return Results.Created($"/api/locations/{location.Id}",
                new LocationResponse(location.Id, location.Name, location.Address, location.Phone, location.IsMain, location.CreatedAt));
        }).RequireAuthorization("AdminOrAbove");

        group.MapPut("/{id:guid}", async (Guid id, UpdateLocationRequest req, AppDbContext db, TenantContext tenant) =>
        {
            var location = await db.Locations
                .FirstOrDefaultAsync(l => l.Id == id && l.TenantId == tenant.TenantId);
            if (location is null) return Results.NotFound();

            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest("Nome é obrigatório.");

            if (req.IsMain && !location.IsMain)
            {
                var existingMain = await db.Locations
                    .Where(l => l.TenantId == tenant.TenantId && l.IsMain && l.Id != id)
                    .FirstOrDefaultAsync();
                if (existingMain != null)
                    existingMain.IsMain = false;
            }

            location.Name = req.Name;
            location.Address = req.Address;
            location.Phone = req.Phone;
            location.IsMain = req.IsMain;
            await db.SaveChangesAsync();
            return Results.Ok(new LocationResponse(location.Id, location.Name, location.Address, location.Phone, location.IsMain, location.CreatedAt));
        }).RequireAuthorization("AdminOrAbove");

        group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, TenantContext tenant) =>
        {
            var location = await db.Locations
                .FirstOrDefaultAsync(l => l.Id == id && l.TenantId == tenant.TenantId);
            if (location is null) return Results.NotFound();

            var futureSessions = await db.Sessions
                .Where(s => s.LocationId == id && s.Date >= DateOnly.FromDateTime(DateTime.UtcNow))
                .CountAsync();
            if (futureSessions > 0)
                return Results.BadRequest($"Esta localização possui {futureSessions} sessão(ões) futuras. Cancele ou mova as sessões antes de excluir.");

            var totalLocations = await db.Locations
                .Where(l => l.TenantId == tenant.TenantId)
                .CountAsync();
            if (totalLocations <= 1)
                return Results.BadRequest("Não é possível excluir a última localização.");

            db.Locations.Remove(location);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("AdminOrAbove");
    }
}
