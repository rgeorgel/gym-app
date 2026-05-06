using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GymApp.Api.Core;

public static partial class TenantSlugResolver
{
    public static string? ExtractSlug(string host)
    {
        var parts = host.Split('.');
        return parts.Length >= 3 ? parts[0] : null;
    }

    public static string GenerateSlug(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "academia";

        var normalized = name.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        var clean = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        clean = InvalidCharsRegex().Replace(clean, "");
        clean = WhitespaceRegex().Replace(clean, "-");
        clean = MultipleHyphensRegex().Replace(clean, "-").Trim('-');

        return string.IsNullOrEmpty(clean) ? "academia" : clean;
    }

    public static bool IsValidHexColor(string? color) =>
        !string.IsNullOrWhiteSpace(color) && HexColorRegex().IsMatch(color.Trim());

    [GeneratedRegex(@"^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColorRegex();

    [GeneratedRegex(@"[^a-z0-9\s-]")]
    private static partial Regex InvalidCharsRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex MultipleHyphensRegex();
}

public static class PasswordGenerator
{
    private static readonly Random Random = new();

    public static string GenerateTempPassword(int length = 10)
    {
        const string chars = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
        return new string(Enumerable.Repeat(chars, length).Select(s => s[Random.Next(s.Length)]).ToArray());
    }

    public static string GenerateResetToken()
    {
        return Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-").Replace("/", "_").Replace("=", "");
    }
}