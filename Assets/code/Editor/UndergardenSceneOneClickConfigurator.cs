#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using GameQuestSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// 将 undergarden 中已经调好的主角与 UI 系统复制到当前打开的场景，并重绑跨对象引用。
/// </summary>
public static class UndergardenSceneOneClickConfigurator
{
    const string MenuPath = "Tools/场景配置/一键套用 undergarden 主角和 UI";
    const string AddSceneBuildSettingsMenuPath = "Tools/场景配置/将 Assets/scene 加入 Build Settings";
    const string TemplateScenePath = "Assets/scene/undergarden.unity";
    const string ProjectSceneFolder = "Assets/scene";

    static readonly string[] NamedTemplateObjects =
    {
        "Phone",
        "QuestSystemManager",
        "QuestSystemUI",
        "StoryQuestRuntime",
        "TargetGuideSystem",
        "GameServer_Network",
        "GameServer_LoginPanel",
        "QuestBriefPanel",
        "QuestFullPanel",
        "DormMallCanvas",
        "FoodMallCanvas",
        "FriendChatSidebarCanvas",
        "EventSystem"
    };

    [MenuItem(MenuPath, false, 0)]
    public static void ApplyToActiveScene()
    {
        Scene targetScene = SceneManager.GetActiveScene();
        if (!targetScene.IsValid() || string.IsNullOrEmpty(targetScene.path))
        {
            EditorUtility.DisplayDialog("一键配置", "请先打开要配置的目标场景。", "确定");
            return;
        }

        if (string.Equals(targetScene.path, TemplateScenePath, StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog("一键配置", "当前已经是 undergarden 模板场景，请切换到其他场景后再执行。", "确定");
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("一键配置", "请先退出 Play Mode，再执行场景配置。", "确定");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Scene templateScene = default;
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("一键套用 undergarden 主角和 UI");

        try
        {
            int addedScenes = AddProjectScenesToBuildSettings();
            templateScene = EditorSceneManager.OpenScene(TemplateScenePath, OpenSceneMode.Additive);
            var report = ApplyFromTemplate(templateScene, targetScene);
            EditorSceneManager.MarkSceneDirty(targetScene);
            Selection.activeObject = report.SelectedObject;
            if (report.SelectedObject != null)
                EditorGUIUtility.PingObject(report.SelectedObject);

            EditorUtility.DisplayDialog(
                "一键配置完成",
                $"已套用 undergarden 的主角与 UI 配置。\n\n新增对象：{report.CreatedCount} 个\n跳过已有：{report.SkippedCount} 个\n重绑引用：{report.ReboundCount} 处\nBuild Settings 新增场景：{addedScenes} 个",
                "确定");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("一键配置失败", ex.Message, "确定");
        }
        finally
        {
            if (templateScene.IsValid() && templateScene.isLoaded)
                EditorSceneManager.CloseScene(templateScene, true);
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    [MenuItem(AddSceneBuildSettingsMenuPath, false, 1)]
    public static void AddProjectScenesToBuildSettingsMenu()
    {
        int added = AddProjectScenesToBuildSettings();
        EditorUtility.DisplayDialog("Build Settings", $"已检查 {ProjectSceneFolder}，新增 {added} 个场景到 Scenes In Build。", "确定");
    }

    public static int AddProjectScenesToBuildSettings()
    {
        var existing = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        var knownPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var scene in existing)
        {
            if (scene != null && !string.IsNullOrEmpty(scene.path))
                knownPaths.Add(scene.path);
        }

        int added = 0;
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { ProjectSceneFolder });
        foreach (string guid in sceneGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) || knownPaths.Contains(path))
                continue;

            existing.Add(new EditorBuildSettingsScene(path, true));
            knownPaths.Add(path);
            added++;
        }

        if (added > 0)
            EditorBuildSettings.scenes = existing.ToArray();
        return added;
    }

    /// <summary>供 DormSceneOneClickConfigurator 等其它编辑器工具调用。</summary>
    public static (int created, int skipped, int rebound) ApplyFromTemplateToScene(Scene templateScene, Scene targetScene)
    {
        if (!templateScene.IsValid() || !targetScene.IsValid())
            throw new InvalidOperationException("模板场景或目标场景无效。");

        if (targetScene.isLoaded)
            EditorSceneManager.SetActiveScene(targetScene);

        var report = ApplyFromTemplate(templateScene, targetScene);
        return (report.CreatedCount, report.SkippedCount, report.ReboundCount);
    }

    static SetupReport ApplyFromTemplate(Scene templateScene, Scene targetScene)
    {
        var report = new SetupReport();
        var rootsToCopy = new HashSet<GameObject>();

        AddRootForComponent<PlayerMovement>(templateScene, rootsToCopy, preferCanvasRoot: false);
        AddRootForComponent<PhoneUIManager>(templateScene, rootsToCopy, preferCanvasRoot: true);
        AddRootForComponent<QuestSystemManager>(templateScene, rootsToCopy, preferCanvasRoot: false);
        AddRootForComponent<QuestSystemUI>(templateScene, rootsToCopy, preferCanvasRoot: true);
        AddRootForComponent<TargetGuideUI>(templateScene, rootsToCopy, preferCanvasRoot: true);
        AddRootForComponent<GameServerSync>(templateScene, rootsToCopy, preferCanvasRoot: false);
        AddRootForComponent<GameServerAuthUI>(templateScene, rootsToCopy, preferCanvasRoot: true);

        foreach (string objectName in NamedTemplateObjects)
        {
            var go = FindByNameInScene(templateScene, objectName);
            if (go != null && !UndergardenUiCopyPolicy.ShouldSkipUiCopy(go))
                rootsToCopy.Add(GetCopyRoot(go, preferCanvasRoot: go.GetComponent<RectTransform>() != null));
        }

        var spawnPosition = ResolveSpawnPosition(targetScene);
        foreach (var sourceRoot in rootsToCopy)
        {
            if (sourceRoot == null || UndergardenUiCopyPolicy.ShouldSkipUiCopy(sourceRoot))
                continue;

            if (HasEquivalentSystem(targetScene, sourceRoot))
            {
                report.SkippedCount++;
                continue;
            }

            var clone = UnityEngine.Object.Instantiate(sourceRoot);
            clone.name = sourceRoot.name;
            Undo.RegisterCreatedObjectUndo(clone, "Copy undergarden system");
            SceneManager.MoveGameObjectToScene(clone, targetScene);
            ParentLooseUiToCanvas(clone, targetScene);

            var player = clone.GetComponentInChildren<PlayerMovement>(true);
            if (player != null)
                player.transform.position = spawnPosition;

            report.CreatedCount++;
            if (report.SelectedObject == null)
                report.SelectedObject = clone;
        }

        EnsureEventSystem(targetScene, report);
        RebindSceneReferences(targetScene, report);
        NormalizeMainCamera(targetScene);
        return report;
    }

    static void AddRootForComponent<T>(Scene scene, HashSet<GameObject> roots, bool preferCanvasRoot) where T : Component
    {
        var component = FindInScene<T>(scene);
        if (component == null)
            return;

        var root = GetCopyRoot(component.gameObject, preferCanvasRoot);
        if (root != null && !UndergardenUiCopyPolicy.ShouldSkipUiCopy(root))
            roots.Add(root);
    }

    static GameObject GetCopyRoot(GameObject go, bool preferCanvasRoot)
    {
        if (go == null)
            return null;

        if (preferCanvasRoot)
        {
            var canvasOnSelf = go.GetComponent<Canvas>();
            if (canvasOnSelf != null)
                return canvasOnSelf.gameObject;
        }

        return go;
    }

    static bool HasEquivalentSystem(Scene targetScene, GameObject sourceRoot)
    {
        bool knownSystem = false;
        if (sourceRoot.GetComponentInChildren<PlayerMovement>(true) != null && FindInScene<PlayerMovement>(targetScene) != null)
            return true;
        knownSystem |= sourceRoot.GetComponentInChildren<PlayerMovement>(true) != null;
        if (sourceRoot.GetComponentInChildren<PhoneUIManager>(true) != null && FindInScene<PhoneUIManager>(targetScene) != null)
            return true;
        knownSystem |= sourceRoot.GetComponentInChildren<PhoneUIManager>(true) != null;
        if (sourceRoot.GetComponentInChildren<QuestSystemManager>(true) != null && FindInScene<QuestSystemManager>(targetScene) != null)
            return true;
        knownSystem |= sourceRoot.GetComponentInChildren<QuestSystemManager>(true) != null;
        if (sourceRoot.GetComponentInChildren<QuestSystemUI>(true) != null && FindInScene<QuestSystemUI>(targetScene) != null)
            return true;
        knownSystem |= sourceRoot.GetComponentInChildren<QuestSystemUI>(true) != null;
        if (sourceRoot.GetComponentInChildren<TargetGuideUI>(true) != null && FindInScene<TargetGuideUI>(targetScene) != null)
            return true;
        knownSystem |= sourceRoot.GetComponentInChildren<TargetGuideUI>(true) != null;
        if (sourceRoot.GetComponentInChildren<GameServerSync>(true) != null && FindInScene<GameServerSync>(targetScene) != null)
            return true;
        knownSystem |= sourceRoot.GetComponentInChildren<GameServerSync>(true) != null;
        if (sourceRoot.GetComponentInChildren<GameServerAuthUI>(true) != null && FindInScene<GameServerAuthUI>(targetScene) != null)
            return true;
        knownSystem |= sourceRoot.GetComponentInChildren<GameServerAuthUI>(true) != null;
        if (sourceRoot.GetComponentInChildren<MapSystem>(true) != null && FindInScene<MapSystem>(targetScene) != null)
            return true;
        knownSystem |= sourceRoot.GetComponentInChildren<MapSystem>(true) != null;
        if (sourceRoot.GetComponentInChildren<EventSystem>(true) != null && FindInScene<EventSystem>(targetScene) != null)
            return true;
        knownSystem |= sourceRoot.GetComponentInChildren<EventSystem>(true) != null;

        return !knownSystem && FindByNameInScene(targetScene, sourceRoot.name) != null;
    }

    static Vector3 ResolveSpawnPosition(Scene targetScene)
    {
        if (Selection.activeTransform != null && Selection.activeTransform.gameObject.scene == targetScene)
            return Selection.activeTransform.position + Vector3.up * 0.1f;

        foreach (string name in new[] { "PlayerSpawn", "SpawnPoint", "RespawnPoint", "StartPoint" })
        {
            var spawn = FindByNameInScene(targetScene, name);
            if (spawn != null)
                return spawn.transform.position;
        }

        return Vector3.up * 1.5f;
    }

    static void EnsureEventSystem(Scene targetScene, SetupReport report)
    {
        if (FindInScene<EventSystem>(targetScene) != null)
            return;

        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
        SceneManager.MoveGameObjectToScene(go, targetScene);
        report.CreatedCount++;
    }

    static void RebindSceneReferences(Scene targetScene, SetupReport report)
    {
        var player = FindInScene<PlayerMovement>(targetScene);
        var fpCamera = FindInScene<FirstPersonCamera>(targetScene);
        var mapSystem = FindInScene<MapSystem>(targetScene);
        var questManager = FindInScene<QuestSystemManager>(targetScene);
        var serverSync = FindInScene<GameServerSync>(targetScene);
        var camera = fpCamera != null ? fpCamera.GetComponent<Camera>() : FindMainCameraInScene(targetScene);
        var cameraHolder = fpCamera != null ? fpCamera.cameraHolder : FindChildTransform(player != null ? player.transform : null, "CameraHolder");

        if (player != null)
        {
            Undo.RecordObject(player, "Rebind Player");
            if (IsTagDefined("Player"))
                player.gameObject.tag = "Player";
            if (fpCamera != null)
            {
                player.firstPersonCamera = fpCamera.transform;
                player.externalFirstPersonLook = fpCamera;
            }
            if (mapSystem != null)
                player.mapSystem = mapSystem;
            if (player.respawnPoint == null)
                player.respawnPoint = EnsureRespawnPoint(targetScene, player.transform.position);
            EditorUtility.SetDirty(player);
        }

        if (fpCamera != null && player != null)
        {
            Undo.RecordObject(fpCamera, "Rebind First Person Camera");
            fpCamera.playerObject = player.gameObject;
            fpCamera.cameraHolder = cameraHolder != null ? cameraHolder : fpCamera.transform.parent;
            EditorUtility.SetDirty(fpCamera);
        }

        foreach (var phone in FindAllInScene<PhoneUIManager>(targetScene))
        {
            if (phone == null)
                continue;
            Undo.RecordObject(phone, "Rebind Phone UI");
            phone.playerMovement = player;
            EditorUtility.SetDirty(phone);
            report.ReboundCount++;
        }

        foreach (var behaviour in FindAllInScene<MonoBehaviour>(targetScene))
        {
            if (behaviour == null)
                continue;
            report.ReboundCount += RebindFieldsByName(behaviour, targetScene, player, fpCamera, camera, cameraHolder, mapSystem, questManager, serverSync);
        }
    }

    static int RebindFieldsByName(
        MonoBehaviour behaviour,
        Scene targetScene,
        PlayerMovement player,
        FirstPersonCamera fpCamera,
        Camera camera,
        Transform cameraHolder,
        MapSystem mapSystem,
        QuestSystemManager questManager,
        GameServerSync serverSync)
    {
        int changed = 0;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var fields = behaviour.GetType().GetFields(flags);
        foreach (var field in fields)
        {
            if (field.IsStatic)
                continue;

            string fieldName = field.Name.ToLowerInvariant();
            object currentValue = field.GetValue(behaviour);
            var remappedSceneObject = TryResolveCopiedSceneReference(currentValue, field.FieldType, targetScene);
            object value = null;

            if (remappedSceneObject != null)
                value = remappedSceneObject;
            else if (player != null && fieldName.Contains("playermovement") && field.FieldType.IsAssignableFrom(typeof(PlayerMovement)))
                value = player;
            else if (player != null && (fieldName == "player" || fieldName.Contains("playertransform")) && field.FieldType.IsAssignableFrom(typeof(Transform)))
                value = player.transform;
            else if (player != null && fieldName.Contains("playerobject") && field.FieldType.IsAssignableFrom(typeof(GameObject)))
                value = player.gameObject;
            else if (fpCamera != null && fieldName.Contains("firstpersoncamera") && field.FieldType.IsAssignableFrom(typeof(FirstPersonCamera)))
                value = fpCamera;
            else if (camera != null && fieldName.Contains("firstpersoncamera") && field.FieldType.IsAssignableFrom(typeof(Camera)))
                value = camera;
            else if (camera != null && fieldName.Contains("maincamera") && field.FieldType.IsAssignableFrom(typeof(Camera)))
                value = camera;
            else if (cameraHolder != null && fieldName.Contains("cameraholder") && field.FieldType.IsAssignableFrom(typeof(Transform)))
                value = cameraHolder;
            else if (mapSystem != null && fieldName.Contains("mapsystem") && field.FieldType.IsAssignableFrom(typeof(MapSystem)))
                value = mapSystem;
            else if (questManager != null && fieldName.Contains("questmanager") && field.FieldType.IsAssignableFrom(typeof(QuestSystemManager)))
                value = questManager;
            else if (serverSync != null && fieldName.Contains("serversync") && field.FieldType.IsAssignableFrom(typeof(GameServerSync)))
                value = serverSync;

            if (value == null || Equals(currentValue, value))
                continue;

            Undo.RecordObject(behaviour, "Rebind undergarden references");
            field.SetValue(behaviour, value);
            EditorUtility.SetDirty(behaviour);
            changed++;
        }

        return changed;
    }

    static object TryResolveCopiedSceneReference(object currentValue, Type fieldType, Scene targetScene)
    {
        var currentObject = currentValue as UnityEngine.Object;
        if (currentObject == null)
            return null;

        var currentGameObject = currentObject as GameObject;
        if (currentGameObject == null && currentObject is Component component)
            currentGameObject = component.gameObject;
        if (currentGameObject == null || currentGameObject.scene == targetScene)
            return null;

        var copied = FindByNameInScene(targetScene, currentGameObject.name);
        if (copied == null)
            return null;

        if (fieldType.IsAssignableFrom(typeof(GameObject)))
            return copied;
        if (typeof(Component).IsAssignableFrom(fieldType))
            return copied.GetComponent(fieldType);
        return null;
    }

    static void ParentLooseUiToCanvas(GameObject clone, Scene targetScene)
    {
        if (clone == null || clone.GetComponent<Canvas>() != null || clone.GetComponent<RectTransform>() == null)
            return;

        var canvas = FindPreferredCanvas(targetScene);
        if (canvas == null)
            canvas = CreateMainCanvas(targetScene);

        clone.transform.SetParent(canvas.transform, false);
    }

    static Canvas FindPreferredCanvas(Scene targetScene)
    {
        Canvas best = null;
        int bestScore = int.MinValue;
        foreach (var canvas in FindAllInScene<Canvas>(targetScene))
        {
            if (canvas.renderMode == RenderMode.WorldSpace)
                continue;
            int score = canvas.sortingOrder * 10 + canvas.transform.childCount;
            string name = canvas.name.ToLowerInvariant();
            if (name.Contains("phone") || name.Contains("main") || name == "canvas")
                score += 100;
            if (score > bestScore)
            {
                best = canvas;
                bestScore = score;
            }
        }
        return best;
    }

    static Canvas CreateMainCanvas(Scene targetScene)
    {
        var go = new GameObject("Canvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(go, "Create Canvas");
        SceneManager.MoveGameObjectToScene(go, targetScene);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    static Transform EnsureRespawnPoint(Scene targetScene, Vector3 playerPosition)
    {
        var existing = FindByNameInScene(targetScene, "RespawnPoint");
        if (existing != null)
            return existing.transform;

        var go = new GameObject("RespawnPoint");
        Undo.RegisterCreatedObjectUndo(go, "Create RespawnPoint");
        go.transform.position = playerPosition;
        SceneManager.MoveGameObjectToScene(go, targetScene);
        return go.transform;
    }

    static void NormalizeMainCamera(Scene targetScene)
    {
        var firstPerson = FindInScene<FirstPersonCamera>(targetScene);
        if (firstPerson == null)
            return;

        var mainCamera = firstPerson.GetComponent<Camera>();
        foreach (var cam in FindAllInScene<Camera>(targetScene))
        {
            if (cam == null)
                continue;
            Undo.RecordObject(cam.gameObject, "Normalize Main Camera");
            cam.gameObject.tag = cam == mainCamera ? "MainCamera" : "Untagged";
            EditorUtility.SetDirty(cam.gameObject);
        }
    }

    static bool IsTagDefined(string tag)
    {
        foreach (string definedTag in UnityEditorInternal.InternalEditorUtility.tags)
        {
            if (definedTag == tag)
                return true;
        }
        return false;
    }

    static Camera FindMainCameraInScene(Scene scene)
    {
        foreach (var cam in FindAllInScene<Camera>(scene))
        {
            if (cam != null && cam.CompareTag("MainCamera"))
                return cam;
        }
        return FindInScene<Camera>(scene);
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

    static Transform FindChildTransform(Transform root, string name)
    {
        if (root == null)
            return null;
        return FindByNameRecursive(root, name);
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

    sealed class SetupReport
    {
        public int CreatedCount;
        public int SkippedCount;
        public int ReboundCount;
        public UnityEngine.Object SelectedObject;
    }
}
#endif
