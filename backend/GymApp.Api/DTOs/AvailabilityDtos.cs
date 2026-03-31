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

public record VacationBlockResponse(Guid Id, DateOnly StartDate, DateOnly EndDate, string? Reason, DateTime CreatedAt);
public record CreateVacationBlockRequest(DateOnly StartDate, DateOnly EndDate, string? Reason);
