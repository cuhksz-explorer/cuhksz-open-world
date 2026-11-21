#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 菜单一键生成目标引导 UI（TargetGuideUI + Overlay 图标 + 距离文本）。
/// 图标：可将 PNG 放到 Assets/TargetGuide/GuideIcon.png，或使用菜单「将选中图片设为引导图标」。
/// </summary>
public static class TargetGuideOneClickSetup
{
    const string MenuOneClick = "Tools/UI/Target Guide/One Click Setup";
    const string MenuAssignIcon = "Tools/UI/Target Guide/Use Selected Texture as Guide Icon";
    const string IconAssetPath = "Assets/TargetGuide/GuideIcon.png";

    [MenuItem(MenuOneClick, false, 0)]
    static void OneClickSetup()
    {
        EnsureTargetGuideIconFolder();

        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        var existing = canvas.GetComponentInChildren<TargetGuideUI>(true);
        if (existing != null)
            Undo.DestroyObjectImmediate(existing.gameObject);

        var root = new GameObject("TargetGuideSystem",typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, "Create TargetGuideSystem");
        root.transform.SetParent(canvas.transform, false);
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        var iconGo = new GameObject("GuideIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        Undo.RegisterCreatedObjectUndo(iconGo, "Create GuideIcon");
        iconGo.transform.SetParent(root.transform, false);
        var iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.5f, 0.5f);
        iconRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.sizeDelta = new Vector2(72f, 72f);
        iconRt.anchoredPosition = Vector2.zero;
        var iconImg = iconGo.GetComponent<Image>();
        iconImg.raycastTarget = false;
        iconImg.preserveAspect = true;
        var sprite = LoadGuideSpriteOrNull();
        if (sprite != null)
            iconImg.sprite = sprite;
        var iconOutline = iconGo.GetComponent<Outline>();
        if (iconOutline != null)
        {
            iconOutline.effectColor = new Color(0f, 0f, 0f, 0.88f);
            iconOutline.effectDistance = new Vector2(2.5f, -2.5f);
            iconOutline.useGraphicAlpha = true;
        }

        var textGo = new GameObject("GuideDistanceText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
        Undo.RegisterCreatedObjectUndo(textGo, "Create GuideDistanceText");
        textGo.transform.SetParent(iconGo.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0.5f, 0.5f);
        textRt.anchorMax = new Vector2(0.5f, 0.5f);
        textRt.pivot = new Vector2(0.5f, 1f);
        textRt.anchoredPosition = new Vector2(0f, -46f);
        textRt.sizeDelta = new Vector2(280f, 40f);
        var txt = textGo.GetComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 28;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.UpperCenter;
        txt.color = Color.white;
        txt.text = "--";
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        var textOutline = textGo.GetComponent<Outline>();
        if (textOutline != null)
        {
            textOutline.effectColor = new Color(0f, 0f, 0f, 0.88f);
            textOutline.effectDistance = new Vector2(1.5f, -1.5f);
            textOutline.useGraphicAlpha = true;
        }

        var guide = Undo.AddComponent<TargetGuideUI>(root);
        SetPrivateField(guide, "followActiveQuestReachLocation", true);
        var switcher = Object.FindObjectOfType<CameraSwitcher>();
        if (switcher != null)
            SetPrivateField(guide, "cameraSwitcher", switcher);
        SetPrivateField(guide, "iconRect", iconRt);
        SetPrivateField(guide, "iconImage", iconImg);
        SetPrivateField(guide, "distanceText", txt);
        if (sprite != null)
            SetPrivateField(guide, "guideIcon", sprite);

        Selection.activeGameObject = root;
        EditorUtility.SetDirty(root);
        Debug.Log("[TargetGuide] One Click 完成。已勾选「跟随当前任务到达点」；需场景中有 QuestSystemManager 且当前子任务为 ReachLocation。备用目标可填 Manual Target Override。");
        if (sprite == null)
            Debug.LogWarning("[TargetGuide] 未找到 " + IconAssetPath + "。请放入 PNG 后重试，或使用菜单「Use Selected Texture as Guide Icon」。");
    }

    [MenuItem(MenuAssignIcon, true)]
    static bool ValidateAssignIcon()
    {
        var tex = Selection.activeObject as Texture2D;
        return tex != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(tex));
    }

    [MenuItem(MenuAssignIcon, false, 11)]
    static void AssignSelectedTextureAsIcon()
    {
        var tex = Selection.activeObject as Texture2D;
        if (tex == null) return;
        string path = AssetDatabase.GetAssetPath(tex);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }

        Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sp == null)
        {
            var any = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var o in any)
            {
                if (o is Sprite s)
                {
                    sp = s;
                    break;
                }
            }
        }

        if (sp == null)
        {
            Debug.LogError("[TargetGuide] 无法从选中资源得到 Sprite: " + path);
            return;
        }

        foreach (var g in Object.FindObjectsOfType<TargetGuideUI>(true))
        {
            SetPrivateField(g, "guideIcon", sp);
            var img = GetPrivateField<Image>(g, "iconImage");
            if (img != null)
            {
                img.sprite = sp;
                EditorUtility.SetDirty(img);
            }
            EditorUtility.SetDirty(g);
        }

        Debug.Log("[TargetGuide] 已将选中图片设为引导图标（场景中所有 TargetGuideUI）。");
    }

    static void EnsureTargetGuideIconFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/TargetGuide"))
            AssetDatabase.CreateFolder("Assets", "TargetGuide");
    }

    static Sprite LoadGuideSpriteOrNull()
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(IconAssetPath);
    }

    static void SetPrivateField(object target, string fieldName, object value)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var f = target.GetType().GetField(fieldName, flags);
        if (f != null) f.SetValue(target, value);
    }

    static T GetPrivateField<T>(object target, string fieldName) where T : class
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var f = target.GetType().GetField(fieldName, flags);
        return f != null ? f.GetValue(target) as T : null;
    }
}
#endif
