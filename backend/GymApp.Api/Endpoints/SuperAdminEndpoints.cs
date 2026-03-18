using System.Security.Claims;
using GymApp.Api.DTOs;
using GymApp.Domain.Entities;
using GymApp.Domain.Enums;
using GymApp.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace GymApp.Api.Endpoints;

public static class SuperAdminEndpoints
{
    public static void MapSuperAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/super-admins").RequireAuthorization("SuperAdmin");

        // List all super admins
        group.MapGet("/", async (AppDbContext db) =>
        {
            var admins = await db.Users.AsNoTracking()
                .Where(u => u.Role == UserRole.SuperAdmin)
                .OrderBy(u => u.Name)
                .Select(u => new SuperAdminResponse(
                    u.Id, u.Name, u.Email, u.Phone,
                    u.Status == StudentStatus.Active,
                    u.CreatedAt))
                .ToListAsync();
            return Results.Ok(admins);
        });

        // Create new super admin
        group.MapPost("/", async (CreateSuperAdminRequest req, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name) ||
                string.IsNullOrWhiteSpace(req.Email) ||
                string.IsNullOrWhiteSpace(req.Password))
                return Results.BadRequest("Name, email and password are required.");

            var emailLower = req.Email.ToLowerInvariant().Trim();
            if (await db.Users.AnyAsync(u => u.Email == emailLower))
                return Results.Conflict("E-mail already in use.");

            var user = new User
            {
                TenantId = null,
                Name = req.Name.Trim(),
                Email = emailLower,
                Phone = req.Phone,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
                Role = UserRole.SuperAdmin,
                Status = StudentStatus.Active
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            return Results.Created($"/api/admin/super-admins/{user.Id}",
                new SuperAdminResponse(user.Id, user.Name, user.Email, user.Phone, true, user.CreatedAt));
        });

        // Edit super admin (name, email, phone, optional password)
        group.MapPut("/{id:guid}", async (Guid id, UpdateSuperAdminRequest req, AppDbContext db) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == UserRole.SuperAdmin);
            if (user is null) return Results.NotFound();

            if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Email))
                return Results.BadRequest("Name and email are required.");

            var emailLower = req.Email.ToLowerInvariant().Trim();
            if (await db.Users.AnyAsync(u => u.Email == emailLower && u.Id != id))
                return Results.Conflict("E-mail already in use.");

            user.Name = req.Name.Trim();
            user.Email = emailLower;
            user.Phone = req.Phone;

            if (!string.IsNullOrWhiteSpace(req.Password))
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);

            await db.SaveChangesAsync();
            return Results.Ok(new SuperAdminResponse(user.Id, user.Name, user.Email, user.Phone,
                user.Status == StudentStatus.Active, user.CreatedAt));
        });

        // Enable / disable a super admin — cannot disable yourself
        group.MapPut("/{id:guid}/status", async (
            Guid id,
            SetSuperAdminStatusRequest req,
            AppDbContext db,
            ClaimsPrincipal principal) =>
        {
            var currentUserId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (id == currentUserId)
                return Results.BadRequest("Cannot change the status of your own account.");

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == UserRole.SuperAdmin);
            if (user is null) return Results.NotFound();

            user.Status = req.IsActive ? StudentStatus.Active : StudentStatus.Inactive;
            await db.SaveChangesAsync();

            return Results.Ok(new SuperAdminResponse(user.Id, user.Name, user.Email, user.Phone,
                user.Status == StudentStatus.Active, user.CreatedAt));
        });
    }
}
