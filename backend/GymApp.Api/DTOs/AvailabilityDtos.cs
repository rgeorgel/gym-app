namespace GymApp.Api.DTOs;

public record AvailabilityResponse(
    Guid Id,
    int Weekday,
    TimeOnly StartTime,
    TimeOnly EndTime,
    Guid? InstructorId,
    string? InstructorName,
    bool IsActive
);

public record CreateAvailabilityRequest(int Weekday, TimeOnly StartTime, TimeOnly EndTime, Guid? InstructorId);
