using FluentAssertions;
using GymApp.Api.Core;
using GymApp.Domain.Entities;
using Xunit;

namespace GymApp.Tests.Core;

public class LocationValidatorTests
{
    [Fact]
    public void CanDeleteLocation_WithMultipleLocationsAndNoSessions_ReturnsTrue()
    {
        var location = new Location { Id = Guid.NewGuid(), Name = "Test" };
        var result = LocationValidator.CanDeleteLocation(location, totalLocations: 2, futureSessions: 0);
        result.Should().BeTrue();
    }

    [Fact]
    public void CanDeleteLocation_WithSingleLocation_ReturnsFalse()
    {
        var location = new Location { Id = Guid.NewGuid(), Name = "Test" };
        var result = LocationValidator.CanDeleteLocation(location, totalLocations: 1, futureSessions: 0);
        result.Should().BeFalse();
    }

    [Fact]
    public void CanDeleteLocation_WithFutureSessions_ReturnsFalse()
    {
        var location = new Location { Id = Guid.NewGuid(), Name = "Test" };
        var result = LocationValidator.CanDeleteLocation(location, totalLocations: 2, futureSessions: 5);
        result.Should().BeFalse();
    }

    [Fact]
    public void GetDeleteLocationError_WithSingleLocation_ReturnsErrorMessage()
    {
        var location = new Location();
        var error = LocationValidator.GetDeleteLocationError(location, totalLocations: 1, futureSessions: 0);
        error.Should().Contain("última localização");
    }

    [Fact]
    public void GetDeleteLocationError_WithFutureSessions_ReturnsSessionCount()
    {
        var location = new Location();
        var error = LocationValidator.GetDeleteLocationError(location, totalLocations: 2, futureSessions: 5);
        error.Should().Contain("5");
        error.Should().Contain("sessão");
    }

    [Fact]
    public void GetDeleteLocationError_WhenDeletable_ReturnsNull()
    {
        var location = new Location();
        var error = LocationValidator.GetDeleteLocationError(location, totalLocations: 2, futureSessions: 0);
        error.Should().BeNull();
    }

    [Fact]
    public void EnsureSingleMainLocation_SetsNewMainAndUnsetsOld()
    {
        var main = new Location { Id = Guid.NewGuid(), IsMain = true };
        var other = new Location { Id = Guid.NewGuid(), IsMain = true };
        var newMain = new Location { Id = Guid.NewGuid(), IsMain = false };

        var locations = new[] { main, other, newMain };
        LocationValidator.EnsureSingleMainLocation(newMain, locations);

        newMain.IsMain.Should().BeTrue();
        main.IsMain.Should().BeFalse();
        other.IsMain.Should().BeFalse();
    }
}

public class InstructorServiceManagerTests
{
    [Fact]
    public void FilterValidServiceIds_ReturnsOnlyValidIds()
    {
        var existing = new List<Guid> { Guid.NewGuid() };
        var requested = new List<Guid> { existing[0], Guid.NewGuid() };

        var result = InstructorServiceManager.FilterValidServiceIds(
            requested, existing, id => existing.Contains(id));

        result.Should().HaveCount(1);
        result.Should().Contain(existing[0]);
    }

    [Fact]
    public void FilterValidServiceIds_DeduplicatesIds()
    {
        var validId = Guid.NewGuid();
        var requested = new List<Guid> { validId, validId, validId };

        var result = InstructorServiceManager.FilterValidServiceIds(
            requested, new List<Guid>(), id => id == validId);

        result.Should().HaveCount(1);
    }

    [Fact]
    public void FilterValidServiceIds_WithNoValidIds_ReturnsEmpty()
    {
        var result = InstructorServiceManager.FilterValidServiceIds(
            new List<Guid> { Guid.NewGuid() },
            new List<Guid>(),
            id => false);

        result.Should().BeEmpty();
    }

    [Fact]
    public void SyncInstructorServices_ClearsOldAndAddsNew()
    {
        var instructor = new Instructor
        {
            Id = Guid.NewGuid(),
            Services = new List<InstructorService>
            {
                new() { ClassTypeId = Guid.NewGuid() },
                new() { ClassTypeId = Guid.NewGuid() }
            }
        };

        var newServiceId = Guid.NewGuid();
        InstructorServiceManager.SyncInstructorServices(
            instructor,
            new[] { newServiceId },
            id => id == newServiceId);

        instructor.Services.Should().HaveCount(1);
        instructor.Services.First().ClassTypeId.Should().Be(newServiceId);
    }
}