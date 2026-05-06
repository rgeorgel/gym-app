using GymApp.Domain.Entities;
using Xunit;

namespace GymApp.Tests.Entities;

public class PackageItemTests
{
    [Fact]
    public void RemainingCredits_WithNoUsedCredits_ReturnsTotalCredits()
    {
        var item = new PackageItem { TotalCredits = 10, UsedCredits = 0 };
        Assert.Equal(10, item.RemainingCredits);
    }

    [Fact]
    public void RemainingCredits_WithSomeUsedCredits_ReturnsCorrectDifference()
    {
        var item = new PackageItem { TotalCredits = 10, UsedCredits = 3 };
        Assert.Equal(7, item.RemainingCredits);
    }

    [Fact]
    public void RemainingCredits_WithAllUsed_ReturnsZero()
    {
        var item = new PackageItem { TotalCredits = 10, UsedCredits = 10 };
        Assert.Equal(0, item.RemainingCredits);
    }

    [Fact]
    public void RemainingCredits_WithMoreUsedThanTotal_ReturnsNegative()
    {
        var item = new PackageItem { TotalCredits = 10, UsedCredits = 15 };
        Assert.Equal(-5, item.RemainingCredits);
    }
}