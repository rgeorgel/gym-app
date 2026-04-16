namespace GymApp.Api.DTOs;

// GET /tenants/by-instance
public record BotTenantResponse(Guid TenantId, string Name, string Slug);

// GET /tenants/:id/services
public record BotServiceItem(Guid Id, string Name, int? DurationMinutes, decimal? Price);
public record BotServicesResponse(IList<BotServiceItem> Services);

// GET /tenants/:id/professionals
public record BotProfessionalItem(Guid Id, string Name, string? PhotoUrl, string? Specialties);

// GET /tenants/:id/availability
public record BotSlotItem(string Time, Guid ProfessionalId, string ProfessionalName);
public record BotAvailabilityResponse(string Date, IList<BotSlotItem> Slots);

// POST /tenants/:id/appointments
public record BotCreateAppointmentRequest(
    Guid ServiceId,
    Guid ProfessionalId,
    DateTime Datetime,
    string ClientName,
    string ClientPhone
);

public record BotAppointmentResponse(
    Guid AppointmentId,
    Guid SessionId,
    string Date,
    string Time,
    int DurationMinutes,
    string ServiceName,
    decimal? ServicePrice,
    string ClientName,
    string ClientPhone,
    string ProfessionalName,
    string Status
);

// PATCH /tenants/:id/appointments/:appointmentId
public record BotRescheduleRequest(
    DateTime NewDatetime,
    Guid? ProfessionalId = null
);
