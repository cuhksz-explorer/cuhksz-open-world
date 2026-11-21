#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 以 undergarden 为模板，用 Instantiate 整棵复制 Phone / QuestSystemUI 等（保留完整嵌套与引用）。
/// </summary>
public static class UndergardenUiSceneSetup
{
    const string BatchMenuPath = "Tools/场景配置/一键统一所有场景 UI（undergarden 标准）";
    const string TemplateScenePath = "Assets/scene/undergarden.unity";
    const string SceneFolder = "Assets/scene";
    const string BatchTempScenePath = "Assets/scene/__ui_batch_temp.unity";
    const string OpenPanelName = "openpanel";
    const string PhoneCanvasName = "Phone";
    const string QuestSystemUiName = "QuestSystemUI";
    const string QuestSystemManagerName = "QuestSystemManager";
    const string InteractionHubName = "SceneInteractionHub";

    static readonly string[] UiSystemRootNames =
    {
        PhoneCanvasName,
        QuestSystemUiName,
        QuestSystemManagerName,
        "EventSystem",
        "InteractPrompt_Shared",
        OpenPanelName,
        "closepanel",
        "PhoneMenuRoot",
        "PhoneLauncher",
        "PhoneShell",
        "PhoneFrame",
    };

    static readonly string[] OrphanUiRootPrefixes =
    {
        "Btn_",
        "AppSlot_",
        "Slot",
        "TitleText",
        "IntroText",
        "Text (TMP)",
        "Qualitychanger",
    };

    [MenuItem(BatchMenuPath, false, 1)]
    public static void ConfigureAllFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("统一场景 UI", "请先退出 Play Mode。", "确定");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "统一所有场景 UI",
                "将处理 Assets/scene 下全部 gameplay 场景（跳过 undergarden、Loadingscene 等）：\n\n" +
                "· 清除 YAML 散件等错误 UI 根节点\n" +
                "· 从 undergarden Instantiate 复制 Phone / QuestSystemUI / QuestSystemManager\n" +
                "· 配置 InteractPrompt_Shared 与 CanvasScaler\n\n" +
                "未保存的修改请先保存。是否继续？",
                "继续",
                "取消"))
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var summary = ConfigureAllScenesInternal();
        EditorUtility.DisplayDialog(
            "统一场景 UI 完成",
            $"处理: {summary.Processed}\n保存: {summary.Saved}\n" +
            $"新增 Phone HUD: {summary.CopiedPhoneHud}\n" +
            $"新增 QuestSystemUI: {summary.CopiedQuestSystemUi}\n" +
            $"清除散件: {summary.RemovedBrokenUiRoots}",
            "确定");
    }

    /// <summary>供 Unity -batchmode -executeMethod 调用。</summary>
    public static void ConfigureAllScenesAndSave()
    {
        ConfigureAllScenesInternal();
    }

    public struct BatchSummary
    {
        public int Processed;
        public int Saved;
        public int CopiedPhoneHud;
        public int CopiedQuestSystemUi;
        public int RemovedBrokenUiRoots;
    }

    public static BatchSummary ConfigureAllScenesInternal()
    {
        var summary = new BatchSummary();
        string[] scenePaths = CollectScenePaths();
        var originalSetup = EditorSceneManager.GetSceneManagerSetup();
        Scene templateScene = default;

        try
        {
            templateScene = EditorSceneManager.OpenScene(TemplateScenePath, OpenSceneMode.Additive);

            for (int i = 0; i < scenePaths.Length; i++)
            {
                string path = scenePaths[i];
                EditorUtility.DisplayProgressBar(
                    "统一场景 UI",
                    path,
                    scenePaths.Length <= 1 ? 1f : (float)i / scenePaths.Length);

                var scene = OpenTargetScene(path);
                if (!scene.IsValid())
                    continue;

                var report = ConfigureScene(scene, templateScene);
                summary.Processed++;
                summary.CopiedPhoneHud += report.CopiedPhoneHud ? 1 : 0;
                summary.CopiedQuestSystemUi += report.CopiedQuestSystemUi ? 1 : 0;
                summary.RemovedBrokenUiRoots += report.RemovedBrokenUiRoots;

                if (scene.isDirty)
                {
                    SaveTargetScene(scene, path);
                    summary.Saved++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (templateScene.IsValid() && templateScene.isLoaded)
                EditorSceneManager.CloseScene(templateScene, true);
            if (originalSetup != null && originalSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            CleanupBatchTempScene();
        }

        return summary;
    }

    public static SetupReport ConfigureScene(Scene targetScene, bool replaceExistingUi = false)
    {
        Scene templateScene = default;
        bool openedTemplate = false;
        try
        {
            templateScene = EditorSceneManager.OpenScene(TemplateScenePath, OpenSceneMode.Additive);
            openedTemplate = true;
            return ConfigureScene(targetScene, templateScene, replaceExistingUi);
        }
        finally
        {
            if (openedTemplate && templateScene.IsValid() && templateScene.isLoaded)
                EditorSceneManager.CloseScene(templateScene, true);
        }
    }

    public static SetupReport ConfigureScene(Scene targetScene, Scene templateScene)
    {
        return ConfigureScene(targetScene, templateScene, replaceExistingUi: false);
    }

    public static SetupReport ConfigureScene(Scene targetScene, Scene templateScene, bool replaceExistingUi)
    {
        var report = new SetupReport();
        if (!targetScene.IsValid() || !templateScene.IsValid())
            return report;

        if (string.Equals(targetScene.path, TemplateScenePath, StringComparison.OrdinalIgnoreCase))
            return report;

        EditorSceneManager.SetActiveScene(targetScene);
        report.RemovedBrokenUiRoots = replaceExistingUi
            ? PurgeExistingUndergardenUiSystems(targetScene)
            : PurgeBrokenImportedUi(targetScene);
        report.CanvasScaleFixed = FixZeroScaleCanvases(targetScene);
        EnsureEventSystem(targetScene, report);

        report.CopiedPhoneHud = CopyPhoneHudIfMissing(templateScene, targetScene);
        report.CopiedOpenPanel = report.CopiedPhoneHud || CopyOpenPanelIfMissing(templateScene, targetScene);
        report.CopiedQuestSystemUi = CopyQuestSystemUiIfMissing(templateScene, targetScene);
        report.CopiedQuestSystemManager = CopyQuestSystemManagerIfMissing(templateScene, targetScene);

        report.PromptConfigured = InteractionPromptOneClickSetup.SetupSharedPromptForScene(targetScene);
        report.DuplicatePanelsRemoved = InteractPromptAllScenesConfigurator.ConfigureLoadedScene(targetScene);
        report.InteractionHubCreated = EnsureInteractionHub(targetScene, report);
        report.PhoneHudBound = BindPhoneHudPanel(targetScene);
        WireQuestManagerReferences(targetScene);

        EditorSceneManager.MarkSceneDirty(targetScene);
        return report;
    }

    static string[] CollectScenePaths()
    {
        string root = Path.Combine(Application.dataPath, "scene");
        if (!Directory.Exists(root))
            return Array.Empty<string>();

        var list = new List<string>();
        foreach (string file in Directory.GetFiles(root, "*.*", SearchOption.AllDirectories))
        {
            string ext = Path.GetExtension(file);
            if (!string.Equals(ext, ".unity", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(ext, ".scene", StringComparison.OrdinalIgnoreCase))
                continue;

            string assetPath = ToAssetPath(file);
            if (string.IsNullOrEmpty(assetPath))
                continue;
            if (IsBackupOrBrokenScenePath(assetPath))
                continue;
            if (string.Equals(assetPath, TemplateScenePath, StringComparison.OrdinalIgnoreCase))
                continue;
            if (GameplayUiSceneExclusions.ShouldSkipScenePath(assetPath))
                continue;

            list.Add(assetPath);
        }

        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list.ToArray();
    }

    static bool IsBackupOrBrokenScenePath(string assetPath)
    {
        string fileName = Path.GetFileName(assetPath);
        return fileName.IndexOf(".bak", StringComparison.OrdinalIgnoreCase) >= 0
               || fileName.IndexOf(".broken", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static string ToAssetPath(string fullPath)
    {
        fullPath = fullPath.Replace('\\', '/');
        string dataPath = Application.dataPath.Replace('\\', '/');
        if (!fullPath.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
            return null;
        return "Assets" + fullPath.Substring(dataPath.Length);
    }

    public static Scene OpenTargetScene(string assetPath)
    {
        if (assetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            return EditorSceneManager.OpenScene(assetPath, OpenSceneMode.Single);

        CopySceneAsset(assetPath, BatchTempScenePath);
        AssetDatabase.ImportAsset(BatchTempScenePath, ImportAssetOptions.ForceUpdate);
        return EditorSceneManager.OpenScene(BatchTempScenePath, OpenSceneMode.Single);
    }

    public static void SaveTargetScene(Scene scene, string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            EditorSceneManager.SaveScene(scene);
            return;
        }

        if (assetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
        {
            EditorSceneManager.SaveScene(scene);
            return;
        }

        if (assetPath.EndsWith(".scene", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(scene.path, BatchTempScenePath, StringComparison.OrdinalIgnoreCase))
            {
                EditorSceneManager.SaveScene(scene);
                CopySceneAsset(BatchTempScenePath, assetPath);
            }
            else
            {
                EditorSceneManager.SaveScene(scene);
            }

            return;
        }

        EditorSceneManager.SaveScene(scene);
    }

    public static string FormatSetupReport(SetupReport report)
    {
        return
            $"配置完成。\n\n" +
            $"修复 Canvas 缩放: {report.CanvasScaleFixed}\n" +
            $"复制 Phone HUD: {(report.CopiedPhoneHud ? "是" : "否（已存在）")}\n" +
            $"复制 openpanel: {(report.CopiedOpenPanel ? "是" : "否（已存在）")}\n" +
            $"复制 QuestSystemUI: {(report.CopiedQuestSystemUi ? "是" : "否（已存在）")}\n" +
            $"复制 QuestSystemManager: {(report.CopiedQuestSystemManager ? "是" : "否（已存在）")}\n" +
            $"交互提示面板: {(report.PromptConfigured ? "已配置" : "失败")}\n" +
            $"交互控制器: {(report.InteractionHubCreated ? "已创建" : "已存在")}\n" +
            $"绑定 Phone HUD: {(report.PhoneHudBound ? "是" : "否")}\n" +
            $"删除重复提示面板: {report.DuplicatePanelsRemoved}\n" +
            $"清除错误散件 UI: {report.RemovedBrokenUiRoots}";
    }

    static void CopySceneAsset(string fromAssetPath, string toAssetPath)
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string FromFull(string rel) => Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar));
        File.Copy(FromFull(fromAssetPath), FromFull(toAssetPath), true);
    }

    static void CleanupBatchTempScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BatchTempScenePath) != null)
            AssetDatabase.DeleteAsset(BatchTempScenePath);
    }

    static int PurgeBrokenImportedUi(Scene scene)
    {
        int removed = 0;
        foreach (var root in scene.GetRootGameObjects())
            removed += TryDestroyBrokenUiRoot(root);
        return removed;
    }

    public static int PurgeExistingUiSystems(Scene scene) => PurgeExistingUndergardenUiSystems(scene);

    static int PurgeExistingUndergardenUiSystems(Scene scene)
    {
        int removed = PurgeBrokenImportedUi(scene);

        var phone = FindByNameInScene(scene, PhoneCanvasName);
        if (phone != null)
        {
            Undo.DestroyObjectImmediate(phone);
            removed++;
        }

        var questUi = FindInScene<GameQuestSystem.QuestSystemUI>(scene);
        if (questUi != null)
        {
            Undo.DestroyObjectImmediate(questUi.gameObject);
            removed++;
        }

        var questManager = FindInScene<GameQuestSystem.QuestSystemManager>(scene);
        if (questManager != null)
        {
            Undo.DestroyObjectImmediate(questManager.gameObject);
            removed++;
        }

        var interactPrompt = FindByNameInScene(scene, "InteractPrompt_Shared");
        if (interactPrompt != null && interactPrompt.transform.parent == null)
        {
            Undo.DestroyObjectImmediate(interactPrompt);
            removed++;
        }

        return removed;
    }

    static int TryDestroyBrokenUiRoot(GameObject root)
    {
        if (root == null)
            return 0;

        if (Array.IndexOf(UiSystemRootNames, root.name) >= 0)
        {
            Undo.DestroyObjectImmediate(root);
            return 1;
        }

        if (root.GetComponent<RectTransform>() == null)
            return 0;

        if (IsLikelyOrphanPhoneUiRoot(root.name))
        {
            Undo.DestroyObjectImmediate(root);
            return 1;
        }

        return 0;
    }

    static bool IsLikelyOrphanPhoneUiRoot(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return false;

        for (int i = 0; i < OrphanUiRootPrefixes.Length; i++)
        {
            if (objectName.StartsWith(OrphanUiRootPrefixes[i], StringComparison.Ordinal))
                return true;
        }

        return string.Equals(objectName, "Background", StringComparison.Ordinal)
               || string.Equals(objectName, "Viewport", StringComparison.Ordinal)
               || string.Equals(objectName, "Fill Area", StringComparison.Ordinal)
               || string.Equals(objectName, "ButtonRow", StringComparison.Ordinal)
               || string.Equals(objectName, "Body", StringComparison.Ordinal)
               || string.Equals(objectName, "Name", StringComparison.Ordinal)
               || string.Equals(objectName, "Category", StringComparison.Ordinal);
    }

    static bool CopyPhoneHudIfMissing(Scene templateScene, Scene targetScene)
    {
        if (FindByNameInScene(targetScene, PhoneCanvasName) != null)
            return false;

        var source = FindByNameInScene(templateScene, PhoneCanvasName);
        if (source == null)
            return false;

        var clone = UnityEngine.Object.Instantiate(source);
        clone.name = PhoneCanvasName;
        Undo.RegisterCreatedObjectUndo(clone, "Copy Phone HUD");
        SceneManager.MoveGameObjectToScene(clone, targetScene);

        var rt = clone.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.localScale = Vector3.zero;
            rt.localPosition = Vector3.zero;
        }

        clone.SetActive(source.activeSelf);
        EditorUtility.SetDirty(clone);
        return true;
    }

    static bool CopyQuestSystemUiIfMissing(Scene templateScene, Scene targetScene)
    {
        if (FindInScene<GameQuestSystem.QuestSystemUI>(targetScene) != null)
            return false;

        var source = FindInScene<GameQuestSystem.QuestSystemUI>(templateScene);
        if (source == null)
            return false;

        var clone = UnityEngine.Object.Instantiate(source.gameObject);
        clone.name = QuestSystemUiName;
        Undo.RegisterCreatedObjectUndo(clone, "Copy QuestSystemUI");
        SceneManager.MoveGameObjectToScene(clone, targetScene);
        clone.transform.SetAsLastSibling();
        EditorUtility.SetDirty(clone);
        return true;
    }

    static bool CopyQuestSystemManagerIfMissing(Scene templateScene, Scene targetScene)
    {
        if (FindInScene<GameQuestSystem.QuestSystemManager>(targetScene) != null)
            return false;

        var source = FindInScene<GameQuestSystem.QuestSystemManager>(templateScene);
        if (source == null)
            return false;

        var clone = UnityEngine.Object.Instantiate(source.gameObject);
        clone.name = QuestSystemManagerName;
        Undo.RegisterCreatedObjectUndo(clone, "Copy QuestSystemManager");
        SceneManager.MoveGameObjectToScene(clone, targetScene);
        clone.transform.SetAsLastSibling();
        EditorUtility.SetDirty(clone);
        return true;
    }

    static void WireQuestManagerReferences(Scene scene)
    {
        var manager = FindInScene<GameQuestSystem.QuestSystemManager>(scene);
        if (manager == null)
            return;

        Undo.RecordObject(manager, "Wire QuestSystemManager");
        var so = new SerializedObject(manager);
        var playerProp = so.FindProperty("playerTransform");
        var syncProp = so.FindProperty("gameServerSync");
        var dbProp = so.FindProperty("storyQuestDatabase");

        if (playerProp != null && playerProp.objectReferenceValue == null)
        {
            var player = FindInScene<PlayerMovement>(scene);
            if (player != null)
                playerProp.objectReferenceValue = player.transform;
        }

        if (syncProp != null && syncProp.objectReferenceValue == null)
            syncProp.objectReferenceValue = FindInScene<GameServerSync>(scene);

        if (dbProp != null && dbProp.objectReferenceValue == null)
        {
            dbProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameQuestSystem.StoryDialogueDatabase>(
                "Assets/code/GameQuestSystem/Content1StoryDatabase.asset");
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(manager);
    }

    static int FixZeroScaleCanvases(Scene scene)
    {
        int fixedCount = 0;
        foreach (var canvas in FindAllInScene<Canvas>(scene))
        {
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace)
                continue;

            Undo.RecordObject(canvas.gameObject, "Undergarden UI Scale");
            UndergardenUiScaleProfile.ApplyToCanvas(canvas);
            EditorUtility.SetDirty(canvas.gameObject);
            fixedCount++;
        }

        return fixedCount;
    }

    static void EnsureEventSystem(Scene scene, SetupReport report)
    {
        if (FindInScene<EventSystem>(scene) != null)
            return;

        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
        SceneManager.MoveGameObjectToScene(go, scene);
        report.EventSystemCreated = true;
    }

    static bool CopyOpenPanelIfMissing(Scene templateScene, Scene targetScene)
    {
        if (FindByNameInScene(targetScene, OpenPanelName) != null)
            return false;

        var source = FindByNameInScene(templateScene, OpenPanelName);
        if (source == null)
            return false;

        var canvas = InteractionPromptOneClickSetup.FindPreferredCanvasInScene(targetScene);
        if (canvas == null)
            return false;

        var clone = UnityEngine.Object.Instantiate(source);
        clone.name = OpenPanelName;
        Undo.RegisterCreatedObjectUndo(clone, "Copy openpanel");
        SceneManager.MoveGameObjectToScene(clone, targetScene);
        clone.transform.SetParent(canvas.transform, false);
        clone.transform.SetAsLastSibling();

        var rt = clone.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.localScale = Vector3.one;
            rt.localPosition = Vector3.zero;
        }

        if (!clone.activeSelf)
            clone.SetActive(true);

        EditorUtility.SetDirty(clone);
        return true;
    }

    static bool EnsureInteractionHub(Scene scene, SetupReport report)
    {
        var existing = FindAllInScene<InteractWithMouseAndMovementLock>(scene);
        if (existing.Count > 0)
        {
            foreach (var existingCtrl in existing)
                WireInteractionController(existingCtrl, scene);
            return false;
        }

        var player = FindInScene<PlayerMovement>(scene);
        if (player == null)
        {
            Debug.LogWarning($"[{scene.name}] 未找到 PlayerMovement，跳过 SceneInteractionHub。");
            return false;
        }

        var hub = new GameObject(InteractionHubName);
        Undo.RegisterCreatedObjectUndo(hub, "Create SceneInteractionHub");
        SceneManager.MoveGameObjectToScene(hub, scene);

        var ctrl = hub.AddComponent<InteractWithMouseAndMovementLock>();
        WireInteractionController(ctrl, scene);
        report.InteractionHubCreated = true;
        return true;
    }

    static void WireInteractionController(InteractWithMouseAndMovementLock ctrl, Scene scene)
    {
        if (ctrl == null)
            return;

        var player = FindInScene<PlayerMovement>(scene);
        Undo.RecordObject(ctrl, "Wire Interaction Hub");

        if (player != null)
        {
            ctrl.playerMovement = player;
            ctrl.playerModelTransform = player.transform;
        }

        ctrl.useSharedPromptResolution = false;
        ctrl.autoBindSharedInteractPromptFromScene = true;

        InteractionPromptOneClickSetup.TryBindExistingSharedPanelInScene(scene);
        var panel = FindByNameInScene(scene, InteractPromptSharedUiLocator.PanelObjectName);
        if (panel != null)
        {
            var panelRt = panel.GetComponent<RectTransform>();
            var nameTmp = InteractionPromptOneClickSetup.FindTmpInPanelPublic(panel.transform, "InteractNameText");
            var typeTmp = InteractionPromptOneClickSetup.FindTmpInPanelPublic(panel.transform, "InteractTypeText");
            if (panelRt != null && nameTmp != null && typeTmp != null)
            {
                ctrl.interactPromptPanel = panelRt;
                ctrl.interactNameText = nameTmp;
                ctrl.interactTypeText = typeTmp;
            }
        }

        EditorUtility.SetDirty(ctrl);
    }

    static bool BindPhoneHudPanel(Scene scene)
    {
        var openpanel = FindByNameInScene(scene, OpenPanelName);
        if (openpanel == null)
            return false;

        bool bound = false;
        foreach (var phone in FindAllInScene<PhoneUIManager>(scene))
        {
            if (phone == null)
                continue;
            Undo.RecordObject(phone, "Bind Phone HUD");
            phone.hudPanel = openpanel;
            if (phone.playerMovement == null)
                phone.playerMovement = FindInScene<PlayerMovement>(scene);
            EditorUtility.SetDirty(phone);
            bound = true;
        }

        foreach (var session in FindAllInScene<PhoneUI.PhoneSessionController>(scene))
        {
            if (session == null)
                continue;
            Undo.RecordObject(session, "Bind Phone Session HUD");
            session.hudPanel = openpanel;
            EditorUtility.SetDirty(session);
            bound = true;
        }

        return bound;
    }

    static GameObject FindByNameInScene(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var t = FindByNameRecursive(root.transform, name);
            if (t != null)
                return t.gameObject;
        }

        return null;
    }

    static Transform FindByNameRecursive(Transform root, string name)
    {
        if (root == null)
            return null;
        if (string.Equals(root.name, name, StringComparison.Ordinal))
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindByNameRecursive(root.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }

    static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (var item in Resources.FindObjectsOfTypeAll<T>())
        {
            if (item != null && item.gameObject.scene == scene)
                return item;
        }

        return null;
    }

    static List<T> FindAllInScene<T>(Scene scene) where T : Component
    {
        var list = new List<T>();
        foreach (var item in Resources.FindObjectsOfTypeAll<T>())
        {
            if (item != null && item.gameObject.scene == scene)
                list.Add(item);
        }

        return list;
    }

    public struct SetupReport
    {
        public int RemovedBrokenUiRoots;
        public int CanvasScaleFixed;
        public bool CopiedPhoneHud;
        public bool CopiedOpenPanel;
        public bool CopiedQuestSystemUi;
        public bool CopiedQuestSystemManager;
        public bool PromptConfigured;
        public bool InteractionHubCreated;
        public bool PhoneHudBound;
        public bool EventSystemCreated;
        public int DuplicatePanelsRemoved;
    }
}
#endif
