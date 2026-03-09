using GymApp.Domain.Enums;

namespace GymApp.Api.DTOs;

public record SessionResponse(
    Guid Id,
    Guid ScheduleId,
    DateOnly Date,
    TimeOnly StartTime,
    int DurationMinutes,
    string ClassTypeName,
    string ClassTypeColor,
    ModalityType ModalityType,
    string? InstructorName,
    int Capacity,
    int SlotsAvailable,
    SessionStatus Status,
    int BookingsCount
);

public record CancelSessionRequest(string? Reason);
