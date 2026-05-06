namespace GymApp.Api.Core;

public static class SalonSlotGenerator
{
    public static IReadOnlyList<TimeOnly> GenerateAvailableSlots(
        IReadOnlyList<AvailabilityBlock> blocks,
        IReadOnlyList<OccupiedSession> existingSessions,
        IReadOnlyList<TimeBlockRange> timeBlocks,
        int durationMinutes)
    {
        if (blocks.Count == 0) return Array.Empty<TimeOnly>();

        var allSlots = new SortedSet<TimeOnly>();
        foreach (var block in blocks)
        {
            var slot = block.StartTime;
            while (slot.Add(TimeSpan.FromMinutes(durationMinutes)) <= block.EndTime)
            {
                allSlots.Add(slot);
                slot = slot.Add(TimeSpan.FromMinutes(durationMinutes));
            }
        }

        var available = allSlots.Where(slot => !IsSlotBlocked(slot, existingSessions, timeBlocks, durationMinutes)).ToList();
        return available;
    }

    public static bool IsSlotBlocked(
        TimeOnly slot,
        IReadOnlyList<OccupiedSession> existingSessions,
        IReadOnlyList<TimeBlockRange> timeBlocks,
        int durationMinutes)
    {
        var newStart = slot.Hour * 60 + slot.Minute;
        var newEnd = newStart + durationMinutes;

        var blockedBySession = existingSessions.Any(s =>
        {
            var sStart = s.StartTime.Hour * 60 + s.StartTime.Minute;
            var sEnd = sStart + s.DurationMinutes;
            return newStart < sEnd && newEnd > sStart;
        });

        if (blockedBySession) return true;

        var blockedByTimeBlock = timeBlocks.Any(tb =>
        {
            var tbStart = tb.StartTime.Hour * 60 + tb.StartTime.Minute;
            var tbEnd = tb.EndTime.Hour * 60 + tb.EndTime.Minute;
            return newStart < tbEnd && newEnd > tbStart;
        });

        return blockedByTimeBlock;
    }

    public static bool ValidateTimeBlock(TimeOnly start, TimeOnly end) => start < end;

    public static bool ValidateVacationBlock(DateOnly start, DateOnly end) => start <= end;

    public static bool IsDateOnVacation(DateOnly date, IReadOnlyList<VacationRange> vacationBlocks) =>
        vacationBlocks.Any(v => v.StartDate <= date && v.EndDate >= date);

    public static int GetWeekday(DateOnly date) => (int)date.DayOfWeek;

    public static TimeOnly AddMinutes(TimeOnly time, int minutes) =>
        time.Add(TimeSpan.FromMinutes(minutes));

    public static int ToMinutes(TimeOnly time) => time.Hour * 60 + time.Minute;

    public static bool TimesOverlap(int start1, int end1, int start2, int end2) =>
        start1 < end2 && end1 > start2;

    public record AvailabilityBlock(TimeOnly StartTime, TimeOnly EndTime, Guid? InstructorId);
    public record OccupiedSession(TimeOnly StartTime, int DurationMinutes, Guid? InstructorId);
    public record TimeBlockRange(TimeOnly StartTime, TimeOnly EndTime);
    public record VacationRange(DateOnly StartDate, DateOnly EndDate);
}