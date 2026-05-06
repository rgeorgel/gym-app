using GymApp.Infra.Services;
using Xunit;

namespace GymApp.Tests.Services;

public class TenantContextTests
{
    [Fact]
    public void TenantId_DefaultValue_IsEmptyGuid()
    {
        var ctx = new TenantContext();
        Assert.Equal(Guid.Empty, ctx.TenantId);
    }

    [Fact]
    public void Slug_DefaultValue_IsEmptyString()
    {
        var ctx = new TenantContext();
        Assert.Equal(string.Empty, ctx.Slug);
    }

    [Fact]
    public void IsResolved_DefaultValue_IsFalse()
    {
        var ctx = new TenantContext();
        Assert.False(ctx.IsResolved);
    }

    [Fact]
    public void HasStudentAccess_DefaultValue_IsTrue()
    {
        var ctx = new TenantContext();
        Assert.True(ctx.HasStudentAccess);
    }

    [Fact]
    public void LocationId_DefaultValue_IsNull()
    {
        var ctx = new TenantContext();
        Assert.Null(ctx.LocationId);
    }

    [Fact]
    public void Resolve_SetsAllProperties()
    {
        var ctx = new TenantContext();
        var tenantId = Guid.NewGuid();
        var slug = "test-gym";

        ctx.Resolve(tenantId, slug, hasStudentAccess: false);

        Assert.Equal(tenantId, ctx.TenantId);
        Assert.Equal(slug, ctx.Slug);
        Assert.False(ctx.HasStudentAccess);
        Assert.True(ctx.IsResolved);
    }

    [Fact]
    public void Resolve_WithDefaultHasStudentAccess_SetsTrue()
    {
        var ctx = new TenantContext();
        var tenantId = Guid.NewGuid();

        ctx.Resolve(tenantId, "test");

        Assert.True(ctx.HasStudentAccess);
    }

    [Fact]
    public void LocationId_CanBeSetIndependently()
    {
        var ctx = new TenantContext();
        var locationId = Guid.NewGuid();

        ctx.LocationId = locationId;

        Assert.Equal(locationId, ctx.LocationId);
    }

    [Fact]
    public void Resolve_CanBeCalledMultipleTimes()
    {
        var ctx = new TenantContext();
        var tenantId1 = Guid.NewGuid();
        var tenantId2 = Guid.NewGuid();

        ctx.Resolve(tenantId1, "first");
        ctx.Resolve(tenantId2, "second");

        Assert.Equal(tenantId2, ctx.TenantId);
        Assert.Equal("second", ctx.Slug);
        Assert.True(ctx.IsResolved);
    }
}