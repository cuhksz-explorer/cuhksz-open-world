using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Localizes bus-stop world-space button labels via <see cref="Loc.Term"/>.
/// </summary>
public static class BusStopCanvasLocalizer
{
    static readonly Dictionary<int, string> OriginalZhByTmpId = new Dictionary<int, string>(64);
    static readonly Dictionary<int, string> OriginalZhByLegacyTextId = new Dictionary<int, string>(16);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        LocalizationService.OnLanguageChanged += RequestRefresh;
        RequestRefresh();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RequestRefresh();

    public static void RequestRefresh() => BusStopCanvasLocalizerRunner.EnsureScheduled();

    public static void RefreshAll()
    {
        var canvases = Object.FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            var canvas = canvases[i];
            if (canvas == null || canvas.renderMode != RenderMode.WorldSpace)
                continue;
            LocalizeCanvas(canvas);
        }

        var interactors = Object.FindObjectsOfType<InteractWithMouseAndMovementLock>(true);
        for (int i = 0; i < interactors.Length; i++)
        {
            var interactor = interactors[i];
            if (interactor == null || interactor.targetCanvas == null)
                continue;
            if (interactor.targetCanvas.renderMode == RenderMode.WorldSpace)
                LocalizeCanvas(interactor.targetCanvas);
        }
    }

    public static void LocalizeCanvas(Canvas canvas)
    {
        if (canvas == null)
            return;
        LocalizeTransform(canvas.transform);
    }

    public static void LocalizeTransform(Transform root)
    {
        if (root == null)
            return;

        var tmps = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < tmps.Length; i++)
            LocalizeTmp(tmps[i]);

        var legacyTexts = root.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < legacyTexts.Length; i++)
            LocalizeLegacyText(legacyTexts[i]);
    }

    static void LocalizeTmp(TMP_Text tmp)
    {
        if (tmp == null)
            return;

        int id = tmp.GetInstanceID();
        if (!OriginalZhByTmpId.TryGetValue(id, out string zh))
        {
            if (!TryCaptureChineseLabel(tmp.text, out zh))
                return;
            OriginalZhByTmpId[id] = zh;
        }

        tmp.text = LocalizationService.IsEnglish ? Loc.Term(zh) : zh;
    }

    static void LocalizeLegacyText(Text text)
    {
        if (text == null)
            return;

        int id = text.GetInstanceID();
        if (!OriginalZhByLegacyTextId.TryGetValue(id, out string zh))
        {
            if (!TryCaptureChineseLabel(text.text, out zh))
                return;
            OriginalZhByLegacyTextId[id] = zh;
        }

        text.text = LocalizationService.IsEnglish ? Loc.Term(zh) : zh;
    }

    static bool TryCaptureChineseLabel(string text, out string zh)
    {
        zh = (text ?? "").Trim();
        if (string.IsNullOrEmpty(zh))
            return false;
        if (!ContainsCjk(zh))
            return false;
        return IsBusDestinationLabel(zh);
    }

    static bool IsBusDestinationLabel(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;
        if (text.StartsWith("前往", System.StringComparison.Ordinal))
            return true;
        if (text.Contains("公交"))
            return true;
        return false;
    }

    static bool ContainsCjk(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c >= 0x4E00 && c <= 0x9FFF)
                return true;
        }

        return false;
    }
}

sealed class BusStopCanvasLocalizerRunner : MonoBehaviour
{
    static BusStopCanvasLocalizerRunner _instance;

    public static void EnsureScheduled()
    {
        if (_instance == null)
        {
            var go = new GameObject(nameof(BusStopCanvasLocalizerRunner));
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<BusStopCanvasLocalizerRunner>();
            return;
        }

        _instance.StopAllCoroutines();
        _instance.StartCoroutine(_instance.CoRefresh());
    }

    void OnEnable() => StartCoroutine(CoRefresh());

    IEnumerator CoRefresh()
    {
        for (int i = 0; i < 10; i++)
        {
            BusStopCanvasLocalizer.RefreshAll();
            yield return null;
        }
    }
}
