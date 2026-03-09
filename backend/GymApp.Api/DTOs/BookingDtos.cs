using GymApp.Domain.Enums;

namespace GymApp.Api.DTOs;

public record CreateBookingRequest(Guid SessionId, Guid StudentId, Guid PackageItemId);
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
    DateTime CreatedAt
);
