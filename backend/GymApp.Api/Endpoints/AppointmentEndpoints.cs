using GymApp.Api.DTOs;
using GymApp.Domain.Enums;
using GymApp.Infra.Data;
using GymApp.Infra.Services;
using Microsoft.EntityFrameworkCore;

namespace GymApp.Api.Endpoints;

public static class AppointmentEndpoints
{
    public static void MapAppointmentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/appointments").RequireAuthorization("AdminOrAbove");

        // List all bookings for a given date (appointments view for BeautySalon admin)
        group.MapGet("/", async (DateOnly? date, AppDbContext db, TenantContext tenant) =>
        {
            var day = date ?? DateOnly.FromDateTime(DateTime.Today);

            var bookings = await db.Bookings.AsNoTracking()
                .Include(b => b.Session).ThenInclude(s => s.ClassType)
                .Include(b => b.Student)
                .Where(b =>
                    b.Session.Date == day &&
                    b.Session.TenantId == tenant.TenantId &&
                    b.Status != BookingStatus.Cancelled)
                .OrderBy(b => b.Session.StartTime)
                .ThenBy(b => b.Student.Name)
                .Select(b => new AppointmentResponse(
                    b.Id,
                    b.SessionId,
                    b.Session.StartTime,
                    b.Session.DurationMinutes,
                    b.Session.ClassType != null ? b.Session.ClassType.Name : "",
                    b.Session.ClassType != null ? b.Session.ClassType.Color : "#ccc",
                    b.Session.ClassType != null ? b.Session.ClassType.Price : null,
                    b.StudentId,
                    b.Student.Name,
                    b.Student.Phone,
                    b.Status,
                    b.CheckedInAt,
                    b.CreatedAt))
                .ToListAsync();

            return Results.Ok(bookings);
        });
    }
}
