using System.Security.Claims;
using GymApp.Api.DTOs;
using GymApp.Api.Helpers;
using GymApp.Domain.Entities;
using GymApp.Domain.Enums;
using GymApp.Infra.Data;
using GymApp.Infra.Services;
using Microsoft.EntityFrameworkCore;

namespace GymApp.Api.Endpoints;

public static class StudentEndpoints
{
    public static void MapStudentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/students").RequireAuthorization("AdminOrAbove");

        group.MapGet("/", async (AppDbContext db, TenantContext tenant, string? search, StudentStatus? status) =>
        {
            var query = db.Users.AsNoTracking()
                .Where(u => u.TenantId == tenant.TenantId && u.Role == UserRole.Student);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(u => u.Name.Contains(search) || u.Email.Contains(search));

            if (status.HasValue)
                query = query.Where(u => u.Status == status.Value);

            var students = await query
                .OrderBy(u => u.Name)
                .Select(u => new StudentResponse(
                    u.Id, u.Name, u.Email, u.Phone, u.BirthDate, u.Status, u.PhotoUrl, u.CreatedAt,
                    u.Packages.Where(p => p.IsActive).SelectMany(p => p.Items).Sum(i => i.TotalCredits - i.UsedCredits),
                    u.Bookings
                        .Where(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn)
                        .OrderByDescending(b => b.Session.Date)
                        .Select(b => (DateOnly?)b.Session.Date)
                        .FirstOrDefault()))
                .ToListAsync();

            return Results.Ok(students);
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, TenantContext tenant) =>
        {
            var user = await db.Users.AsNoTracking()
                .Include(u => u.Packages).ThenInclude(p => p.Items)
                .Include(u => u.Bookings).ThenInclude(b => b.Session)
                .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenant.TenantId && u.Role == UserRole.Student);

            return user is null ? Results.NotFound() : Results.Ok(ToStudentResponse(user));
        });

        group.MapPost("/", async (CreateStudentRequest req, AppDbContext db, TenantContext tenant) =>
        {
            var email = req.Email.ToLowerInvariant();
            if (await db.Users.AnyAsync(u => u.TenantId == tenant.TenantId && u.Email == email))
                return Results.Conflict("Email already registered.");

            var password = !string.IsNullOrWhiteSpace(req.Password) ? req.Password : GenerateTempPassword();
            var user = new User
            {
                TenantId = tenant.TenantId,
                Name = req.Name,
                Email = email,
                Phone = req.Phone,
                BirthDate = req.BirthDate,
                HealthNotes = req.HealthNotes,
                Role = UserRole.Student,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
            };
            db.Users.Add(user);
            await PackageHelper.AssignDefaultPackageIfConfiguredAsync(db, tenant.TenantId, user.Id);
            await db.SaveChangesAsync();

            return Results.Created($"/api/students/{user.Id}", ToStudentResponse(user));
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateStudentRequest req, AppDbContext db, TenantContext tenant) =>
        {
            var user = await db.Users
                .Include(u => u.Packages).ThenInclude(p => p.Items)
                .Include(u => u.Bookings).ThenInclude(b => b.Session)
                .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenant.TenantId && u.Role == UserRole.Student);
            if (user is null) return Results.NotFound();

            user.Name = req.Name;
            user.Phone = req.Phone;
            user.BirthDate = req.BirthDate;
            user.HealthNotes = req.HealthNotes;
            user.Status = req.Status;
            await db.SaveChangesAsync();

            return Results.Ok(ToStudentResponse(user));
        });

        // Sub-routes accessible by admins OR by the student themselves
        var selfOrAdminGroup = app.MapGroup("/api/students").RequireAuthorization();

        selfOrAdminGroup.MapGet("/{id:guid}/bookings", async (Guid id, AppDbContext db, TenantContext tenant, ClaimsPrincipal principal) =>
        {
            var role = principal.FindFirstValue(ClaimTypes.Role);
            var callerId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (role == nameof(UserRole.Student) && id != callerId)
                return Results.Forbid();

            var bookings = await db.Bookings.AsNoTracking()
                .Include(b => b.Session).ThenInclude(s => s.ClassType)
                .Where(b => b.StudentId == id && b.Session.TenantId == tenant.TenantId)
                .OrderByDescending(b => b.Session.Date)
                .Select(b => new BookingResponse(
                    b.Id, b.SessionId, b.Session.Date, b.Session.StartTime,
                    b.Session.ClassType != null ? b.Session.ClassType.Name : "",
                    b.StudentId, b.Student.Name,
                    b.Status, b.CheckedInAt, b.CreatedAt))
                .ToListAsync();

            return Results.Ok(bookings);
        });

        group.MapPost("/{id:guid}/reset-link", async (Guid id, AppDbContext db, TenantContext tenant) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenant.TenantId && u.Role == UserRole.Student);
            if (user is null) return Results.NotFound();

            var token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
                .Replace("+", "-").Replace("/", "_").Replace("=", "");
            user.PasswordResetToken = token;
            user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(48);
            await db.SaveChangesAsync();

            return Results.Ok(new ResetLinkResponse(token));
        });

        selfOrAdminGroup.MapGet("/{id:guid}/packages", async (Guid id, AppDbContext db, TenantContext tenant, ClaimsPrincipal principal) =>
        {
            var role = principal.FindFirstValue(ClaimTypes.Role);
            var callerId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (role == nameof(UserRole.Student) && id != callerId)
                return Results.Forbid();

            var packages = await db.Packages.AsNoTracking()
                .Include(p => p.Items).ThenInclude(i => i.ClassType)
                .Where(p => p.StudentId == id && p.TenantId == tenant.TenantId && p.IsActive)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return Results.Ok(packages.Select(p => new PackageResponse(
                p.Id, p.Name, p.ExpiresAt, p.IsActive, p.CreatedAt,
                p.Items.Select(i => new PackageItemResponse(
                    i.Id, i.ClassTypeId, i.ClassType.Name, i.ClassType.Color,
                    i.TotalCredits, i.UsedCredits, i.TotalCredits - i.UsedCredits, i.PricePerCredit
                )).ToList()
            )));
        });
    }

    private static StudentResponse ToStudentResponse(User u)
    {
        var remaining = u.Packages
            .Where(p => p.IsActive)
            .SelectMany(p => p.Items)
            .Sum(i => i.TotalCredits - i.UsedCredits);
        var lastBooking = u.Bookings
            .Where(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn)
            .OrderByDescending(b => b.Session.Date)
            .Select(b => (DateOnly?)b.Session.Date)
            .FirstOrDefault();
        return new StudentResponse(u.Id, u.Name, u.Email, u.Phone, u.BirthDate, u.Status, u.PhotoUrl, u.CreatedAt, remaining, lastBooking);
    }

    private static string GenerateTempPassword()
    {
        const string chars = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 10).Select(s => s[random.Next(s.Length)]).ToArray());
    }
}
