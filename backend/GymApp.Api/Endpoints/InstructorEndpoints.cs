using GymApp.Api.DTOs;
using GymApp.Domain.Entities;
using GymApp.Domain.Enums;
using GymApp.Infra.Data;
using GymApp.Infra.Services;
using Microsoft.EntityFrameworkCore;

namespace GymApp.Api.Endpoints;

public static class InstructorEndpoints
{
    public static void MapInstructorEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/instructors").RequireAuthorization();

        group.MapGet("/", async (AppDbContext db, TenantContext tenant) =>
        {
            var instructors = await db.Instructors.AsNoTracking()
                .Include(i => i.User)
                .Include(i => i.Services)
                .Where(i => i.TenantId == tenant.TenantId)
                .OrderBy(i => i.User.Name)
                .Select(i => new InstructorResponse(i.Id, i.User.Name, i.User.Email, i.User.Phone, i.Bio, i.Specialties, i.User.PhotoUrl, i.Services.Select(s => s.ClassTypeId).ToList()))
                .ToListAsync();
            return Results.Ok(instructors);
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, TenantContext tenant) =>
        {
            var i = await db.Instructors.AsNoTracking()
                .Include(x => x.User)
                .Include(x => x.Services)
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.TenantId);
            return i is null ? Results.NotFound() :
                Results.Ok(new InstructorResponse(i.Id, i.User.Name, i.User.Email, i.User.Phone, i.Bio, i.Specialties, i.User.PhotoUrl, i.Services.Select(s => s.ClassTypeId).ToList()));
        });

        group.MapPost("/", async (CreateInstructorRequest req, AppDbContext db, TenantContext tenant) =>
        {
            var email = req.Email.ToLowerInvariant();
            var user = await db.Users.FirstOrDefaultAsync(u => u.TenantId == tenant.TenantId && u.Email == email);
            if (user is null)
            {
                user = new User
                {
                    TenantId = tenant.TenantId,
                    Name = req.Name,
                    Email = email,
                    Phone = req.Phone,
                    Role = UserRole.Admin,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString())
                };
                db.Users.Add(user);
            }

            if (req.PhotoUrl is not null) user.PhotoUrl = req.PhotoUrl.Trim();

            var instructor = new Instructor
            {
                TenantId = tenant.TenantId,
                UserId = user.Id,
                Bio = req.Bio,
                Specialties = req.Specialties
            };
            db.Instructors.Add(instructor);
            await db.SaveChangesAsync();

            return Results.Created($"/api/instructors/{instructor.Id}",
                new InstructorResponse(instructor.Id, user.Name, user.Email, user.Phone, instructor.Bio, instructor.Specialties, user.PhotoUrl, new List<Guid>()));
        }).RequireAuthorization("AdminOrAbove");

        group.MapPut("/{id:guid}", async (Guid id, UpdateInstructorRequest req, AppDbContext db, TenantContext tenant) =>
        {
            var instructor = await db.Instructors
                .Include(i => i.User)
                .Include(i => i.Services)
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.TenantId);
            if (instructor is null) return Results.NotFound();

            instructor.Bio = req.Bio;
            instructor.Specialties = req.Specialties;
            instructor.User.Name = req.Name;
            instructor.User.Phone = req.Phone;
            if (req.PhotoUrl is not null) instructor.User.PhotoUrl = req.PhotoUrl.Trim();
            await db.SaveChangesAsync();

            return Results.Ok(new InstructorResponse(instructor.Id, instructor.User.Name, instructor.User.Email,
                instructor.User.Phone, instructor.Bio, instructor.Specialties, instructor.User.PhotoUrl,
                instructor.Services.Select(s => s.ClassTypeId).ToList()));
        }).RequireAuthorization("AdminOrAbove");

        group.MapPut("/{id:guid}/services", async (Guid id, UpdateInstructorServicesRequest req, AppDbContext db, TenantContext tenant) =>
        {
            var instructor = await db.Instructors
                .Include(i => i.Services)
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.TenantId);
            if (instructor is null) return Results.NotFound();

            // Remove all current associations, add new ones
            instructor.Services.Clear();
            foreach (var svcId in req.ServiceIds.Distinct())
            {
                var exists = await db.ClassTypes.AnyAsync(ct => ct.Id == svcId && ct.TenantId == tenant.TenantId);
                if (exists) instructor.Services.Add(new InstructorService { InstructorId = id, ClassTypeId = svcId });
            }
            await db.SaveChangesAsync();
            return Results.Ok(instructor.Services.Select(s => s.ClassTypeId).ToList());
        }).RequireAuthorization("AdminOrAbove");

        group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, TenantContext tenant) =>
        {
            var instructor = await db.Instructors
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.TenantId);
            if (instructor is null) return Results.NotFound();
            db.Instructors.Remove(instructor);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("AdminOrAbove");
    }
}
