using GymApp.Api.DTOs;
using GymApp.Domain.Entities;

namespace GymApp.Api.Core;

/// <summary>
/// Pure functions for WhatsApp bot slot generation logic.
/// </summary>
public static class WhatsAppSlotGenerator
{
    /// <summary>
    /// Represents an occupied session that blocks slot generation.
    /// </summary>
    public record OccupiedSession(TimeOnly StartTime, int DurationMinutes, Guid InstructorId);

    /// <summary>
    /// Represents a time block that blocks slot generation for all professionals.
    /// </summary>
    public record TimeBlockItem(TimeOnly StartTime, TimeOnly EndTime);

    /// <summary>
    /// Represents a professional's availability block.
    /// </summary>
    public record AvailabilityBlock(
        Guid InstructorId,
        string InstructorName,
        TimeOnly StartTime,
        TimeOnly EndTime);

    /// <summary>
    /// Generates available time slots for a given date and service duration.
    /// Uses HashSet for deduplication of slots across availability blocks.
    /// </summary>
    /// <param name="blocks">Professional availability blocks for the day.</param>
    /// <param name="occupiedSessions">Existing sessions that occupy time slots.</param>
    /// <param name="timeBlocks">General time blocks that block all slots.</param>
    /// <param name="durationMinutes">Service duration in minutes.</param>
    /// <returns>List of available BotSlotItem sorted by time.</returns>
    public static IReadOnlyList<BotSlotItem> GenerateSlots(
        IReadOnlyList<AvailabilityBlock> blocks,
        IReadOnlyList<OccupiedSession> occupiedSessions,
        IReadOnlyList<TimeBlockItem> timeBlocks,
        int durationMinutes)
    {
        var slots = new List<BotSlotItem>();
        var seen = new HashSet<(TimeOnly, Guid)>();

        foreach (var block in blocks)
        {
            var profId = block.InstructorId;
            var profName = block.InstructorName;

            // Existing occupied sessions for this professional
            var occupiedForProf = occupiedSessions
                .Where(s => s.InstructorId == profId)
                .ToList();

            var slot = block.StartTime;
            while (slot.Add(TimeSpan.FromMinutes(durationMinutes)) <= block.EndTime)
            {
                var slotKey = (slot, profId);
                if (!seen.Contains(slotKey))
                {
                    var newStart = slot.Hour * 60 + slot.Minute;
                    var newEnd = newStart + durationMinutes;

                    var blockedBySession = occupiedForProf.Any(s =>
                    {
                        var sStart = s.StartTime.Hour * 60 + s.StartTime.Minute;
                        var sEnd = sStart + s.DurationMinutes;
                        return newStart < sEnd && newEnd > sStart;
                    });

                    var blockedByTimeBlock = timeBlocks.Any(tb =>
                    {
                        var tbStart = tb.StartTime.Hour * 60 + tb.StartTime.Minute;
                        var tbEnd = tb.EndTime.Hour * 60 + tb.EndTime.Minute;
                        return newStart < tbEnd && newEnd > tbStart;
                    });

                    if (!blockedBySession && !blockedByTimeBlock)
                    {
                        slots.Add(new BotSlotItem(slot.ToString("HH:mm"), profId, profName));
                        seen.Add(slotKey);
                    }
                }
                slot = slot.Add(TimeSpan.FromMinutes(durationMinutes));
            }
        }

        slots.Sort((a, b) => string.Compare(a.Time, b.Time, StringComparison.Ordinal));
        return slots;
    }

    /// <summary>
    /// Checks if two time ranges overlap.
    /// </summary>
    /// <param name="start1">Start time of range 1.</param>
    /// <param name="end1">End time of range 1.</param>
    /// <param name="start2">Start time of range 2.</param>
    /// <param name="end2">End time of range 2.</param>
    /// <returns>True if ranges overlap, false otherwise.</returns>
    public static bool FindOverlapping(TimeOnly start1, TimeOnly end1, TimeOnly start2, TimeOnly end2)
    {
        var s1 = start1.Hour * 60 + start1.Minute;
        var e1 = end1.Hour * 60 + end1.Minute;
        var s2 = start2.Hour * 60 + start2.Minute;
        var e2 = end2.Hour * 60 + end2.Minute;
        return s1 < e2 && e1 > s2;
    }

    /// <summary>
    /// Automatically assigns a professional based on:
    /// - If only one professional available, return that one
    /// - If multiple, return the one with fewest occupied sessions on the given date
    /// </summary>
    /// <param name="availableProfessionals">List of available professional IDs.</param>
    /// <param name="existingSessions">Existing sessions to analyze load.</param>
    /// <param name="serviceProfessionals">Optional: professionals who offer the specific service.</param>
    /// <returns>The selected professional ID, or null if none available.</returns>
    public static Guid? AutoAssignProfessional(
        IReadOnlyList<Guid> availableProfessionals,
        IReadOnlyList<OccupiedSession> existingSessions,
        IReadOnlyList<Guid>? serviceProfessionals = null)
    {
        if (availableProfessionals.Count == 0)
            return null;

        var candidates = serviceProfessionals != null && serviceProfessionals.Count > 0
            ? availableProfessionals.Intersect(serviceProfessionals).ToList()
            : availableProfessionals;

        if (candidates.Count == 0)
            return null;

        if (candidates.Count == 1)
            return candidates[0];

        // Return the professional with the fewest sessions on this date
        return candidates
            .Select(p => new
            {
                ProfessionalId = p,
                SessionCount = existingSessions.Count(s => s.InstructorId == p)
            })
            .OrderBy(x => x.SessionCount)
            .First()
            .ProfessionalId;
    }
}
