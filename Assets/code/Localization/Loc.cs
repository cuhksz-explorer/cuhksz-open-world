using System;

/// <summary>
/// Shorthand for bilingual UI strings. Use Choose(zh, en) for inline text.
/// </summary>
public static class Loc
{
    public static bool IsEnglish => LocalizationService.IsEnglish;

    public static string Choose(string zh, string en)
    {
        if (string.IsNullOrEmpty(zh))
            return en ?? "";
        if (string.IsNullOrEmpty(en))
            return zh;
        return IsEnglish ? en : zh;
    }

    public static string F(string zh, string en, params object[] args)
    {
        return string.Format(Choose(zh, en), args);
    }

    public static event Action OnLanguageChanged
    {
        add => LocalizationService.OnLanguageChanged += value;
        remove => LocalizationService.OnLanguageChanged -= value;
    }

    /// <summary>Common NPC / story term and location mapping for runtime dialogue labels.</summary>
    public static string StoryTerm(string zh) => StoryLocalizationUtil.NormalizeStoryLabel(zh);

    /// <summary>Alias for scene labels, interaction prompts, shop strings.</summary>
    public static string Term(string zh) => LocTerms.Translate(zh);
}
