using GymApp.Domain.Enums;

namespace GymApp.Api.Core;

public static class UserValidator
{
    public static bool IsValidEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) && email.Contains('@') && email.Contains('.');

    public static bool IsValidPassword(string? password) =>
        !string.IsNullOrWhiteSpace(password) && password.Length >= 6;

    public static bool IsValidName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name.Trim().Length >= 2;

    public static bool IsValidPhone(string? phone) =>
        string.IsNullOrWhiteSpace(phone) || phone.Length >= 10;

    public static string NormalizeEmail(string email) =>
        email.ToLowerInvariant().Trim();

    public static string NormalizeName(string name) =>
        name.Trim();

    public static bool CanChangeOwnStatus(Guid targetId, Guid currentUserId) =>
        targetId != currentUserId;

    public static bool IsValidUserId(Guid? userId) =>
        userId.HasValue && userId.Value != Guid.Empty;
}

public static class TenantPlanHelper
{
    public static decimal GetSubscriptionPrice(TenantType type, TenantPlan? plan = null)
    {
        if (plan.HasValue)
        {
            return plan.Value switch
            {
                TenantPlan.Basic => 4900,
                TenantPlan.Pro => 9900,
                TenantPlan.Enterprise => 19900,
                _ => 4900
            };
        }
        return type switch
        {
            TenantType.BeautySalon => 1900,
            TenantType.Gym => 4900,
            _ => 4900
        };
    }

    public static int GetTrialDays(TenantType type, bool hasReferrer = false, bool hasAffiliate = false) =>
        hasReferrer ? 44 : hasAffiliate ? 15 : 14;
}