using GymApp.Domain.Enums;
using GymApp.Infra.Data;
using GymApp.Infra.Services;
using Microsoft.EntityFrameworkCore;

namespace GymApp.Api.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/dashboard").RequireAuthorization("AdminOrAbove");

        group.MapGet("/occupancy", async (AppDbContext db, TenantContext tenant) =>
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var nextWeek = today.AddDays(7);

            var sessions = await db.Sessions.AsNoTracking()
                .Include(s => s.Schedule).ThenInclude(s => s.ClassType)
                .Include(s => s.Bookings)
                .Where(s => s.Schedule.TenantId == tenant.TenantId
                    && s.Date >= today && s.Date <= nextWeek
                    && s.Status == SessionStatus.Scheduled)
                .OrderBy(s => s.Date).ThenBy(s => s.Schedule.StartTime)
                .ToListAsync();

            var result = sessions.Select(s => new
            {
                s.Id,
                s.Date,
                StartTime = s.Schedule.StartTime,
                ClassType = s.Schedule.ClassType.Name,
                ClassTypeColor = s.Schedule.ClassType.Color,
                s.Schedule.Capacity,
                s.SlotsAvailable,
                Bookings = s.Bookings.Count(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn),
                OccupancyPct = s.Schedule.Capacity > 0
                    ? (double)s.Bookings.Count(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn) / s.Schedule.Capacity * 100
                    : 0
            });

            return Results.Ok(result);
        });

        group.MapGet("/stats", async (AppDbContext db, TenantContext tenant) =>
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var startOfMonth = new DateOnly(today.Year, today.Month, 1);

            var totalStudents = await db.Users.CountAsync(u =>
                u.TenantId == tenant.TenantId &&
                u.Role == UserRole.Student &&
                u.Status == StudentStatus.Active);

            var bookingsThisMonth = await db.Bookings.CountAsync(b =>
                b.Session.Schedule.TenantId == tenant.TenantId &&
                b.Session.Date >= startOfMonth &&
                b.Status != BookingStatus.Cancelled);

            var sessionsToday = await db.Sessions.CountAsync(s =>
                s.Schedule.TenantId == tenant.TenantId &&
                s.Date == today &&
                s.Status == SessionStatus.Scheduled);

            var expiringPackages = await db.Packages.CountAsync(p =>
                p.TenantId == tenant.TenantId &&
                p.IsActive &&
                p.ExpiresAt.HasValue &&
                p.ExpiresAt <= DateTime.UtcNow.AddDays(7));

            return Results.Ok(new
            {
                TotalStudents = totalStudents,
                BookingsThisMonth = bookingsThisMonth,
                SessionsToday = sessionsToday,
                ExpiringPackages = expiringPackages
            });
        });

        group.MapGet("/frequency", async (AppDbContext db, TenantContext tenant, int? days) =>
        {
            var period = days ?? 30;
            var since = DateOnly.FromDateTime(DateTime.Today.AddDays(-period));

            var data = await db.Bookings.AsNoTracking()
                .Include(b => b.Student)
                .Include(b => b.Session).ThenInclude(s => s.Schedule).ThenInclude(s => s.ClassType)
                .Where(b =>
                    b.Session.Schedule.TenantId == tenant.TenantId &&
                    b.Session.Date >= since &&
                    b.Status == BookingStatus.CheckedIn)
                .GroupBy(b => new { b.StudentId, b.Student.Name })
                .Select(g => new
                {
                    StudentId = g.Key.StudentId,
                    StudentName = g.Key.Name,
                    CheckIns = g.Count()
                })
                .OrderByDescending(x => x.CheckIns)
                .ToListAsync();

            return Results.Ok(data);
        });

        group.MapGet("/expiring-packages", async (AppDbContext db, TenantContext tenant) =>
        {
            var until = DateTime.UtcNow.AddDays(14);

            var packages = await db.Packages.AsNoTracking()
                .Include(p => p.Student)
                .Include(p => p.Items).ThenInclude(i => i.ClassType)
                .Where(p =>
                    p.TenantId == tenant.TenantId &&
                    p.IsActive &&
                    p.ExpiresAt.HasValue &&
                    p.ExpiresAt <= until)
                .OrderBy(p => p.ExpiresAt)
                .ToListAsync();

            return Results.Ok(packages.Select(p => new
            {
                p.Id,
                p.Name,
                p.ExpiresAt,
                StudentName = p.Student.Name,
                StudentEmail = p.Student.Email,
                RemainingCredits = p.Items.Sum(i => i.TotalCredits - i.UsedCredits)
            }));
        });
    }
}
