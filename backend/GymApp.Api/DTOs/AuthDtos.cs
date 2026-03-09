namespace GymApp.Api.DTOs;

public record LoginRequest(string Email, string Password, string? TenantSlug = null);
public record LoginResponse(string AccessToken, string RefreshToken, string Role, string Name, Guid UserId, string? TenantSlug = null);
public record RefreshRequest(string RefreshToken);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
