using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// undergarden 场景 UI 缩放参数（作为全项目标准）。
/// </summary>
public static class UndergardenUiScaleProfile
{
    public static readonly Vector2 HudReferenceResolution = new Vector2(800f, 600f);
    public static readonly Vector2 DefaultReferenceResolution = new Vector2(1920f, 1080f);
    public const float HudMatchWidthOrHeight = 0f;
    public const float DefaultMatchWidthOrHeight = 0.5f;

    public static bool IsHudCanvas(Canvas canvas)
    {
        if (canvas == null)
            return false;
        if (IsModalCanvas(canvas))
            return false;

        string name = canvas.gameObject.name.ToLowerInvariant();
        string path = GetHierarchyPath(canvas.transform).ToLowerInvariant();

        if (name.Contains("phone") || name.Contains("openpanel") || name.Contains("hud"))
            return true;
        if (name.Contains("quest") || name.Contains("map") || name.Contains("brief"))
            return true;
        if (path.Contains("/phone") || path.Contains("/openpanel") || path.Contains("/quest"))
            return true;
        if (canvas.GetComponentInChildren<PhoneUIManager>(true) != null)
            return true;
        if (canvas.GetComponentInChildren<GameQuestSystem.QuestSystemUI>(true) != null)
            return true;

        return false;
    }

    public static void ApplyToCanvas(Canvas canvas)
    {
        if (canvas == null || canvas.renderMode == RenderMode.WorldSpace)
            return;

        bool hud = IsHudCanvas(canvas);
        Vector2 reference = hud ? HudReferenceResolution : DefaultReferenceResolution;
        float match = hud ? HudMatchWidthOrHeight : DefaultMatchWidthOrHeight;

        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = reference;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = match;
        scaler.referencePixelsPerUnit = 100f;
        scaler.scaleFactor = 1f;

        canvas.pixelPerfect = false;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var rt = canvas.GetComponent<RectTransform>();
        if (rt != null && rt.localScale.sqrMagnitude < 0.001f)
        {
            // Modal 全屏 UI（选车等）须可见；HUD（Phone）仍保持 scale=0 由 CanvasScaler 缩放。
            rt.localScale = IsModalCanvas(canvas) ? Vector3.one : Vector3.zero;
        }
    }

    static bool IsModalCanvas(Canvas canvas)
    {
        if (canvas == null)
            return false;
        if (canvas.GetComponent<VehicleShopUI>() != null)
            return true;
        if (canvas.GetComponentInChildren<VehicleShopUI>(true) != null)
            return true;

        string name = canvas.gameObject.name.ToLowerInvariant();
        return name.Contains("vehicleshop");
    }

    static string GetHierarchyPath(Transform t)
    {
        if (t == null)
            return string.Empty;
        var parts = new System.Collections.Generic.List<string>(8);
        while (t != null)
        {
            parts.Add(t.name);
            t = t.parent;
        }
        parts.Reverse();
        return string.Join("/", parts);
    }
}
