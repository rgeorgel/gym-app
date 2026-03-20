using System.Security.Claims;
using GymApp.Api.DTOs;
using GymApp.Domain.Entities;
using GymApp.Domain.Enums;
using GymApp.Domain.Interfaces;
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
                .Include(b => b.Session).ThenInclude(s => s.ClassType)
                .Where(b => b.StudentId == callerId && b.Session.TenantId == tenant.TenantId)
                .OrderByDescending(b => b.Session.Date)
                .Select(b => new BookingResponse(
                    b.Id, b.SessionId, b.Session.Date, b.Session.StartTime,
                    b.Session.ClassType != null ? b.Session.ClassType.Name : "",
                    b.StudentId, b.Student.Name,
                    b.Status, b.CheckedInAt, b.CreatedAt))
                .ToListAsync();
            return Results.Ok(bookings);
        });

        // Gym booking — book into an existing pre-generated session
        group.MapPost("/", async (CreateBookingRequest req, AppDbContext db, TenantContext tenant, ClaimsPrincipal principal, IEmailService email) =>
        {
            var role = principal.FindFirstValue(ClaimTypes.Role);
            var callerId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (role == nameof(UserRole.Student) && req.StudentId != callerId)
                return Results.Forbid();

            var session = await db.Sessions
                .Include(s => s.ClassType)
                .Include(s => s.Tenant)
                .Include(s => s.Bookings)
                .FirstOrDefaultAsync(s => s.Id == req.SessionId && s.TenantId == tenant.TenantId);

            if (session is null) return Results.NotFound("Session not found.");
            if (session.Status == SessionStatus.Cancelled) return Results.BadRequest("Session is cancelled.");
            if (session.SlotsAvailable <= 0) return Results.BadRequest("No slots available.");

            if (session.Bookings.Any(b => b.StudentId == req.StudentId && b.Status != BookingStatus.Cancelled))
                return Results.Conflict("Student already booked for this session.");

            PackageItem? packageItem = null;
            if (req.PackageItemId.HasValue)
            {
                packageItem = await db.PackageItems
                    .Include(pi => pi.Package)
                    .Include(pi => pi.ClassType)
                    .FirstOrDefaultAsync(pi =>
                        pi.Id == req.PackageItemId.Value &&
                        pi.Package.TenantId == tenant.TenantId &&
                        pi.Package.StudentId == req.StudentId &&
                        pi.Package.IsActive);

                if (packageItem is null) return Results.NotFound("Package item not found.");
                if (packageItem.UsedCredits >= packageItem.TotalCredits)
                    return Results.BadRequest($"No credits left for {packageItem.ClassType.Name}.");
                if (packageItem.Package.ExpiresAt.HasValue && packageItem.Package.ExpiresAt < DateOnly.FromDateTime(DateTime.UtcNow))
                    return Results.BadRequest("Package has expired.");
                if (packageItem.ClassTypeId != session.ClassTypeId)
                    return Results.BadRequest("Package item class type doesn't match session class type.");
            }

            var booking = new Booking
            {
                SessionId = req.SessionId,
                StudentId = req.StudentId,
                PackageItemId = req.PackageItemId,
                Status = BookingStatus.Confirmed
            };
            db.Bookings.Add(booking);
            if (packageItem is not null) packageItem.UsedCredits++;
            session.SlotsAvailable--;

            await db.SaveChangesAsync();

            var student = await db.Users.FindAsync(req.StudentId);
            if (student is not null)
            {
                var sessionDateTime = session.Date.ToDateTime(session.StartTime);
                var serviceName = session.ClassType?.Name ?? "";
                var tenantName = session.Tenant?.Name ?? "";
                _ = email.SendBookingConfirmationAsync(student.Email, student.Name, serviceName, tenantName, sessionDateTime);
            }

            return Results.Created($"/api/bookings/{booking.Id}", booking.Id);
        });

        // BeautySalon booking — pick date + time + service, session created on demand
        group.MapPost("/salon", async (CreateSalonBookingRequest req, AppDbContext db, TenantContext tenant, ClaimsPrincipal principal, IEmailService email) =>
        {
            var role = principal.FindFirstValue(ClaimTypes.Role);
            var callerId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (role == nameof(UserRole.Student) && req.StudentId != callerId)
                return Results.Forbid();

            // Validate service
            var service = await db.ClassTypes
                .Include(ct => ct.Tenant)
                .FirstOrDefaultAsync(ct => ct.Id == req.ServiceId && ct.TenantId == tenant.TenantId && ct.IsActive);
            if (service is null) return Results.NotFound("Service not found.");

            int duration = service.DurationMinutes ?? 30;

            // Validate availability block covers this slot
            var weekday = (int)req.Date.DayOfWeek;
            var slotEnd = req.StartTime.Add(TimeSpan.FromMinutes(duration));
            var blockExists = await db.ProfessionalAvailability
                .AnyAsync(a =>
                    a.TenantId == tenant.TenantId &&
                    a.Weekday == weekday &&
                    a.IsActive &&
                    a.StartTime <= req.StartTime &&
                    a.EndTime >= slotEnd);

            if (!blockExists) return Results.BadRequest("No availability configured for this time slot.");

            // Check for booking conflicts in memory (EF can't translate TimeOnly arithmetic to SQL)
            var daySessions = await db.Sessions
                .Where(s =>
                    s.TenantId == tenant.TenantId &&
                    s.Date == req.Date &&
                    s.ScheduleId == null &&
                    s.Status != SessionStatus.Cancelled)
                .Select(s => new { s.StartTime, s.DurationMinutes })
                .ToListAsync();

            var newStart = req.StartTime.Hour * 60 + req.StartTime.Minute;
            var newEnd = newStart + duration;
            bool hasConflict = daySessions.Any(s =>
            {
                var sStart = s.StartTime.Hour * 60 + s.StartTime.Minute;
                var sEnd = sStart + s.DurationMinutes;
                return newStart < sEnd && newEnd > sStart;
            });
            if (hasConflict) return Results.Conflict("This time slot is already booked.");

            // Check if student already has a booking at this time
            var studentConflict = await db.Bookings
                .AnyAsync(b =>
                    b.StudentId == req.StudentId &&
                    b.Session.TenantId == tenant.TenantId &&
                    b.Session.Date == req.Date &&
                    b.Session.StartTime == req.StartTime &&
                    b.Status != BookingStatus.Cancelled);
            if (studentConflict) return Results.Conflict("You already have a booking at this time.");

            // Create the session on demand
            var session = new Session
            {
                TenantId = tenant.TenantId,
                ClassTypeId = service.Id,
                StartTime = req.StartTime,
                DurationMinutes = duration,
                Date = req.Date,
                SlotsAvailable = 0 // will be set to 0 immediately after booking (capacity = 1)
            };
            db.Sessions.Add(session);
            await db.SaveChangesAsync(); // need session ID before creating booking

            var booking = new Booking
            {
                SessionId = session.Id,
                StudentId = req.StudentId,
                Status = BookingStatus.Confirmed
            };
            db.Bookings.Add(booking);
            await db.SaveChangesAsync();

            var student = await db.Users.FindAsync(req.StudentId);
            if (student is not null)
            {
                var sessionDateTime = req.Date.ToDateTime(req.StartTime);
                _ = email.SendBookingConfirmationAsync(student.Email, student.Name, service.Name, service.Tenant?.Name ?? "", sessionDateTime);
                var admin = await db.Users
                    .Where(u => u.TenantId == tenant.TenantId && u.Role == UserRole.Admin)
                    .FirstOrDefaultAsync();
                if (admin is not null)
                    _ = email.SendNewBookingNotificationAsync(admin.Email, admin.Name, student.Name, student.Phone, service.Name, sessionDateTime);
            }

            return Results.Created($"/api/bookings/{booking.Id}", booking.Id);
        });

        group.MapDelete("/{id:guid}", async (Guid id, string? reason, AppDbContext db, TenantContext tenant,
            ClaimsPrincipal principal, IConfiguration config) =>
        {
            var role = principal.FindFirstValue(ClaimTypes.Role);
            var callerId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var booking = await db.Bookings
                .Include(b => b.Session).ThenInclude(s => s.Tenant)
                .Include(b => b.PackageItem)
                .FirstOrDefaultAsync(b => b.Id == id && b.Session.TenantId == tenant.TenantId);

            if (booking is null) return Results.NotFound();
            if (booking.Status == BookingStatus.Cancelled) return Results.Conflict("Already cancelled.");

            if (role == nameof(UserRole.Student) && booking.StudentId != callerId)
                return Results.Forbid();

            booking.Status = BookingStatus.Cancelled;
            booking.CancelledAt = DateTime.UtcNow;
            booking.CancellationReason = reason;

            var sessionDateTime = booking.Session.Date.ToDateTime(booking.Session.StartTime);
            var hoursLimit = booking.Session.Tenant?.CancellationHoursLimit ?? 2;
            var cancellationDeadline = sessionDateTime.AddHours(-hoursLimit);

            if (booking.PackageItem is not null)
            {
                // Gym: refund credit and free slot only within cancellation window
                if (DateTime.UtcNow <= cancellationDeadline)
                {
                    booking.PackageItem.UsedCredits = Math.Max(0, booking.PackageItem.UsedCredits - 1);
                    booking.Session.SlotsAvailable++;
                }
            }
            else
            {
                // BeautySalon: always free the slot on cancellation
                booking.Session.SlotsAvailable++;
            }

            await db.SaveChangesAsync();

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
                .Include(b => b.Session)
                .FirstOrDefaultAsync(b => b.Id == id && b.Session.TenantId == tenant.TenantId);

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
                .Where(w => w.SessionId == sessionId && w.Session.TenantId == tenant.TenantId)
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
                .FirstOrDefaultAsync(s => s.Id == req.SessionId && s.TenantId == tenant.TenantId);

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
