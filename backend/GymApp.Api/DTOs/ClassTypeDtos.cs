using GymApp.Domain.Enums;

namespace GymApp.Api.DTOs;

public record CreateClassTypeRequest(string Name, string? Description, string Color, ModalityType ModalityType, decimal? Price, int? DurationMinutes, Guid? CategoryId = null);
public record UpdateClassTypeRequest(string Name, string? Description, string Color, ModalityType ModalityType, bool IsActive, decimal? Price, int? DurationMinutes, Guid? CategoryId = null);
public record ClassTypeResponse(Guid Id, string Name, string? Description, string Color, ModalityType ModalityType, bool IsActive, decimal? Price, int? DurationMinutes, Guid? CategoryId = null, string? CategoryName = null);

public record ServiceCategoryResponse(Guid Id, string Name, int SortOrder, bool IsActive);
public record CreateServiceCategoryRequest(string Name, int SortOrder = 0);
public record UpdateServiceCategoryRequest(string Name, int SortOrder, bool IsActive);
