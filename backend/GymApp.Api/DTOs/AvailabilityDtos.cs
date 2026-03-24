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

public record TimeBlockResponse(Guid Id, DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, string? Reason, DateTime CreatedAt);
public record CreateTimeBlockRequest(DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, string? Reason);
