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

        group.MapGet("/stats", async (AppDbContext db, TenantContext tenant) =>
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var startOfMonth = new DateOnly(today.Year, today.Month, 1);
            var startOfMonthUtc = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var thirtyDaysAgo = today.AddDays(-30);

            var totalStudents = await db.Users.CountAsync(u =>
                u.TenantId == tenant.TenantId && u.Role == UserRole.Student && u.Status == StudentStatus.Active);

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
                p.ExpiresAt <= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)));

            // Average occupancy — last 30 days
            var sessionStats = await db.Sessions.AsNoTracking()
                .Where(s => s.Schedule.TenantId == tenant.TenantId && s.Date >= thirtyDaysAgo && s.Date < today)
                .Select(s => new
                {
                    Capacity = s.Schedule.Capacity,
                    Active = s.Bookings.Count(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn)
                })
                .ToListAsync();

            var avgOccupancyPct = sessionStats.Count > 0
                ? Math.Round(sessionStats.Average(s => s.Capacity > 0 ? (double)s.Active / s.Capacity * 100 : 0), 1)
                : 0.0;

            // Revenue this month (sum of price × credits for packages created this month)
            var revenueThisMonth = await db.PackageItems.AsNoTracking()
                .Where(i => i.Package.TenantId == tenant.TenantId && i.Package.CreatedAt >= startOfMonthUtc)
                .SumAsync(i => i.PricePerCredit * i.TotalCredits);

            // New students this month
            var newStudentsThisMonth = await db.Users.CountAsync(u =>
                u.TenantId == tenant.TenantId &&
                u.Role == UserRole.Student &&
                u.CreatedAt >= startOfMonthUtc);

            // Active students with no remaining credits
            var studentsWithCreditIds = await db.PackageItems.AsNoTracking()
                .Where(i => i.Package.TenantId == tenant.TenantId && i.Package.IsActive && i.UsedCredits < i.TotalCredits)
                .Select(i => i.Package.StudentId)
                .Distinct()
                .ToListAsync();

            var studentsWithNoCredits = await db.Users.CountAsync(u =>
                u.TenantId == tenant.TenantId &&
                u.Role == UserRole.Student &&
                u.Status == StudentStatus.Active &&
                !studentsWithCreditIds.Contains(u.Id));

            // Cancellation rate this month
            var totalBookingsMonth = await db.Bookings.CountAsync(b =>
                b.Session.Schedule.TenantId == tenant.TenantId && b.Session.Date >= startOfMonth);

            var cancelledBookingsMonth = await db.Bookings.CountAsync(b =>
                b.Session.Schedule.TenantId == tenant.TenantId &&
                b.Session.Date >= startOfMonth &&
                b.Status == BookingStatus.Cancelled);

            var cancellationRatePct = totalBookingsMonth > 0
                ? Math.Round((double)cancelledBookingsMonth / totalBookingsMonth * 100, 1)
                : 0.0;

            return Results.Ok(new
            {
                TotalStudents = totalStudents,
                BookingsThisMonth = bookingsThisMonth,
                SessionsToday = sessionsToday,
                ExpiringPackages = expiringPackages,
                AvgOccupancyPct = avgOccupancyPct,
                RevenueThisMonth = revenueThisMonth,
                NewStudentsThisMonth = newStudentsThisMonth,
                StudentsWithNoCredits = studentsWithNoCredits,
                CancellationRatePct = cancellationRatePct,
            });
        });

        group.MapGet("/today", async (AppDbContext db, TenantContext tenant) =>
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var sessions = await db.Sessions.AsNoTracking()
                .Include(s => s.Schedule).ThenInclude(s => s.ClassType)
                .Include(s => s.Bookings)
                .Where(s => s.Schedule.TenantId == tenant.TenantId && s.Date == today)
                .OrderBy(s => s.Schedule.StartTime)
                .ToListAsync();

            return Results.Ok(sessions.Select(s => new
            {
                s.Id,
                s.Date,
                StartTime = s.Schedule.StartTime,
                ClassType = s.Schedule.ClassType.Name,
                ClassTypeColor = s.Schedule.ClassType.Color,
                s.Schedule.Capacity,
                s.Status,
                Bookings = s.Bookings.Count(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn),
                CheckedIn = s.Bookings.Count(b => b.Status == BookingStatus.CheckedIn),
                OccupancyPct = s.Schedule.Capacity > 0
                    ? Math.Round((double)s.Bookings.Count(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn) / s.Schedule.Capacity * 100)
                    : 0.0
            }));
        });

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

            return Results.Ok(sessions.Select(s => new
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
                    ? Math.Round((double)s.Bookings.Count(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn) / s.Schedule.Capacity * 100)
                    : 0.0
            }));
        });

        group.MapGet("/expiring-packages", async (AppDbContext db, TenantContext tenant) =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var until = today.AddDays(14);

            var packages = await db.Packages.AsNoTracking()
                .Include(p => p.Student)
                .Include(p => p.Items)
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
                IsExpired = p.ExpiresAt < today,
                StudentName = p.Student.Name,
                StudentEmail = p.Student.Email,
                RemainingCredits = p.Items.Sum(i => i.TotalCredits - i.UsedCredits)
            }));
        });

        group.MapGet("/frequency", async (AppDbContext db, TenantContext tenant, int? days) =>
        {
            var period = days ?? 30;
            var since = DateOnly.FromDateTime(DateTime.Today.AddDays(-period));

            var data = await db.Bookings.AsNoTracking()
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

        group.MapGet("/weekly-checkins", async (AppDbContext db, TenantContext tenant) =>
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var since = today.AddDays(-56); // 8 weeks

            var dates = await db.Bookings.AsNoTracking()
                .Where(b =>
                    b.Session.Schedule.TenantId == tenant.TenantId &&
                    b.Session.Date >= since &&
                    (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn))
                .Select(b => b.Session.Date)
                .ToListAsync();

            var result = Enumerable.Range(0, 8).Select(weekAgo =>
            {
                var weekEnd = today.AddDays(-(weekAgo * 7));
                var weekStart = weekEnd.AddDays(-6);
                var count = dates.Count(d => d >= weekStart && d <= weekEnd);
                return new { WeekStart = weekStart, WeekEnd = weekEnd, Count = count };
            })
            .OrderBy(x => x.WeekStart)
            .ToList();

            return Results.Ok(result);
        });

        group.MapGet("/inactive-students", async (AppDbContext db, TenantContext tenant, int? days) =>
        {
            var inactiveSince = DateOnly.FromDateTime(DateTime.Today.AddDays(-(days ?? 14)));

            var activeStudents = await db.Users.AsNoTracking()
                .Where(u => u.TenantId == tenant.TenantId && u.Role == UserRole.Student && u.Status == StudentStatus.Active)
                .Select(u => new { u.Id, u.Name, u.Email, u.Phone })
                .ToListAsync();

            var recentIds = await db.Bookings
                .Where(b =>
                    b.Session.Schedule.TenantId == tenant.TenantId &&
                    b.Session.Date >= inactiveSince &&
                    b.Status != BookingStatus.Cancelled)
                .Select(b => b.StudentId)
                .Distinct()
                .ToListAsync();

            return Results.Ok(activeStudents.Where(s => !recentIds.Contains(s.Id)));
        });
    }
}
