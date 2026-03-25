namespace GymApp.Api.DTOs;

public record CreateInstructorRequest(string Name, string Email, string? Phone, string? Bio, string? Specialties, string? PhotoUrl);
public record UpdateInstructorRequest(string Name, string? Phone, string? Bio, string? Specialties, string? PhotoUrl);
public record InstructorResponse(Guid Id, string Name, string Email, string? Phone, string? Bio, string? Specialties, string? PhotoUrl, List<Guid> ServiceIds);
public record ProfessionalResponse(Guid Id, string Name, string? PhotoUrl, string? Bio, string? Specialties);
public record UpdateInstructorServicesRequest(List<Guid> ServiceIds);
