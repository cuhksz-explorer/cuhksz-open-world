using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 全服喇叭：屏幕中上方较小字号横向飘过一行字（高于普通 HUD，低于好友聊天全屏层）。
/// </summary>
[DisallowMultipleComponent]
public class GlobalHornOverlay : MonoBehaviour
{
    const int CanvasSortOrder = 2600;

    public static GlobalHornOverlay Instance { get; private set; }

    TextMeshProUGUI _tmp;
    RectTransform _textRt;
    CanvasGroup _group;
    Coroutine _run;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (Instance != null) return;
        var go = new GameObject("GlobalHornOverlay");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<GlobalHornOverlay>();
        Instance.Build();
        CoopNetworkPlayer.OnServerHorn += Instance.OnServerHorn;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            CoopNetworkPlayer.OnServerHorn -= OnServerHorn;
            Instance = null;
        }
    }

    void Build()
    {
        var canvasGo = new GameObject("HornCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortOrder;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>().ignoreReversedGraphics = true;

        var bar = new GameObject("Bar");
        bar.transform.SetParent(canvasGo.transform, false);
        var barRt = bar.AddComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0f, 1f);
        barRt.anchorMax = new Vector2(1f, 1f);
        barRt.pivot = new Vector2(0.5f, 1f);
        barRt.anchoredPosition = new Vector2(0f, -72f);
        barRt.sizeDelta = new Vector2(0f, 44f);
        _group = bar.AddComponent<CanvasGroup>();
        _group.blocksRaycasts = false;
        _group.interactable = false;
        _group.alpha = 0f;

        var go = new GameObject("Marquee");
        go.transform.SetParent(bar.transform, false);
        _tmp = go.AddComponent<TextMeshProUGUI>();
        var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF");
        if (font != null)
            _tmp.font = font;
        _tmp.alignment = TextAlignmentOptions.MidlineLeft;
        _tmp.fontSize = 22f;
        _tmp.color = new Color(1f, 0.92f, 0.35f, 1f);
        _tmp.text = "";
        _tmp.enableWordWrapping = false;
        _tmp.overflowMode = TextOverflowModes.Overflow;
        _textRt = go.GetComponent<RectTransform>();
        _textRt.anchorMin = new Vector2(0f, 0f);
        _textRt.anchorMax = new Vector2(0f, 1f);
        _textRt.pivot = new Vector2(0f, 0.5f);
        _textRt.anchoredPosition = Vector2.zero;
        _textRt.sizeDelta = new Vector2(4000f, 0f);
    }

    void OnServerHorn(string speaker, string body)
    {
        Show(speaker, body);
    }

    public void Show(string speaker, string message)
    {
        if (_tmp == null) return;
        string line = string.IsNullOrEmpty(speaker)
            ? (message ?? "")
            : $"【喇叭】{speaker}：{message ?? ""}";
        if (_run != null)
            StopCoroutine(_run);
        _run = StartCoroutine(CoMarquee(line));
    }

    IEnumerator CoMarquee(string line)
    {
        _tmp.text = line;
        Canvas.ForceUpdateCanvases();
        float w = Mathf.Max(800f, _tmp.GetPreferredValues(line, Mathf.Infinity, 40f).x + 40f);
        Canvas cv = _textRt != null ? _textRt.GetComponentInParent<Canvas>() : null;
        float screenW = cv != null ? cv.rootCanvas.pixelRect.width : Screen.width;
        float startX = screenW * 0.52f + w * 0.5f;
        float endX = -screenW * 0.52f - w * 0.5f;
        _textRt.anchoredPosition = new Vector2(startX, 0f);
        _group.alpha = 1f;
        float dur = Mathf.Clamp(w / 180f, 6f, 18f);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = t / dur;
            _textRt.anchoredPosition = new Vector2(Mathf.Lerp(startX, endX, u), 0f);
            yield return null;
        }

        for (float f = 1f; f > 0f; f -= Time.unscaledDeltaTime * 3f)
        {
            _group.alpha = f;
            yield return null;
        }
        _group.alpha = 0f;
        _tmp.text = "";
        _run = null;
    }
}
