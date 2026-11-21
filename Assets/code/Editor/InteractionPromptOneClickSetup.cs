#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 一键在主 UI Canvas 上生成交互提示（统一样式、TMP 阿里巴巴普惠体），多物体共用同一面板；接近时显示，远离隐藏。
/// </summary>
public static class InteractionPromptOneClickSetup
{
    const string AlibabaTmpFontResourcesPath = "Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF";

    const string MenuSharedMainUi = "Tools/UI/Interaction Prompt/一键配置交互提示（主 UI · 多物体共用）";
    const string MenuUnderSelection = "Tools/UI/Interaction Prompt/一键配置（挂到当前选中 Canvas 下）";
    const string MenuLegacy = "Tools/UI/Interaction Prompt/一键配置（旧版 · 仅绑定第一个交互器）";
    const string MenuConsolidate = "Tools/UI/Interaction Prompt/合并重复的 InteractPrompt_Shared";

    const string PanelName = "InteractPrompt_Shared";

    [MenuItem(MenuConsolidate, false, 5)]
    public static void ConsolidateDuplicatesInOpenScenes()
    {
        int removed = ConsolidateDuplicatesEditor(false);
        EditorUtility.DisplayDialog(
            "交互提示",
            removed > 0
                ? $"已合并共用面板，删除重复 {removed} 套，并重新绑定交互器/赛车 HUD。"
                : "当前打开的场景中未发现需要合并的重复 InteractPrompt_Shared。",
            "确定");
    }

    /// <summary>场景里已有共用面板时只绑定引用，不新建（供赛车一键配置等调用）。</summary>
    public static bool TryBindExistingSharedPanelInScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return false;

        var panels = FindAllSharedPanelsInScene(scene);
        if (panels.Count == 0)
            return false;

        GameObject canonical = SelectBestCanonicalPanel(panels);
        if (canonical == null)
            return false;

        var panelRt = canonical.GetComponent<RectTransform>();
        var nameTmp = FindTmpInPanel(canonical.transform, "InteractNameText");
        var typeTmp = FindTmpInPanel(canonical.transform, "InteractTypeText");
        if (panelRt == null || nameTmp == null || typeTmp == null)
            return false;

        BindAllConsumersEditor(panelRt, nameTmp, typeTmp);

        int removed = 0;
        for (int i = 0; i < panels.Count; i++)
        {
            if (panels[i] == null || panels[i] == canonical)
                continue;
            Undo.DestroyObjectImmediate(panels[i]);
            removed++;
        }

        EditorUtility.SetDirty(canonical);
        if (removed > 0)
            Debug.Log($"[Interaction Prompt] 场景 {scene.name}：保留 {canonical.name}，删除重复 {removed} 套。");
        return true;
    }

    static int ConsolidateDuplicatesEditor(bool silent)
    {
        int totalRemoved = 0;
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            Scene scene = EditorSceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;
            var panels = FindAllSharedPanelsInScene(scene);
            if (panels.Count <= 1)
                continue;

            GameObject canonical = SelectBestCanonicalPanel(panels);
            if (canonical == null)
                continue;

            var panelRt = canonical.GetComponent<RectTransform>();
            var nameTmp = FindTmpInPanel(canonical.transform, "InteractNameText");
            var typeTmp = FindTmpInPanel(canonical.transform, "InteractTypeText");
            if (panelRt == null || nameTmp == null || typeTmp == null)
                continue;

            BindAllConsumersEditor(panelRt, nameTmp, typeTmp);

            for (int pi = 0; pi < panels.Count; pi++)
            {
                if (panels[pi] == null || panels[pi] == canonical)
                    continue;
                Undo.DestroyObjectImmediate(panels[pi]);
                totalRemoved++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
        }

        if (!silent && totalRemoved > 0)
            Debug.Log($"[Interaction Prompt] 已删除重复面板 {totalRemoved} 套。");
        return totalRemoved;
    }

    /// <summary>为指定已加载场景配置共用交互提示面板（供 libraryfloor1 等一键脚本调用）。</summary>
    public static bool SetupSharedPromptForScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return false;

        Canvas canvas = FindPreferredCanvasInScene(scene);
        if (canvas == null)
            return false;

        if (EditorSceneManager.GetActiveScene() != scene)
            EditorSceneManager.SetActiveScene(scene);

        RunCoreShared(canvas.transform);
        EditorSceneManager.MarkSceneDirty(scene);
        return true;
    }

    public static Canvas FindPreferredCanvasInScene(Scene scene)
    {
        Canvas best = null;
        int bestScore = int.MinValue;
        foreach (var c in Resources.FindObjectsOfTypeAll<Canvas>())
        {
            if (c == null || c.gameObject.scene != scene)
                continue;
            if (c.renderMode == RenderMode.WorldSpace)
                continue;
            if (c.GetComponentInParent<NewUserIntroCinematic>(true) != null)
                continue;

            int score = 0;
            var n = c.gameObject.name.ToLowerInvariant();
            if (n.Contains("main") || n.Contains("hud") || n.Contains("gameui") || n == "canvas")
                score += 80;
            score += c.sortingOrder * 3;
            score += Mathf.Min(c.transform.childCount, 40);

            var phone = c.GetComponentInChildren<PhoneUIManager>(true);
            if (phone != null)
                score += 100;

            if (score > bestScore)
            {
                bestScore = score;
                best = c;
            }
        }

        return best;
    }

    [MenuItem(MenuSharedMainUi, false, 10)]
    static void RunSharedOnMainUi()
    {
        var canvas = FindPreferredMainCanvas();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("交互提示", "未找到合适的 Screen Space Canvas。请先创建 UI Canvas，或在 Hierarchy 中选中主 UI 下的任意物体后再执行。", "确定");
            return;
        }
        RunCoreShared(canvas.transform);
    }

    [MenuItem(MenuUnderSelection, false, 11)]
    static void RunUnderSelection()
    {
        if (Selection.activeGameObject == null)
        {
            EditorUtility.DisplayDialog("交互提示", "请在 Hierarchy 中选中「主 UI Canvas 或其子物体」。", "确定");
            return;
        }
        var canvas = Selection.activeGameObject.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("交互提示", "选中物体不在任何 Canvas 下。", "确定");
            return;
        }
        RunCoreShared(canvas.transform);
    }

    [MenuItem(MenuLegacy, false, 20)]
    static void RunLegacySingle()
    {
        var ctrl = Object.FindObjectOfType<InteractWithMouseAndMovementLock>(true);
        if (ctrl == null)
        {
            EditorUtility.DisplayDialog("交互提示", "场景中未找到 InteractWithMouseAndMovementLock。", "确定");
            return;
        }
        Transform canvas = ctrl.targetCanvas != null
            ? ctrl.targetCanvas.transform
            : null;
        if (canvas == null)
        {
            var main = FindPreferredMainCanvas();
            if (main != null)
                canvas = main.transform;
        }
        if (canvas == null)
        {
            var c = Object.FindObjectOfType<Canvas>();
            if (c != null) canvas = c.transform;
        }
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("交互提示", "未找到 Canvas。", "确定");
            return;
        }
        RunCoreSimpleSingle(ctrl, canvas);
    }

    static void RunCoreSimpleSingle(InteractWithMouseAndMovementLock ctrl, Transform canvasTransform)
    {
        var font = Resources.Load<TMP_FontAsset>(AlibabaTmpFontResourcesPath);
        if (font == null)
        {
            EditorUtility.DisplayDialog("交互提示", "无法加载 TMP 字体：\n" + AlibabaTmpFontResourcesPath, "确定");
            return;
        }

        const string legacyName = "InteractPromptPanel_TMP";
        var dup = canvasTransform.Find(legacyName);
        if (dup != null)
            Undo.DestroyObjectImmediate(dup.gameObject);
        if (ctrl.interactPromptPanel != null && ctrl.interactPromptPanel.parent == canvasTransform)
            Undo.DestroyObjectImmediate(ctrl.interactPromptPanel.gameObject);

        var panelGo = new GameObject(legacyName, typeof(RectTransform), typeof(CanvasGroup));
        Undo.RegisterCreatedObjectUndo(panelGo, "Interaction Prompt Panel");
        var panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.SetParent(canvasTransform, false);
        panelRt.SetAsLastSibling();
        panelRt.anchorMin = new Vector2(0.5f, 0f);
        panelRt.anchorMax = new Vector2(0.5f, 0f);
        panelRt.pivot = new Vector2(0.5f, 0f);
        panelRt.anchoredPosition = new Vector2(0f, 200f);
        panelRt.sizeDelta = new Vector2(InteractPromptSharedStyle.PanelWidth, InteractPromptSharedStyle.PanelHeight);

        AddBackground(panelGo.transform);
        var content = CreateContentStack(panelGo.transform, out var vlg);
        vlg.padding = new RectOffset(
            InteractPromptSharedStyle.LayoutPaddingHorizontal,
            InteractPromptSharedStyle.LayoutPaddingHorizontal,
            InteractPromptSharedStyle.LayoutPaddingVertical,
            InteractPromptSharedStyle.LayoutPaddingVertical);
        vlg.spacing = InteractPromptSharedStyle.LayoutSpacing;

        var nameTmp = CreateLineTmp(
            content, "InteractNameText", font,
            InteractPromptSharedStyle.NameFontSize, FontStyles.Bold, Color.white,
            InteractPromptSharedStyle.NameOutlineWidth, new Color32(0, 0, 0, 200),
            "交互点名称", InteractPromptSharedStyle.NamePreferredHeight);
        var typeTmp = CreateLineTmp(
            content, "InteractTypeText", font,
            InteractPromptSharedStyle.TypeFontSize, FontStyles.Normal, new Color(0.85f, 0.92f, 1f),
            InteractPromptSharedStyle.TypeOutlineWidth, new Color32(0, 0, 0, 180),
            "[F] 交互说明", InteractPromptSharedStyle.TypePreferredHeight);

        Undo.RecordObject(ctrl, "Bind Interaction Prompt");
        ctrl.interactPromptPanel = panelRt;
        ctrl.interactNameText = nameTmp;
        ctrl.interactTypeText = typeTmp;
        ctrl.useSharedPromptResolution = false;
        EditorUtility.SetDirty(ctrl);

        Selection.activeGameObject = panelGo;
        EditorGUIUtility.PingObject(panelGo);
        Debug.Log("[Interaction Prompt] 旧版单绑完成（无共用解析器）。");
    }

    static Canvas FindPreferredMainCanvas()
    {
        Canvas best = null;
        int bestScore = int.MinValue;

        var canvases = Object.FindObjectsOfType<Canvas>(true);
        foreach (var c in canvases)
        {
            if (c.renderMode == RenderMode.WorldSpace)
                continue;
            if (!c.gameObject.scene.IsValid())
                continue;
            if (c.GetComponentInParent<NewUserIntroCinematic>(true) != null)
                continue;

            int score = 0;
            var n = c.gameObject.name.ToLowerInvariant();
            if (n.Contains("main") || n.Contains("hud") || n.Contains("gameui") || n.Contains("playerui"))
                score += 80;
            if (n.Contains("ui") && !n.Contains("world"))
                score += 30;
            if (n == "canvas")
                score += 15;
            score += c.sortingOrder * 3;
            score += Mathf.Min(c.transform.childCount, 40);

            if (Selection.activeGameObject != null)
            {
                if (Selection.activeGameObject.transform.IsChildOf(c.transform))
                    score += 200;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = c;
            }
        }

        return best;
    }

    static void RunCoreShared(Transform canvasTransform)
    {
        var font = Resources.Load<TMP_FontAsset>(AlibabaTmpFontResourcesPath);
        if (font == null)
        {
            EditorUtility.DisplayDialog(
                "交互提示",
                "无法加载 TMP 字体：\n" + AlibabaTmpFontResourcesPath,
                "确定");
            return;
        }

        var controllers = new List<InteractWithMouseAndMovementLock>();
        controllers.AddRange(Object.FindObjectsOfType<InteractWithMouseAndMovementLock>(true));

        if (controllers.Count == 0)
        {
            EditorUtility.DisplayDialog("交互提示", "场景中没有 InteractWithMouseAndMovementLock，无法绑定。", "确定");
            return;
        }

        // 场景里已有共用面板（含 Phone/openpanel 下那套）时只绑定，不新建第二套。
        var existing = FindAllSharedPanelsInOpenScenes();
        if (existing.Count > 0)
        {
            GameObject canonical = SelectBestCanonicalPanel(existing);
            if (canonical != null)
            {
                var existingRt = canonical.GetComponent<RectTransform>();
                var existingName = FindTmpInPanel(canonical.transform, "InteractNameText");
                var existingType = FindTmpInPanel(canonical.transform, "InteractTypeText");
                if (existingRt != null && existingName != null && existingType != null)
                {
                    BindControllersToPanel(controllers, existingRt, existingName, existingType);
                    ConsolidateDuplicatesEditor(true);
                    Selection.activeGameObject = canonical;
                    EditorGUIUtility.PingObject(canonical);
                    Debug.Log("[Interaction Prompt] 场景已有 InteractPrompt_Shared，已重新绑定全部交互器（未新建第二套）。");
                    return;
                }
            }
        }

        var dup = canvasTransform.Find(PanelName);
        if (dup != null)
            Undo.DestroyObjectImmediate(dup.gameObject);

        var panelGo = new GameObject(PanelName, typeof(RectTransform), typeof(CanvasGroup));
        Undo.RegisterCreatedObjectUndo(panelGo, "Interaction Prompt Shared");
        var panelCg = panelGo.GetComponent<CanvasGroup>();
        panelCg.alpha = 0f;
        panelCg.interactable = false;
        panelCg.blocksRaycasts = false;
        panelGo.SetActive(false);
        var panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.SetParent(canvasTransform, false);
        panelRt.SetAsLastSibling();
        panelRt.anchorMin = new Vector2(0.5f, 0f);
        panelRt.anchorMax = new Vector2(0.5f, 0f);
        panelRt.pivot = new Vector2(0.5f, 0f);
        panelRt.anchoredPosition = new Vector2(0f, 200f);
        panelRt.sizeDelta = new Vector2(InteractPromptSharedStyle.PanelWidth, InteractPromptSharedStyle.PanelHeight);

        AddBackground(panelGo.transform);
        var content = CreateContentStack(panelGo.transform, out var vlg);
        vlg.padding = new RectOffset(
            InteractPromptSharedStyle.LayoutPaddingHorizontal,
            InteractPromptSharedStyle.LayoutPaddingHorizontal,
            InteractPromptSharedStyle.LayoutPaddingVertical,
            InteractPromptSharedStyle.LayoutPaddingVertical);
        vlg.spacing = InteractPromptSharedStyle.LayoutSpacing;

        var nameTmp = CreateLineTmp(
            content, "InteractNameText", font,
            InteractPromptSharedStyle.NameFontSize, FontStyles.Bold, Color.white,
            InteractPromptSharedStyle.NameOutlineWidth, new Color32(0, 0, 0, 200),
            "交互点名称", InteractPromptSharedStyle.NamePreferredHeight);
        var typeTmp = CreateLineTmp(
            content, "InteractTypeText", font,
            InteractPromptSharedStyle.TypeFontSize, FontStyles.Normal, new Color(0.85f, 0.92f, 1f),
            InteractPromptSharedStyle.TypeOutlineWidth, new Color32(0, 0, 0, 180),
            "[F] 交互说明", InteractPromptSharedStyle.TypePreferredHeight);

        var follower = panelGo.GetComponent<InteractPromptSharedScreenFollower>();
        if (follower == null)
            follower = panelGo.AddComponent<InteractPromptSharedScreenFollower>();
        follower.CaptureDefaultLayout();

        BindControllersToPanel(controllers, panelRt, nameTmp, typeTmp);

        Selection.activeGameObject = panelGo;
        EditorGUIUtility.PingObject(panelGo);

        Debug.Log("[Interaction Prompt] 一键配置完成。面板由 InteractWithMouseAndMovementLock 接近时淡入（与旧版亮图标逻辑相同）。请取消勾选 Use Shared Prompt Resolution；若 InteractPrompt_Shared 上仍有 InteractPromptSharedResolver，请删除该组件。");
    }

    static void AddBackground(Transform panel)
    {
        var bgGo = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(bgGo, "InteractPrompt BG");
        bgGo.transform.SetParent(panel, false);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        bgRt.SetAsFirstSibling();
        var bgImg = bgGo.GetComponent<Image>();
        bgImg.color = new Color(0.04f, 0.06f, 0.1f, 0.72f);
        bgImg.raycastTarget = false;
    }

    static RectTransform CreateContentStack(Transform panel, out VerticalLayoutGroup vlg)
    {
        var go = new GameObject("Content", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "InteractPrompt Content");
        go.transform.SetParent(panel, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.spacing = 4f;
        vlg.childControlHeight = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;
        return rt;
    }

    static TextMeshProUGUI CreateLineTmp(Transform content, string objName, TMP_FontAsset font, float size, FontStyles style, Color color, float outline, Color32 outlineColor, string placeholder, float preferredHeight)
    {
        var go = new GameObject(objName, typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(go, objName);
        go.transform.SetParent(content, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        ApplyTmpStyle(tmp, font, size, style, color, outline, outlineColor);
        tmp.text = placeholder;
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = preferredHeight;
        le.flexibleWidth = 1f;
        return tmp;
    }

    static void ApplyTmpStyle(TextMeshProUGUI tmp, TMP_FontAsset font, float size, FontStyles style, Color color, float outline, Color32 outlineColor)
    {
        tmp.font = font;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.enableAutoSizing = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        tmp.outlineWidth = outline;
        tmp.outlineColor = outlineColor;
    }

    static void BindControllersToPanel(
        List<InteractWithMouseAndMovementLock> controllers,
        RectTransform panelRt,
        TextMeshProUGUI nameTmp,
        TextMeshProUGUI typeTmp)
    {
        BindAllConsumersEditor(panelRt, nameTmp, typeTmp);
    }

    static void BindAllConsumersEditor(RectTransform panelRt, TextMeshProUGUI nameTmp, TextMeshProUGUI typeTmp)
    {
        BindAllConsumersEditorPublic(panelRt, nameTmp, typeTmp);
    }

    public static void BindAllConsumersEditorPublic(RectTransform panelRt, TextMeshProUGUI nameTmp, TextMeshProUGUI typeTmp)
    {
        foreach (var ctrl in Object.FindObjectsOfType<InteractWithMouseAndMovementLock>(true))
        {
            if (ctrl == null) continue;
            Undo.RecordObject(ctrl, "Bind Interaction Prompt");
            ctrl.interactPromptPanel = panelRt;
            ctrl.interactNameText = nameTmp;
            ctrl.interactTypeText = typeTmp;
            ctrl.useSharedPromptResolution = false;
            EditorUtility.SetDirty(ctrl);
        }

        foreach (var conv in Object.FindObjectsOfType<CanvasInteractionUIManager>(true))
        {
            if (conv == null) continue;
            Undo.RecordObject(conv, "Bind Interaction Prompt (Dialogue)");
            conv.interactPromptPanel = panelRt;
            conv.interactNameText = nameTmp;
            conv.interactTypeText = typeTmp;
            EditorUtility.SetDirty(conv);
        }

        foreach (var hud in Object.FindObjectsOfType<CarSystemVehicleHUD>(true))
        {
            if (hud == null) continue;
            Undo.RecordObject(hud, "Bind Car HUD Shared Prompt");
            hud.sharedPromptPanel = panelRt;
            hud.sharedPromptNameText = nameTmp;
            hud.sharedPromptTypeText = typeTmp;
            hud.preferSharedPrompt = true;
            hud.autoFindSharedPrompt = true;
            EditorUtility.SetDirty(hud);
        }

        foreach (var dealer in Object.FindObjectsOfType<VehicleDealerNPC>(true))
        {
            if (dealer == null) continue;
            Undo.RecordObject(dealer, "Bind Dealer Shared Prompt");
            dealer.sharedPromptPanel = panelRt;
            dealer.sharedPromptNameText = nameTmp;
            dealer.sharedPromptTypeText = typeTmp;
            dealer.useSharedPrompt = true;
            dealer.autoFindSharedPromptIfUnassigned = true;
            EditorUtility.SetDirty(dealer);
        }

        foreach (var car in Object.FindObjectsOfType<VehicleController>(true))
        {
            if (car == null) continue;
            Undo.RecordObject(car, "Bind Vehicle Shared Prompt");
            car.sharedInteractNameText = nameTmp;
            car.sharedInteractTypeText = typeTmp;
            car.useSharedInteractPrompt = true;
            car.autoFindSharedInteractPromptIfUnassigned = true;
            EditorUtility.SetDirty(car);
        }
    }

    static List<GameObject> FindAllSharedPanelsInOpenScenes()
    {
        var list = new List<GameObject>(4);
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            Scene scene = EditorSceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;
            list.AddRange(FindAllSharedPanelsInScene(scene));
        }
        return list;
    }

    static List<GameObject> FindAllSharedPanelsInScene(Scene scene)
    {
        var list = new List<GameObject>(2);
        if (!scene.IsValid() || !scene.isLoaded)
            return list;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int ri = 0; ri < roots.Length; ri++)
            CollectSharedPanelsRecursive(roots[ri].transform, list);
        return list;
    }

    static void CollectSharedPanelsRecursive(Transform root, List<GameObject> list)
    {
        if (root == null)
            return;
        if (string.Equals(root.name, PanelName, System.StringComparison.Ordinal))
            list.Add(root.gameObject);
        for (int i = 0; i < root.childCount; i++)
            CollectSharedPanelsRecursive(root.GetChild(i), list);
    }

    static GameObject SelectBestCanonicalPanel(List<GameObject> panels)
    {
        return SelectBestCanonicalPanelPublic(panels);
    }

    public static GameObject SelectBestCanonicalPanelPublic(List<GameObject> panels)
    {
        GameObject best = null;
        int bestScore = int.MinValue;
        for (int i = 0; i < panels.Count; i++)
        {
            GameObject go = panels[i];
            if (go == null) continue;
            int score = ScorePanelEditor(go);
            if (score > bestScore)
            {
                bestScore = score;
                best = go;
            }
        }
        return best;
    }

    static int ScorePanelEditor(GameObject panelGo)
    {
        int score = 0;
        if (FindTmpInPanel(panelGo.transform, "InteractNameText") != null) score += 30;
        if (FindTmpInPanel(panelGo.transform, "InteractTypeText") != null) score += 30;

        string path = GetHierarchyPathEditor(panelGo.transform).ToLowerInvariant();
        if (path.Contains("openpanel") || path.Contains("/phone"))
            score += 1200;
        if (path.Contains("newuserintrocinematic"))
            score -= 1500;

        var phone = Object.FindObjectOfType<PhoneUIManager>(true);
        if (phone != null && phone.hudPanel != null &&
            panelGo.transform.IsChildOf(phone.hudPanel.transform))
            score += 900;

        if (panelGo.scene.IsValid() && panelGo.scene.name != "DontDestroyOnLoad")
            score += 120;

        return score;
    }

    static string GetHierarchyPathEditor(Transform t)
    {
        if (t == null) return "";
        var parts = new List<string>(8);
        while (t != null)
        {
            parts.Add(t.name);
            t = t.parent;
        }
        parts.Reverse();
        return string.Join("/", parts);
    }

    static TextMeshProUGUI FindTmpInPanel(Transform root, string objectName)
    {
        return FindTmpInPanelPublic(root, objectName);
    }

    public static TextMeshProUGUI FindTmpInPanelPublic(Transform root, string objectName)
    {
        if (root == null)
            return null;
        if (string.Equals(root.name, objectName, System.StringComparison.Ordinal))
        {
            var onSelf = root.GetComponent<TextMeshProUGUI>();
            if (onSelf != null)
                return onSelf;
        }
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindTmpInPanel(root.GetChild(i), objectName);
            if (found != null)
                return found;
        }
        return null;
    }
}
#endif
