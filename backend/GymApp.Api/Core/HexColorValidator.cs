using System.Text.RegularExpressions;

namespace GymApp.Api.Core;

public static partial class HexColorValidator
{
    private static readonly Regex HexColorRegex = new(@"^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

    public static bool IsValidHexColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
            return false;

        return HexColorRegex.IsMatch(color.Trim());
    }
}