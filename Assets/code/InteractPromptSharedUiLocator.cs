using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 运行时查找「一键配置交互提示」生成的共用面板 <see cref="PanelObjectName"/> 及其 TMP，
/// 供未在 Inspector 绑定提示引线的交互器自动参与 <see cref="InteractPromptSharedLabelFrame"/>。
/// </summary>
public static class InteractPromptSharedUiLocator
{
    public const string PanelObjectName = "InteractPrompt_Shared";
    const string NameObjectName = "InteractNameText";
    const string TypeObjectName = "InteractTypeText";

    static Transform _cachedHudParent;
    static RectTransform _canonicalPanel;
    static TextMeshProUGUI _canonicalNameTmp;
    static TextMeshProUGUI _canonicalTypeTmp;
    static Canvas _dedicatedPromptCanvas;

    const int DedicatedPromptCanvasSortOrder = 900;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void OnAfterSceneLoad()
    {
        SceneManager.sceneLoaded -= OnSceneLoadedRefreshSharedPrompt;
        SceneManager.sceneLoaded += OnSceneLoadedRefreshSharedPrompt;
        RefreshSharedPromptAfterSceneLoad();
    }

    static void OnSceneLoadedRefreshSharedPrompt(Scene scene, LoadSceneMode mode)
    {
        RefreshSharedPromptAfterSceneLoad();
    }

    static void RefreshSharedPromptAfterSceneLoad()
    {
        _cachedHudParent = null;
        _canonicalPanel = null;
        _canonicalNameTmp = null;
        _canonicalTypeTmp = null;
        EnsureGameplayHudCanvasVisible();
        ConsolidateDuplicateSharedPanels();
        HideAllSharedPanelsInLoadedScenes();
        InteractPromptSceneRebindRunner.ScheduleNextFrameRebind();
    }

    /// <summary>
    /// undergarden 等场景中 Phone Canvas 默认 inactive 且 scale=0，子物体 openpanel / InteractPrompt_Shared 无法显示。
    /// </summary>
    public static void EnsureGameplayHudCanvasVisible()
    {
        var phoneMgr = Object.FindObjectOfType<PhoneUIManager>(true);
        if (phoneMgr != null)
            EnsureCanvasRootVisible(phoneMgr.gameObject, phoneMgr.hudPanel);

        var session = Object.FindObjectOfType<PhoneUI.PhoneSessionController>(true);
        if (session != null)
        {
            session.EnsureHudPanelReference();
            if (session.hudPanel != null)
            {
                var canvasGo = session.hudPanel.GetComponentInParent<Canvas>(true)?.gameObject;
                if (canvasGo != null)
                    EnsureCanvasRootVisible(canvasGo, session.hudPanel);
            }
        }
    }

    static bool EnsureCanvasRootVisible(GameObject canvasRoot, GameObject hudPanel)
    {
        if (canvasRoot == null)
            return false;

        bool changed = false;
        if (!canvasRoot.activeSelf)
        {
            canvasRoot.SetActive(true);
            changed = true;
        }

        var rt = canvasRoot.GetComponent<RectTransform>();
        if (rt != null && rt.localScale.sqrMagnitude < 0.001f)
        {
            rt.localScale = Vector3.one;
            changed = true;
        }

        if (hudPanel != null && !hudPanel.activeSelf)
        {
            hudPanel.SetActive(true);
            changed = true;
        }

        return changed;
    }

    /// <summary>场景加载并完成 Awake/Start 后再次合并与绑定，修复切场景后提示不显示。</summary>
    public static void RefreshConsumersAfterSceneReady()
    {
        ConsolidateDuplicateSharedPanels();
        if (TryGetCanonical(out var panel, out var nameTmp, out var typeTmp))
            RebindAllConsumers(panel, nameTmp, typeTmp);
    }

    public static bool IsSharedPromptPanel(RectTransform rt)
    {
        return rt != null && string.Equals(rt.name, PanelObjectName, System.StringComparison.Ordinal);
    }

    /// <summary>合并场景中多套共用面板，保留最早/最合适的一套，删除其余并统一绑定。</summary>
    public static void ConsolidateDuplicateSharedPanels()
    {
        var panels = CollectAllSharedPanels();
        if (panels.Count == 0)
            return;

        if (panels.Count == 1)
        {
            CacheCanonicalFromPanel(panels[0]);
            EnsureSharedPanelOnHud(panels[0]);
            RebindAllConsumers(_canonicalPanel, _canonicalNameTmp, _canonicalTypeTmp);
            return;
        }

        GameObject canonicalGo = SelectCanonicalPanel(panels);
        if (canonicalGo == null)
            return;

        CacheCanonicalFromPanel(canonicalGo);
        RebindAllConsumers(_canonicalPanel, _canonicalNameTmp, _canonicalTypeTmp);

        for (int i = 0; i < panels.Count; i++)
        {
            GameObject go = panels[i];
            if (go == null || go == canonicalGo)
                continue;
            Object.Destroy(go);
        }

        EnsureSharedPanelOnHud(canonicalGo);
        Debug.Log(
            $"[InteractPrompt] 已合并 {panels.Count} 套 {PanelObjectName}，保留：{GetHierarchyPath(canonicalGo.transform)}，其余已删除。",
            canonicalGo);
    }

    /// <summary>在已加载场景中查找共用面板（合并后始终返回 canonical）。</summary>
    public static bool TryFindShared(out RectTransform panelRt, out TextMeshProUGUI nameTmp, out TextMeshProUGUI typeTmp)
    {
        panelRt = null;
        nameTmp = null;
        typeTmp = null;

        if (TryGetCanonical(out panelRt, out nameTmp, out typeTmp))
            return true;

        var panels = CollectAllSharedPanels();
        if (panels.Count == 0)
            return false;

        if (panels.Count > 1)
            ConsolidateDuplicateSharedPanels();

        return TryGetCanonical(out panelRt, out nameTmp, out typeTmp);
    }

    public static bool TryGetCanonical(out RectTransform panelRt, out TextMeshProUGUI nameTmp, out TextMeshProUGUI typeTmp)
    {
        panelRt = _canonicalPanel;
        nameTmp = _canonicalNameTmp;
        typeTmp = _canonicalTypeTmp;
        return panelRt != null && nameTmp != null && typeTmp != null;
    }

    /// <summary>若 panel / name / type 缺失或不是 canonical，则补齐为场景唯一共用面板。</summary>
    public static bool TryAutoBind(ref RectTransform panel, ref TextMeshProUGUI nameTmp, ref TextMeshProUGUI typeTmp)
    {
        return EnsureBoundToCanonicalSharedPrompt(ref panel, ref nameTmp, ref typeTmp);
    }

    /// <summary>强制将引用对齐到 canonical 共用面板（切场景后 intro 副本被删时必需）。</summary>
    public static bool EnsureBoundToCanonicalSharedPrompt(
        ref RectTransform panel,
        ref TextMeshProUGUI nameTmp,
        ref TextMeshProUGUI typeTmp)
    {
        if (!TryFindShared(out var canonicalPanel, out var canonicalName, out var canonicalType))
            return false;

        bool changed = panel != canonicalPanel
                       || nameTmp != canonicalName
                       || typeTmp != canonicalType
                       || panel == null
                       || nameTmp == null
                       || typeTmp == null;

        panel = canonicalPanel;
        nameTmp = canonicalName;
        typeTmp = canonicalType;
        return changed;
    }

    /// <summary>将所有交互/赛车脚本绑定到指定共用面板（编辑器合并菜单也会调用）。</summary>
    public static void RebindAllConsumers(RectTransform panel, TextMeshProUGUI nameTmp, TextMeshProUGUI typeTmp)
    {
        if (panel == null || nameTmp == null || typeTmp == null)
            return;

        var locks = Object.FindObjectsOfType<InteractWithMouseAndMovementLock>(true);
        for (int i = 0; i < locks.Length; i++)
        {
            var c = locks[i];
            if (c == null) continue;
            c.interactPromptPanel = panel;
            c.interactNameText = nameTmp;
            c.interactTypeText = typeTmp;
            c.useSharedPromptResolution = false;
        }

        var dialogues = Object.FindObjectsOfType<CanvasInteractionUIManager>(true);
        for (int i = 0; i < dialogues.Length; i++)
        {
            var c = dialogues[i];
            if (c == null) continue;
            c.interactPromptPanel = panel;
            c.interactNameText = nameTmp;
            c.interactTypeText = typeTmp;
        }

        var carHuds = Object.FindObjectsOfType<CarSystemVehicleHUD>(true);
        for (int i = 0; i < carHuds.Length; i++)
        {
            var h = carHuds[i];
            if (h == null) continue;
            h.sharedPromptPanel = panel;
            h.sharedPromptNameText = nameTmp;
            h.sharedPromptTypeText = typeTmp;
            h.preferSharedPrompt = true;
            h.autoFindSharedPrompt = true;
        }

        var dealers = Object.FindObjectsOfType<VehicleDealerNPC>(true);
        for (int i = 0; i < dealers.Length; i++)
        {
            var d = dealers[i];
            if (d == null) continue;
            d.sharedPromptPanel = panel;
            d.sharedPromptNameText = nameTmp;
            d.sharedPromptTypeText = typeTmp;
            d.useSharedPrompt = true;
            d.autoFindSharedPromptIfUnassigned = true;
        }

        var legacyCars = Object.FindObjectsOfType<VehicleController>(true);
        for (int i = 0; i < legacyCars.Length; i++)
        {
            var v = legacyCars[i];
            if (v == null) continue;
            v.sharedInteractNameText = nameTmp;
            v.sharedInteractTypeText = typeTmp;
            v.useSharedInteractPrompt = true;
            v.autoFindSharedInteractPromptIfUnassigned = true;
        }

        var storyNpcs = Object.FindObjectsOfType<GameQuestSystem.StoryQuestNpc>(true);
        for (int i = 0; i < storyNpcs.Length; i++)
        {
            var npc = storyNpcs[i];
            if (npc == null) continue;
            npc.RebindSharedPrompt(panel, nameTmp, typeTmp);
        }

        var creditsTriggers = Object.FindObjectsOfType<FlowerBedCreditsTrigger>(true);
        for (int i = 0; i < creditsTriggers.Length; i++)
        {
            var trigger = creditsTriggers[i];
            if (trigger == null) continue;
            trigger.RebindSharedPrompt(panel, nameTmp, typeTmp);
        }

        var elevators = Object.FindObjectsOfType<ElevatorInteractor>(true);
        for (int i = 0; i < elevators.Length; i++)
        {
            var elevator = elevators[i];
            if (elevator == null) continue;
            elevator.RebindSharedPrompt(panel, nameTmp, typeTmp);
        }
    }

    /// <summary>场景加载后隐藏所有共用交互面板（无 Offer 时不应显示背景框）。</summary>
    public static void HideAllSharedPanelsInLoadedScenes()
    {
        if (TryGetCanonical(out _, out var nameTmp, out var typeTmp))
        {
            SetSharedPanelVisible(nameTmp, typeTmp, false);
            return;
        }

        var panels = CollectAllSharedPanels();
        for (int i = 0; i < panels.Count; i++)
        {
            if (panels[i] != null)
                SetSharedPanelVisible(panels[i], false);
        }
    }

    public static void SetSharedPanelVisible(TextMeshProUGUI nameTmp, TextMeshProUGUI typeTmp, bool visible)
    {
        if (!TryResolveSharedPanel(nameTmp, typeTmp, out var panelGo)
            && TryGetCanonical(out var canonicalPanel, out _, out _))
        {
            panelGo = canonicalPanel.gameObject;
        }

        if (panelGo == null)
            return;

        SetSharedPanelVisible(panelGo, visible);
    }

    public static void SetSharedPanelVisible(GameObject panelGo, bool visible)
    {
        if (panelGo == null)
            return;

        if (visible)
        {
            EnsureGameplayHudCanvasVisible();
            EnsureSharedPanelOnHud(panelGo);
            EnsurePromptTextVisible(panelGo);
        }

        var cg = panelGo.GetComponent<CanvasGroup>();
        if (visible)
        {
            if (!panelGo.activeSelf)
                panelGo.SetActive(true);
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
        }
        else
        {
            if (cg != null)
            {
                cg.alpha = 0f;
                cg.interactable = false;
                cg.blocksRaycasts = false;
            }
            if (panelGo.activeSelf)
                panelGo.SetActive(false);
        }
    }

    public static bool TryResolveSharedPanel(TextMeshProUGUI nameTmp, TextMeshProUGUI typeTmp, out GameObject panelGo)
    {
        panelGo = null;
        if (TryGetCanonical(out var panel, out var cn, out var ct))
        {
            if (nameTmp == cn || typeTmp == ct || nameTmp == null || typeTmp == null)
            {
                panelGo = panel.gameObject;
                return true;
            }
        }

        TextMeshProUGUI any = nameTmp != null ? nameTmp : typeTmp;
        if (any == null)
            return false;

        Transform t = any.transform;
        while (t != null)
        {
            if (string.Equals(t.name, PanelObjectName, System.StringComparison.Ordinal))
            {
                panelGo = t.gameObject;
                return true;
            }
            t = t.parent;
        }

        return false;
    }

    public static void EnsureSharedPanelOnHud(GameObject panelGo)
    {
        if (panelGo == null)
            return;

        EnsureGameplayHudCanvasVisible();
        Transform parent = EnsureDedicatedInteractPromptCanvas().transform;

        var rt = panelGo.GetComponent<RectTransform>();
        if (rt == null)
            return;

        bool needsReparent = rt.parent != parent;
        if (needsReparent)
        {
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 200f);
            rt.sizeDelta = new Vector2(InteractPromptSharedStyle.PanelWidth, InteractPromptSharedStyle.PanelHeight);
            rt.localScale = Vector3.one;
            InteractPromptSharedStyle.ApplyIfNeeded(rt);

            var follower = panelGo.GetComponent<InteractPromptSharedScreenFollower>();
            if (follower != null)
                follower.RecaptureDefaultLayoutAfterReparent();
        }

        rt.SetAsLastSibling();
        SetUiLayerRecursively(panelGo, 5);
        EnsurePromptTextVisible(panelGo);
    }

    /// <summary>openpanel 被 Phone HUD 隐藏前，先把共用提示移到独立 Canvas，避免一并被关掉。</summary>
    public static void DetachSharedPromptFromHudIfNeeded()
    {
        if (!TryFindShared(out RectTransform panel, out _, out _))
            return;
        EnsureSharedPanelOnHud(panel.gameObject);
    }

    public static Canvas EnsureDedicatedInteractPromptCanvas()
    {
        if (_dedicatedPromptCanvas != null)
            return _dedicatedPromptCanvas;

        var existing = GameObject.Find("InteractPrompt_HudCanvas");
        if (existing != null)
        {
            _dedicatedPromptCanvas = existing.GetComponent<Canvas>();
            if (_dedicatedPromptCanvas != null)
                return _dedicatedPromptCanvas;
        }

        var go = new GameObject(
            "InteractPrompt_HudCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler),
            typeof(UnityEngine.UI.GraphicRaycaster));
        Object.DontDestroyOnLoad(go);

        _dedicatedPromptCanvas = go.GetComponent<Canvas>();
        _dedicatedPromptCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _dedicatedPromptCanvas.sortingOrder = DedicatedPromptCanvasSortOrder;
        _dedicatedPromptCanvas.pixelPerfect = false;

        var scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return _dedicatedPromptCanvas;
    }

    static void SetUiLayerRecursively(GameObject root, int layer)
    {
        if (root == null)
            return;

        root.layer = layer;
        Transform tr = root.transform;
        for (int i = 0; i < tr.childCount; i++)
            SetUiLayerRecursively(tr.GetChild(i).gameObject, layer);
    }

    static void EnsurePromptTextVisible(GameObject panelGo)
    {
        if (panelGo == null)
            return;

        var content = panelGo.transform.Find("Content");
        if (content != null && !content.gameObject.activeSelf)
            content.gameObject.SetActive(true);

        var fallbackFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF");

        var tmps = panelGo.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < tmps.Length; i++)
        {
            TextMeshProUGUI tmp = tmps[i];
            if (tmp == null)
                continue;

            tmp.enabled = true;
            tmp.alpha = 1f;
            Color c = tmp.color;
            c.a = 1f;
            tmp.color = c;
            if (tmp.font == null && fallbackFont != null)
                tmp.font = fallbackFont;

            // 挂到 DontDestroyOnLoad Canvas 后，场景内嵌的 Atlas Material (Instance) 可能丢失引用导致无字。
            if (tmp.font != null)
            {
                var sharedMat = tmp.fontSharedMaterial;
                if (sharedMat == null || sharedMat.name.Contains("(Instance)"))
                    tmp.fontSharedMaterial = tmp.font.material;
            }

            tmp.ForceMeshUpdate(true);
        }
    }

    static void CacheCanonicalFromPanel(GameObject panelGo)
    {
        if (panelGo == null)
            return;
        _canonicalPanel = panelGo.GetComponent<RectTransform>();
        InteractPromptSharedStyle.ApplyIfNeeded(_canonicalPanel);
        _canonicalNameTmp = FindTmpDeep(panelGo.transform, NameObjectName);
        _canonicalTypeTmp = FindTmpDeep(panelGo.transform, TypeObjectName);
        EnsureScreenFollower(panelGo);
    }

    public static Vector3? ResolveProximityWorldAnchor(Transform trigger, Transform npcRoot = null)
    {
        if (npcRoot != null)
            return npcRoot.position;
        if (trigger != null)
            return trigger.position;
        return null;
    }

    static void EnsureScreenFollower(GameObject panelGo)
    {
        if (panelGo == null)
            return;
        var follower = panelGo.GetComponent<InteractPromptSharedScreenFollower>();
        if (follower == null)
            follower = panelGo.AddComponent<InteractPromptSharedScreenFollower>();
        InteractPromptSharedStyle.ApplyIfNeeded(panelGo.GetComponent<RectTransform>());
        follower.CaptureDefaultLayout();
    }

    static GameObject SelectCanonicalPanel(List<GameObject> panels)
    {
        GameObject best = null;
        int bestScore = int.MinValue;
        for (int i = 0; i < panels.Count; i++)
        {
            GameObject go = panels[i];
            if (go == null) continue;
            int score = ScorePanelForCanonical(go);
            if (score > bestScore)
            {
                bestScore = score;
                best = go;
            }
        }
        return best;
    }

    /// <summary>分数越高越应作为唯一保留的面板（优先 Phone/openpanel，惩罚开场/赛车误建）。</summary>
    static int ScorePanelForCanonical(GameObject panelGo)
    {
        int score = 0;
        if (FindTmpDeep(panelGo.transform, NameObjectName) != null) score += 30;
        if (FindTmpDeep(panelGo.transform, TypeObjectName) != null) score += 30;

        Transform t = panelGo.transform;
        string path = GetHierarchyPath(t).ToLowerInvariant();

        if (path.Contains("openpanel") || path.Contains("/phone"))
            score += 1200;
        if (path.Contains("newuserintrocinematic") || path.Contains("introcinematic"))
            score -= 1500;
        if (IsUnderSuppressedIntroRoot(t))
            score -= 800;

        var phone = Object.FindObjectOfType<PhoneUIManager>(true);
        if (phone != null && phone.hudPanel != null && t.IsChildOf(phone.hudPanel.transform))
            score += 900;

        if (!IsDontDestroyOnLoadObject(panelGo) && panelGo.scene.IsValid())
            score += 120;

        if (t.lossyScale.sqrMagnitude > 0.0001f)
            score += 80;

        score -= GetHierarchyDepth(t) * 2;
        return score;
    }

    static bool IsDontDestroyOnLoadObject(GameObject go)
    {
        if (go == null || !go.scene.IsValid())
            return false;
        return go.scene.name == "DontDestroyOnLoad";
    }

    static int GetHierarchyDepth(Transform t)
    {
        int d = 0;
        while (t != null)
        {
            d++;
            t = t.parent;
        }
        return d;
    }

    static string GetHierarchyPath(Transform t)
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

    static bool IsUnderSuppressedIntroRoot(Transform panel)
    {
        if (panel == null)
            return false;
        var intro = panel.GetComponentInParent<NewUserIntroCinematic>(true);
        if (intro == null)
            return false;
        return GetEffectiveCanvasGroupAlpha(intro.transform) < 0.01f;
    }

    static float GetEffectiveCanvasGroupAlpha(Transform t)
    {
        float a = 1f;
        while (t != null)
        {
            var cg = t.GetComponent<CanvasGroup>();
            if (cg != null)
                a *= cg.alpha;
            t = t.parent;
        }
        return a;
    }

    static Transform FindHudPromptParent()
    {
        if (_cachedHudParent != null)
            return _cachedHudParent;

        var phone = Object.FindObjectOfType<PhoneUIManager>(true);
        if (phone != null && phone.hudPanel != null)
        {
            _cachedHudParent = phone.hudPanel.transform;
            return _cachedHudParent;
        }

        Canvas best = null;
        int bestScore = int.MinValue;
        var canvases = Object.FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas c = canvases[i];
            if (c == null || c.renderMode == RenderMode.WorldSpace)
                continue;
            if (c.GetComponentInParent<NewUserIntroCinematic>(true) != null)
                continue;

            var crt = c.transform as RectTransform;
            if (crt == null || crt.lossyScale.sqrMagnitude < 0.0001f)
                continue;

            int score = c.sortingOrder;
            string n = c.gameObject.name.ToLowerInvariant();
            if (n.Contains("phone"))
                score += 200;
            if (n.Contains("hud") || n.Contains("main") || n.Contains("gameui"))
                score += 80;

            if (score > bestScore)
            {
                bestScore = score;
                best = c;
            }
        }

        if (best != null)
            _cachedHudParent = best.transform;

        return _cachedHudParent;
    }

    static List<GameObject> CollectAllSharedPanels()
    {
        var list = new List<GameObject>(4);
        var seen = new HashSet<int>();

        void TryAdd(GameObject go)
        {
            if (go == null) return;
            int id = go.GetInstanceID();
            if (!seen.Add(id)) return;
            list.Add(go);
        }

        for (int si = 0; si < SceneManager.sceneCount; si++)
        {
            Scene s = SceneManager.GetSceneAt(si);
            if (!s.isLoaded)
                continue;
            GameObject[] roots = s.GetRootGameObjects();
            for (int ri = 0; ri < roots.Length; ri++)
                CollectSharedPanelsRecursive(roots[ri].transform, TryAdd);
        }

        Scene ddol = GetDontDestroyOnLoadScene();
        if (ddol.IsValid() && ddol.isLoaded)
        {
            GameObject[] roots = ddol.GetRootGameObjects();
            for (int ri = 0; ri < roots.Length; ri++)
                CollectSharedPanelsRecursive(roots[ri].transform, TryAdd);
        }

        return list;
    }

    static Scene GetDontDestroyOnLoadScene()
    {
        var probe = new GameObject("InteractPrompt_DDOL_Probe");
        Object.DontDestroyOnLoad(probe);
        Scene scene = probe.scene;
        Object.Destroy(probe);
        return scene;
    }

    static void CollectSharedPanelsRecursive(Transform root, System.Action<GameObject> onFound)
    {
        if (root == null)
            return;
        if (string.Equals(root.name, PanelObjectName, System.StringComparison.Ordinal))
            onFound(root.gameObject);
        for (int i = 0; i < root.childCount; i++)
            CollectSharedPanelsRecursive(root.GetChild(i), onFound);
    }

    static TextMeshProUGUI FindTmpDeep(Transform root, string objectName)
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
            var found = FindTmpDeep(root.GetChild(i), objectName);
            if (found != null)
                return found;
        }
        return null;
    }
}
