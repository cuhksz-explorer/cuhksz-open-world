#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using GameQuestSystem;
using PhoneUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// libraryfloor2：以 undergarden 为模板补齐所有已存在的 gameplay UI。
/// </summary>
public static class LibraryFloor2UndergardenUiSetup
{
    const string MenuPath = "Tools/场景配置/libraryfloor2 - 一键补齐 undergarden 全部UI";
    const string TargetScenePath = "Assets/scene/libraryfloor2.scene";
    const string TemplateScenePath = "Assets/scene/undergarden.unity";
    const string OpenPanelName = "openpanel";
    const string QuestBriefPanelName = "QuestBriefPanel";
    const string QuestFullPanelName = "QuestFullPanel";

    static readonly string[] UiNameHints =
    {
        "canvas", "panel", "ui", "phone", "quest", "targetguide", "interactprompt",
        "openpanel", "mall", "inventory", "chat", "gameserver", "login",
        "quality", "timenow", "settings", "hud", "placement"
    };

    static readonly string[] RequiredHudObjectNames =
    {
        "Buttonopen",
        "openpanel",
        "HP",
        "STamount"
    };

    [MenuItem(MenuPath, false, 4)]
    public static void ConfigureFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("libraryfloor2 UI", "请先退出 Play Mode。", "确定");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Scene templateScene = default;
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("libraryfloor2 undergarden UI setup");

        try
        {
            var targetScene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
            templateScene = EditorSceneManager.OpenScene(TemplateScenePath, OpenSceneMode.Additive);

            var report = ConfigureScene(targetScene, templateScene);
            EditorSceneManager.SaveScene(targetScene);

            EditorUtility.DisplayDialog(
                "libraryfloor2 UI",
                LibraryFloor2UndergardenUiSetup.FormatSetupReport(report),
                "确定");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("libraryfloor2 UI 配置失败", ex.Message, "确定");
        }
        finally
        {
            if (templateScene.IsValid() && templateScene.isLoaded)
                EditorSceneManager.CloseScene(templateScene, true);
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    /// <summary>BatchMode: -executeMethod LibraryFloor2UndergardenUiSetup.ConfigureAndSave</summary>
    public static void ConfigureAndSave()
    {
        var targetScene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
        var templateScene = EditorSceneManager.OpenScene(TemplateScenePath, OpenSceneMode.Additive);
        try
        {
            ConfigureScene(targetScene, templateScene);
            EditorSceneManager.SaveScene(targetScene);
        }
        finally
        {
            if (templateScene.IsValid() && templateScene.isLoaded)
                EditorSceneManager.CloseScene(templateScene, true);
        }
    }

    public static SetupReport ConfigureScene(Scene targetScene, Scene templateScene)
    {
        if (!targetScene.IsValid())
            throw new InvalidOperationException("libraryfloor2 场景无效。");
        if (!templateScene.IsValid())
            throw new InvalidOperationException("undergarden 模板场景无效。");

        EditorSceneManager.SetActiveScene(targetScene);
        var report = new SetupReport();

        var coreReport = UndergardenSceneOneClickConfigurator.ApplyFromTemplateToScene(templateScene, targetScene);
        report.CoreCreated = coreReport.created;
        report.CoreSkipped = coreReport.skipped;
        report.ReboundReferences += coreReport.rebound;

        CopyAllMissingTemplateUi(templateScene, targetScene, report);
        CopyRequiredHudObjects(templateScene, targetScene, report);
        report.DuplicateQuestPanelsDisabled = DisableOpenPanelQuestPanels(targetScene);
        report.QuestBriefBackgroundsDisabled = DisableQuestBriefPanelBackgrounds(targetScene);
        EnsureEventSystem(targetScene, report);
        report.CanvasesConfigured = GameplayUiScaleSetup.ApplyToScene(targetScene);

        report.PromptConfigured = InteractionPromptOneClickSetup.SetupSharedPromptForScene(targetScene);
        if (!report.PromptConfigured)
            report.PromptConfigured = InteractionPromptOneClickSetup.TryBindExistingSharedPanelInScene(targetScene);
        report.DuplicatePromptPanelsRemoved = InteractPromptAllScenesConfigurator.ConfigureLoadedScene(targetScene);

        BindCommonGameplayUiReferences(targetScene, report);
        report.ReboundReferences += UndergardenUiPostConfigure.RewireSettingsButtons(targetScene);
        report.ReboundReferences += RebindTemplateSceneReferences(templateScene, targetScene);
        report.ReboundReferences += RetargetCopiedSceneNames(targetScene);

        EditorSceneManager.MarkSceneDirty(targetScene);
        AssetDatabase.SaveAssets();
        return report;
    }

    static void CopyRequiredHudObjects(Scene templateScene, Scene targetScene, SetupReport report)
    {
        for (int i = 0; i < RequiredHudObjectNames.Length; i++)
        {
            string objectName = RequiredHudObjectNames[i];
            if (FindByNameInScene(targetScene, objectName) != null)
                continue;

            var source = FindByNameInScene(templateScene, objectName);
            if (source == null)
            {
                Debug.LogWarning($"[libraryfloor2 UI] undergarden 中未找到必需 HUD：{objectName}");
                continue;
            }

            if (UndergardenUiCopyPolicy.ShouldSkipUiCopy(source))
                continue;

            if (CopyNearestUiRootForObject(source, targetScene, report))
                continue;

            CopySingleHudObject(source, targetScene, report);
        }
    }

    static bool CopyNearestUiRootForObject(GameObject sourceObject, Scene targetScene, SetupReport report)
    {
        var copyRoot = FindNearestCopyableUiRoot(sourceObject != null ? sourceObject.transform : null);
        if (copyRoot == null)
            return false;

        if (FindRootByName(targetScene, copyRoot.name) != null)
            return false;

        var clone = UnityEngine.Object.Instantiate(copyRoot.gameObject);
        clone.name = copyRoot.name;
        Undo.RegisterCreatedObjectUndo(clone, "Copy required undergarden HUD root");
        SceneManager.MoveGameObjectToScene(clone, targetScene);
        NormalizeRectTransforms(clone.transform);
        report.UiRootsCopied++;
        report.RequiredHudObjectsCopied++;
        return true;
    }

    static Transform FindNearestCopyableUiRoot(Transform t)
    {
        Transform candidate = null;
        for (Transform current = t; current != null; current = current.parent)
        {
            if (current.GetComponent<Canvas>() != null)
                candidate = current;
        }

        if (candidate != null && UndergardenUiCopyPolicy.ShouldSkipUiCopy(candidate))
            candidate = null;

        if (candidate != null)
            return candidate;
        return t != null ? t.root : null;
    }

    static void CopySingleHudObject(GameObject sourceObject, Scene targetScene, SetupReport report)
    {
        if (sourceObject == null)
            return;

        var clone = UnityEngine.Object.Instantiate(sourceObject);
        clone.name = sourceObject.name;
        Undo.RegisterCreatedObjectUndo(clone, "Copy required undergarden HUD object");
        SceneManager.MoveGameObjectToScene(clone, targetScene);

        var parent = ResolveTargetParentForCopiedObject(sourceObject.transform, targetScene);
        if (parent != null)
            clone.transform.SetParent(parent, false);

        NormalizeRectTransforms(clone.transform);
        report.UiChildrenCopied++;
        report.RequiredHudObjectsCopied++;
    }

    static Transform ResolveTargetParentForCopiedObject(Transform sourceObject, Scene targetScene)
    {
        if (sourceObject == null || sourceObject.parent == null)
            return null;

        var pathToTarget = BuildTargetPathLookup(targetScene);
        string parentPath = GetScenePath(sourceObject.parent);
        if (!string.IsNullOrEmpty(parentPath) &&
            pathToTarget.TryGetValue(parentPath, out var targetParent) &&
            targetParent != null)
            return targetParent.transform;

        var canvas = InteractionPromptOneClickSetup.FindPreferredCanvasInScene(targetScene);
        return canvas != null ? canvas.transform : null;
    }

    static void CopyAllMissingTemplateUi(Scene templateScene, Scene targetScene, SetupReport report)
    {
        foreach (var sourceRoot in templateScene.GetRootGameObjects())
        {
            if (!IsTemplateUiRoot(sourceRoot))
                continue;

            var targetRoot = FindRootByName(targetScene, sourceRoot.name);
            if (targetRoot == null)
            {
                var clone = UnityEngine.Object.Instantiate(sourceRoot);
                clone.name = sourceRoot.name;
                Undo.RegisterCreatedObjectUndo(clone, "Copy undergarden UI root");
                SceneManager.MoveGameObjectToScene(clone, targetScene);
                NormalizeRectTransforms(clone.transform);
                report.UiRootsCopied++;
                continue;
            }

            CopyMissingChildrenRecursive(sourceRoot.transform, targetRoot.transform, targetScene, report);
        }
    }

    static bool IsTemplateUiRoot(GameObject root)
    {
        if (root == null)
            return false;

        if (UndergardenUiCopyPolicy.ShouldSkipUiCopy(root))
            return false;

        if (root.GetComponentInChildren<PlayerMovement>(true) != null)
            return false;
        if (root.GetComponentInChildren<Camera>(true) != null && root.GetComponent<RectTransform>() == null)
            return false;

        if (root.GetComponent<Canvas>() != null || root.GetComponent<RectTransform>() != null)
            return true;

        string lower = root.name.ToLowerInvariant();
        bool nameLooksLikeUi = false;
        for (int i = 0; i < UiNameHints.Length; i++)
        {
            if (lower.Contains(UiNameHints[i]))
            {
                nameLooksLikeUi = true;
                break;
            }
        }

        return nameLooksLikeUi && root.GetComponentInChildren<Canvas>(true) != null;
    }

    static void CopyMissingChildrenRecursive(Transform sourceParent, Transform targetParent, Scene targetScene, SetupReport report)
    {
        if (sourceParent == null || targetParent == null)
            return;

        for (int i = 0; i < sourceParent.childCount; i++)
        {
            var sourceChild = sourceParent.GetChild(i);
            if (UndergardenUiCopyPolicy.ShouldSkipUiCopy(sourceChild))
                continue;

            if (ShouldSkipOpenPanelQuestPanel(sourceParent, sourceChild))
                continue;

            var targetChild = FindDirectChildByName(targetParent, sourceChild.name);

            if (targetChild == null)
            {
                var clone = UnityEngine.Object.Instantiate(sourceChild.gameObject);
                clone.name = sourceChild.name;
                Undo.RegisterCreatedObjectUndo(clone, "Copy undergarden UI child");
                SceneManager.MoveGameObjectToScene(clone, targetScene);
                clone.transform.SetParent(targetParent, false);
                clone.transform.SetSiblingIndex(Mathf.Min(i, targetParent.childCount - 1));
                NormalizeRectTransforms(clone.transform);
                report.UiChildrenCopied++;
                continue;
            }

            if (IsUiBranch(sourceChild))
                CopyMissingChildrenRecursive(sourceChild, targetChild, targetScene, report);
        }
    }

    static bool ShouldSkipOpenPanelQuestPanel(Transform sourceParent, Transform sourceChild)
    {
        if (sourceParent == null || sourceChild == null)
            return false;
        if (!string.Equals(sourceParent.name, OpenPanelName, StringComparison.Ordinal))
            return false;
        return string.Equals(sourceChild.name, QuestBriefPanelName, StringComparison.Ordinal) ||
               string.Equals(sourceChild.name, QuestFullPanelName, StringComparison.Ordinal);
    }

    static int DisableOpenPanelQuestPanels(Scene scene)
    {
        var openpanel = FindByNameInScene(scene, OpenPanelName);
        if (openpanel == null)
            return 0;

        int disabled = 0;
        disabled += DisableDirectChild(openpanel.transform, QuestBriefPanelName);
        disabled += DisableDirectChild(openpanel.transform, QuestFullPanelName);
        return disabled;
    }

    static int DisableDirectChild(Transform parent, string childName)
    {
        var child = FindDirectChildByName(parent, childName);
        if (child == null || !child.gameObject.activeSelf)
            return 0;

        Undo.RecordObject(child.gameObject, "Disable duplicate quest panel");
        child.gameObject.SetActive(false);
        EditorUtility.SetDirty(child.gameObject);
        return 1;
    }

    static int DisableQuestBriefPanelBackgrounds(Scene scene)
    {
        int disabled = 0;
        foreach (var image in FindAllInScene<Image>(scene))
        {
            if (image == null || !image.enabled)
                continue;
            if (!string.Equals(image.gameObject.name, QuestBriefPanelName, StringComparison.Ordinal))
                continue;

            Undo.RecordObject(image, "Disable quest brief background");
            image.enabled = false;
            EditorUtility.SetDirty(image);
            disabled++;
        }

        return disabled;
    }

    static int DisableMapSystemConfiguration(Scene scene)
    {
        int disabled = 0;
        foreach (var mapSystem in FindAllInScene<MapSystem>(scene))
        {
            if (mapSystem == null)
                continue;
            Undo.RecordObject(mapSystem, "Disable undergarden MapSystem");
            mapSystem.enabled = false;
            EditorUtility.SetDirty(mapSystem);
            disabled++;
        }

        var player = FindInScene<PlayerMovement>(scene);
        if (player != null && player.mapSystem != null)
        {
            Undo.RecordObject(player, "Clear libraryfloor2 MapSystem reference");
            player.mapSystem = null;
            EditorUtility.SetDirty(player);
        }

        return disabled;
    }

    static bool IsUiBranch(Transform t)
    {
        return t != null &&
               (t.GetComponent<RectTransform>() != null ||
                t.GetComponent<Canvas>() != null ||
                t.GetComponentInChildren<RectTransform>(true) != null ||
                t.GetComponentInChildren<Canvas>(true) != null);
    }

    static void NormalizeRectTransforms(Transform root)
    {
        if (root == null)
            return;

        foreach (var rt in root.GetComponentsInChildren<RectTransform>(true))
        {
            Undo.RecordObject(rt, "Normalize copied UI RectTransform");
            rt.localScale = Vector3.one;
            EditorUtility.SetDirty(rt);
        }
    }

    static void EnsureEventSystem(Scene scene, SetupReport report)
    {
        if (FindInScene<EventSystem>(scene) != null)
            return;

        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
        SceneManager.MoveGameObjectToScene(go, scene);
        report.UiRootsCopied++;
    }

    static int RetargetCopiedSceneNames(Scene scene)
    {
        string sceneKey = Path.GetFileNameWithoutExtension(scene.path);
        if (string.IsNullOrEmpty(sceneKey))
            return 0;

        int changed = 0;
        foreach (var sync in FindAllInScene<GameServerSync>(scene))
        {
            if (sync == null)
                continue;

            var so = new SerializedObject(sync);
            var prop = so.FindProperty("recreateDirectionalLightSceneName");
            if (prop == null || prop.propertyType != SerializedPropertyType.String)
                continue;
            if (string.Equals(prop.stringValue, sceneKey, StringComparison.OrdinalIgnoreCase))
                continue;

            Undo.RecordObject(sync, "Retarget GameServerSync scene name");
            prop.stringValue = sceneKey;
            if (so.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(sync);
                changed++;
            }
        }

        return changed;
    }

    static void BindCommonGameplayUiReferences(Scene scene, SetupReport report)
    {
        var player = FindInScene<PlayerMovement>(scene);
        var openpanel = FindByNameInScene(scene, OpenPanelName);
        var questManager = FindInScene<QuestSystemManager>(scene);
        var serverSync = FindInScene<GameServerSync>(scene);

        foreach (var phone in FindAllInScene<PhoneUIManager>(scene))
        {
            if (phone == null)
                continue;
            Undo.RecordObject(phone, "Bind libraryfloor2 Phone UI");
            if (openpanel != null)
                phone.hudPanel = openpanel;
            if (player != null)
                phone.playerMovement = player;
            EditorUtility.SetDirty(phone);
            report.ReboundReferences++;
        }

        foreach (var session in FindAllInScene<PhoneSessionController>(scene))
        {
            if (session == null || openpanel == null)
                continue;
            Undo.RecordObject(session, "Bind libraryfloor2 Phone Session");
            session.hudPanel = openpanel;
            EditorUtility.SetDirty(session);
            report.ReboundReferences++;
        }

        if (player != null)
        {
            Undo.RecordObject(player, "Bind libraryfloor2 Player UI refs");
            if (player.respawnPoint == null)
                player.respawnPoint = EnsureRespawnPoint(scene, player.transform.position);
            EditorUtility.SetDirty(player);
            report.ReboundReferences++;
        }

        foreach (var questUi in FindAllInScene<QuestSystemUI>(scene))
        {
            if (questUi == null || questManager == null)
                continue;
            report.ReboundReferences += RebindSerializedObjectReference(questUi, questManager);
        }

        foreach (var healthBar in FindAllInScene<HealthBarUI>(scene))
        {
            if (healthBar != null && player != null)
                report.ReboundReferences += RebindSerializedObjectReference(healthBar, player);
        }

        foreach (var staminaBar in FindAllInScene<StaminaBarUI>(scene))
        {
            if (staminaBar != null && player != null)
                report.ReboundReferences += RebindSerializedObjectReference(staminaBar, player);
        }

        if (serverSync != null && player != null)
            report.ReboundReferences += RebindSerializedObjectReference(serverSync, player);
    }

    static int RebindTemplateSceneReferences(Scene templateScene, Scene targetScene)
    {
        int changed = 0;
        var pathToTarget = BuildTargetPathLookup(targetScene);

        foreach (var component in FindAllComponentsInScene(targetScene))
        {
            if (component == null)
                continue;

            var so = new SerializedObject(component);
            var prop = so.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (prop.propertyType != SerializedPropertyType.ObjectReference)
                    continue;

                var value = prop.objectReferenceValue;
                if (!TryResolveTemplateReference(value, templateScene, pathToTarget, out var replacement))
                    continue;
                if (replacement == null || replacement == value)
                    continue;

                prop.objectReferenceValue = replacement;
                changed++;
            }

            if (so.ApplyModifiedProperties())
                EditorUtility.SetDirty(component);
        }

        return changed;
    }

    static bool TryResolveTemplateReference(
        UnityEngine.Object value,
        Scene templateScene,
        Dictionary<string, GameObject> pathToTarget,
        out UnityEngine.Object replacement)
    {
        replacement = null;
        if (value == null)
            return false;

        GameObject sourceGo = value as GameObject;
        if (sourceGo == null && value is Component sourceComponent)
            sourceGo = sourceComponent.gameObject;
        if (sourceGo == null || sourceGo.scene != templateScene)
            return false;

        string path = GetScenePath(sourceGo.transform);
        if (string.IsNullOrEmpty(path) || !pathToTarget.TryGetValue(path, out var targetGo) || targetGo == null)
            return false;

        if (value is GameObject)
        {
            replacement = targetGo;
            return true;
        }

        var sourceType = value.GetType();
        replacement = targetGo.GetComponent(sourceType);
        return replacement != null;
    }

    static int RebindSerializedObjectReference(UnityEngine.Object owner, UnityEngine.Object target)
    {
        if (owner == null || target == null)
            return 0;

        int changed = 0;
        var so = new SerializedObject(owner);
        var prop = so.GetIterator();
        bool enterChildren = true;
        while (prop.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (prop.propertyType != SerializedPropertyType.ObjectReference || prop.objectReferenceValue != null)
                continue;
            if (!prop.type.Contains(target.GetType().Name))
                continue;
            prop.objectReferenceValue = target;
            changed++;
        }

        if (so.ApplyModifiedProperties())
            EditorUtility.SetDirty(owner);
        return changed;
    }

    static Dictionary<string, GameObject> BuildTargetPathLookup(Scene scene)
    {
        var lookup = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        foreach (var root in scene.GetRootGameObjects())
            AddPathRecursive(root.transform, lookup);
        return lookup;
    }

    static void AddPathRecursive(Transform t, Dictionary<string, GameObject> lookup)
    {
        string path = GetScenePath(t);
        if (!string.IsNullOrEmpty(path) && !lookup.ContainsKey(path))
            lookup.Add(path, t.gameObject);

        for (int i = 0; i < t.childCount; i++)
            AddPathRecursive(t.GetChild(i), lookup);
    }

    static string GetScenePath(Transform t)
    {
        if (t == null)
            return "";

        var parts = new Stack<string>();
        Transform current = t;
        while (current != null)
        {
            parts.Push(current.name + "#" + GetSameNameSiblingIndex(current));
            current = current.parent;
        }

        return string.Join("/", parts.ToArray());
    }

    static int GetSameNameSiblingIndex(Transform t)
    {
        if (t == null || t.parent == null)
            return 0;

        int index = 0;
        for (int i = 0; i < t.GetSiblingIndex(); i++)
        {
            var sibling = t.parent.GetChild(i);
            if (sibling != null && sibling.name == t.name)
                index++;
        }
        return index;
    }

    static Transform EnsureRespawnPoint(Scene scene, Vector3 position)
    {
        var existing = FindByNameInScene(scene, "RespawnPoint");
        if (existing != null)
            return existing.transform;

        var go = new GameObject("RespawnPoint");
        Undo.RegisterCreatedObjectUndo(go, "Create RespawnPoint");
        go.transform.position = position;
        SceneManager.MoveGameObjectToScene(go, scene);
        return go.transform;
    }

    static GameObject FindRootByName(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (string.Equals(root.name, name, StringComparison.Ordinal))
                return root;
        }
        return null;
    }

    static Transform FindDirectChildByName(Transform parent, string name)
    {
        if (parent == null)
            return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child != null && string.Equals(child.name, name, StringComparison.Ordinal))
                return child;
        }
        return null;
    }

    static GameObject FindByNameInScene(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var found = FindByNameRecursive(root.transform, name);
            if (found != null)
                return found.gameObject;
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

    static List<Component> FindAllComponentsInScene(Scene scene)
    {
        var list = new List<Component>();
        foreach (var component in Resources.FindObjectsOfTypeAll<Component>())
        {
            if (component != null && component.gameObject.scene == scene)
                list.Add(component);
        }
        return list;
    }

    public static string FormatSetupReport(SetupReport report)
    {
        return
            "配置完成。\n\n" +
            $"基础系统新增：{report.CoreCreated}\n" +
            $"基础系统跳过：{report.CoreSkipped}\n" +
            $"复制 UI 根对象：{report.UiRootsCopied}\n" +
            $"补齐 UI 子对象：{report.UiChildrenCopied}\n" +
            $"强制补齐 HUD：{report.RequiredHudObjectsCopied}\n" +
            $"禁用重复任务面板：{report.DuplicateQuestPanelsDisabled}\n" +
            $"禁用任务栏黑色背景：{report.QuestBriefBackgroundsDisabled}\n" +
            $"重绑引用：{report.ReboundReferences}\n" +
            $"配置 Canvas：{report.CanvasesConfigured}\n" +
            $"交互提示：{(report.PromptConfigured ? "已配置" : "未配置")}\n" +
            $"删除重复提示面板：{report.DuplicatePromptPanelsRemoved}";
    }

    public struct SetupReport
    {
        public int CoreCreated;
        public int CoreSkipped;
        public int UiRootsCopied;
        public int UiChildrenCopied;
        public int RequiredHudObjectsCopied;
        public int DuplicateQuestPanelsDisabled;
        public int QuestBriefBackgroundsDisabled;
        public int MapSystemsDisabled;
        public int ReboundReferences;
        public int CanvasesConfigured;
        public bool PromptConfigured;
        public int DuplicatePromptPanelsRemoved;
    }
}
#endif
