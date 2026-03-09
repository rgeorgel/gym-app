using System.Security.Claims;
using GymApp.Api.DTOs;
using GymApp.Domain.Entities;
using GymApp.Domain.Enums;
using GymApp.Infra.Data;
using GymApp.Infra.Services;
using Microsoft.EntityFrameworkCore;

namespace GymApp.Api.Endpoints;

public static class BookingEndpoints
{
    public static void MapBookingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/bookings").RequireAuthorization();

        group.MapGet("/", async (AppDbContext db, TenantContext tenant, ClaimsPrincipal principal) =>
        {
            var callerId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var bookings = await db.Bookings.AsNoTracking()
                .Include(b => b.Session).ThenInclude(s => s.Schedule).ThenInclude(s => s.ClassType)
                .Where(b => b.StudentId == callerId && b.Session.Schedule.TenantId == tenant.TenantId)
                .OrderByDescending(b => b.Session.Date)
                .Select(b => new BookingResponse(
                    b.Id, b.SessionId, b.Session.Date, b.Session.Schedule.StartTime,
                    b.Session.Schedule.ClassType.Name, b.StudentId, b.Student.Name,
                    b.Status, b.CheckedInAt, b.CreatedAt))
                .ToListAsync();
            return Results.Ok(bookings);
        });

        group.MapPost("/", async (CreateBookingRequest req, AppDbContext db, TenantContext tenant, ClaimsPrincipal principal) =>
        {
            var role = principal.FindFirstValue(ClaimTypes.Role);
            var callerId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // Students can only book for themselves
            if (role == nameof(UserRole.Student) && req.StudentId != callerId)
                return Results.Forbid();

            // Validate session
            var session = await db.Sessions
                .Include(s => s.Schedule)
                .Include(s => s.Bookings)
                .FirstOrDefaultAsync(s => s.Id == req.SessionId && s.Schedule.TenantId == tenant.TenantId);

            if (session is null) return Results.NotFound("Session not found.");
            if (session.Status == SessionStatus.Cancelled) return Results.BadRequest("Session is cancelled.");
            if (session.SlotsAvailable <= 0) return Results.BadRequest("No slots available.");

            // Check duplicate
            if (session.Bookings.Any(b => b.StudentId == req.StudentId && b.Status != BookingStatus.Cancelled))
                return Results.Conflict("Student already booked for this session.");

            // Validate package item
            var packageItem = await db.PackageItems
                .Include(pi => pi.Package)
                .Include(pi => pi.ClassType)
                .FirstOrDefaultAsync(pi =>
                    pi.Id == req.PackageItemId &&
                    pi.Package.TenantId == tenant.TenantId &&
                    pi.Package.StudentId == req.StudentId &&
                    pi.Package.IsActive);

            if (packageItem is null) return Results.NotFound("Package item not found.");
            if (packageItem.UsedCredits >= packageItem.TotalCredits)
                return Results.BadRequest($"No credits left for {packageItem.ClassType.Name}.");
            if (packageItem.Package.ExpiresAt.HasValue && packageItem.Package.ExpiresAt < DateOnly.FromDateTime(DateTime.UtcNow))
                return Results.BadRequest("Package has expired.");
            if (packageItem.ClassTypeId != session.Schedule.ClassTypeId)
                return Results.BadRequest("Package item class type doesn't match session class type.");

            // Create booking and debit credit
            var booking = new Booking
            {
                SessionId = req.SessionId,
                StudentId = req.StudentId,
                PackageItemId = req.PackageItemId,
                Status = BookingStatus.Confirmed
            };
            db.Bookings.Add(booking);
            packageItem.UsedCredits++;
            session.SlotsAvailable--;

            await db.SaveChangesAsync();

            return Results.Created($"/api/bookings/{booking.Id}", booking.Id);
        });

        group.MapDelete("/{id:guid}", async (Guid id, string? reason, AppDbContext db, TenantContext tenant,
            ClaimsPrincipal principal, IConfiguration config) =>
        {
            var role = principal.FindFirstValue(ClaimTypes.Role);
            var callerId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var booking = await db.Bookings
                .Include(b => b.Session).ThenInclude(s => s.Schedule)
                .Include(b => b.PackageItem)
                .FirstOrDefaultAsync(b => b.Id == id && b.Session.Schedule.TenantId == tenant.TenantId);

            if (booking is null) return Results.NotFound();
            if (booking.Status == BookingStatus.Cancelled) return Results.Conflict("Already cancelled.");

            // Student can only cancel own bookings
            if (role == nameof(UserRole.Student) && booking.StudentId != callerId)
                return Results.Forbid();

            booking.Status = BookingStatus.Cancelled;
            booking.CancelledAt = DateTime.UtcNow;
            booking.CancellationReason = reason;

            // Refund credit if within cancellation window
            var sessionDateTime = booking.Session.Date.ToDateTime(booking.Session.Schedule.StartTime);
            var hoursLimit = booking.Session.Schedule.Tenant?.CancellationHoursLimit ?? 2;
            var cancellationDeadline = sessionDateTime.AddHours(-hoursLimit);

            if (DateTime.UtcNow <= cancellationDeadline)
            {
                booking.PackageItem.UsedCredits = Math.Max(0, booking.PackageItem.UsedCredits - 1);
                booking.Session.SlotsAvailable++;
            }

            await db.SaveChangesAsync();

            // Promote from waiting list if slot freed
            if (booking.Session.SlotsAvailable > 0)
            {
                var nextInLine = await db.WaitingList
                    .Where(w => w.SessionId == booking.SessionId)
                    .OrderBy(w => w.Position)
                    .FirstOrDefaultAsync();

                if (nextInLine is not null)
                {
                    db.WaitingList.Remove(nextInLine);
                    // TODO: notify student via email/WhatsApp
                }
                await db.SaveChangesAsync();
            }

            return Results.NoContent();
        });

        group.MapPost("/{id:guid}/checkin", async (Guid id, AppDbContext db, TenantContext tenant) =>
        {
            var booking = await db.Bookings
                .Include(b => b.Session).ThenInclude(s => s.Schedule)
                .FirstOrDefaultAsync(b => b.Id == id && b.Session.Schedule.TenantId == tenant.TenantId);

            if (booking is null) return Results.NotFound();
            if (booking.Status != BookingStatus.Confirmed) return Results.BadRequest("Booking is not in confirmed status.");

            booking.Status = BookingStatus.CheckedIn;
            booking.CheckedInAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization("AdminOrAbove");

        group.MapGet("/waiting-list/{sessionId:guid}", async (Guid sessionId, AppDbContext db, TenantContext tenant) =>
        {
            var entries = await db.WaitingList.AsNoTracking()
                .Include(w => w.Student)
                .Where(w => w.SessionId == sessionId && w.Session.Schedule.TenantId == tenant.TenantId)
                .OrderBy(w => w.Position)
                .Select(w => new { w.Id, w.StudentId, w.Student.Name, w.Position, w.CreatedAt })
                .ToListAsync();
            return Results.Ok(entries);
        });

        group.MapPost("/waiting-list", async (
            (Guid SessionId, Guid StudentId) req,
            AppDbContext db, TenantContext tenant, ClaimsPrincipal principal) =>
        {
            var session = await db.Sessions
                .Include(s => s.Schedule)
                .FirstOrDefaultAsync(s => s.Id == req.SessionId && s.Schedule.TenantId == tenant.TenantId);

            if (session is null) return Results.NotFound();
            if (session.SlotsAvailable > 0) return Results.BadRequest("Session still has slots available.");

            var alreadyWaiting = await db.WaitingList.AnyAsync(w => w.SessionId == req.SessionId && w.StudentId == req.StudentId);
            if (alreadyWaiting) return Results.Conflict("Already in waiting list.");

            var position = await db.WaitingList.CountAsync(w => w.SessionId == req.SessionId) + 1;
            db.WaitingList.Add(new WaitingListEntry
            {
                SessionId = req.SessionId,
                StudentId = req.StudentId,
                Position = position
            });
            await db.SaveChangesAsync();
            return Results.Ok(new { Position = position });
        });
    }
}
