using FluentAssertions;
using GymApp.Api.Core;
using GymApp.Domain.Enums;
using Xunit;

namespace GymApp.Tests.Core;

public class TenantStatusCheckerTests
{
    [Theory]
    [InlineData("Acme Gym", true)]
    [InlineData("AB", true)] // min length
    [InlineData("A", false)] // too short
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidTenantName_ReturnsExpected(string? name, bool expected)
    {
        TenantStatusChecker.IsValidTenantName(name).Should().Be(expected);
    }

    [Theory]
    [InlineData("11999999999", true)]
    [InlineData("+55 11 99999-9999", true)]
    [InlineData("1234567890", true)]
    [InlineData("12345", false)] // too short
    [InlineData("", false)]
    public void IsValidPhoneNumber_ReturnsExpected(string? phone, bool expected)
    {
        TenantStatusChecker.IsValidPhoneNumber(phone).Should().Be(expected);
    }

    [Fact]
    public void IsValidServiceId_WithEmptyGuid_ReturnsFalse()
    {
        TenantStatusChecker.IsValidServiceId(Guid.Empty).Should().BeFalse();
    }

    [Fact]
    public void IsValidServiceId_WithValidGuid_ReturnsTrue()
    {
        TenantStatusChecker.IsValidServiceId(Guid.NewGuid()).Should().BeTrue();
    }

    [Fact]
    public void ParseBotDatetime_WithValidString_ReturnsDateTime()
    {
        var result = TenantStatusChecker.ParseBotDatetime("2024-05-15T10:30:00");
        result.Should().Be(new DateTime(2024, 5, 15, 10, 30, 0));
    }

    [Fact]
    public void ParseBotDatetime_WithInvalidString_ThrowsException()
    {
        var action = () => TenantStatusChecker.ParseBotDatetime("not-a-date");
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SplitDateTime_SplitsCorrectly()
    {
        var dt = new DateTime(2024, 5, 15, 14, 30, 0);
        var (date, time) = TenantStatusChecker.SplitDateTime(dt);
        date.Should().Be(new DateOnly(2024, 5, 15));
        time.Should().Be(new TimeOnly(14, 30));
    }

    [Theory]
    [InlineData(BookingStatus.Confirmed, true)]
    [InlineData(BookingStatus.CheckedIn, true)]
    [InlineData(BookingStatus.Cancelled, false)]
    public void CanReschedule_ReturnsExpected(BookingStatus status, bool expected)
    {
        TenantStatusChecker.CanReschedule(status).Should().Be(expected);
    }

    [Theory]
    [InlineData(2024, 5, 15, 3)] // Wednesday
    [InlineData(2024, 1, 1, 1)]  // Monday
    [InlineData(2024, 1, 7, 0)]  // Sunday
    public void GetWeekdayIndex_ReturnsCorrectDay(int year, int month, int day, int expected)
    {
        var dt = new DateTime(year, month, day);
        TenantStatusChecker.GetWeekdayIndex(dt).Should().Be(expected);
    }

    [Fact]
    public void IsVacationDate_WhenDateInRange_ReturnsTrue()
    {
        var vacations = new List<TenantStatusChecker.VacationRange>
        {
            new(new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 31))
        };
        TenantStatusChecker.IsVacationDate(new DateOnly(2024, 1, 15), vacations).Should().BeTrue();
    }

    [Fact]
    public void IsVacationDate_WhenDateOutsideRange_ReturnsFalse()
    {
        var vacations = new List<TenantStatusChecker.VacationRange>
        {
            new(new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 31))
        };
        TenantStatusChecker.IsVacationDate(new DateOnly(2024, 2, 15), vacations).Should().BeFalse();
    }

    [Fact]
    public void IsSameDay_WithSameDay_ReturnsTrue()
    {
        var dt1 = new DateTime(2024, 5, 15, 10, 0, 0);
        var dt2 = new DateTime(2024, 5, 15, 14, 30, 0);
        TenantStatusChecker.IsSameDay(dt1, dt2).Should().BeTrue();
    }

    [Fact]
    public void IsSameDay_WithDifferentDays_ReturnsFalse()
    {
        var dt1 = new DateTime(2024, 5, 15, 10, 0, 0);
        var dt2 = new DateTime(2024, 5, 16, 10, 0, 0);
        TenantStatusChecker.IsSameDay(dt1, dt2).Should().BeFalse();
    }

    [Fact]
    public void DurationBetween_WithValidRange_ReturnsDuration()
    {
        var start = new TimeOnly(10, 0);
        var end = new TimeOnly(11, 30);
        TenantStatusChecker.DurationBetween(start, end).Should().Be(new TimeSpan(1, 30, 0));
    }

    [Fact]
    public void DurationBetween_WithEndBeforeStart_ReturnsZero()
    {
        var start = new TimeOnly(11, 0);
        var end = new TimeOnly(10, 0);
        TenantStatusChecker.DurationBetween(start, end).Should().Be(TimeSpan.Zero);
    }
}