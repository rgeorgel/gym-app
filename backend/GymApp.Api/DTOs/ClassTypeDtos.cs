using GymApp.Domain.Enums;

namespace GymApp.Api.DTOs;

public record CreateClassTypeRequest(string Name, string? Description, string Color, ModalityType ModalityType);
public record UpdateClassTypeRequest(string Name, string? Description, string Color, ModalityType ModalityType, bool IsActive);
public record ClassTypeResponse(Guid Id, string Name, string? Description, string Color, ModalityType ModalityType, bool IsActive);
