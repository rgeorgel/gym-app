using GymApp.Api.DTOs;
using GymApp.Domain.Entities;
using GymApp.Domain.Enums;
using GymApp.Infra.Data;
using GymApp.Infra.Services;
using Microsoft.EntityFrameworkCore;

namespace GymApp.Api.Endpoints;

public static class AvailabilityEndpoints
{
    public static void MapAvailabilityEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/availability").RequireAuthorization();

        // List availability blocks for the tenant
        group.MapGet("/", async (AppDbContext db, TenantContext tenant) =>
        {
            var blocks = await db.ProfessionalAvailability.AsNoTracking()
                .Include(a => a.Instructor).ThenInclude(i => i!.User)
                .Where(a => a.TenantId == tenant.TenantId && a.IsActive)
                .OrderBy(a => a.Weekday).ThenBy(a => a.StartTime)
                .Select(a => new AvailabilityResponse(
                    a.Id, a.Weekday, a.StartTime, a.EndTime,
                    a.InstructorId, a.Instructor != null ? a.Instructor.User.Name : null,
                    a.IsActive))
                .ToListAsync();

            return Results.Ok(blocks);
        }).RequireAuthorization("AdminOrAbove");

        // Create an availability block
        group.MapPost("/", async (CreateAvailabilityRequest req, AppDbContext db, TenantContext tenant) =>
        {
            if (req.StartTime >= req.EndTime)
                return Results.BadRequest("StartTime must be before EndTime.");

            var block = new ProfessionalAvailability
            {
                TenantId = tenant.TenantId,
                InstructorId = req.InstructorId,
                Weekday = req.Weekday,
                StartTime = req.StartTime,
                EndTime = req.EndTime
            };
            db.ProfessionalAvailability.Add(block);
            await db.SaveChangesAsync();

            return Results.Created($"/api/availability/{block.Id}",
                new AvailabilityResponse(block.Id, block.Weekday, block.StartTime, block.EndTime,
                    block.InstructorId, null, block.IsActive));
        }).RequireAuthorization("AdminOrAbove");

        // Delete an availability block
        group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, TenantContext tenant) =>
        {
            var block = await db.ProfessionalAvailability
                .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenant.TenantId);

            if (block is null) return Results.NotFound();
            block.IsActive = false;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("AdminOrAbove");

        // Get available time slots for a given date and service
        app.MapGet("/api/slots", async (DateOnly date, Guid serviceId, AppDbContext db, TenantContext tenant) =>
        {
            var service = await db.ClassTypes.AsNoTracking()
                .FirstOrDefaultAsync(ct => ct.Id == serviceId && ct.TenantId == tenant.TenantId && ct.IsActive);

            if (service is null) return Results.NotFound("Service not found.");

            int duration = service.DurationMinutes ?? 30;
            var weekday = (int)date.DayOfWeek;

            var blocks = await db.ProfessionalAvailability.AsNoTracking()
                .Where(a => a.TenantId == tenant.TenantId && a.Weekday == weekday && a.IsActive)
                .ToListAsync();

            if (!blocks.Any()) return Results.Ok(Array.Empty<TimeOnly>());

            // Get existing salon sessions on that date that might conflict
            var existingSessions = await db.Sessions.AsNoTracking()
                .Where(s =>
                    s.TenantId == tenant.TenantId &&
                    s.Date == date &&
                    s.ScheduleId == null &&
                    s.Status != SessionStatus.Cancelled)
                .Select(s => new { s.StartTime, s.DurationMinutes })
                .ToListAsync();

            // Generate all candidate slots across all availability blocks
            var allSlots = new SortedSet<TimeOnly>();
            foreach (var block in blocks)
            {
                var slot = block.StartTime;
                while (slot.Add(TimeSpan.FromMinutes(duration)) <= block.EndTime)
                {
                    allSlots.Add(slot);
                    slot = slot.Add(TimeSpan.FromMinutes(duration));
                }
            }

            // Filter out slots that overlap with existing sessions
            var available = allSlots.Where(slot =>
            {
                var newStart = slot.Hour * 60 + slot.Minute;
                var newEnd = newStart + duration;
                return !existingSessions.Any(s =>
                {
                    var sStart = s.StartTime.Hour * 60 + s.StartTime.Minute;
                    var sEnd = sStart + s.DurationMinutes;
                    return newStart < sEnd && newEnd > sStart;
                });
            }).ToList();

            return Results.Ok(available);
        }).RequireAuthorization();
    }
}
