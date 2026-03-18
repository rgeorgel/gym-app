using GymApp.Domain.Enums;

namespace GymApp.Api.DTOs;

public record AdminUserResponse(
    Guid Id,
    string Name,
    string Email,
    StudentStatus Status,
    DateTime CreatedAt,
    bool ReceivesSubscriptionReminders
);

public record CreateAdminUserRequest(
    string Name,
    string Email,
    string Password
);

public record UpdateAdminUserRequest(
    string Name,
    StudentStatus Status,
    bool ReceivesSubscriptionReminders
);

public record ResetAdminPasswordRequest(
    string NewPassword
);
