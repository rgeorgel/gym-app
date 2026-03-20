using GymApp.Domain.Enums;

namespace GymApp.Api.DTOs;

public record CreateClassTypeRequest(string Name, string? Description, string Color, ModalityType ModalityType, decimal? Price, int? DurationMinutes);
public record UpdateClassTypeRequest(string Name, string? Description, string Color, ModalityType ModalityType, bool IsActive, decimal? Price, int? DurationMinutes);
public record ClassTypeResponse(Guid Id, string Name, string? Description, string Color, ModalityType ModalityType, bool IsActive, decimal? Price, int? DurationMinutes);
