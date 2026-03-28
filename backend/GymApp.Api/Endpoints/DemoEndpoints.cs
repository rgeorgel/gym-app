using GymApp.Api.Services;
using GymApp.Infra.Data;

namespace GymApp.Api.Endpoints;

public static class DemoEndpoints
{
    public static void MapDemoEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/tenants").RequireAuthorization("SuperAdmin");

        group.MapPost("/{id:guid}/demo", async (Guid id, AppDbContext db, DemoSeedService seeder) =>
        {
            var tenant = await db.Tenants.FindAsync(id);
            if (tenant is null) return Results.NotFound();

            if (await seeder.HasDemoDataAsync(id))
                return Results.Conflict(new { message = "Este tenant já possui dados demo." });

            await seeder.SeedAsync(tenant);
            return Results.Ok(new { message = "Dados demo inseridos com sucesso." });
        });

        group.MapDelete("/{id:guid}/demo", async (Guid id, AppDbContext db, DemoSeedService seeder) =>
        {
            var tenant = await db.Tenants.FindAsync(id);
            if (tenant is null) return Results.NotFound();

            if (!await seeder.HasDemoDataAsync(id))
                return Results.NotFound(new { message = "Nenhum dado demo encontrado para este tenant." });

            await seeder.RemoveAsync(id);
            return Results.Ok(new { message = "Dados demo removidos com sucesso." });
        });
    }
}
