using FluentAssertions;
using GymApp.Api.Core;
using Xunit;

namespace GymApp.Tests.Core;

public class DashboardKpiCalculatorTests
{
    [Fact]
    public void GetToday_ReturnsTodayDateOnly()
    {
        var result = DashboardKpiCalculator.GetToday();
        result.Should().Be(DateOnly.FromDateTime(DateTime.Today));
    }

    [Fact]
    public void GetStartOfMonth_ReturnsFirstDayOfMonth()
    {
        var today = new DateOnly(2024, 3, 15);
        var result = DashboardKpiCalculator.GetStartOfMonth(today);
        result.Should().Be(new DateOnly(2024, 3, 1));
    }

    [Fact]
    public void GetStartOfMonthUtc_ReturnsUtcDateTime()
    {
        var today = new DateOnly(2024, 6, 20);
        var result = DashboardKpiCalculator.GetStartOfMonthUtc(today);
        result.Year.Should().Be(2024);
        result.Month.Should().Be(6);
        result.Day.Should().Be(1);
        result.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void GetDaysAgo_ReturnsCorrectDate()
    {
        var result = DashboardKpiCalculator.GetDaysAgo(30);
        result.Should().Be(DateOnly.FromDateTime(DateTime.Today.AddDays(-30)));
    }

    [Fact]
    public void GetNextWeek_ReturnsTodayPlus7()
    {
        var today = new DateOnly(2024, 5, 1);
        var result = DashboardKpiCalculator.GetNextWeek(today);
        result.Should().Be(new DateOnly(2024, 5, 8));
    }

    [Theory]
    [InlineData(10, 20, 50.0)]
    [InlineData(5, 20, 25.0)]
    [InlineData(20, 20, 100.0)]
    [InlineData(0, 20, 0.0)]
    public void CalculateOccupancyPct_ReturnsCorrectPercentage(int activeBookings, int capacity, double expected)
    {
        var result = DashboardKpiCalculator.CalculateOccupancyPct(activeBookings, capacity);
        result.Should().Be(expected);
    }

    [Fact]
    public void CalculateOccupancyPct_WithZeroCapacity_ReturnsZero()
    {
        var result = DashboardKpiCalculator.CalculateOccupancyPct(5, 0);
        result.Should().Be(0.0);
    }

    [Theory]
    [InlineData(3, 10, 30.0)]
    [InlineData(0, 10, 0.0)]
    [InlineData(5, 5, 100.0)]
    public void CalculateCancellationRatePct_ReturnsCorrectPercentage(int cancelled, int total, double expected)
    {
        var result = DashboardKpiCalculator.CalculateCancellationRatePct(cancelled, total);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(1000, 10, 100.00)]
    [InlineData(500, 3, 166.67)]
    [InlineData(0, 5, 0.0)]
    public void CalculateAverageTicket_ReturnsCorrectValue(decimal revenue, int appointments, decimal expected)
    {
        var result = DashboardKpiCalculator.CalculateAverageTicket(revenue, appointments);
        result.Should().Be(expected);
    }

    [Fact]
    public void CalculateWeeklyCheckins_WithEmptyDates_ReturnsAllZeros()
    {
        var today = new DateOnly(2024, 5, 5);
        var result = DashboardKpiCalculator.CalculateWeeklyCheckins(today, 8, new List<DateOnly>());
        result.Should().HaveCount(8);
        result.Should().OnlyContain(w => w.Count == 0);
    }

    [Fact]
    public void CalculateWeeklyCheckins_WithBookingDates_CountsCorrectly()
    {
        var today = new DateOnly(2024, 5, 5);
        var dates = new List<DateOnly>
        {
            new(2024, 4, 29),
            new(2024, 4, 30),
            new(2024, 5, 1),
        };

        var result = DashboardKpiCalculator.CalculateWeeklyCheckins(today, 4, dates);

        result.Should().HaveCount(4);
    }

    [Fact]
    public void CalculateMonthlyHistory_WithBookings_CalculatesCorrectly()
    {
        var startOfMonth = new DateOnly(2024, 3, 1);
        var bookings = new List<DashboardKpiCalculator.BookingHistoryItem>
        {
            new(new DateOnly(2024, 3, 15), 100m),
            new(new DateOnly(2024, 3, 20), 150m),
            new(new DateOnly(2024, 4, 10), 200m),
        };

        var result = DashboardKpiCalculator.CalculateMonthlyHistory(startOfMonth, bookings);

        result.Should().HaveCount(12);
    }

    [Fact]
    public void CalculateHeatmap_WithSessions_AggregatesCorrectly()
    {
        var sessions = new List<DashboardKpiCalculator.HeatmapSession>
        {
            new(DayOfWeek.Monday, 9, 5, 20),
            new(DayOfWeek.Monday, 9, 10, 20),
            new(DayOfWeek.Monday, 10, 3, 15),
        };

        var result = DashboardKpiCalculator.CalculateHeatmap(sessions);

        result.Should().HaveCount(2);
        var monday9 = result.First(r => r.Hour == 9);
        monday9.SessionCount.Should().Be(2);
        monday9.TotalBookings.Should().Be(15);
        monday9.TotalCapacity.Should().Be(40);
    }
}