using GymApp.Domain.Enums;

namespace GymApp.Api.Core;

public static class TenantStatusChecker
{
    public static bool IsValidTenantName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name.Length >= 2 && name.Length <= 100;

    public static bool IsValidPhoneNumber(string? phone) =>
        !string.IsNullOrWhiteSpace(phone) && phone.Length >= 10;

    public static bool IsValidServiceId(Guid? serviceId) => serviceId != Guid.Empty;

    public static bool IsValidProfessionalId(Guid? professionalId) => professionalId != Guid.Empty;

    public static bool IsValidAppointmentId(Guid? appointmentId) => appointmentId != Guid.Empty;

    public static bool IsValidTenantId(Guid? tenantId) => tenantId != Guid.Empty;

    public static DateTime ParseBotDatetime(string datetime) =>
        DateTime.TryParse(datetime, out var result) ? result : throw new ArgumentException("Invalid datetime format");

    public static (DateOnly Date, TimeOnly Time) SplitDateTime(DateTime dateTime) =>
        (DateOnly.FromDateTime(dateTime), TimeOnly.FromDateTime(dateTime));

    public static bool CanReschedule(BookingStatus status) =>
        status != BookingStatus.Cancelled;

    public static int GetWeekdayIndex(DateTime dateTime) =>
        (int)dateTime.DayOfWeek;

    public static bool IsVacationDate(DateOnly date, IReadOnlyList<VacationRange> vacationRanges) =>
        vacationRanges.Any(v => v.StartDate <= date && v.EndDate >= date);

    public static bool IsSameDay(DateTime dt1, DateTime dt2) =>
        DateOnly.FromDateTime(dt1) == DateOnly.FromDateTime(dt2);

    public static TimeSpan DurationBetween(TimeOnly start, TimeOnly end) =>
        end > start ? end - start : TimeSpan.Zero;

    public record VacationRange(DateOnly StartDate, DateOnly EndDate);
}