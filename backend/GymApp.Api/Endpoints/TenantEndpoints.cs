using GymApp.Api.DTOs;
using GymApp.Domain.Entities;
using GymApp.Domain.Interfaces;
using GymApp.Infra.Data;
using GymApp.Infra.Services;
using Microsoft.EntityFrameworkCore;

namespace GymApp.Api.Endpoints;

public static class TenantEndpoints
{
    public static void MapTenantEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tenant");

        // Public: get current tenant config (used by frontend for white label)
        group.MapGet("/config", async (HttpContext ctx, AppDbContext db) =>
        {
            var host = ctx.Request.Host.Host.ToLowerInvariant();
            var slug = ExtractSlug(host) ?? ctx.Request.Headers["X-Tenant-Slug"].FirstOrDefault();

            if (string.IsNullOrEmpty(slug))
                return Results.BadRequest("Tenant not identified.");

            var tenant = await db.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Slug == slug && t.IsActive);

            if (tenant is null) return Results.NotFound();

            return Results.Ok(new TenantConfigResponse(
                tenant.Name, tenant.LogoUrl, tenant.PrimaryColor, tenant.SecondaryColor, tenant.Slug));
        }).AllowAnonymous();

        // Super Admin: list all tenants
        var adminGroup = app.MapGroup("/api/admin/tenants").RequireAuthorization("SuperAdmin");

        adminGroup.MapGet("/", async (AppDbContext db) =>
        {
            var tenants = await db.Tenants.AsNoTracking()
                .OrderBy(t => t.Name)
                .Select(t => new TenantResponse(t.Id, t.Name, t.Slug, t.LogoUrl, t.PrimaryColor, t.SecondaryColor, t.Plan, t.IsActive, t.CreatedAt))
                .ToListAsync();
            return Results.Ok(tenants);
        });

        adminGroup.MapPost("/", async (CreateTenantRequest req, AppDbContext db) =>
        {
            if (await db.Tenants.AnyAsync(t => t.Slug == req.Slug))
                return Results.Conflict("Slug already in use.");

            var tenant = new Tenant
            {
                Name = req.Name,
                Slug = req.Slug.ToLowerInvariant(),
                LogoUrl = req.LogoUrl,
                PrimaryColor = req.PrimaryColor,
                SecondaryColor = req.SecondaryColor,
                Plan = req.Plan
            };
            db.Tenants.Add(tenant);

            var admin = new User
            {
                TenantId = tenant.Id,
                Name = req.AdminName,
                Email = req.AdminEmail.ToLowerInvariant(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.AdminPassword),
                Role = GymApp.Domain.Enums.UserRole.Admin
            };
            db.Users.Add(admin);

            await db.SaveChangesAsync();
            return Results.Created($"/api/admin/tenants/{tenant.Id}",
                new TenantResponse(tenant.Id, tenant.Name, tenant.Slug, tenant.LogoUrl,
                    tenant.PrimaryColor, tenant.SecondaryColor, tenant.Plan, tenant.IsActive, tenant.CreatedAt));
        });

        adminGroup.MapPut("/{id:guid}", async (Guid id, UpdateTenantRequest req, AppDbContext db) =>
        {
            var tenant = await db.Tenants.FindAsync(id);
            if (tenant is null) return Results.NotFound();

            tenant.Name = req.Name;
            tenant.LogoUrl = req.LogoUrl;
            tenant.PrimaryColor = req.PrimaryColor;
            tenant.SecondaryColor = req.SecondaryColor;
            tenant.IsActive = req.IsActive;

            await db.SaveChangesAsync();
            return Results.Ok(new TenantResponse(tenant.Id, tenant.Name, tenant.Slug, tenant.LogoUrl,
                tenant.PrimaryColor, tenant.SecondaryColor, tenant.Plan, tenant.IsActive, tenant.CreatedAt));
        });
    }

    private static string? ExtractSlug(string host)
    {
        var parts = host.Split('.');
        return parts.Length >= 3 ? parts[0] : null;
    }
}
