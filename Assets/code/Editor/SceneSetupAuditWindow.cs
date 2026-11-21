#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using DormMall;
using GameQuestSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// 检查当前已打开场景：1) Missing Script / 断链资源引用；2) 常见系统配置与空引用。
/// 支持对部分项一键补齐或清除断链引用。
/// </summary>
public class SceneSetupAuditWindow : EditorWindow
{
    enum IssueLevel
    {
        Info,
        Warning,
        Error
    }

    sealed class AuditIssue
    {
        /// <summary>0 = 缺失脚本/资源优先，1 = 断链引用，2 = 常规配置。</summary>
        public int SortBucket = 2;

        public IssueLevel Level;
        public string Title;
        public string Detail;
        public UnityEngine.Object Context;
        public Action Fix;
        public bool CanAutoFix => Fix != null;
    }

    readonly List<AuditIssue> _issues = new List<AuditIssue>();
    Vector2 _scroll;
    GUIStyle _wrap;

    [SerializeField] bool _scanMissingScripts = true;
    [SerializeField] bool _scanBrokenObjectReferences = true;

    bool _scanCancelled;

    [MenuItem("Tools/场景配置检查")]
    static void Open()
    {
        var w = GetWindow<SceneSetupAuditWindow>(true, "场景配置检查", true);
        w.minSize = new Vector2(520, 380);
        w.RunAudit();
    }

    void OnEnable()
    {
        RunAudit();
    }

    void OnGUI()
    {
        if (_wrap == null)
        {
            _wrap = new GUIStyle(EditorStyles.label) { wordWrap = true };
        }

        EditorGUILayout.LabelField(
            "对已加载场景（含未激活物体）做巡检：优先列出缺失脚本与断链资源，其次为任务/商城等常见配置。修复前建议先保存场景或备份。",
            _wrap);
        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("缺失项扫描", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        _scanMissingScripts = EditorGUILayout.ToggleLeft("检测 Missing Script（脚本资源丢失）", _scanMissingScripts);
        _scanBrokenObjectReferences = EditorGUILayout.ToggleLeft(
            "检测断链 Object 引用（材质/网格/预制体等文件丢失；大场景可能较慢）",
            _scanBrokenObjectReferences);
        if (EditorGUI.EndChangeCheck())
            RunAudit();

        EditorGUILayout.Space(6);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("重新检查", GUILayout.Height(26)))
                RunAudit();

            if (GUILayout.Button("一键修复全部可自动修复项", GUILayout.Height(26)))
                FixAll();
        }

        EditorGUILayout.Space(6);

        int err = 0, warn = 0;
        for (int i = 0; i < _issues.Count; i++)
        {
            if (_issues[i].Level == IssueLevel.Error) err++;
            else if (_issues[i].Level == IssueLevel.Warning) warn++;
        }

        if (_issues.Count == 0)
            EditorGUILayout.HelpBox("未发现配置问题（或尚未实现对应检查项）。", MessageType.Info);
        else
            EditorGUILayout.HelpBox($"共 {_issues.Count} 条：错误 {err}，警告 {warn}", err > 0 ? MessageType.Error : MessageType.Warning);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        for (int i = 0; i < _issues.Count; i++)
        {
            var it = _issues[i];
            MessageType mt = it.Level == IssueLevel.Error ? MessageType.Error :
                it.Level == IssueLevel.Warning ? MessageType.Warning : MessageType.Info;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(it.Title, EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(it.Detail))
                EditorGUILayout.LabelField(it.Detail, _wrap);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (it.Context != null && GUILayout.Button("选中对象", GUILayout.Width(72)))
                {
                    Selection.activeObject = it.Context;
                    EditorGUIUtility.PingObject(it.Context);
                }
                if (it.CanAutoFix && GUILayout.Button("一键补齐", GUILayout.Width(72)))
                {
                    try
                    {
                        it.Fix?.Invoke();
                        if (it.Context != null)
                            EditorUtility.SetDirty(it.Context);
                        RunAudit();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }
        EditorGUILayout.EndScrollView();
    }

    void FixAll()
    {
        Undo.SetCurrentGroupName("场景配置检查-一键修复");
        int group = Undo.GetCurrentGroup();
        try
        {
            for (int i = 0; i < _issues.Count; i++)
            {
                var it = _issues[i];
                if (!it.CanAutoFix) continue;
                it.Fix?.Invoke();
                if (it.Context != null)
                    EditorUtility.SetDirty(it.Context);
            }
        }
        finally
        {
            Undo.CollapseUndoOperations(group);
        }

        RunAudit();
        Debug.Log("[场景配置检查] 已尝试执行全部可自动修复项；请再看一遍列表确认。");
    }

    void RunAudit()
    {
        _issues.Clear();

        CheckMissingScriptsAndBrokenReferences();
        if (_scanCancelled)
        {
            _issues.Add(new AuditIssue
            {
                SortBucket = 2,
                Level = IssueLevel.Warning,
                Title = "缺失引用扫描已中止",
                Detail = "进度条上取消了扫描；下列「断链 / Missing Script」仅包含中止前的部分结果。可再次点「重新检查」完整扫描。",
                Context = null,
                Fix = null
            });
        }

        CheckEventSystem();
        CheckMainCamera();

        CheckQuestSystemManager();
        CheckQuestSystemUI();

        CheckFirstPersonCamera();
        CheckPlacementMode();
        CheckDormMallAndInventory();

        _issues.Sort((a, b) =>
        {
            int c = a.SortBucket.CompareTo(b.SortBucket);
            if (c != 0) return c;
            int la = a.Level == IssueLevel.Error ? 0 : a.Level == IssueLevel.Warning ? 1 : 2;
            int lb = b.Level == IssueLevel.Error ? 0 : b.Level == IssueLevel.Warning ? 1 : 2;
            return la != lb ? la.CompareTo(lb) : string.Compare(a.Title, b.Title, StringComparison.Ordinal);
        });
        Repaint();
    }

    void CheckMissingScriptsAndBrokenReferences()
    {
        if (!_scanMissingScripts && !_scanBrokenObjectReferences)
            return;

        _scanCancelled = false;
        int totalTransforms = CountTransformsInOpenScenes();
        int processed = 0;

        try
        {
            for (int si = 0; si < EditorSceneManager.sceneCount; si++)
            {
                var scene = EditorSceneManager.GetSceneAt(si);
                if (!scene.isLoaded || !scene.IsValid())
                    continue;

                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root == null)
                        continue;
                    WalkForMissingAndBrokenRefs(root.transform, ref processed, totalTransforms);
                    if (_scanCancelled)
                        break;
                }

                if (_scanCancelled)
                    break;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    static int CountTransformsInOpenScenes()
    {
        int n = 0;
        for (int si = 0; si < EditorSceneManager.sceneCount; si++)
        {
            var scene = EditorSceneManager.GetSceneAt(si);
            if (!scene.isLoaded || !scene.IsValid())
                continue;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root != null)
                    CountTransformsRecursive(root.transform, ref n);
            }
        }

        return n;
    }

    static void CountTransformsRecursive(Transform t, ref int count)
    {
        count++;
        for (int i = 0; i < t.childCount; i++)
            CountTransformsRecursive(t.GetChild(i), ref count);
    }

    void WalkForMissingAndBrokenRefs(Transform t, ref int processed, int totalTransforms)
    {
        if (_scanCancelled)
            return;

        var go = t.gameObject;
        processed++;

        if (totalTransforms > 400 && (processed & 63) == 0)
        {
            float p = totalTransforms > 0 ? (float)processed / totalTransforms : 0f;
            if (EditorUtility.DisplayCancelableProgressBar(
                    "场景检查",
                    $"扫描缺失引用… {processed}/{totalTransforms}",
                    p))
            {
                _scanCancelled = true;
                return;
            }
        }

        if (_scanMissingScripts)
        {
            int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (missing > 0)
            {
                string path = GetHierarchyPath(go.transform);
                _issues.Add(new AuditIssue
                {
                    SortBucket = 0,
                    Level = IssueLevel.Error,
                    Title = $"Missing Script ×{missing}",
                    Detail =
                        $"{path}\n存在脚本组件丢失（脚本被删、GUID 变更或未导入）。「一键补齐」将移除占位组件；之后请在 Inspector 重新挂载正确脚本。",
                    Context = go,
                    Fix = () =>
                    {
                        Undo.RegisterCompleteObjectUndo(go, "Remove Missing Scripts");
                        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                        MarkSceneDirty(go);
                    }
                });
            }
        }

        if (_scanBrokenObjectReferences)
        {
            var comps = go.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                var c = comps[i];
                if (c == null)
                    continue;
                AddIssuesForBrokenObjectReferences(c);
            }
        }

        for (int i = 0; i < t.childCount; i++)
        {
            WalkForMissingAndBrokenRefs(t.GetChild(i), ref processed, totalTransforms);
            if (_scanCancelled)
                return;
        }
    }

    void AddIssuesForBrokenObjectReferences(Component c)
    {
        using (var so = new SerializedObject(c))
        {
            so.Update();
            var prop = so.GetIterator();
            for (bool enterChildren = true; prop.NextVisible(enterChildren); enterChildren = false)
            {
                if (prop.propertyType != SerializedPropertyType.ObjectReference)
                    continue;
                if (prop.name == "m_Script")
                    continue;
                if (prop.objectReferenceValue != null)
                    continue;
                if (prop.objectReferenceInstanceIDValue == 0)
                    continue;

                string propPath = prop.propertyPath;
                string hierarchy = GetHierarchyPath(c.transform);
                string typeName = c.GetType().Name;
                _issues.Add(new AuditIssue
                {
                    SortBucket = 1,
                    Level = IssueLevel.Error,
                    Title = "断链资源引用（文件缺失或无法加载）",
                    Detail =
                        $"{hierarchy}\n组件：{typeName}\n字段：{propPath}\n" +
                        "Unity 仍记录该引用 ID，但资源已不可用。「一键补齐」将清空该字段；请之后在 Inspector 重新指定正确资源。",
                    Context = c,
                    Fix = () =>
                    {
                        Undo.RecordObject(c, "Clear broken object reference");
                        var so2 = new SerializedObject(c);
                        so2.Update();
                        var p2 = so2.FindProperty(propPath);
                        if (p2 != null && p2.propertyType == SerializedPropertyType.ObjectReference)
                        {
                            p2.objectReferenceValue = null;
                            so2.ApplyModifiedProperties();
                        }

                        EditorUtility.SetDirty(c);
                        MarkSceneDirty(c.gameObject);
                    }
                });
            }
        }
    }

    static string GetHierarchyPath(Transform t)
    {
        if (t == null)
            return "(null)";
        var sb = new StringBuilder();
        var stack = new List<string>();
        Transform cur = t;
        while (cur != null)
        {
            stack.Add(cur.name);
            cur = cur.parent;
        }

        for (int i = stack.Count - 1; i >= 0; i--)
        {
            if (sb.Length > 0)
                sb.Append('/');
            sb.Append(stack[i]);
        }

        var sc = t.gameObject.scene;
        string prefix = sc.IsValid() ? $"[{sc.name}] " : "";
        return prefix + sb;
    }

    static void MarkSceneDirty(GameObject go)
    {
        if (go == null)
            return;
        var sc = go.scene;
        if (sc.IsValid())
            EditorSceneManager.MarkSceneDirty(sc);
    }

    static T[] FindAll<T>() where T : UnityEngine.Object
    {
#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        return UnityEngine.Object.FindObjectsOfType<T>(true);
#endif
    }

    void CheckEventSystem()
    {
        var es = FindAll<EventSystem>();
        if (es == null || es.Length == 0)
        {
            _issues.Add(new AuditIssue
            {
                Level = IssueLevel.Error,
                Title = "场景中缺少 EventSystem",
                Detail = "UGUI / 按钮需要 EventSystem。可一键在场景根节点创建。",
                Context = null,
                Fix = () =>
                {
                    var go = new GameObject("EventSystem");
                    Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
                    go.AddComponent<EventSystem>();
                    go.AddComponent<StandaloneInputModule>();
                }
            });
            return;
        }

        foreach (var e in es)
        {
            if (e.GetComponent<StandaloneInputModule>() == null && e.GetComponent<BaseInputModule>() == null)
            {
                _issues.Add(new AuditIssue
                {
                    Level = IssueLevel.Warning,
                    Title = "EventSystem 上未找到输入模块",
                    Detail = $"物体：{e.name}。通常需要 StandaloneInputModule（或项目使用的输入模块）。",
                    Context = e,
                    Fix = () =>
                    {
                        Undo.AddComponent<StandaloneInputModule>(e.gameObject);
                    }
                });
            }
        }
    }

    void CheckMainCamera()
    {
        if (Camera.main == null)
        {
            _issues.Add(new AuditIssue
            {
                Level = IssueLevel.Warning,
                Title = "未找到标记为 MainCamera 的相机",
                Detail = "Camera.main 为空时，部分脚本（如放置模式）可能回退异常。请在主相机上勾选 Tag = MainCamera。",
                Context = null,
                Fix = null
            });
        }
    }

    void CheckQuestSystemManager()
    {
        var list = FindAll<QuestSystemManager>();
        if (list == null || list.Length == 0)
        {
            _issues.Add(new AuditIssue
            {
                Level = IssueLevel.Error,
                Title = "场景中没有 QuestSystemManager",
                Detail = "任务系统需要 QuestSystemManager。可用菜单 Tools/Quest System/One Click Setup 生成。",
                Context = null,
                Fix = null
            });
            return;
        }

        if (list.Length > 1)
        {
            _issues.Add(new AuditIssue
            {
                Level = IssueLevel.Warning,
                Title = $"存在 {list.Length} 个 QuestSystemManager",
                Detail = "可能导致逻辑混乱，建议场景中只保留一个。",
                Context = list[0],
                Fix = null
            });
        }

        foreach (var m in list)
        {
            var so = new SerializedObject(m);
            var player = so.FindProperty("playerTransform");
            if (player != null && player.objectReferenceValue == null)
            {
                _issues.Add(new AuditIssue
                {
                    Level = IssueLevel.Warning,
                    Title = "QuestSystemManager.playerTransform 未指定",
                    Detail = "距离判定等可能异常。可尝试从 PlayerMovement / 名称 Player 自动绑定。",
                    Context = m,
                    Fix = () =>
                    {
                        var tr = FindPlayerTransform();
                        if (tr == null) return;
                        var s = new SerializedObject(m);
                        s.FindProperty("playerTransform").objectReferenceValue = tr;
                        s.ApplyModifiedProperties();
                    }
                });
            }

            var chains = so.FindProperty("questChains");
            if (chains != null && chains.arraySize == 0)
            {
                _issues.Add(new AuditIssue
                {
                    Level = IssueLevel.Warning,
                    Title = "QuestSystemManager.questChains 为空",
                    Detail = "没有配置任务链时不会有可追踪任务。请在 Inspector 中添加 Quest Chain。",
                    Context = m,
                    Fix = null
                });
            }
        }
    }

    static Transform FindPlayerTransform()
    {
        var pm = UnityEngine.Object.FindObjectOfType<PlayerMovement>(true);
        if (pm != null) return pm.transform;
        var go = GameObject.Find("Player");
        return go != null ? go.transform : null;
    }

    void CheckQuestSystemUI()
    {
        var uis = FindAll<QuestSystemUI>();
        if (uis == null || uis.Length == 0)
        {
            _issues.Add(new AuditIssue
            {
                Level = IssueLevel.Warning,
                Title = "场景中没有 QuestSystemUI",
                Detail = "简要任务栏 / J 键详情面板需要该组件。可用 Tools/Quest System/One Click Setup。",
                Context = null,
                Fix = null
            });
            return;
        }

        foreach (var ui in uis)
        {
            var so = new SerializedObject(ui);

            FixNullRef(so, ui, "questManager", IssueLevel.Error, "QuestSystemUI.questManager 未指定",
                "将指向场景中的 QuestSystemManager。", () =>
                {
                    var m = UnityEngine.Object.FindObjectOfType<QuestSystemManager>(true);
                    if (m == null) return;
                    so.FindProperty("questManager").objectReferenceValue = m;
                    so.ApplyModifiedProperties();
                });

            so = new SerializedObject(ui);
            FixNullRef(so, ui, "briefPanel", IssueLevel.Error, "QuestSystemUI.briefPanel 未指定",
                "简要任务栏根物体缺失。", null);

            so = new SerializedObject(ui);
            FixNullRef(so, ui, "fullPanel", IssueLevel.Error, "QuestSystemUI.fullPanel 未指定",
                "完整任务面板根物体缺失。可用 One Click Setup 生成 UI。", null);

            string[] important = { "briefTitleText", "briefProgressText", "briefIntroText",
                "fullCloseButton", "fullDetailText", "fullTitleText" };
            foreach (var field in important)
            {
                so = new SerializedObject(ui);
                var p = so.FindProperty(field);
                if (p == null) continue;
                if (p.objectReferenceValue == null)
                {
                    _issues.Add(new AuditIssue
                    {
                        Level = IssueLevel.Warning,
                        Title = $"QuestSystemUI.{field} 未指定",
                        Detail = "部分 UI 功能会异常。建议在 Inspector 补齐或运行 QuestSystemAutoUIBuilder。",
                        Context = ui,
                        Fix = null
                    });
                }
            }
        }
    }

    void FixNullRef(SerializedObject so, UnityEngine.Object ctx, string propName, IssueLevel lv, string title, string detail, Action fix)
    {
        var p = so.FindProperty(propName);
        if (p == null || p.objectReferenceValue != null) return;
        _issues.Add(new AuditIssue
        {
            Level = lv,
            Title = title,
            Detail = detail,
            Context = ctx,
            Fix = fix
        });
    }

    void CheckFirstPersonCamera()
    {
        var fps = FindAll<FirstPersonCamera>();
        if (fps == null || fps.Length == 0)
        {
            _issues.Add(new AuditIssue
            {
                Level = IssueLevel.Info,
                Title = "未找到 FirstPersonCamera",
                Detail = "若本场景为第一人称玩法，需要挂载 CC1P（FirstPersonCamera）；纯 UI 场景可忽略。",
                Context = null,
                Fix = null
            });
        }
    }

    void CheckPlacementMode()
    {
        var pm = FindAll<PlacementModeController>();
        if (pm == null || pm.Length == 0)
            return;

        if (pm.Length > 1)
        {
            _issues.Add(new AuditIssue
            {
                Level = IssueLevel.Warning,
                Title = $"存在 {pm.Length} 个 PlacementModeController",
                Detail = "单例会销毁多余实例，建议只保留一个。",
                Context = pm[0],
                Fix = null
            });
        }

        foreach (var p in pm)
        {
            if (p.firstPersonCamera == null)
            {
                _issues.Add(new AuditIssue
                {
                    Level = IssueLevel.Warning,
                    Title = "PlacementModeController.firstPersonCamera 未指定",
                    Detail = "运行时可能会自动查找；建议在 Inspector 指定主第一人称相机以提高稳定性。",
                    Context = p,
                    Fix = () =>
                    {
                        var f = UnityEngine.Object.FindObjectOfType<FirstPersonCamera>(true);
                        if (f == null) return;
                        Undo.RecordObject(p, "Bind FirstPersonCamera");
                        p.firstPersonCamera = f;
                        EditorUtility.SetDirty(p);
                    }
                });
            }

            if (p.placementCamera == null)
            {
                _issues.Add(new AuditIssue
                {
                    Level = IssueLevel.Info,
                    Title = "PlacementModeController.placementCamera 未指定",
                    Detail = "会尝试使用 FirstPersonCamera 或 Camera.main。可手动指定。",
                    Context = p,
                    Fix = () =>
                    {
                        Camera c = null;
                        if (p.firstPersonCamera != null) c = p.firstPersonCamera.GetComponent<Camera>();
                        if (c == null) c = Camera.main;
                        if (c == null) return;
                        Undo.RecordObject(p, "Bind placementCamera");
                        p.placementCamera = c;
                        EditorUtility.SetDirty(p);
                    }
                });
            }

            if (!p.autoFindHelpPanel && p.placementHelpPanel == null)
            {
                _issues.Add(new AuditIssue
                {
                    Level = IssueLevel.Info,
                    Title = "PlacementModeController.placementHelpPanel 未指定且关闭自动查找",
                    Detail = "放置模式说明条不会显示。可勾选 autoFindHelpPanel 或拖入 PlacementHelpPanel。",
                    Context = p,
                    Fix = () =>
                    {
                        var h = UnityEngine.Object.FindObjectOfType<PlacementHelpPanel>(true);
                        if (h == null) return;
                        Undo.RecordObject(p, "Bind PlacementHelpPanel");
                        p.placementHelpPanel = h;
                        EditorUtility.SetDirty(p);
                    }
                });
            }
            else if (p.autoFindHelpPanel && p.placementHelpPanel == null)
            {
                var h = UnityEngine.Object.FindObjectOfType<PlacementHelpPanel>(true);
                if (h == null)
                {
                    _issues.Add(new AuditIssue
                    {
                        Level = IssueLevel.Info,
                        Title = "场景中无 PlacementHelpPanel",
                        Detail = "放置模式顶栏说明需要该组件。菜单：GameObject → DormMall → 创建放置模式说明面板。",
                        Context = p,
                        Fix = null
                    });
                }
            }
        }
    }

    void CheckDormMallAndInventory()
    {
        var malls = FindAll<DormMallUI>();
        foreach (var mall in malls)
        {
            if (mall.database == null)
            {
                _issues.Add(new AuditIssue
                {
                    Level = IssueLevel.Error,
                    Title = "DormMallUI.database 未指定",
                    Detail = "商城无商品数据。可放入 Resources/DormMall/ProductDatabase 或指定 asset。",
                    Context = mall,
                    Fix = () =>
                    {
                        var db = Resources.Load<ProductDatabase>("DormMall/ProductDatabase");
                        if (db == null) return;
                        Undo.RecordObject(mall, "Bind ProductDatabase");
                        mall.database = db;
                        EditorUtility.SetDirty(mall);
                    }
                });
            }

            void CheckField(string name, string label)
            {
                var so = new SerializedObject(mall);
                var prop = so.FindProperty(name);
                if (prop != null && prop.objectReferenceValue == null)
                {
                    _issues.Add(new AuditIssue
                    {
                        Level = IssueLevel.Warning,
                        Title = $"DormMallUI.{name} 未指定",
                        Detail = label,
                        Context = mall,
                        Fix = null
                    });
                }
            }

            CheckField("productGridContent", "商品列表无法生成。");
            CheckField("categoryContainer", "分类栏可能无法使用。");
            CheckField("modalPanel", "购买弹窗无法显示。");
        }

        var inv = FindAll<InventoryUI>();
        foreach (var i in inv)
        {
            if (i.inventoryPanel == null || i.inventoryContent == null)
            {
                _issues.Add(new AuditIssue
                {
                    Level = IssueLevel.Warning,
                    Title = "InventoryUI 面板引用不完整",
                    Detail = "inventoryPanel 或 inventoryContent 为空时运行时会尝试 CreateRuntimeUI；建议在 Inspector 配置稳定结构。",
                    Context = i,
                    Fix = null
                });
            }
        }

        var helps = FindAll<PlacementHelpPanel>();
        foreach (var h in helps)
        {
            var so = new SerializedObject(h);
            var pr = so.FindProperty("panelRoot");
            var bt = so.FindProperty("bodyText");
            if ((pr != null && pr.objectReferenceValue == null) || (bt != null && bt.objectReferenceValue == null))
            {
                _issues.Add(new AuditIssue
                {
                    Level = IssueLevel.Error,
                    Title = "PlacementHelpPanel 引用缺失",
                    Detail = "panelRoot 或 bodyText 未指定，说明条无法工作。请重新用菜单「创建放置模式说明面板」生成并绑定。",
                    Context = h,
                    Fix = null
                });
            }
        }
    }
}
#endif
