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
                b.Session.TenantId == tenant.TenantId &&
                b.Session.Date >= startOfMonth &&
                b.Status != BookingStatus.Cancelled);

            var sessionsToday = await db.Sessions.CountAsync(s =>
                s.TenantId == tenant.TenantId &&
                s.Date == today &&
                s.Status == SessionStatus.Scheduled);

            var expiringPackages = await db.Packages.CountAsync(p =>
                p.TenantId == tenant.TenantId &&
                p.IsActive &&
                p.ExpiresAt.HasValue &&
                p.ExpiresAt <= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)));

            // Average occupancy — last 30 days (gym sessions only, since salon sessions have capacity=1)
            var sessionQuery = db.Sessions.AsNoTracking()
                .Include(s => s.Schedule)
                .Where(s => s.TenantId == tenant.TenantId && s.Date >= thirtyDaysAgo && s.Date < today);

            if (tenant.LocationId.HasValue)
                sessionQuery = sessionQuery.Where(s => s.LocationId == tenant.LocationId.Value);

            var sessionStats = await sessionQuery
                .Select(s => new
                {
                    Capacity = s.Schedule != null ? s.Schedule.Capacity : 1,
                    Active = s.Bookings.Count(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn)
                })
                .ToListAsync();

            var avgOccupancyPct = sessionStats.Count > 0
                ? Math.Round(sessionStats.Average(s => s.Capacity > 0 ? (double)s.Active / s.Capacity * 100 : 0), 1)
                : 0.0;

            // Revenue this month
            var revenueThisMonth = await db.PackageItems.AsNoTracking()
                .Where(i => i.Package.TenantId == tenant.TenantId && i.Package.CreatedAt >= startOfMonthUtc)
                .SumAsync(i => i.PricePerCredit * i.TotalCredits);

            var newStudentsThisMonth = await db.Users.CountAsync(u =>
                u.TenantId == tenant.TenantId &&
                u.Role == UserRole.Student &&
                u.CreatedAt >= startOfMonthUtc);

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

            var totalBookingsMonth = await db.Bookings.CountAsync(b =>
                b.Session.TenantId == tenant.TenantId && b.Session.Date >= startOfMonth);

            var cancelledBookingsMonth = await db.Bookings.CountAsync(b =>
                b.Session.TenantId == tenant.TenantId &&
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
            var sessionsQuery = db.Sessions.AsNoTracking()
                .Include(s => s.ClassType)
                .Include(s => s.Schedule)
                .Include(s => s.Bookings)
                .Where(s => s.TenantId == tenant.TenantId && s.Date == today);

            if (tenant.LocationId.HasValue)
                sessionsQuery = sessionsQuery.Where(s => s.LocationId == tenant.LocationId.Value);

            var sessions = await sessionsQuery
                .OrderBy(s => s.StartTime)
                .ToListAsync();

            return Results.Ok(sessions.Select(s => new
            {
                s.Id,
                s.Date,
                s.StartTime,
                ClassType = s.ClassType?.Name ?? "",
                ClassTypeColor = s.ClassType?.Color ?? "#ccc",
                Capacity = s.Schedule?.Capacity ?? 1,
                s.Status,
                Bookings = s.Bookings.Count(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn),
                CheckedIn = s.Bookings.Count(b => b.Status == BookingStatus.CheckedIn),
                OccupancyPct = s.Schedule != null && s.Schedule.Capacity > 0
                    ? Math.Round((double)s.Bookings.Count(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn) / s.Schedule.Capacity * 100)
                    : 0.0
            }));
        });

        group.MapGet("/occupancy", async (AppDbContext db, TenantContext tenant) =>
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var nextWeek = today.AddDays(7);

            var sessionsQuery = db.Sessions.AsNoTracking()
                .Include(s => s.ClassType)
                .Include(s => s.Schedule)
                .Include(s => s.Bookings)
                .Where(s => s.TenantId == tenant.TenantId
                    && s.Date >= today && s.Date <= nextWeek
                    && s.Status == SessionStatus.Scheduled);

            if (tenant.LocationId.HasValue)
                sessionsQuery = sessionsQuery.Where(s => s.LocationId == tenant.LocationId.Value);

            var sessions = await sessionsQuery
                .OrderBy(s => s.Date).ThenBy(s => s.StartTime)
                .ToListAsync();

            return Results.Ok(sessions.Select(s => new
            {
                s.Id,
                s.Date,
                s.StartTime,
                ClassType = s.ClassType?.Name ?? "",
                ClassTypeColor = s.ClassType?.Color ?? "#ccc",
                Capacity = s.Schedule?.Capacity ?? 1,
                s.SlotsAvailable,
                Bookings = s.Bookings.Count(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn),
                OccupancyPct = s.Schedule != null && s.Schedule.Capacity > 0
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
                StudentId = p.StudentId,
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
                    b.Session.TenantId == tenant.TenantId &&
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
                    b.Session.TenantId == tenant.TenantId &&
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
                    b.Session.TenantId == tenant.TenantId &&
                    b.Session.Date >= inactiveSince &&
                    b.Status != BookingStatus.Cancelled)
                .Select(b => b.StudentId)
                .Distinct()
                .ToListAsync();

            return Results.Ok(activeStudents.Where(s => !recentIds.Contains(s.Id)));
        });

        // ── BeautySalon dashboard endpoints ─────────────────────────────────

        group.MapGet("/salon-stats", async (AppDbContext db, TenantContext tenant) =>
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var startOfMonth = new DateOnly(today.Year, today.Month, 1);
            var startOfMonthUtc = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var appointmentsToday = await db.Bookings.CountAsync(b =>
                b.Session.TenantId == tenant.TenantId &&
                b.Session.Date == today &&
                b.Status != BookingStatus.Cancelled);

            var appointmentsThisMonth = await db.Bookings.CountAsync(b =>
                b.Session.TenantId == tenant.TenantId &&
                b.Session.Date >= startOfMonth &&
                b.Status != BookingStatus.Cancelled);

            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);
            var revenueThisMonth = await db.Bookings
                .Where(b =>
                    b.Session.TenantId == tenant.TenantId &&
                    b.Session.Date >= startOfMonth &&
                    b.Session.Date <= endOfMonth &&
                    b.Status != BookingStatus.Cancelled)
                .Include(b => b.Session).ThenInclude(s => s.ClassType)
                .SumAsync(b => b.Session.ClassType != null ? (b.Session.ClassType.Price ?? 0) : 0);

            var totalClients = await db.Users.CountAsync(u =>
                u.TenantId == tenant.TenantId && u.Role == UserRole.Student && u.Status == StudentStatus.Active);

            var newClientsThisMonth = await db.Users.CountAsync(u =>
                u.TenantId == tenant.TenantId &&
                u.Role == UserRole.Student &&
                u.CreatedAt >= startOfMonthUtc);

            var totalBookingsMonth = await db.Bookings.CountAsync(b =>
                b.Session.TenantId == tenant.TenantId && b.Session.Date >= startOfMonth);

            var cancelledBookingsMonth = await db.Bookings.CountAsync(b =>
                b.Session.TenantId == tenant.TenantId &&
                b.Session.Date >= startOfMonth &&
                b.Status == BookingStatus.Cancelled);

            var cancellationRatePct = totalBookingsMonth > 0
                ? Math.Round((double)cancelledBookingsMonth / totalBookingsMonth * 100, 1)
                : 0.0;

            return Results.Ok(new
            {
                AppointmentsToday = appointmentsToday,
                AppointmentsThisMonth = appointmentsThisMonth,
                RevenueThisMonth = revenueThisMonth,
                TotalClients = totalClients,
                NewClientsThisMonth = newClientsThisMonth,
                CancellationRatePct = cancellationRatePct,
            });
        });

        group.MapGet("/salon-today", async (AppDbContext db, TenantContext tenant) =>
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var bookings = await db.Bookings.AsNoTracking()
                .Include(b => b.Session).ThenInclude(s => s.ClassType)
                .Include(b => b.Student)
                .Where(b =>
                    b.Session.TenantId == tenant.TenantId &&
                    b.Session.Date == today &&
                    b.Status != BookingStatus.Cancelled)
                .OrderBy(b => b.Session.StartTime)
                .Select(b => new
                {
                    b.Id,
                    b.Session.StartTime,
                    b.Session.DurationMinutes,
                    ServiceName = b.Session.ClassType != null ? b.Session.ClassType.Name : "",
                    ServiceColor = b.Session.ClassType != null ? b.Session.ClassType.Color : "#ccc",
                    ServicePrice = b.Session.ClassType != null ? b.Session.ClassType.Price : null,
                    ClientId = b.StudentId,
                    ClientName = b.Student.Name,
                    ClientPhone = b.Student.Phone,
                    b.Status,
                    b.CheckedInAt,
                })
                .ToListAsync();

            return Results.Ok(bookings);
        });

        group.MapGet("/salon-top-services", async (AppDbContext db, TenantContext tenant, int? days) =>
        {
            var since = DateOnly.FromDateTime(DateTime.Today.AddDays(-(days ?? 30)));
            var data = await db.Bookings.AsNoTracking()
                .Include(b => b.Session).ThenInclude(s => s.ClassType)
                .Where(b =>
                    b.Session.TenantId == tenant.TenantId &&
                    b.Session.Date >= since &&
                    b.Status != BookingStatus.Cancelled)
                .GroupBy(b => new
                {
                    b.Session.ClassTypeId,
                    Name = b.Session.ClassType != null ? b.Session.ClassType.Name : "",
                    Color = b.Session.ClassType != null ? b.Session.ClassType.Color : "#ccc"
                })
                .Select(g => new { g.Key.Name, g.Key.Color, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(6)
                .ToListAsync();

            return Results.Ok(data);
        });

        group.MapGet("/salon-top-clients", async (AppDbContext db, TenantContext tenant, int? days) =>
        {
            var since = DateOnly.FromDateTime(DateTime.Today.AddDays(-(days ?? 30)));
            var data = await db.Bookings.AsNoTracking()
                .Where(b =>
                    b.Session.TenantId == tenant.TenantId &&
                    b.Session.Date >= since &&
                    b.Status != BookingStatus.Cancelled)
                .GroupBy(b => new { b.StudentId, b.Student.Name, b.Student.Phone })
                .Select(g => new { ClientId = g.Key.StudentId, ClientName = g.Key.Name, ClientPhone = g.Key.Phone, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToListAsync();

            return Results.Ok(data);
        });

        group.MapGet("/salon-weekly", async (AppDbContext db, TenantContext tenant) =>
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var since = today.AddDays(-56);

            var dates = await db.Bookings.AsNoTracking()
                .Where(b =>
                    b.Session.TenantId == tenant.TenantId &&
                    b.Session.Date >= since &&
                    b.Status != BookingStatus.Cancelled)
                .Select(b => b.Session.Date)
                .ToListAsync();

            var result = Enumerable.Range(0, 8).Select(weekAgo =>
            {
                var weekEnd = today.AddDays(-(weekAgo * 7));
                var weekStart = weekEnd.AddDays(-6);
                return new { WeekStart = weekStart, WeekEnd = weekEnd, Count = dates.Count(d => d >= weekStart && d <= weekEnd) };
            })
            .OrderBy(x => x.WeekStart)
            .ToList();

            return Results.Ok(result);
        });

        group.MapGet("/salon-billing", async (AppDbContext db, TenantContext tenant, int? year, int? month) =>
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var y = year ?? today.Year;
            var m = month ?? today.Month;

            var startOfMonth = new DateOnly(y, m, 1);
            var endOfMonth   = startOfMonth.AddMonths(1).AddDays(-1);

            var bookings = await db.Bookings.AsNoTracking()
                .Include(b => b.Session).ThenInclude(s => s.ClassType)
                .Where(b =>
                    b.Session.TenantId == tenant.TenantId &&
                    b.Session.Date >= startOfMonth &&
                    b.Session.Date <= endOfMonth)
                .Select(b => new
                {
                    b.Status,
                    ServiceName  = b.Session.ClassType != null ? b.Session.ClassType.Name  : "Sem serviço",
                    ServiceColor = b.Session.ClassType != null ? b.Session.ClassType.Color : "#ccc",
                    Price        = b.Session.ClassType != null ? (b.Session.ClassType.Price ?? 0m) : 0m,
                    b.Session.Date,
                })
                .ToListAsync();

            var nonCancelled = bookings.Where(b => b.Status != BookingStatus.Cancelled).ToList();

            var totalRevenue       = nonCancelled.Sum(b => b.Price);
            var totalAppointments  = nonCancelled.Count;
            var averageTicket      = totalAppointments > 0 ? Math.Round(totalRevenue / totalAppointments, 2) : 0m;
            var cancelledCount     = bookings.Count(b => b.Status == BookingStatus.Cancelled);

            var byService = nonCancelled
                .GroupBy(b => new { b.ServiceName, b.ServiceColor })
                .Select(g => new
                {
                    Name    = g.Key.ServiceName,
                    Color   = g.Key.ServiceColor,
                    Count   = g.Count(),
                    Revenue = g.Sum(b => b.Price),
                })
                .OrderByDescending(x => x.Revenue)
                .ToList();

            // Last 12 months history
            var histStart = startOfMonth.AddMonths(-11);
            var histBookings = await db.Bookings.AsNoTracking()
                .Include(b => b.Session).ThenInclude(s => s.ClassType)
                .Where(b =>
                    b.Session.TenantId == tenant.TenantId &&
                    b.Session.Date >= histStart &&
                    b.Session.Date <= endOfMonth &&
                    b.Status != BookingStatus.Cancelled)
                .Select(b => new
                {
                    b.Session.Date,
                    Price = b.Session.ClassType != null ? (b.Session.ClassType.Price ?? 0m) : 0m,
                })
                .ToListAsync();

            var monthlyHistory = Enumerable.Range(0, 12).Select(i =>
            {
                var ms = startOfMonth.AddMonths(-11 + i);
                var me = ms.AddMonths(1).AddDays(-1);
                var mb = histBookings.Where(b => b.Date >= ms && b.Date <= me).ToList();
                return new { Year = ms.Year, Month = ms.Month, Revenue = mb.Sum(b => b.Price), Appointments = mb.Count };
            }).ToList();

            return Results.Ok(new
            {
                Year = y,
                Month = m,
                TotalRevenue      = totalRevenue,
                TotalAppointments = totalAppointments,
                CancelledAppointments = cancelledCount,
                AverageTicket     = averageTicket,
                ByService         = byService,
                MonthlyHistory    = monthlyHistory,
            });
        });

        group.MapGet("/heatmap/weekday-timeslot", async (AppDbContext db, TenantContext tenant, int? days) =>
        {
            var period = days ?? 90;
            var since = DateOnly.FromDateTime(DateTime.Today.AddDays(-period));

            var sessionsQuery = db.Sessions.AsNoTracking()
                .Include(s => s.Schedule)
                .Include(s => s.Bookings)
                .Where(s =>
                    s.TenantId == tenant.TenantId &&
                    s.Date >= since &&
                    s.Status == SessionStatus.Scheduled);

            if (tenant.LocationId.HasValue)
                sessionsQuery = sessionsQuery.Where(s => s.LocationId == tenant.LocationId.Value);

            var sessions = await sessionsQuery.ToListAsync();

            var heatmapData = sessions
                .GroupBy(s => new { Weekday = s.Date.DayOfWeek, Hour = s.StartTime.Hour })
                .Select(g => new
                {
                    Weekday = (int)g.Key.Weekday,
                    Hour = g.Key.Hour,
                    SessionCount = g.Count(),
                    TotalBookings = g.Sum(s => s.Bookings.Count(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn)),
                    TotalCapacity = g.Sum(s => s.Schedule?.Capacity ?? 1),
                    AvgOccupancyPct = g.Average(s =>
                    {
                        var capacity = s.Schedule?.Capacity ?? 1;
                        var bookings = s.Bookings.Count(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn);
                        return capacity > 0 ? (double)bookings / capacity * 100 : 0;
                    })
                })
                .ToList();

            return Results.Ok(new
            {
                Data = heatmapData,
                Period = period,
                Since = since
            });
        });
    }
}
