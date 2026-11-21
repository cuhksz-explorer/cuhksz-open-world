using System;
using UnityEngine;

public enum GameLanguage
{
    Chinese = 0,
    English = 1
}

/// <summary>
/// Central language service. Persists choice in PlayerPrefs and broadcasts changes.
/// </summary>
public static class LocalizationService
{
    const string PrefsKey = "GameLanguage_v1";

    static GameLanguage _current = GameLanguage.Chinese;
    static bool _initialized;

    public static GameLanguage Current
    {
        get
        {
            EnsureInitialized();
            return _current;
        }
    }

    public static bool IsEnglish => Current == GameLanguage.English;

    public static event Action OnLanguageChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap() => EnsureInitialized();

    public static void EnsureInitialized()
    {
        if (_initialized)
            return;

        _current = (GameLanguage)PlayerPrefs.GetInt(PrefsKey, (int)GameLanguage.Chinese);
        _initialized = true;
    }

    public static void SetLanguage(GameLanguage language)
    {
        EnsureInitialized();
        if (_current == language)
            return;

        _current = language;
        PlayerPrefs.SetInt(PrefsKey, (int)language);
        PlayerPrefs.Save();
        GameQuestSystem.Content1StoryEnglishOverlay.InvalidateCache();
        OnLanguageChanged?.Invoke();
    }

    public static void ToggleLanguage()
    {
        SetLanguage(IsEnglish ? GameLanguage.Chinese : GameLanguage.English);
    }
}
