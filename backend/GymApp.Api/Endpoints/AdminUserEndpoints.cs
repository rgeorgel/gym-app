using System.Security.Claims;
using GymApp.Api.DTOs;
using GymApp.Domain.Entities;
using GymApp.Domain.Enums;
using GymApp.Infra.Data;
using GymApp.Infra.Services;
using Microsoft.EntityFrameworkCore;

namespace GymApp.Api.Endpoints;

public static class AdminUserEndpoints
{
    public static void MapAdminUserEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admins").RequireAuthorization("AdminOrAbove");

        group.MapGet("/", async (AppDbContext db, TenantContext tenant) =>
        {
            var admins = await db.Users.AsNoTracking()
                .Where(u => u.TenantId == tenant.TenantId && u.Role == UserRole.Admin)
                .OrderBy(u => u.Name)
                .Select(u => new AdminUserResponse(u.Id, u.Name, u.Email, u.Status, u.CreatedAt, u.ReceivesSubscriptionReminders))
                .ToListAsync();
            return Results.Ok(admins);
        });

        group.MapPost("/", async (CreateAdminUserRequest req, AppDbContext db, TenantContext tenant) =>
        {
            var email = req.Email.ToLowerInvariant();
            if (await db.Users.AnyAsync(u => u.TenantId == tenant.TenantId && u.Email == email))
                return Results.Conflict("Email already registered.");

            if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
                return Results.BadRequest("Password must be at least 6 characters.");

            var user = new User
            {
                TenantId = tenant.TenantId,
                Name = req.Name,
                Email = email,
                Role = UserRole.Admin,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password)
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            return Results.Created($"/api/admins/{user.Id}",
                new AdminUserResponse(user.Id, user.Name, user.Email, user.Status, user.CreatedAt, user.ReceivesSubscriptionReminders));
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateAdminUserRequest req, AppDbContext db, TenantContext tenant, ClaimsPrincipal principal) =>
        {
            var callerId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var user = await db.Users.FirstOrDefaultAsync(u =>
                u.Id == id && u.TenantId == tenant.TenantId && u.Role == UserRole.Admin);
            if (user is null) return Results.NotFound();

            // Prevent admin from deactivating themselves
            if (user.Id == callerId && req.Status != StudentStatus.Active)
                return Results.BadRequest("You cannot deactivate your own account.");

            user.Name = req.Name;
            user.Status = req.Status;
            user.ReceivesSubscriptionReminders = req.ReceivesSubscriptionReminders;
            await db.SaveChangesAsync();

            return Results.Ok(new AdminUserResponse(user.Id, user.Name, user.Email, user.Status, user.CreatedAt, user.ReceivesSubscriptionReminders));
        });

        group.MapPost("/{id:guid}/reset-password", async (Guid id, ResetAdminPasswordRequest req, AppDbContext db, TenantContext tenant) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u =>
                u.Id == id && u.TenantId == tenant.TenantId && u.Role == UserRole.Admin);
            if (user is null) return Results.NotFound();

            if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 6)
                return Results.BadRequest("Password must be at least 6 characters.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });
    }
}
