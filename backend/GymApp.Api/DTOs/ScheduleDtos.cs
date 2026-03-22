namespace GymApp.Api.DTOs;

public record CreateScheduleRequest(
    Guid ClassTypeId,
    Guid? InstructorId,
    Guid LocationId,
    int Weekday,
    TimeOnly StartTime,
    int DurationMinutes,
    int Capacity
);

public record UpdateScheduleRequest(
    Guid ClassTypeId,
    Guid? InstructorId,
    Guid LocationId,
    int Weekday,
    TimeOnly StartTime,
    int DurationMinutes,
    int Capacity,
    bool IsActive
);

public record ScheduleResponse(
    Guid Id,
    Guid ClassTypeId,
    string ClassTypeName,
    string ClassTypeColor,
    Guid? InstructorId,
    string? InstructorName,
    Guid LocationId,
    int Weekday,
    TimeOnly StartTime,
    int DurationMinutes,
    int Capacity,
    bool IsActive
);
