using GymApp.Domain.Entities;
using GymApp.Domain.Enums;
using GymApp.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymApp.Tests.Helpers;

public class PackageHelperTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task AssignFromTemplateAsync_WithValidTemplate_CreatesPackageWithItems()
    {
        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var classTypeId = Guid.NewGuid();

        using var db = CreateInMemoryDb();

        var tenant = new Tenant { Id = tenantId, Name = "Test Gym", Slug = "test-gym" };
        var classType = new ClassType { Id = classTypeId, TenantId = tenantId, Name = "Boxing" };
        var template = new PackageTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Basic Package",
            DurationDays = 30
        };
        var templateItem = new PackageTemplateItem
        {
            Id = Guid.NewGuid(),
            TemplateId = template.Id,
            ClassTypeId = classTypeId,
            TotalCredits = 10,
            PricePerCredit = 5m
        };
        template.Items.Add(templateItem);

        db.Tenants.Add(tenant);
        db.ClassTypes.Add(classType);
        db.PackageTemplates.Add(template);
        await db.SaveChangesAsync();

        var result = await GymApp.Api.Helpers.PackageHelper.AssignFromTemplateAsync(db, tenantId, studentId, template.Id);

        Assert.NotNull(result);
        Assert.Equal(tenantId, result.TenantId);
        Assert.Equal(studentId, result.StudentId);
        Assert.Equal("Basic Package", result.Name);
        Assert.Single(result.Items);
        var item = result.Items.First();
        Assert.Equal(classTypeId, item.ClassTypeId);
        Assert.Equal(10, item.TotalCredits);
        Assert.Equal(5m, item.PricePerCredit);
    }

    [Fact]
    public async Task AssignFromTemplateAsync_WithNonExistentTemplate_ReturnsNull()
    {
        using var db = CreateInMemoryDb();
        var result = await GymApp.Api.Helpers.PackageHelper.AssignFromTemplateAsync(db, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task AssignFromTemplateAsync_WithNoDurationDays_ExpiresAtIsNull()
    {
        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        using var db = CreateInMemoryDb();

        var tenant = new Tenant { Id = tenantId, Name = "Test Gym", Slug = "test-gym2" };
        var template = new PackageTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Unlimited Package",
            DurationDays = null
        };

        db.Tenants.Add(tenant);
        db.PackageTemplates.Add(template);
        await db.SaveChangesAsync();

        var result = await GymApp.Api.Helpers.PackageHelper.AssignFromTemplateAsync(db, tenantId, studentId, template.Id);

        Assert.NotNull(result);
        Assert.Null(result.ExpiresAt);
    }

    [Fact]
    public async Task AssignDefaultPackageIfConfiguredAsync_WithNoDefaultTemplate_DoesNothing()
    {
        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        using var db = CreateInMemoryDb();

        var tenant = new Tenant { Id = tenantId, Name = "Test Gym", Slug = "test-gym", DefaultPackageTemplateId = null };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        await GymApp.Api.Helpers.PackageHelper.AssignDefaultPackageIfConfiguredAsync(db, tenantId, studentId);

        var packages = await db.Packages.Where(p => p.StudentId == studentId).ToListAsync();
        Assert.Empty(packages);
    }

    [Fact]
    public async Task AssignDefaultPackageIfConfiguredAsync_WithDefaultTemplate_CreatesPackage()
    {
        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        using var db = CreateInMemoryDb();

        var template = new PackageTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Default Package",
            DurationDays = 60
        };
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Test Gym",
            Slug = "test-gym",
            DefaultPackageTemplateId = template.Id
        };

        db.Tenants.Add(tenant);
        db.PackageTemplates.Add(template);
        await db.SaveChangesAsync();

        await GymApp.Api.Helpers.PackageHelper.AssignDefaultPackageIfConfiguredAsync(db, tenantId, studentId);
        await db.SaveChangesAsync();

        var packages = await db.Packages.Where(p => p.StudentId == studentId).ToListAsync();
        Assert.Single(packages);
        Assert.Equal("Default Package", packages[0].Name);
    }
}