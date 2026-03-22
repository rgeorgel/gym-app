using GymApp.Domain.Enums;

namespace GymApp.Api.DTOs;

public record SessionResponse(
    Guid Id,
    Guid? ScheduleId,       // null for BeautySalon sessions
    DateOnly Date,
    TimeOnly StartTime,
    int DurationMinutes,
    Guid? ClassTypeId,
    string ClassTypeName,
    string ClassTypeColor,
    decimal? ClassTypePrice,
    ModalityType ModalityType,
    string? InstructorName,
    int Capacity,
    int SlotsAvailable,
    SessionStatus Status,
    int BookingsCount,
    Guid LocationId
);

public record CancelSessionRequest(string? Reason);
