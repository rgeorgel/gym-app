namespace GymApp.Api.Core;

public static class BookingCancellationLogic
{
    public static DateTime GetCancellationDeadline(DateTime sessionDateTime, int cancellationHoursLimit)
    {
        return sessionDateTime.AddHours(-cancellationHoursLimit);
    }

    public static bool IsWithinCancellationWindow(DateTime sessionDateTime, int cancellationHoursLimit)
    {
        var deadline = GetCancellationDeadline(sessionDateTime, cancellationHoursLimit);
        return DateTime.UtcNow <= deadline;
    }

    public static bool ShouldRefundCredit(DateTime sessionDateTime, int cancellationHoursLimit, bool hasPackageItem)
    {
        return hasPackageItem && IsWithinCancellationWindow(sessionDateTime, cancellationHoursLimit);
    }

    public static int CalculateRemainingHours(DateTime sessionDateTime, int cancellationHoursLimit)
    {
        var deadline = GetCancellationDeadline(sessionDateTime, cancellationHoursLimit);
        var remaining = (deadline - DateTime.UtcNow).TotalHours;
        return remaining > 0 ? (int)remaining : 0;
    }
}
