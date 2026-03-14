using GymApp.Domain.Enums;

namespace GymApp.Api.DTOs;

public record AdminUserResponse(
    Guid Id,
    string Name,
    string Email,
    StudentStatus Status,
    DateTime CreatedAt
);

public record CreateAdminUserRequest(
    string Name,
    string Email,
    string Password
);

public record UpdateAdminUserRequest(
    string Name,
    StudentStatus Status
);

public record ResetAdminPasswordRequest(
    string NewPassword
);
