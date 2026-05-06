using FluentAssertions;
using GymApp.Api.Core;
using Xunit;

namespace GymApp.Tests.Core;

public class WhatsAppSlotGeneratorTests
{
    [Fact]
    public void FindOverlapping_WithOverlappingRanges_ReturnsTrue()
    {
        var start1 = new TimeOnly(9, 0);
        var end1 = new TimeOnly(10, 0);
        var start2 = new TimeOnly(9, 30);
        var end2 = new TimeOnly(10, 30);

        var result = WhatsAppSlotGenerator.FindOverlapping(start1, end1, start2, end2);

        result.Should().BeTrue();
    }

    [Fact]
    public void FindOverlapping_WithNonOverlappingRanges_ReturnsFalse()
    {
        var start1 = new TimeOnly(9, 0);
        var end1 = new TimeOnly(10, 0);
        var start2 = new TimeOnly(10, 0);
        var end2 = new TimeOnly(11, 0);

        var result = WhatsAppSlotGenerator.FindOverlapping(start1, end1, start2, end2);

        result.Should().BeFalse();
    }

    [Fact]
    public void FindOverlapping_WithContainedRange_ReturnsTrue()
    {
        var start1 = new TimeOnly(9, 0);
        var end1 = new TimeOnly(12, 0);
        var start2 = new TimeOnly(10, 0);
        var end2 = new TimeOnly(11, 0);

        var result = WhatsAppSlotGenerator.FindOverlapping(start1, end1, start2, end2);

        result.Should().BeTrue();
    }

    [Fact]
    public void FindOverlapping_AdjacentRanges_ReturnsFalse()
    {
        var start1 = new TimeOnly(9, 0);
        var end1 = new TimeOnly(10, 0);
        var start2 = new TimeOnly(10, 0);
        var end2 = new TimeOnly(11, 0);

        var result = WhatsAppSlotGenerator.FindOverlapping(start1, end1, start2, end2);

        result.Should().BeFalse();
    }

    [Fact]
    public void AutoAssignProfessional_WithNoAvailableProfessisonals_ReturnsNull()
    {
        var result = WhatsAppSlotGenerator.AutoAssignProfessional(
            new List<Guid>(),
            new List<WhatsAppSlotGenerator.OccupiedSession>());

        result.Should().BeNull();
    }

    [Fact]
    public void AutoAssignProfessional_WithOneProfessional_ReturnsThatProfessional()
    {
        var profId = Guid.NewGuid();
        var result = WhatsAppSlotGenerator.AutoAssignProfessional(
            new List<Guid> { profId },
            new List<WhatsAppSlotGenerator.OccupiedSession>());

        result.Should().Be(profId);
    }

    [Fact]
    public void AutoAssignProfessional_WithMultipleProfessorsSelectsLeastLoaded()
    {
        var prof1 = Guid.NewGuid();
        var prof2 = Guid.NewGuid();
        var prof3 = Guid.NewGuid();

        var existingSessions = new List<WhatsAppSlotGenerator.OccupiedSession>
        {
            new(new TimeOnly(9, 0), 60, prof1),
            new(new TimeOnly(10, 0), 60, prof1),
            new(new TimeOnly(11, 0), 60, prof2),
        };

        var result = WhatsAppSlotGenerator.AutoAssignProfessional(
            new List<Guid> { prof1, prof2, prof3 },
            existingSessions);

        result.Should().Be(prof3);
    }

    [Fact]
    public void AutoAssignProfessional_WithServiceProfessionals_FiltersCorrectly()
    {
        var prof1 = Guid.NewGuid();
        var prof2 = Guid.NewGuid();
        var prof3 = Guid.NewGuid();

        var serviceProfs = new List<Guid> { prof1, prof2 };

        var result = WhatsAppSlotGenerator.AutoAssignProfessional(
            new List<Guid> { prof1, prof2, prof3 },
            new List<WhatsAppSlotGenerator.OccupiedSession>(),
            serviceProfs);

        result.Should().NotBeNull();
        var val = result!.Value;
        (val == prof1 || val == prof2).Should().BeTrue();
    }

    [Fact]
    public void AutoAssignProfessional_WhenNoServiceProfessionalsMatch_ReturnsNull()
    {
        var prof1 = Guid.NewGuid();
        var prof2 = Guid.NewGuid();

        var serviceProfs = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        var result = WhatsAppSlotGenerator.AutoAssignProfessional(
            new List<Guid> { prof1, prof2 },
            new List<WhatsAppSlotGenerator.OccupiedSession>(),
            serviceProfs);

        result.Should().BeNull();
    }

    [Fact]
    public void GenerateSlots_WithNoBlocks_ReturnsEmpty()
    {
        var result = WhatsAppSlotGenerator.GenerateSlots(
            new List<WhatsAppSlotGenerator.AvailabilityBlock>(),
            new List<WhatsAppSlotGenerator.OccupiedSession>(),
            new List<WhatsAppSlotGenerator.TimeBlockItem>(),
            60);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GenerateSlots_WithAvailabilityBlock_CreatesSlots()
    {
        var profId = Guid.NewGuid();
        var blocks = new List<WhatsAppSlotGenerator.AvailabilityBlock>
        {
            new(profId, "John", new TimeOnly(9, 0), new TimeOnly(12, 0))
        };

        var result = WhatsAppSlotGenerator.GenerateSlots(
            blocks,
            new List<WhatsAppSlotGenerator.OccupiedSession>(),
            new List<WhatsAppSlotGenerator.TimeBlockItem>(),
            60);

        result.Should().HaveCount(3);
        result[0].Time.Should().Be("09:00");
        result[1].Time.Should().Be("10:00");
        result[2].Time.Should().Be("11:00");
    }

    [Fact]
    public void GenerateSlots_WithOccupiedSession_SkipsBlockedSlot()
    {
        var profId = Guid.NewGuid();
        var blocks = new List<WhatsAppSlotGenerator.AvailabilityBlock>
        {
            new(profId, "John", new TimeOnly(9, 0), new TimeOnly(12, 0))
        };
        var occupied = new List<WhatsAppSlotGenerator.OccupiedSession>
        {
            new(new TimeOnly(10, 0), 60, profId)
        };

        var result = WhatsAppSlotGenerator.GenerateSlots(
            blocks,
            occupied,
            new List<WhatsAppSlotGenerator.TimeBlockItem>(),
            60);

        result.Should().HaveCount(2);
        result.Select(s => s.Time).Should().Contain("09:00");
        result.Select(s => s.Time).Should().Contain("11:00");
    }

    [Fact]
    public void GenerateSlots_WithTimeBlock_SkipsBlockedSlot()
    {
        var profId = Guid.NewGuid();
        var blocks = new List<WhatsAppSlotGenerator.AvailabilityBlock>
        {
            new(profId, "John", new TimeOnly(9, 0), new TimeOnly(12, 0))
        };
        var timeBlocks = new List<WhatsAppSlotGenerator.TimeBlockItem>
        {
            new(new TimeOnly(10, 0), new TimeOnly(11, 0))
        };

        var result = WhatsAppSlotGenerator.GenerateSlots(
            blocks,
            new List<WhatsAppSlotGenerator.OccupiedSession>(),
            timeBlocks,
            60);

        result.Should().HaveCount(2);
        result.Select(s => s.Time).Should().Contain("09:00");
        result.Select(s => s.Time).Should().Contain("11:00");
    }

    [Fact]
    public void GenerateSlots_WithMultipleProfessors_DeduplicatesByTime()
    {
        var prof1 = Guid.NewGuid();
        var prof2 = Guid.NewGuid();
        var blocks = new List<WhatsAppSlotGenerator.AvailabilityBlock>
        {
            new(prof1, "John", new TimeOnly(9, 0), new TimeOnly(10, 0)),
            new(prof2, "Jane", new TimeOnly(9, 0), new TimeOnly(10, 0))
        };

        var result = WhatsAppSlotGenerator.GenerateSlots(
            blocks,
            new List<WhatsAppSlotGenerator.OccupiedSession>(),
            new List<WhatsAppSlotGenerator.TimeBlockItem>(),
            60);

        result.Should().HaveCount(2);
        result.Select(s => s.ProfessionalId).Should().Contain(prof1);
        result.Select(s => s.ProfessionalId).Should().Contain(prof2);
    }

    [Fact]
    public void GenerateSlots_ReturnsSlotsSortedByTime()
    {
        var profId = Guid.NewGuid();
        var blocks = new List<WhatsAppSlotGenerator.AvailabilityBlock>
        {
            new(profId, "John", new TimeOnly(14, 0), new TimeOnly(17, 0))
        };

        var result = WhatsAppSlotGenerator.GenerateSlots(
            blocks,
            new List<WhatsAppSlotGenerator.OccupiedSession>(),
            new List<WhatsAppSlotGenerator.TimeBlockItem>(),
            60);

        result.Should().HaveCount(3);
        result[0].Time.Should().Be("14:00");
        result[1].Time.Should().Be("15:00");
        result[2].Time.Should().Be("16:00");
    }

    [Fact]
    public void GenerateSlots_SetsCorrectInstructorInfo()
    {
        var profId = Guid.NewGuid();
        var blocks = new List<WhatsAppSlotGenerator.AvailabilityBlock>
        {
            new(profId, "John Doe", new TimeOnly(9, 0), new TimeOnly(10, 0))
        };

        var result = WhatsAppSlotGenerator.GenerateSlots(
            blocks,
            new List<WhatsAppSlotGenerator.OccupiedSession>(),
            new List<WhatsAppSlotGenerator.TimeBlockItem>(),
            60);

        result.Should().HaveCount(1);
        result[0].ProfessionalId.Should().Be(profId);
        result[0].ProfessionalName.Should().Be("John Doe");
    }
}