using System.Text.RegularExpressions;

/// <summary>
/// Story dialogue string cleanup: unescape botched JSON unicode literals and map Chinese labels when English is active.
/// </summary>
public static class StoryLocalizationUtil
{
    static readonly Regex UnicodeLiteralPattern = new Regex(@"\\u([0-9a-fA-F]{4})", RegexOptions.Compiled);

    public static string NormalizeStoryLabel(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        text = UnescapeUnicodeLiterals(text.Trim());
        if (!LocalizationService.IsEnglish)
            return text;

        return LocTerms.Translate(text);
    }

    public static string UnescapeUnicodeLiterals(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('\\') < 0)
            return text;

        return UnicodeLiteralPattern.Replace(text, match =>
            ((char)int.Parse(match.Groups[1].Value, System.Globalization.NumberStyles.HexNumber)).ToString());
    }
}
