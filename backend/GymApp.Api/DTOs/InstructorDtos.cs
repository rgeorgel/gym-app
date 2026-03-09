namespace GymApp.Api.DTOs;

public record CreateInstructorRequest(string Name, string Email, string? Phone, string? Bio, string? Specialties);
public record UpdateInstructorRequest(string Name, string? Phone, string? Bio, string? Specialties);
public record InstructorResponse(Guid Id, string Name, string Email, string? Phone, string? Bio, string? Specialties, string? PhotoUrl);
