using GymApp.Domain.Enums;

namespace GymApp.Api.DTOs;

// Gym booking: book into an existing pre-generated session
public record CreateBookingRequest(Guid SessionId, Guid StudentId, Guid? PackageItemId);

// BeautySalon booking: pick a date/time/service — session is created on demand
public record CreateSalonBookingRequest(DateOnly Date, TimeOnly StartTime, Guid ServiceId, Guid StudentId);

public record CancelBookingRequest(string? Reason);

public record BookingResponse(
    Guid Id,
    Guid SessionId,
    DateOnly SessionDate,
    TimeOnly SessionStartTime,
    string ClassTypeName,
    Guid StudentId,
    string StudentName,
    BookingStatus Status,
    DateTime? CheckedInAt,
    DateTime CreatedAt,
    Guid LocationId
);
