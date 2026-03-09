using GymApp.Domain.Enums;

namespace GymApp.Api.DTOs;

public record CreateStudentRequest(
    string Name,
    string Email,
    string? Phone,
    DateTime? BirthDate,
    string? HealthNotes
);

public record UpdateStudentRequest(
    string Name,
    string? Phone,
    DateTime? BirthDate,
    string? HealthNotes,
    StudentStatus Status
);

public record StudentResponse(
    Guid Id,
    string Name,
    string Email,
    string? Phone,
    DateTime? BirthDate,
    StudentStatus Status,
    string? PhotoUrl,
    DateTime CreatedAt
);
