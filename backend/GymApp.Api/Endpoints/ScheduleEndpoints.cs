using GymApp.Api.DTOs;
using GymApp.Domain.Entities;
using GymApp.Infra.Data;
using GymApp.Infra.Services;
using Microsoft.EntityFrameworkCore;

namespace GymApp.Api.Endpoints;

public static class ScheduleEndpoints
{
    public static void MapScheduleEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/schedules").RequireAuthorization();

        group.MapGet("/", async (AppDbContext db, TenantContext tenant) =>
        {
            var schedules = await db.Schedules.AsNoTracking()
                .Include(s => s.ClassType)
                .Include(s => s.Instructor).ThenInclude(i => i!.User)
                .Where(s => s.TenantId == tenant.TenantId && s.IsActive)
                .Where(s => tenant.LocationId == null || s.LocationId == tenant.LocationId)
                .OrderBy(s => s.Weekday).ThenBy(s => s.StartTime)
                .Select(s => ToResponse(s))
                .ToListAsync();
            return Results.Ok(schedules);
        });

        group.MapGet("/week", async (AppDbContext db, TenantContext tenant) =>
        {
            var schedules = await db.Schedules.AsNoTracking()
                .Include(s => s.ClassType)
                .Include(s => s.Instructor).ThenInclude(i => i!.User)
                .Where(s => s.TenantId == tenant.TenantId && s.IsActive)
                .Where(s => tenant.LocationId == null || s.LocationId == tenant.LocationId)
                .OrderBy(s => s.Weekday).ThenBy(s => s.StartTime)
                .ToListAsync();

            // Group by weekday for easier frontend consumption
            var grouped = schedules
                .GroupBy(s => s.Weekday)
                .ToDictionary(g => g.Key, g => g.Select(ToResponse).ToList());

            return Results.Ok(grouped);
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, TenantContext tenant) =>
        {
            var s = await db.Schedules.AsNoTracking()
                .Include(s => s.ClassType)
                .Include(s => s.Instructor).ThenInclude(i => i!.User)
                .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenant.TenantId);
            return s is null ? Results.NotFound() : Results.Ok(ToResponse(s));
        });

        group.MapPost("/", async (CreateScheduleRequest req, AppDbContext db, TenantContext tenant) =>
        {
            if (req.LocationId == Guid.Empty)
                return Results.BadRequest("Local é obrigatório.");

            var schedule = new Schedule
            {
                TenantId = tenant.TenantId,
                ClassTypeId = req.ClassTypeId,
                InstructorId = req.InstructorId,
                LocationId = req.LocationId,
                Weekday = req.Weekday,
                StartTime = req.StartTime,
                DurationMinutes = req.DurationMinutes,
                Capacity = req.Capacity
            };
            db.Schedules.Add(schedule);
            await db.SaveChangesAsync();

            var created = await db.Schedules.AsNoTracking()
                .Include(s => s.ClassType).Include(s => s.Instructor).ThenInclude(i => i!.User)
                .FirstAsync(s => s.Id == schedule.Id);

            return Results.Created($"/api/schedules/{schedule.Id}", ToResponse(created));
        }).RequireAuthorization("AdminOrAbove");

        group.MapPut("/{id:guid}", async (Guid id, UpdateScheduleRequest req, AppDbContext db, TenantContext tenant) =>
        {
            var schedule = await db.Schedules.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenant.TenantId);
            if (schedule is null) return Results.NotFound();

            schedule.ClassTypeId = req.ClassTypeId;
            schedule.InstructorId = req.InstructorId;
            schedule.LocationId = req.LocationId;
            schedule.Weekday = req.Weekday;
            schedule.StartTime = req.StartTime;
            schedule.DurationMinutes = req.DurationMinutes;
            schedule.Capacity = req.Capacity;
            schedule.IsActive = req.IsActive;
            await db.SaveChangesAsync();

            var updated = await db.Schedules.AsNoTracking()
                .Include(s => s.ClassType).Include(s => s.Instructor).ThenInclude(i => i!.User)
                .FirstAsync(s => s.Id == id);

            return Results.Ok(ToResponse(updated));
        }).RequireAuthorization("AdminOrAbove");

        group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, TenantContext tenant) =>
        {
            var schedule = await db.Schedules.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenant.TenantId);
            if (schedule is null) return Results.NotFound();
            schedule.IsActive = false;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("AdminOrAbove");
    }

    private static ScheduleResponse ToResponse(Schedule s) => new(
        s.Id, s.ClassTypeId, s.ClassType.Name, s.ClassType.Color,
        s.InstructorId, s.Instructor?.User.Name,
        s.LocationId,
        s.Weekday, s.StartTime, s.DurationMinutes, s.Capacity, s.IsActive
    );
}
