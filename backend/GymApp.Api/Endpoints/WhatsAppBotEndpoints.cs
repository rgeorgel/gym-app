using GymApp.Api.DTOs;
using GymApp.Domain.Entities;
using GymApp.Domain.Enums;
using GymApp.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace GymApp.Api.Endpoints;

public static class WhatsAppBotEndpoints
{
    public static void MapWhatsAppBotEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/bot").AllowAnonymous();

        // ── Resolve tenant by Evolution instance name ─────────────────────────
        // GET /api/bot/tenants/by-instance?instance=xxx
        group.MapGet("/tenants/by-instance", async (string instance, AppDbContext db) =>
        {
            var tenant = await db.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.WhatsAppInstanceName == instance && t.IsActive && t.WhatsAppAutoServiceEnabled);

            if (tenant is null) return Results.NotFound();

            return Results.Ok(new BotTenantResponse(tenant.Id, tenant.Name, tenant.Slug));
        });

        // ── Services ──────────────────────────────────────────────────────────
        // GET /api/bot/tenants/:tenant_id/services
        group.MapGet("/tenants/{tenantId:guid}/services", async (Guid tenantId, AppDbContext db) =>
        {
            if (!await TenantExistsAndEnabled(db, tenantId))
                return Results.NotFound();

            var services = await db.ClassTypes.AsNoTracking()
                .Where(ct => ct.TenantId == tenantId && ct.IsActive)
                .OrderBy(ct => ct.Name)
                .Select(ct => new BotServiceItem(ct.Id, ct.Name, ct.DurationMinutes, ct.Price))
                .ToListAsync();

            return Results.Ok(new BotServicesResponse(services));
        });

        // ── Professionals ─────────────────────────────────────────────────────
        // GET /api/bot/tenants/:tenant_id/professionals?service_id=xxx
        group.MapGet("/tenants/{tenantId:guid}/professionals", async (Guid tenantId, Guid? serviceId, AppDbContext db) =>
        {
            if (!await TenantExistsAndEnabled(db, tenantId))
                return Results.NotFound();

            var idsWithAvailability = await db.ProfessionalAvailability.AsNoTracking()
                .Where(a => a.TenantId == tenantId && a.IsActive && a.InstructorId != null)
                .Select(a => a.InstructorId!.Value)
                .Distinct()
                .ToListAsync();

            if (idsWithAvailability.Count == 0)
                return Results.Ok(Array.Empty<BotProfessionalItem>());

            var query = db.Instructors.AsNoTracking()
                .Include(i => i.User)
                .Include(i => i.Services)
                .Where(i => i.TenantId == tenantId && idsWithAvailability.Contains(i.Id));

            if (serviceId.HasValue)
                query = query.Where(i => !i.Services.Any() || i.Services.Any(s => s.ClassTypeId == serviceId.Value));

            var professionals = await query
                .OrderBy(i => i.User.Name)
                .Select(i => new BotProfessionalItem(i.Id, i.User.Name, i.User.PhotoUrl, i.Specialties))
                .ToListAsync();

            return Results.Ok(professionals);
        });

        // ── Availability ──────────────────────────────────────────────────────
        // GET /api/bot/tenants/:tenant_id/availability?service_id=xxx&date=2026-04-17&professional_id=xxx
        group.MapGet("/tenants/{tenantId:guid}/availability", async (
            Guid tenantId,
            Guid serviceId,
            DateOnly date,
            Guid? professionalId,
            AppDbContext db) =>
        {
            if (!await TenantExistsAndEnabled(db, tenantId))
                return Results.NotFound();

            var service = await db.ClassTypes.AsNoTracking()
                .FirstOrDefaultAsync(ct => ct.Id == serviceId && ct.TenantId == tenantId && ct.IsActive);

            if (service is null) return Results.NotFound("Service not found.");

            int duration = service.DurationMinutes ?? 30;
            var weekday = (int)date.DayOfWeek;

            // Check vacation blocks
            var onVacation = await db.VacationBlocks.AsNoTracking()
                .AnyAsync(vb => vb.TenantId == tenantId && vb.StartDate <= date && vb.EndDate >= date);
            if (onVacation)
                return Results.Ok(new BotAvailabilityResponse(date.ToString("yyyy-MM-dd"), []));

            // Time blocks
            var timeBlocks = await db.TimeBlocks.AsNoTracking()
                .Where(tb => tb.TenantId == tenantId && tb.Date == date)
                .Select(tb => new { tb.StartTime, tb.EndTime })
                .ToListAsync();

            // Load professionals with availability on that weekday
            var availQuery = db.ProfessionalAvailability.AsNoTracking()
                .Include(a => a.Instructor).ThenInclude(i => i!.User)
                .Where(a => a.TenantId == tenantId && a.Weekday == weekday && a.IsActive && a.InstructorId != null);

            if (professionalId.HasValue)
                availQuery = availQuery.Where(a => a.InstructorId == professionalId.Value);

            var availBlocks = await availQuery.ToListAsync();

            // Existing occupied sessions that day
            var existingSessionsQuery = db.Sessions.AsNoTracking()
                .Where(s =>
                    s.TenantId == tenantId &&
                    s.Date == date &&
                    s.ScheduleId == null &&
                    s.Status != SessionStatus.Cancelled &&
                    s.SlotsAvailable <= 0);

            if (professionalId.HasValue)
                existingSessionsQuery = existingSessionsQuery.Where(s => s.InstructorId == professionalId.Value);

            var existingSessions = await existingSessionsQuery
                .Select(s => new { s.StartTime, s.DurationMinutes, s.InstructorId })
                .ToListAsync();

            // Generate slots per professional availability block
            var slots = new List<BotSlotItem>();
            var seen = new HashSet<(TimeOnly, Guid)>();

            foreach (var block in availBlocks)
            {
                if (block.InstructorId is null || block.Instructor?.User is null) continue;

                var profId = block.InstructorId.Value;
                var profName = block.Instructor.User.Name;

                // Existing occupied sessions for this professional
                var occupiedForProf = existingSessions
                    .Where(s => s.InstructorId == profId)
                    .ToList();

                var slot = block.StartTime;
                while (slot.Add(TimeSpan.FromMinutes(duration)) <= block.EndTime)
                {
                    var slotKey = (slot, profId);
                    if (!seen.Contains(slotKey))
                    {
                        var newStart = slot.Hour * 60 + slot.Minute;
                        var newEnd = newStart + duration;

                        var blockedBySession = occupiedForProf.Any(s =>
                        {
                            var sStart = s.StartTime.Hour * 60 + s.StartTime.Minute;
                            var sEnd = sStart + s.DurationMinutes;
                            return newStart < sEnd && newEnd > sStart;
                        });

                        var blockedByTimeBlock = timeBlocks.Any(tb =>
                        {
                            var tbStart = tb.StartTime.Hour * 60 + tb.StartTime.Minute;
                            var tbEnd   = tb.EndTime.Hour * 60 + tb.EndTime.Minute;
                            return newStart < tbEnd && newEnd > tbStart;
                        });

                        if (!blockedBySession && !blockedByTimeBlock)
                        {
                            slots.Add(new BotSlotItem(slot.ToString("HH:mm"), profId, profName));
                            seen.Add(slotKey);
                        }
                    }
                    slot = slot.Add(TimeSpan.FromMinutes(duration));
                }
            }

            slots.Sort((a, b) => string.Compare(a.Time, b.Time, StringComparison.Ordinal));

            return Results.Ok(new BotAvailabilityResponse(date.ToString("yyyy-MM-dd"), slots));
        });

        // ── Create appointment ────────────────────────────────────────────────
        // POST /api/bot/tenants/:tenant_id/appointments
        group.MapPost("/tenants/{tenantId:guid}/appointments", async (
            Guid tenantId,
            BotCreateAppointmentRequest req,
            AppDbContext db) =>
        {
            var tenant = await db.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive && t.WhatsAppAutoServiceEnabled);
            if (tenant is null) return Results.NotFound();

            // Validate service
            var service = await db.ClassTypes.AsNoTracking()
                .FirstOrDefaultAsync(ct => ct.Id == req.ServiceId && ct.TenantId == tenantId && ct.IsActive);
            if (service is null) return Results.NotFound("Service not found.");

            // Validate professional
            var instructor = await db.Instructors.AsNoTracking()
                .Include(i => i.User)
                .FirstOrDefaultAsync(i => i.Id == req.ProfessionalId && i.TenantId == tenantId);
            if (instructor is null) return Results.NotFound("Professional not found.");

            var requestedDate = DateOnly.FromDateTime(req.Datetime);
            var requestedTime = TimeOnly.FromDateTime(req.Datetime);
            int duration = service.DurationMinutes ?? 30;

            // Verify the slot is still available
            var isOccupied = await db.Sessions.AnyAsync(s =>
                s.TenantId == tenantId &&
                s.Date == requestedDate &&
                s.ScheduleId == null &&
                s.InstructorId == req.ProfessionalId &&
                s.StartTime == requestedTime &&
                s.Status != SessionStatus.Cancelled &&
                s.SlotsAvailable <= 0);

            if (isOccupied)
                return Results.Conflict("This time slot is no longer available.");

            // Find or create client user (identified by phone)
            var phone = req.ClientPhone.Trim();
            var botEmail = $"{phone}@whatsapp.bot";

            var clientUser = await db.Users
                .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Phone == phone);

            if (clientUser is null)
            {
                // Check by bot email as well (in case of previous partial creation)
                clientUser = await db.Users
                    .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Email == botEmail);
            }

            if (clientUser is null)
            {
                clientUser = new User
                {
                    TenantId = tenantId,
                    Name = req.ClientName.Trim(),
                    Email = botEmail,
                    Phone = phone,
                    PasswordHash = string.Empty,
                    Role = UserRole.Student,
                    Status = StudentStatus.Active
                };
                db.Users.Add(clientUser);
                await db.SaveChangesAsync();
            }

            // Get default location for the tenant
            var locationId = await db.Locations.AsNoTracking()
                .Where(l => l.TenantId == tenantId && l.IsMain)
                .Select(l => l.Id)
                .FirstOrDefaultAsync();

            if (locationId == Guid.Empty)
            {
                locationId = await db.Locations.AsNoTracking()
                    .Where(l => l.TenantId == tenantId)
                    .Select(l => l.Id)
                    .FirstOrDefaultAsync();
            }

            // Find or create session for this slot
            var session = await db.Sessions
                .Include(s => s.Bookings)
                .FirstOrDefaultAsync(s =>
                    s.TenantId == tenantId &&
                    s.Date == requestedDate &&
                    s.StartTime == requestedTime &&
                    s.InstructorId == req.ProfessionalId &&
                    s.ClassTypeId == req.ServiceId &&
                    s.ScheduleId == null &&
                    s.Status != SessionStatus.Cancelled);

            if (session is null)
            {
                session = new Session
                {
                    TenantId = tenantId,
                    ClassTypeId = req.ServiceId,
                    LocationId = locationId,
                    InstructorId = req.ProfessionalId,
                    StartTime = requestedTime,
                    DurationMinutes = duration,
                    Date = requestedDate,
                    SlotsAvailable = 1
                };
                db.Sessions.Add(session);
                await db.SaveChangesAsync();
            }
            else if (session.SlotsAvailable <= 0)
            {
                return Results.Conflict("This time slot is no longer available.");
            }

            // Create booking
            var booking = new Booking
            {
                SessionId = session.Id,
                StudentId = clientUser.Id,
                Status = BookingStatus.Confirmed
            };
            db.Bookings.Add(booking);

            session.SlotsAvailable -= 1;
            await db.SaveChangesAsync();

            return Results.Created($"/api/bot/tenants/{tenantId}/appointments/{booking.Id}",
                new BotAppointmentResponse(
                    booking.Id,
                    session.Id,
                    session.Date.ToString("yyyy-MM-dd"),
                    session.StartTime.ToString("HH:mm"),
                    session.DurationMinutes,
                    service.Name,
                    service.Price,
                    clientUser.Name,
                    clientUser.Phone ?? phone,
                    instructor.User.Name,
                    booking.Status.ToString()));
        });

        // ── List appointments by phone ────────────────────────────────────────
        // GET /api/bot/tenants/:tenant_id/appointments?phone=xxx
        group.MapGet("/tenants/{tenantId:guid}/appointments", async (
            Guid tenantId,
            string phone,
            AppDbContext db) =>
        {
            if (!await TenantExistsAndEnabled(db, tenantId))
                return Results.NotFound();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var bookings = await db.Bookings.AsNoTracking()
                .Include(b => b.Session).ThenInclude(s => s.ClassType)
                .Include(b => b.Session).ThenInclude(s => s.Instructor).ThenInclude(i => i!.User)
                .Include(b => b.Student)
                .Where(b =>
                    b.Session.TenantId == tenantId &&
                    b.Student.Phone == phone.Trim() &&
                    b.Session.Date >= today &&
                    b.Status != BookingStatus.Cancelled)
                .OrderBy(b => b.Session.Date).ThenBy(b => b.Session.StartTime)
                .Select(b => new BotAppointmentResponse(
                    b.Id,
                    b.SessionId,
                    b.Session.Date.ToString("yyyy-MM-dd"),
                    b.Session.StartTime.ToString("HH:mm"),
                    b.Session.DurationMinutes,
                    b.Session.ClassType != null ? b.Session.ClassType.Name : "",
                    b.Session.ClassType != null ? b.Session.ClassType.Price : null,
                    b.Student.Name,
                    b.Student.Phone ?? phone,
                    b.Session.Instructor != null ? b.Session.Instructor.User.Name : "",
                    b.Status.ToString()))
                .ToListAsync();

            return Results.Ok(bookings);
        });

        // ── Cancel appointment ────────────────────────────────────────────────
        // DELETE /api/bot/tenants/:tenant_id/appointments/:appointment_id
        group.MapDelete("/tenants/{tenantId:guid}/appointments/{appointmentId:guid}", async (
            Guid tenantId,
            Guid appointmentId,
            AppDbContext db) =>
        {
            if (!await TenantExistsAndEnabled(db, tenantId))
                return Results.NotFound();

            var booking = await db.Bookings
                .Include(b => b.Session)
                .FirstOrDefaultAsync(b => b.Id == appointmentId && b.Session.TenantId == tenantId);

            if (booking is null) return Results.NotFound();
            if (booking.Status == BookingStatus.Cancelled) return Results.Conflict("Already cancelled.");

            booking.Status = BookingStatus.Cancelled;
            booking.CancelledAt = DateTime.UtcNow;

            // Restore slot
            if (booking.Session.SlotsAvailable >= 0)
                booking.Session.SlotsAvailable += 1;

            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ── Reschedule appointment ────────────────────────────────────────────
        // PATCH /api/bot/tenants/:tenant_id/appointments/:appointment_id
        group.MapPatch("/tenants/{tenantId:guid}/appointments/{appointmentId:guid}", async (
            Guid tenantId,
            Guid appointmentId,
            BotRescheduleRequest req,
            AppDbContext db) =>
        {
            if (!await TenantExistsAndEnabled(db, tenantId))
                return Results.NotFound();

            var booking = await db.Bookings
                .Include(b => b.Session).ThenInclude(s => s.ClassType)
                .Include(b => b.Session).ThenInclude(s => s.Instructor).ThenInclude(i => i!.User)
                .Include(b => b.Student)
                .FirstOrDefaultAsync(b => b.Id == appointmentId && b.Session.TenantId == tenantId);

            if (booking is null) return Results.NotFound();
            if (booking.Status == BookingStatus.Cancelled) return Results.Conflict("Cannot reschedule a cancelled appointment.");

            var oldSession = booking.Session;
            var serviceId = oldSession.ClassTypeId;
            var professionalId = req.ProfessionalId ?? oldSession.InstructorId;
            var newDate = DateOnly.FromDateTime(req.NewDatetime);
            var newTime = TimeOnly.FromDateTime(req.NewDatetime);
            int duration = oldSession.DurationMinutes;

            // Verify new slot is available
            var isOccupied = await db.Sessions.AnyAsync(s =>
                s.TenantId == tenantId &&
                s.Date == newDate &&
                s.ScheduleId == null &&
                s.InstructorId == professionalId &&
                s.StartTime == newTime &&
                s.Status != SessionStatus.Cancelled &&
                s.SlotsAvailable <= 0);

            if (isOccupied)
                return Results.Conflict("The new time slot is not available.");

            // Cancel old booking / restore old session slot
            booking.Status = BookingStatus.Cancelled;
            booking.CancelledAt = DateTime.UtcNow;
            if (oldSession.SlotsAvailable >= 0) oldSession.SlotsAvailable += 1;

            // Find or create new session
            var newSession = await db.Sessions
                .FirstOrDefaultAsync(s =>
                    s.TenantId == tenantId &&
                    s.Date == newDate &&
                    s.StartTime == newTime &&
                    s.InstructorId == professionalId &&
                    s.ClassTypeId == serviceId &&
                    s.ScheduleId == null &&
                    s.Status != SessionStatus.Cancelled);

            if (newSession is null)
            {
                newSession = new Session
                {
                    TenantId = tenantId,
                    ClassTypeId = serviceId,
                    LocationId = oldSession.LocationId,
                    InstructorId = professionalId,
                    StartTime = newTime,
                    DurationMinutes = duration,
                    Date = newDate,
                    SlotsAvailable = 1
                };
                db.Sessions.Add(newSession);
                await db.SaveChangesAsync();
            }
            else if (newSession.SlotsAvailable <= 0)
            {
                return Results.Conflict("The new time slot is not available.");
            }

            // Create new booking
            var newBooking = new Booking
            {
                SessionId = newSession.Id,
                StudentId = booking.StudentId,
                Status = BookingStatus.Confirmed
            };
            db.Bookings.Add(newBooking);
            newSession.SlotsAvailable -= 1;

            await db.SaveChangesAsync();

            // Load instructor info for response
            var instructor = await db.Instructors.AsNoTracking()
                .Include(i => i.User)
                .FirstOrDefaultAsync(i => i.Id == professionalId);

            var service = await db.ClassTypes.AsNoTracking()
                .FirstOrDefaultAsync(ct => ct.Id == serviceId);

            return Results.Ok(new BotAppointmentResponse(
                newBooking.Id,
                newSession.Id,
                newSession.Date.ToString("yyyy-MM-dd"),
                newSession.StartTime.ToString("HH:mm"),
                newSession.DurationMinutes,
                service?.Name ?? "",
                service?.Price,
                booking.Student.Name,
                booking.Student.Phone ?? "",
                instructor?.User.Name ?? "",
                newBooking.Status.ToString()));
        });
    }

    private static async Task<bool> TenantExistsAndEnabled(AppDbContext db, Guid tenantId) =>
        await db.Tenants.AsNoTracking()
            .AnyAsync(t => t.Id == tenantId && t.IsActive && t.WhatsAppAutoServiceEnabled);
}
