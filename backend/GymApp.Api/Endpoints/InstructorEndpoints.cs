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
                .Where(i => i.TenantId == tenant.TenantId)
                .OrderBy(i => i.User.Name)
                .Select(i => new InstructorResponse(i.Id, i.User.Name, i.User.Email, i.User.Phone, i.Bio, i.Specialties, i.User.PhotoUrl))
                .ToListAsync();
            return Results.Ok(instructors);
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, TenantContext tenant) =>
        {
            var i = await db.Instructors.AsNoTracking()
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.TenantId);
            return i is null ? Results.NotFound() :
                Results.Ok(new InstructorResponse(i.Id, i.User.Name, i.User.Email, i.User.Phone, i.Bio, i.Specialties, i.User.PhotoUrl));
        });

        group.MapPost("/", async (CreateInstructorRequest req, AppDbContext db, TenantContext tenant) =>
        {
            var email = req.Email.ToLowerInvariant();
            var user = await db.Users.FirstOrDefaultAsync(u => u.TenantId == tenant.TenantId && u.Email == email)
                ?? new User
                {
                    TenantId = tenant.TenantId,
                    Name = req.Name,
                    Email = email,
                    Phone = req.Phone,
                    Role = UserRole.Admin,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString())
                };

            if (user.Id == Guid.Empty)
                db.Users.Add(user);

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
                new InstructorResponse(instructor.Id, user.Name, user.Email, user.Phone, instructor.Bio, instructor.Specialties, user.PhotoUrl));
        }).RequireAuthorization("AdminOrAbove");

        group.MapPut("/{id:guid}", async (Guid id, UpdateInstructorRequest req, AppDbContext db, TenantContext tenant) =>
        {
            var instructor = await db.Instructors
                .Include(i => i.User)
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.TenantId);
            if (instructor is null) return Results.NotFound();

            instructor.Bio = req.Bio;
            instructor.Specialties = req.Specialties;
            await db.SaveChangesAsync();

            return Results.Ok(new InstructorResponse(instructor.Id, instructor.User.Name, instructor.User.Email,
                instructor.User.Phone, instructor.Bio, instructor.Specialties, instructor.User.PhotoUrl));
        }).RequireAuthorization("AdminOrAbove");
    }
}
