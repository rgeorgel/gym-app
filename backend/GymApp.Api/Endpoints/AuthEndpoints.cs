using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GymApp.Api.DTOs;
using GymApp.Api.Helpers;
using GymApp.Domain.Entities;
using GymApp.Domain.Enums;
using GymApp.Domain.Interfaces;
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
                : await db.Users.FirstOrDefaultAsync(u => u.Email == email
                      && (u.Role == UserRole.SuperAdmin || u.Role == UserRole.Affiliate || u.Role == UserRole.Admin));

            if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
                return Results.Unauthorized();

            if (user.Status != GymApp.Domain.Enums.StudentStatus.Active)
                return Results.Forbid();

            // If tenant wasn't resolved yet (login from landing page), resolve from the user's own tenant
            if (!tenantCtx.IsResolved && user.TenantId != Guid.Empty)
            {
                var t = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == user.TenantId);
                if (t is not null) tenantCtx.Resolve(t.Id, t.Slug);
            }

            var (access, refresh) = GenerateTokens(user, config);
            user.RefreshToken = refresh;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(int.Parse(config["Jwt:RefreshExpiryDays"] ?? "30"));
            await db.SaveChangesAsync();

            return Results.Ok(new LoginResponse(access, refresh, user.Role.ToString(), user.Name, user.Id, tenantCtx.IsResolved ? tenantCtx.Slug : null));
        }).AllowAnonymous();

        group.MapPost("/register", async (RegisterStudentRequest req, AppDbContext db, TenantContext tenantCtx, IConfiguration config) =>
        {
            if (!tenantCtx.IsResolved)
                return Results.BadRequest("Tenant not identified. Access via your gym's URL.");

            if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return Results.BadRequest("Name, email and password are required.");

            if (req.Password.Length < 6)
                return Results.BadRequest("Password must be at least 6 characters.");

            var email = req.Email.ToLowerInvariant();
            if (await db.Users.AnyAsync(u => u.TenantId == tenantCtx.TenantId && u.Email == email))
                return Results.Conflict("Email already registered.");

            var user = new User
            {
                TenantId = tenantCtx.TenantId,
                Name = req.Name.Trim(),
                Email = email,
                Phone = req.Phone,
                BirthDate = req.BirthDate,
                Role = UserRole.Student,
                Status = StudentStatus.Active,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            };
            db.Users.Add(user);

            var (access, refresh) = GenerateTokens(user, config);
            user.RefreshToken = refresh;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(int.Parse(config["Jwt:RefreshExpiryDays"] ?? "30"));

            await PackageHelper.AssignDefaultPackageIfConfiguredAsync(db, tenantCtx.TenantId, user.Id);
            await db.SaveChangesAsync();

            return Results.Ok(new LoginResponse(access, refresh, user.Role.ToString(), user.Name, user.Id, tenantCtx.Slug));
        }).AllowAnonymous();

        group.MapPost("/refresh", async (RefreshRequest req, AppDbContext db, IConfiguration config) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u =>
                u.RefreshToken == req.RefreshToken &&
                u.RefreshTokenExpiry > DateTime.UtcNow);

            if (user is null) return Results.Unauthorized();

            if (user.Status != GymApp.Domain.Enums.StudentStatus.Active)
                return Results.Unauthorized();

            var (access, refresh) = GenerateTokens(user, config);
            user.RefreshToken = refresh;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(int.Parse(config["Jwt:RefreshExpiryDays"] ?? "30"));
            await db.SaveChangesAsync();

            return Results.Ok(new LoginResponse(access, refresh, user.Role.ToString(), user.Name, user.Id));
        }).AllowAnonymous();

        group.MapPost("/forgot-password", async (ForgotPasswordRequest req, HttpContext ctx, AppDbContext db, IEmailService email, TenantContext tenantCtx) =>
        {
            // Always return OK to avoid email enumeration
            var emailAddr = req.Email.ToLowerInvariant();

            // Filter by tenant when resolved; allow SuperAdmin lookup when not
            var user = tenantCtx.IsResolved
                ? await db.Users.FirstOrDefaultAsync(u => u.TenantId == tenantCtx.TenantId && u.Email == emailAddr)
                : await db.Users.FirstOrDefaultAsync(u => u.Email == emailAddr && u.Role == UserRole.SuperAdmin);

            if (user is not null)
            {
                var token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
                    .Replace("+", "-").Replace("/", "_").Replace("=", "");
                user.PasswordResetToken = token;
                user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(2);
                await db.SaveChangesAsync();

                // Build reset URL from the incoming request host so the link
                // preserves the tenant's subdomain or custom domain
                var request = ctx.Request;
                var origin = $"{request.Scheme}://{request.Host}";
                var resetUrl = $"{origin}/reset-password.html?token={Uri.EscapeDataString(token)}";
                await email.SendPasswordResetAsync(user.Email, user.Name, resetUrl);
            }
            return Results.Ok();
        }).AllowAnonymous();

        group.MapGet("/validate-reset-token", async (string token, AppDbContext db) =>
        {
            var user = await db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.PasswordResetToken == token && u.PasswordResetTokenExpiry > DateTime.UtcNow);
            if (user is null) return Results.BadRequest("Token inválido ou expirado.");
            return Results.Ok(new { user.Name, user.Email });
        }).AllowAnonymous();

        group.MapPost("/reset-password", async (ResetPasswordRequest req, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 6)
                return Results.BadRequest("A senha deve ter pelo menos 6 caracteres.");

            var user = await db.Users
                .FirstOrDefaultAsync(u => u.PasswordResetToken == req.Token && u.PasswordResetTokenExpiry > DateTime.UtcNow);
            if (user is null) return Results.BadRequest("Token inválido ou expirado.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiry = null;
            await db.SaveChangesAsync();
            return Results.Ok();
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
