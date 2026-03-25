using GymApp.Domain.Enums;

namespace GymApp.Api.DTOs;

public record AppointmentResponse(
    Guid BookingId,
    Guid SessionId,
    DateOnly Date,
    TimeOnly StartTime,
    int DurationMinutes,
    string ServiceName,
    string ServiceColor,
    decimal? ServicePrice,
    Guid ClientId,
    string ClientName,
    string? ClientPhone,
    BookingStatus Status,
    DateTime? CheckedInAt,
    DateTime CreatedAt,
    Guid? ProfessionalId,
    string? ProfessionalName
);
