using FluentAssertions;
using GymApp.Api.Core;
using Xunit;

namespace GymApp.Tests.Core;

public class SalonSlotGeneratorTests
{
    [Fact]
    public void GenerateAvailableSlots_WithNoBlocks_ReturnsEmpty()
    {
        var result = SalonSlotGenerator.GenerateAvailableSlots(
            new List<SalonSlotGenerator.AvailabilityBlock>(),
            new List<SalonSlotGenerator.OccupiedSession>(),
            new List<SalonSlotGenerator.TimeBlockRange>(),
            60);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GenerateAvailableSlots_WithSingleBlock_CreatesSlots()
    {
        var blocks = new List<SalonSlotGenerator.AvailabilityBlock>
        {
            new(new TimeOnly(9, 0), new TimeOnly(12, 0), null)
        };

        var result = SalonSlotGenerator.GenerateAvailableSlots(
            blocks,
            new List<SalonSlotGenerator.OccupiedSession>(),
            new List<SalonSlotGenerator.TimeBlockRange>(),
            60);

        result.Should().HaveCount(3);
        result.Should().Contain(new TimeOnly(9, 0));
        result.Should().Contain(new TimeOnly(10, 0));
        result.Should().Contain(new TimeOnly(11, 0));
    }

    [Fact]
    public void GenerateAvailableSlots_WithOccupiedSession_SkipsBlockedSlot()
    {
        var blocks = new List<SalonSlotGenerator.AvailabilityBlock>
        {
            new(new TimeOnly(9, 0), new TimeOnly(12, 0), null)
        };
        var occupied = new List<SalonSlotGenerator.OccupiedSession>
        {
            new(new TimeOnly(10, 0), 60, null)
        };

        var result = SalonSlotGenerator.GenerateAvailableSlots(
            blocks,
            occupied,
            new List<SalonSlotGenerator.TimeBlockRange>(),
            60);

        result.Should().HaveCount(2);
        result.Should().NotContain(new TimeOnly(10, 0));
    }

    [Fact]
    public void GenerateAvailableSlots_WithTimeBlock_SkipsBlockedSlot()
    {
        var blocks = new List<SalonSlotGenerator.AvailabilityBlock>
        {
            new(new TimeOnly(9, 0), new TimeOnly(12, 0), null)
        };
        var timeBlocks = new List<SalonSlotGenerator.TimeBlockRange>
        {
            new(new TimeOnly(10, 0), new TimeOnly(11, 0))
        };

        var result = SalonSlotGenerator.GenerateAvailableSlots(
            blocks,
            new List<SalonSlotGenerator.OccupiedSession>(),
            timeBlocks,
            60);

        result.Should().HaveCount(2);
        result.Should().NotContain(new TimeOnly(10, 0));
    }

    [Fact]
    public void IsSlotBlocked_WithOverlappingSession_ReturnsTrue()
    {
        var occupied = new List<SalonSlotGenerator.OccupiedSession>
        {
            new(new TimeOnly(10, 0), 60, null)
        };

        var result = SalonSlotGenerator.IsSlotBlocked(
            new TimeOnly(10, 30),
            occupied,
            new List<SalonSlotGenerator.TimeBlockRange>(),
            60);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsSlotBlocked_WithNonOverlappingSession_ReturnsFalse()
    {
        var occupied = new List<SalonSlotGenerator.OccupiedSession>
        {
            new(new TimeOnly(10, 0), 60, null)
        };

        var result = SalonSlotGenerator.IsSlotBlocked(
            new TimeOnly(11, 0),
            occupied,
            new List<SalonSlotGenerator.TimeBlockRange>(),
            60);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsSlotBlocked_WithAdjacentSession_ReturnsFalse()
    {
        var occupied = new List<SalonSlotGenerator.OccupiedSession>
        {
            new(new TimeOnly(10, 0), 60, null)
        };

        var result = SalonSlotGenerator.IsSlotBlocked(
            new TimeOnly(11, 0),
            occupied,
            new List<SalonSlotGenerator.TimeBlockRange>(),
            60);

        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateTimeBlock_WithValidRange_ReturnsTrue()
    {
        SalonSlotGenerator.ValidateTimeBlock(new TimeOnly(9, 0), new TimeOnly(10, 0)).Should().BeTrue();
    }

    [Fact]
    public void ValidateTimeBlock_WithInvalidRange_ReturnsFalse()
    {
        SalonSlotGenerator.ValidateTimeBlock(new TimeOnly(10, 0), new TimeOnly(9, 0)).Should().BeFalse();
    }

    [Fact]
    public void ValidateTimeBlock_WithEqualTimes_ReturnsFalse()
    {
        SalonSlotGenerator.ValidateTimeBlock(new TimeOnly(9, 0), new TimeOnly(9, 0)).Should().BeFalse();
    }

    [Fact]
    public void ValidateVacationBlock_WithValidRange_ReturnsTrue()
    {
        SalonSlotGenerator.ValidateVacationBlock(new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 15)).Should().BeTrue();
    }

    [Fact]
    public void ValidateVacationBlock_WithSameDay_ReturnsTrue()
    {
        SalonSlotGenerator.ValidateVacationBlock(new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 1)).Should().BeTrue();
    }

    [Fact]
    public void ValidateVacationBlock_WithInvalidRange_ReturnsFalse()
    {
        SalonSlotGenerator.ValidateVacationBlock(new DateOnly(2024, 1, 15), new DateOnly(2024, 1, 1)).Should().BeFalse();
    }

    [Fact]
    public void IsDateOnVacation_WhenDateInRange_ReturnsTrue()
    {
        var vacations = new List<SalonSlotGenerator.VacationRange>
        {
            new(new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 31))
        };

        var result = SalonSlotGenerator.IsDateOnVacation(new DateOnly(2024, 1, 15), vacations);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsDateOnVacation_WhenDateOutsideRange_ReturnsFalse()
    {
        var vacations = new List<SalonSlotGenerator.VacationRange>
        {
            new(new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 31))
        };

        var result = SalonSlotGenerator.IsDateOnVacation(new DateOnly(2024, 2, 15), vacations);

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(2024, 5, 15, 3)] // Wednesday = 3
    [InlineData(2024, 1, 1, 1)]  // Monday = 1
    [InlineData(2024, 1, 7, 0)]  // Sunday = 0
    public void GetWeekday_ReturnsCorrectDayOfWeek(int year, int month, int day, int expectedWeekday)
    {
        var date = new DateOnly(year, month, day);
        SalonSlotGenerator.GetWeekday(date).Should().Be(expectedWeekday);
    }

    [Theory]
    [InlineData(9, 0, 540)]
    [InlineData(14, 30, 870)]
    [InlineData(0, 0, 0)]
    [InlineData(23, 59, 1439)]
    public void ToMinutes_ReturnsCorrectMinutesFromMidnight(int hour, int minute, int expected)
    {
        var time = new TimeOnly(hour, minute);
        SalonSlotGenerator.ToMinutes(time).Should().Be(expected);
    }

    [Theory]
    [InlineData(9, 0, 60, "10:00")]
    [InlineData(14, 30, 30, "15:00")]
    public void AddMinutes_AddsCorrectly(int hour, int minute, int minutesToAdd, string expectedTime)
    {
        var time = new TimeOnly(hour, minute);
        var result = SalonSlotGenerator.AddMinutes(time, minutesToAdd);
        result.Should().Be(TimeOnly.Parse(expectedTime));
    }

    [Theory]
    [InlineData(540, 600, 570, 630, true)]   // [9:00, 10:00) overlaps with [9:30, 10:30)
    [InlineData(540, 600, 600, 660, false)]  // [9:00, 10:00) adjacent to [10:00, 11:00) - no overlap
    [InlineData(540, 600, 550, 590, true)]   // [9:00, 10:00) contains [9:10, 9:50)
    [InlineData(540, 600, 610, 650, false)]  // [9:00, 10:00) before [10:10, 10:50)
    public void TimesOverlap_ReturnsExpectedResult(int start1, int end1, int start2, int end2, bool expected)
    {
        SalonSlotGenerator.TimesOverlap(start1, end1, start2, end2).Should().Be(expected);
    }
}