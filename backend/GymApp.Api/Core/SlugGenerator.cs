using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GymApp.Api.Core;

public static class SlugGenerator
{
    private static readonly Regex InvalidCharsRegex = new(@"[^a-z0-9\s-]", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex MultipleHyphensRegex = new(@"-{2,}", RegexOptions.Compiled);

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
        clean = InvalidCharsRegex.Replace(clean, "");
        clean = WhitespaceRegex.Replace(clean, "-");
        clean = MultipleHyphensRegex.Replace(clean, "-").Trim('-');

        return string.IsNullOrEmpty(clean) ? "academia" : clean;
    }
}