namespace GymApp.Api.DTOs;

public record CreateLocationRequest(string Name, string? Address, string? Phone, bool IsMain = false);
public record UpdateLocationRequest(string Name, string? Address, string? Phone, bool IsMain);
public record LocationResponse(Guid Id, string Name, string? Address, string? Phone, bool IsMain, DateTime CreatedAt);
