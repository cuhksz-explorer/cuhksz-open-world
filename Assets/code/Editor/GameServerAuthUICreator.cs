#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 菜单一键生成 GameServerSync + 登录面板并绑定 GameServerAuthUI。
/// Unity 菜单: Tools > Game Server > Create Login UI (One Click)
/// </summary>
public static class GameServerAuthUICreator
{
    const string MenuPath = "Tools/Game Server/Create Login UI (One Click)";

    /// <summary>
    /// 与工程内 TMP 资源一致：Assets/TextMesh Pro/Resources/Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF.asset
    /// </summary>
    const string LoginUiFontResourcesPath = "Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF";

    [MenuItem(MenuPath, false, 1)]
    public static void CreateOneClick()
    {
        var sync = FindOrCreateNetworkRoot();
        var canvas = FindOrCreateCanvasAndEventSystem();
        var panel = CreateLoginPanel(canvas.transform, sync);
        Selection.activeGameObject = panel;
        EditorGUIUtility.PingObject(panel);
        Debug.Log("[GameServer] Created Login UI. Menu: " + MenuPath);
    }

    [MenuItem("Tools/Game Server/Apply Alibaba Font To Login Panel", false, 2)]
    public static void ApplyFontToExistingPanel()
    {
        GameObject panel = null;
        foreach (var auth in Object.FindObjectsOfType<GameServerAuthUI>(true))
        {
            if (auth.gameObject.name == "GameServer_LoginPanel")
            {
                panel = auth.gameObject;
                break;
            }
        }

        if (panel == null)
        {
            EditorUtility.DisplayDialog("Game Server", "场景中未找到带 GameServerAuthUI 的 GameServer_LoginPanel。", "确定");
            return;
        }

        var font = GetLoginUiFont();
        if (font == null)
        {
            EditorUtility.DisplayDialog("Game Server", "无法加载字体：" + LoginUiFontResourcesPath, "确定");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(panel, "Apply login UI font");
        var tmps = panel.GetComponentsInChildren<TMP_Text>(true);
        foreach (var tmp in tmps)
        {
            Undo.RecordObject(tmp, "Apply login UI font");
            tmp.font = font;
            EditorUtility.SetDirty(tmp);
        }

        Debug.Log($"[GameServer] Applied font to {tmps.Length} TMP objects on GameServer_LoginPanel.");
        Selection.activeGameObject = panel;
        EditorGUIUtility.PingObject(panel);
    }

    static GameServerSync FindOrCreateNetworkRoot()
    {
        var existing = Object.FindObjectOfType<GameServerSync>();
        if (existing != null)
            return existing;

        var go = new GameObject("GameServer_Network");
        Undo.RegisterCreatedObjectUndo(go, "Create GameServer_Network");
        var sync = go.AddComponent<GameServerSync>();
        return sync;
    }

    static Canvas FindOrCreateCanvasAndEventSystem()
    {
        var canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var go = new GameObject("Canvas");
            Undo.RegisterCreatedObjectUndo(go, "Create Canvas");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            go.AddComponent<GraphicRaycaster>();
        }

        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        return canvas;
    }

    static GameObject CreateLoginPanel(Transform canvasRoot, GameServerSync sync)
    {
        if (canvasRoot.Find("GameServer_LoginPanel") != null)
        {
            var old = canvasRoot.Find("GameServer_LoginPanel").gameObject;
            if (!EditorUtility.DisplayDialog(
                    "Game Server",
                    "场景中已存在 GameServer_LoginPanel，是否删除并重新生成？",
                    "重新生成",
                    "取消"))
            {
                return old;
            }

            Undo.DestroyObjectImmediate(old);
        }

        var panel = new GameObject("GameServer_LoginPanel", typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(panel, "Create Login Panel");
        panel.transform.SetParent(canvasRoot, false);

        var auth = Undo.AddComponent<GameServerAuthUI>(panel);
        var so = new SerializedObject(auth);
        so.FindProperty("serverSync").objectReferenceValue = sync;
        so.FindProperty("useLguHubTwoPageLayout").boolValue = true;
        so.FindProperty("hideLogoutButton").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        LguHubAuthUILayout.ApplyToPanel(panel.transform, auth, sync);

        panel.SetActive(false);

        var toggle = sync.gameObject.GetComponent<GameServerLoginPanelToggle>();
        if (toggle == null)
            toggle = Undo.AddComponent<GameServerLoginPanelToggle>(sync.gameObject);
        var tso = new SerializedObject(toggle);
        tso.FindProperty("loginPanel").objectReferenceValue = panel;
        tso.FindProperty("toggleKey").intValue = (int)KeyCode.P;
        tso.FindProperty("lockCursorWhenClosed").boolValue = true;
        tso.ApplyModifiedPropertiesWithoutUndo();

        return panel;
    }

    static void CreateTitle(Transform parent, string text)
    {
        var go = new GameObject("Title", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Title");
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 22;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        ApplyDefaultFont(tmp);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 36f;
        le.preferredHeight = 36f;
    }

    static TextMeshProUGUI CreateStatusText(Transform parent)
    {
        var go = new GameObject("Status", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Status");
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = "请输入用户名和密码";
        tmp.fontSize = 16;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.75f, 0.75f, 0.75f);
        ApplyDefaultFont(tmp);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 28f;
        le.preferredHeight = 28f;
        return tmp;
    }

    static TMP_InputField CreateInputField(Transform parent, string name, string placeholder, bool password)
    {
        var go = new GameObject(name + "Field", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);

        var leRoot = go.AddComponent<LayoutElement>();
        leRoot.minHeight = 44f;
        leRoot.preferredHeight = 44f;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.22f, 0.22f, 0.24f, 1f);

        var input = go.AddComponent<TMP_InputField>();
        input.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        StretchFull(textGo.GetComponent<RectTransform>(), new Vector2(10, 6), new Vector2(-10, -6));
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 18;
        tmp.color = Color.white;
        tmp.enableWordWrapping = false;
        ApplyDefaultFont(tmp);

        var phGo = new GameObject("Placeholder", typeof(RectTransform));
        phGo.transform.SetParent(go.transform, false);
        StretchFull(phGo.GetComponent<RectTransform>(), new Vector2(10, 6), new Vector2(-10, -6));
        var pht = phGo.AddComponent<TextMeshProUGUI>();
        pht.fontSize = 18;
        pht.fontStyle = FontStyles.Italic;
        pht.color = new Color(1f, 1f, 1f, 0.35f);
        pht.text = placeholder;
        ApplyDefaultFont(pht);

        input.textComponent = tmp;
        input.placeholder = pht;
        return input;
    }

    static GameObject CreateButton(Transform parent, string label)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Btn");
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.28f, 0.48f, 0.78f, 1f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var tgo = new GameObject("Text", typeof(RectTransform));
        tgo.transform.SetParent(go.transform, false);
        StretchFull(tgo.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
        var txt = tgo.AddComponent<TextMeshProUGUI>();
        txt.text = label;
        txt.fontSize = 16;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;
        ApplyDefaultFont(txt);

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 40f;
        le.preferredHeight = 40f;

        return go;
    }

    static void StretchFull(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    static TMP_FontAsset GetLoginUiFont()
    {
        var f = Resources.Load<TMP_FontAsset>(LoginUiFontResourcesPath);
        if (f != null) return f;
        return TMP_Settings.defaultFontAsset;
    }

    static void ApplyDefaultFont(TMP_Text tmp)
    {
        var f = GetLoginUiFont();
        if (f != null)
            tmp.font = f;
    }
}
#endif
