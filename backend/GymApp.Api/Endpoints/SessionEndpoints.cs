using GymApp.Api.DTOs;
using GymApp.Domain.Entities;
using GymApp.Domain.Enums;
using GymApp.Infra.Data;
using GymApp.Infra.Services;
using Microsoft.EntityFrameworkCore;

namespace GymApp.Api.Endpoints;

public static class SessionEndpoints
{
    public static void MapSessionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/sessions").RequireAuthorization();

        // Get or generate sessions for a date range
        group.MapGet("/", async (
            AppDbContext db, TenantContext tenant,
            DateOnly? from, DateOnly? to) =>
        {
            var start = from ?? DateOnly.FromDateTime(DateTime.Today);
            var end = to ?? start.AddDays(13); // 2 weeks default

            // Get all active schedules for this tenant
            var schedules = await db.Schedules.AsNoTracking()
                .Include(s => s.ClassType)
                .Include(s => s.Instructor).ThenInclude(i => i!.User)
                .Where(s => s.TenantId == tenant.TenantId && s.IsActive)
                .ToListAsync();

            // Get existing sessions in range
            var scheduleIds = schedules.Select(s => s.Id).ToList();
            var existingSessions = await db.Sessions.AsNoTracking()
                .Include(s => s.Schedule).ThenInclude(s => s.ClassType)
                .Include(s => s.Schedule.Instructor).ThenInclude(i => i!.User)
                .Include(s => s.Bookings)
                .Where(s => scheduleIds.Contains(s.ScheduleId) && s.Date >= start && s.Date <= end)
                .ToListAsync();

            var existingKeys = existingSessions.Select(s => (s.ScheduleId, s.Date)).ToHashSet();

            // Generate missing sessions
            var toCreate = new List<Session>();
            for (var date = start; date <= end; date = date.AddDays(1))
            {
                var dayOfWeek = (int)date.DayOfWeek;
                foreach (var schedule in schedules.Where(s => s.Weekday == dayOfWeek))
                {
                    if (!existingKeys.Contains((schedule.Id, date)))
                    {
                        toCreate.Add(new Session
                        {
                            ScheduleId = schedule.Id,
                            Date = date,
                            SlotsAvailable = schedule.Capacity
                        });
                    }
                }
            }

            if (toCreate.Count > 0)
            {
                db.Sessions.AddRange(toCreate);
                await db.SaveChangesAsync();
                // Reload with all data
                var newSessions = await db.Sessions.AsNoTracking()
                    .Include(s => s.Schedule).ThenInclude(s => s.ClassType)
                    .Include(s => s.Schedule.Instructor).ThenInclude(i => i!.User)
                    .Include(s => s.Bookings)
                    .Where(s => toCreate.Select(c => c.Id).Contains(s.Id))
                    .ToListAsync();
                existingSessions.AddRange(newSessions);
            }

            var result = existingSessions
                .Where(s => s.Date >= start && s.Date <= end)
                .OrderBy(s => s.Date).ThenBy(s => s.Schedule.StartTime)
                .Select(s => ToResponse(s))
                .ToList();

            return Results.Ok(result);
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, TenantContext tenant) =>
        {
            var session = await db.Sessions.AsNoTracking()
                .Include(s => s.Schedule).ThenInclude(s => s.ClassType)
                .Include(s => s.Schedule.Instructor).ThenInclude(i => i!.User)
                .Include(s => s.Bookings)
                .FirstOrDefaultAsync(s => s.Id == id && s.Schedule.TenantId == tenant.TenantId);

            return session is null ? Results.NotFound() : Results.Ok(ToResponse(session));
        });

        group.MapGet("/{id:guid}/bookings", async (Guid id, AppDbContext db, TenantContext tenant) =>
        {
            var bookings = await db.Bookings.AsNoTracking()
                .Include(b => b.Student)
                .Include(b => b.Session).ThenInclude(s => s.Schedule).ThenInclude(s => s.ClassType)
                .Where(b => b.SessionId == id && b.Session.Schedule.TenantId == tenant.TenantId)
                .OrderBy(b => b.Student.Name)
                .Select(b => new BookingResponse(
                    b.Id, b.SessionId, b.Session.Date, b.Session.Schedule.StartTime,
                    b.Session.Schedule.ClassType.Name, b.StudentId, b.Student.Name,
                    b.Status, b.CheckedInAt, b.CreatedAt))
                .ToListAsync();

            return Results.Ok(bookings);
        });

        group.MapPost("/{id:guid}/reactivate", async (Guid id, AppDbContext db, TenantContext tenant) =>
        {
            var session = await db.Sessions
                .Include(s => s.Schedule)
                .Include(s => s.Bookings).ThenInclude(b => b.PackageItem)
                .FirstOrDefaultAsync(s => s.Id == id && s.Schedule.TenantId == tenant.TenantId);

            if (session is null) return Results.NotFound();
            if (session.Status != SessionStatus.Cancelled) return Results.Conflict("Session is not cancelled.");

            session.Status = SessionStatus.Scheduled;
            session.CancellationReason = null;

            // Restore bookings that were cancelled due to this session being cancelled
            foreach (var booking in session.Bookings.Where(b =>
                b.Status == BookingStatus.Cancelled && b.CancellationReason == "Aula cancelada"))
            {
                booking.Status = BookingStatus.Confirmed;
                booking.CancellationReason = null;
                booking.CancelledAt = null;
                booking.PackageItem.UsedCredits += 1;
            }

            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization("AdminOrAbove");

        group.MapPost("/{id:guid}/cancel", async (Guid id, CancelSessionRequest req, AppDbContext db, TenantContext tenant) =>
        {
            var session = await db.Sessions
                .Include(s => s.Schedule)
                .Include(s => s.Bookings).ThenInclude(b => b.PackageItem)
                .FirstOrDefaultAsync(s => s.Id == id && s.Schedule.TenantId == tenant.TenantId);

            if (session is null) return Results.NotFound();
            if (session.Status == SessionStatus.Cancelled) return Results.Conflict("Already cancelled.");

            session.Status = SessionStatus.Cancelled;
            session.CancellationReason = req.Reason;

            // Refund credits for all confirmed bookings
            foreach (var booking in session.Bookings.Where(b => b.Status == BookingStatus.Confirmed))
            {
                booking.Status = BookingStatus.Cancelled;
                booking.CancellationReason = "Aula cancelada";
                booking.CancelledAt = DateTime.UtcNow;
                booking.PackageItem.UsedCredits = Math.Max(0, booking.PackageItem.UsedCredits - 1);
            }

            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization("AdminOrAbove");
    }

    private static SessionResponse ToResponse(Session s) => new(
        s.Id, s.ScheduleId, s.Date, s.Schedule.StartTime, s.Schedule.DurationMinutes,
        s.Schedule.ClassTypeId,
        s.Schedule.ClassType.Name, s.Schedule.ClassType.Color,
        s.Schedule.ClassType.ModalityType,
        s.Schedule.Instructor?.User.Name,
        s.Schedule.Capacity, s.SlotsAvailable, s.Status,
        s.Bookings.Count(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn)
    );
}
