using System.Security.Cryptography;

namespace GymApp.Api.Core;

public static class TokenGenerator
{
    public static string GenerateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    public static string GeneratePasswordResetToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    public static bool IsValidPasswordResetToken(string? token) =>
        !string.IsNullOrWhiteSpace(token) && token.Length >= 40;

    public static bool IsTokenExpired(DateTime? expiry) =>
        !expiry.HasValue || expiry.Value <= DateTime.UtcNow;

    public static DateTime GetRefreshTokenExpiry(int days = 30) =>
        DateTime.UtcNow.AddDays(days);

    public static DateTime GetPasswordResetTokenExpiry(int hours = 2) =>
        DateTime.UtcNow.AddHours(hours);
}

public static class PasswordValidator
{
    public static bool IsValidPassword(string? password) =>
        !string.IsNullOrWhiteSpace(password) && password.Length >= 6;

    public static bool IsValidEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) && email.Contains('@');

    public static bool IsValidName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name.Trim().Length >= 2;
}