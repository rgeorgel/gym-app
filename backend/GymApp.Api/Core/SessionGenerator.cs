using GymApp.Domain.Entities;
using GymApp.Domain.Enums;

namespace GymApp.Api.Core;

public static class SessionGenerator
{
    public static int CalculateSessionsToGenerate(IEnumerable<Schedule> schedules, DateOnly from, DateOnly fromDate, HashSet<(Guid ScheduleId, DateOnly Date)> existingKeys)
    {
        var count = 0;
        for (var date = from; date <= fromDate.AddDays(13); date = date.AddDays(1))
        {
            var dayOfWeek = (int)date.DayOfWeek;
            count += schedules.Count(s => s.Weekday == dayOfWeek && !existingKeys.Contains((s.Id, date)));
        }
        return count;
    }

    public static IEnumerable<Session> GenerateSessionsFromSchedules(
        IEnumerable<Schedule> schedules,
        DateOnly start,
        DateOnly end,
        HashSet<(Guid ScheduleId, DateOnly Date)> existingKeys)
    {
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
                        TenantId = schedule.TenantId,
                        ClassTypeId = schedule.ClassTypeId,
                        LocationId = schedule.LocationId,
                        StartTime = schedule.StartTime,
                        DurationMinutes = schedule.DurationMinutes,
                        Date = date,
                        SlotsAvailable = schedule.Capacity
                    });
                }
            }
        }
        return toCreate;
    }

    public static void ReactivateSession(Session session)
    {
        session.Status = SessionStatus.Scheduled;
        session.CancellationReason = null;
    }

    public static void RestoreBookingCredits(Session session)
    {
        foreach (var booking in session.Bookings.Where(b =>
            b.Status == BookingStatus.Cancelled && b.CancellationReason == "Aula cancelada"))
        {
            booking.Status = BookingStatus.Confirmed;
            booking.CancellationReason = null;
            booking.CancelledAt = null;
            if (booking.PackageItem is not null) booking.PackageItem.UsedCredits += 1;
        }
    }

    public static void CancelSession(Session session, string reason)
    {
        session.Status = SessionStatus.Cancelled;
        session.CancellationReason = reason;
    }

    public static void CancelSessionBookings(Session session)
    {
        foreach (var booking in session.Bookings.Where(b => b.Status == BookingStatus.Confirmed))
        {
            booking.Status = BookingStatus.Cancelled;
            booking.CancellationReason = "Aula cancelada";
            booking.CancelledAt = DateTime.UtcNow;
            if (booking.PackageItem is not null)
                booking.PackageItem.UsedCredits = Math.Max(0, booking.PackageItem.UsedCredits - 1);
        }
    }

    public static int CountConfirmedBookings(Session session)
    {
        return session.Bookings.Count(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn);
    }
}