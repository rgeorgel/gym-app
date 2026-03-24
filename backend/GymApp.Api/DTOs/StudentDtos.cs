using GymApp.Domain.Enums;

namespace GymApp.Api.DTOs;

public record CreateStudentRequest(
    string Name,
    string Email,
    string? Password,
    string? Phone,
    DateOnly? BirthDate,
    string? HealthNotes
);

public record UpdateStudentRequest(
    string Name,
    string? Phone,
    DateOnly? BirthDate,
    string? HealthNotes,
    StudentStatus Status
);

public record StudentResponse(
    Guid Id,
    string Name,
    string Email,
    string? Phone,
    DateOnly? BirthDate,
    StudentStatus Status,
    string? PhotoUrl,
    DateTime CreatedAt,
    int TotalRemainingCredits,
    DateOnly? LastBookingDate,
    string? HealthNotes
);
