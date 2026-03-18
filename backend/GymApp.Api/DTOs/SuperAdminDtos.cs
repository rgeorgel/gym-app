namespace GymApp.Api.DTOs;

public record SuperAdminResponse(
    Guid Id,
    string Name,
    string Email,
    string? Phone,
    bool IsActive,
    DateTime CreatedAt
);

public record CreateSuperAdminRequest(
    string Name,
    string Email,
    string Password,
    string? Phone = null
);

public record UpdateSuperAdminRequest(
    string Name,
    string Email,
    string? Phone,
    string? Password = null
);

public record SetSuperAdminStatusRequest(
    bool IsActive
);
