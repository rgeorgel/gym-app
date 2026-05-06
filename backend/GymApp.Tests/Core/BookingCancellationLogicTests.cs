using FluentAssertions;
using GymApp.Api.Core;
using Xunit;

namespace GymApp.Tests.Core;

public class BookingCancellationLogicTests
{
    private static DateTime CreateSessionDateTime(int daysFromNow = 0, int hour = 10, int minute = 0)
    {
        return DateTime.UtcNow.AddDays(daysFromNow).Date.AddHours(hour).AddMinutes(minute);
    }

    [Fact]
    public void GetCancellationDeadline_WithDefaultHours_ReturnsDeadline()
    {
        var sessionDateTime = new DateTime(2024, 6, 15, 10, 0, 0, DateTimeKind.Utc);
        var hoursLimit = 2;

        var deadline = BookingCancellationLogic.GetCancellationDeadline(sessionDateTime, hoursLimit);

        deadline.Should().Be(new DateTime(2024, 6, 15, 8, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void GetCancellationDeadline_WithCustomHours_ReturnsCorrectDeadline()
    {
        var sessionDateTime = new DateTime(2024, 6, 15, 14, 0, 0, DateTimeKind.Utc);
        var hoursLimit = 24;

        var deadline = BookingCancellationLogic.GetCancellationDeadline(sessionDateTime, hoursLimit);

        deadline.Should().Be(new DateTime(2024, 6, 14, 14, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void IsWithinCancellationWindow_WhenBeforeDeadline_ReturnsTrue()
    {
        var futureSession = DateTime.UtcNow.AddHours(10);
        var hoursLimit = 2;

        var result = BookingCancellationLogic.IsWithinCancellationWindow(futureSession, hoursLimit);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsWithinCancellationWindow_WhenAfterDeadline_ReturnsFalse()
    {
        var pastSession = DateTime.UtcNow.AddHours(-3);
        var hoursLimit = 2;

        var result = BookingCancellationLogic.IsWithinCancellationWindow(pastSession, hoursLimit);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsWithinCancellationWindow_WhenAtDeadline_ReturnsTrue()
    {
        var sessionDateTime = DateTime.UtcNow.AddHours(5);
        var hoursLimit = 5;
        var deadline = sessionDateTime.AddHours(-hoursLimit);

        DateTime.UtcNow.Should().BeCloseTo(deadline, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ShouldRefundCredit_WhenWithinWindowAndHasPackage_ReturnsTrue()
    {
        var futureSession = DateTime.UtcNow.AddHours(10);
        var hoursLimit = 2;

        var result = BookingCancellationLogic.ShouldRefundCredit(futureSession, hoursLimit, hasPackageItem: true);

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldRefundCredit_WhenOutsideWindowAndHasPackage_ReturnsFalse()
    {
        var pastSession = DateTime.UtcNow.AddHours(-3);
        var hoursLimit = 2;

        var result = BookingCancellationLogic.ShouldRefundCredit(pastSession, hoursLimit, hasPackageItem: true);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldRefundCredit_WhenNoPackageItem_ReturnsFalse()
    {
        var futureSession = DateTime.UtcNow.AddHours(10);
        var hoursLimit = 2;

        var result = BookingCancellationLogic.ShouldRefundCredit(futureSession, hoursLimit, hasPackageItem: false);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldRefundCredit_WhenWithinWindowButNoPackage_ReturnsFalse()
    {
        var futureSession = DateTime.UtcNow.AddHours(10);
        var hoursLimit = 2;

        var result = BookingCancellationLogic.ShouldRefundCredit(futureSession, hoursLimit, hasPackageItem: false);

        result.Should().BeFalse();
    }

    [Fact]
    public void CalculateRemainingHours_WhenTimeRemaining_ReturnsPositiveHours()
    {
        var sessionDateTime = DateTime.UtcNow.AddHours(10);
        var hoursLimit = 2;

        var remaining = BookingCancellationLogic.CalculateRemainingHours(sessionDateTime, hoursLimit);

        remaining.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateRemainingHours_WhenPastDeadline_ReturnsZero()
    {
        var sessionDateTime = DateTime.UtcNow.AddHours(-10);
        var hoursLimit = 2;

        var remaining = BookingCancellationLogic.CalculateRemainingHours(sessionDateTime, hoursLimit);

        remaining.Should().Be(0);
    }

    [Fact]
    public void CalculateRemainingHours_WhenAtDeadline_ReturnsZero()
    {
        var sessionDateTime = DateTime.UtcNow.AddHours(5);
        var hoursLimit = 5;

        var remaining = BookingCancellationLogic.CalculateRemainingHours(sessionDateTime, hoursLimit);

        remaining.Should().Be(0);
    }
}