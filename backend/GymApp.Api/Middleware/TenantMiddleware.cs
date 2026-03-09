using GymApp.Infra.Data;
using GymApp.Infra.Services;
using Microsoft.EntityFrameworkCore;

namespace GymApp.Api.Middleware;

public class TenantMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AppDbContext db, TenantContext tenantContext)
    {
        var host = context.Request.Host.Host.ToLowerInvariant();

        // Extract slug from subdomain (e.g. "boxe-elite" from "boxe-elite.gymapp.com")
        // or fall back to X-Tenant-Slug header (used in dev / nginx proxy)
        var slug = ExtractSlug(host)
            ?? context.Request.Headers["X-Tenant-Slug"].FirstOrDefault();

        if (!string.IsNullOrEmpty(slug))
        {
            var tenant = await db.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Slug == slug && t.IsActive);

            if (tenant is not null)
                tenantContext.Resolve(tenant.Id, tenant.Slug);
        }

        await next(context);
    }

    private static string? ExtractSlug(string host)
    {
        // "boxe-elite.gymapp.com" → "boxe-elite"
        // "boxe-elite.gymapp.local" → "boxe-elite"
        // "localhost" → null (super admin or dev)
        var parts = host.Split('.');
        if (parts.Length >= 3)
            return parts[0];

        // Support direct slug header for local dev
        return null;
    }
}
