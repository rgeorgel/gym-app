namespace GymApp.Api.Core;

public static class DashboardKpiCalculator
{
    public static DateOnly GetToday() => DateOnly.FromDateTime(DateTime.Today);

    public static DateOnly GetStartOfMonth(DateOnly today) =>
        new(today.Year, today.Month, 1);

    public static DateTime GetStartOfMonthUtc(DateOnly today) =>
        new(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

    public static DateOnly GetDaysAgo(int days) =>
        GetToday().AddDays(-days);

    public static DateOnly GetNextWeek(DateOnly today) =>
        today.AddDays(7);

    public static double CalculateOccupancyPct(int activeBookings, int capacity)
    {
        return capacity > 0 ? Math.Round((double)activeBookings / capacity * 100, 1) : 0.0;
    }

    public static double CalculateCancellationRatePct(int cancelled, int total)
    {
        return total > 0 ? Math.Round((double)cancelled / total * 100, 1) : 0.0;
    }

    public static decimal CalculateAverageTicket(decimal totalRevenue, int totalAppointments)
    {
        return totalAppointments > 0 ? Math.Round(totalRevenue / totalAppointments, 2) : 0m;
    }

    public static List<WeeklyCheckinResult> CalculateWeeklyCheckins(DateOnly today, int weeks, List<DateOnly> bookingDates)
    {
        return Enumerable.Range(0, weeks).Select(weekAgo =>
        {
            var weekEnd = today.AddDays(-(weekAgo * 7));
            var weekStart = weekEnd.AddDays(-6);
            var count = bookingDates.Count(d => d >= weekStart && d <= weekEnd);
            return new WeeklyCheckinResult(weekStart, weekEnd, count);
        })
        .OrderBy(x => x.WeekStart)
        .ToList();
    }

    public static List<MonthlyHistoryResult> CalculateMonthlyHistory(DateOnly startOfMonth, List<BookingHistoryItem> bookings)
    {
        return Enumerable.Range(0, 12).Select(i =>
        {
            var ms = startOfMonth.AddMonths(-11 + i);
            var me = ms.AddMonths(1).AddDays(-1);
            var mb = bookings.Where(b => b.Date >= ms && b.Date <= me).ToList();
            return new MonthlyHistoryResult(ms.Year, ms.Month, mb.Sum(b => b.Price), mb.Count);
        }).ToList();
    }

    public static List<HeatmapCell> CalculateHeatmap(List<HeatmapSession> sessions)
    {
        return sessions
            .GroupBy(s => new { s.Weekday, s.Hour })
            .Select(g => new HeatmapCell(
                (int)g.Key.Weekday,
                g.Key.Hour,
                g.Count(),
                g.Sum(s => s.TotalBookings),
                g.Sum(s => s.TotalCapacity),
                g.Average(s => s.TotalCapacity > 0 ? (double)s.TotalBookings / s.TotalCapacity * 100 : 0)
            ))
            .ToList();
    }

    public record WeeklyCheckinResult(DateOnly WeekStart, DateOnly WeekEnd, int Count);
    public record MonthlyHistoryResult(int Year, int Month, decimal Revenue, int Appointments);
    public record HeatmapCell(int Weekday, int Hour, int SessionCount, int TotalBookings, int TotalCapacity, double AvgOccupancyPct);
    public record BookingHistoryItem(DateOnly Date, decimal Price);
    public record HeatmapSession(DayOfWeek Weekday, int Hour, int TotalBookings, int TotalCapacity);
}