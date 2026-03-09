using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GymApp.Api.DTOs;
using GymApp.Domain.Enums;
using GymApp.Infra.Data;
using GymApp.Infra.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace GymApp.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/login", async (LoginRequest req, AppDbContext db, IConfiguration config, TenantContext tenantCtx) =>
        {
            var email = req.Email.ToLowerInvariant();

            // Resolve tenant: header > body slug > SuperAdmin fallback
            if (!tenantCtx.IsResolved && !string.IsNullOrEmpty(req.TenantSlug))
            {
                var t = await db.Tenants.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Slug == req.TenantSlug && t.IsActive);
                if (t is not null)
                    tenantCtx.Resolve(t.Id, t.Slug);
            }

            var user = tenantCtx.IsResolved
                ? await db.Users.FirstOrDefaultAsync(u => u.TenantId == tenantCtx.TenantId && u.Email == email)
                : await db.Users.FirstOrDefaultAsync(u => u.Email == email && u.Role == UserRole.SuperAdmin);

            if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
                return Results.Unauthorized();

            if (user.Status == GymApp.Domain.Enums.StudentStatus.Suspended)
                return Results.Forbid();

            var (access, refresh) = GenerateTokens(user, config);
            user.RefreshToken = refresh;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(int.Parse(config["Jwt:RefreshExpiryDays"] ?? "30"));
            await db.SaveChangesAsync();

            return Results.Ok(new LoginResponse(access, refresh, user.Role.ToString(), user.Name, user.Id, tenantCtx.IsResolved ? tenantCtx.Slug : null));
        }).AllowAnonymous();

        group.MapPost("/refresh", async (RefreshRequest req, AppDbContext db, IConfiguration config) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u =>
                u.RefreshToken == req.RefreshToken &&
                u.RefreshTokenExpiry > DateTime.UtcNow);

            if (user is null) return Results.Unauthorized();

            var (access, refresh) = GenerateTokens(user, config);
            user.RefreshToken = refresh;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(int.Parse(config["Jwt:RefreshExpiryDays"] ?? "30"));
            await db.SaveChangesAsync();

            return Results.Ok(new LoginResponse(access, refresh, user.Role.ToString(), user.Name, user.Id));
        }).AllowAnonymous();

        group.MapPost("/change-password", async (ChangePasswordRequest req, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await db.Users.FindAsync(userId);
            if (user is null) return Results.NotFound();

            if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
                return Results.BadRequest("Current password is incorrect.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization();

        group.MapGet("/me", (ClaimsPrincipal principal) =>
        {
            return Results.Ok(new
            {
                Id = principal.FindFirstValue(ClaimTypes.NameIdentifier),
                Name = principal.FindFirstValue(ClaimTypes.Name),
                Email = principal.FindFirstValue(ClaimTypes.Email),
                Role = principal.FindFirstValue(ClaimTypes.Role)
            });
        }).RequireAuthorization();
    }

    private static (string access, string refresh) GenerateTokens(GymApp.Domain.Entities.User user, IConfiguration config)
    {
        var secret = config["Jwt:Secret"] ?? throw new InvalidOperationException("JWT secret not configured");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddMinutes(int.Parse(config["Jwt:ExpiryMinutes"] ?? "60"));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Role, user.Role.ToString()),
        };

        if (user.TenantId.HasValue)
            claims.Add(new Claim("tenant_id", user.TenantId.Value.ToString()));

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: expiry,
            signingCredentials: creds
        );

        var access = new JwtSecurityTokenHandler().WriteToken(token);
        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return (access, refresh);
    }
}
