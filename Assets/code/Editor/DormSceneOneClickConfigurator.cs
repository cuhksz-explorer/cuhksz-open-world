#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DormMall;
using GameQuestSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 一键配置 dorm.scene：套用主角/UI、寝室商城；生成共享出生点 + 单一放置区（同地图不同实例）。
/// </summary>
public static class DormSceneOneClickConfigurator
{
    const string MenuPath = "Tools/场景配置/一键配置 dorm 寝室场景";
    const string PlayerUiTemplatePath = "Assets/scene/undergarden.unity";
    const string DormSystemsTemplatePath = "Assets/scene/lingcollegeB.unity";

    static readonly Vector3 DefaultDormCenter = new Vector3(-606.65f, 3.82f, -218.45f);
    /// <summary>覆盖整间寝室的默认放置盒（可在场景中再调 BoxCollider）。</summary>
    static readonly Vector3 DefaultSharedZoneSize = new Vector3(24f, 5f, 20f);

    static readonly string[] DormSystemObjectNames =
    {
        "PlacementModeController",
        "PlacementHelpCanvas",
        "DormMallCanvas",
        "FoodMallCanvas"
    };

    static readonly string[] DormModelCenterSkipNames =
    {
        "DormRoomSystem",
        "DormTutorialHint",
        "PlayerSpawn",
        "Main Camera",
        "Directional Light"
    };

    const string RebuildMarkersMenuPath = "Tools/场景配置/仅重建 dorm 共享出生点与放置区";

    [MenuItem(RebuildMarkersMenuPath, false, 3)]
    public static void RebuildSharedDormMarkersOnly()
    {
        Scene targetScene = SceneManager.GetActiveScene();
        if (!targetScene.IsValid() || string.IsNullOrEmpty(targetScene.path))
        {
            EditorUtility.DisplayDialog("重建标记", "请先打开 dorm.scene。", "确定");
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        int undoGroup = Undo.GetCurrentGroup();
        try
        {
            EditorSceneManager.SetActiveScene(targetScene);
            EnsureDormRoomSystem(targetScene);
            EditorSceneManager.MarkSceneDirty(targetScene);
            EditorUtility.DisplayDialog("完成", "已重建共享 DormSpawnAnchor 与 DormPlacementZone，并删除旧的 #0~#7 多房间标记。", "确定");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("失败", ex.Message, "确定");
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    [MenuItem(MenuPath, false, 2)]
    public static void ConfigureDormScene()
    {
        Scene targetScene = SceneManager.GetActiveScene();
        if (!targetScene.IsValid() || string.IsNullOrEmpty(targetScene.path))
        {
            EditorUtility.DisplayDialog("配置 dorm", "请先打开 Assets/scene/dorm.scene。", "确定");
            return;
        }

        if (!targetScene.path.Replace("\\", "/").EndsWith("dorm.scene", StringComparison.OrdinalIgnoreCase))
        {
            if (!EditorUtility.DisplayDialog(
                    "配置 dorm",
                    "当前场景不是 dorm.scene，仍要继续配置吗？",
                    "继续",
                    "取消"))
                return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("配置 dorm", "请先退出 Play Mode。", "确定");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(PlayerUiTemplatePath))
        {
            EditorUtility.DisplayDialog("配置失败", "找不到模板场景：\n" + PlayerUiTemplatePath, "确定");
            return;
        }

        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(DormSystemsTemplatePath))
        {
            EditorUtility.DisplayDialog("配置失败", "找不到模板场景：\n" + DormSystemsTemplatePath, "确定");
            return;
        }

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("一键配置 dorm 寝室场景");

        Scene uiTemplate = default;
        Scene dormTemplate = default;
        int uiCreated = 0;
        int uiSkipped = 0;
        int dormCopied = 0;

        try
        {
            UndergardenSceneOneClickConfigurator.AddProjectScenesToBuildSettings();
            EditorSceneManager.SetActiveScene(targetScene);

            RunStep("清理 lingcollegeB 历史布置数据", () =>
            {
                string savedUser = PlayerPrefs.GetString("GameServer_Username", "").Trim();
                int removed = DormPlacementPersistence.PurgeLegacyLingCollegeBLayoutData("local_guest", savedUser);
                Debug.Log("[DormScene] 已删除 lingcollegeB 本地布置键 " + removed + " 个（不迁移到 dorm）。");
            });

            RunStep("套用 undergarden 主角与 UI", () =>
            {
                uiTemplate = EditorSceneManager.OpenScene(PlayerUiTemplatePath, OpenSceneMode.Additive);
                if (!uiTemplate.IsValid() || !uiTemplate.isLoaded)
                    throw new InvalidOperationException("无法加载 undergarden 模板场景。");

                EditorSceneManager.SetActiveScene(targetScene);
                var uiReport = UndergardenSceneOneClickConfigurator.ApplyFromTemplateToScene(uiTemplate, targetScene);
                uiCreated = uiReport.created;
                uiSkipped = uiReport.skipped;
            });

            RunStep("复制 lingcollegeB 寝室系统", () =>
            {
                dormTemplate = EditorSceneManager.OpenScene(DormSystemsTemplatePath, OpenSceneMode.Additive);
                if (!dormTemplate.IsValid() || !dormTemplate.isLoaded)
                    throw new InvalidOperationException("无法加载 lingcollegeB 模板场景。");

                EditorSceneManager.SetActiveScene(targetScene);
                dormCopied = CopyNamedRoots(dormTemplate, targetScene, DormSystemObjectNames);
            });

            RunStep("清理默认相机并生成共享寝室出生/放置区", () =>
            {
                EditorSceneManager.SetActiveScene(targetScene);
                RemoveSceneDefaultCamera(targetScene);
                EnsureDormRoomSystem(targetScene);
            });

            RunStep("写入寝室教学任务", () =>
            {
                EditorSceneManager.SetActiveScene(targetScene);
                ApplyQuestPreset(targetScene);
            });

            RunStep("更新 uppergarden 传送门", PointUppergardenDormPortalToDormScene);

            EditorSceneManager.SetActiveScene(targetScene);
            EditorSceneManager.MarkSceneDirty(targetScene);
            EditorUtility.DisplayDialog(
                "配置完成",
                $"dorm 场景已配置。\n\n" +
                $"主角/UI 新增：{uiCreated}，跳过：{uiSkipped}\n" +
                $"寝室系统对象复制：{dormCopied}\n\n" +
                "请保存场景，并将 DormSpawnAnchor、DormPlacementZone 拖到寝室房间内，调整 BoxCollider 大小覆盖可摆放区域。",
                "确定");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            string detail = ex.Message;
            if (ex.InnerException != null)
                detail += "\n\n" + ex.InnerException.Message;
            EditorUtility.DisplayDialog("配置失败", detail, "确定");
        }
        finally
        {
            if (uiTemplate.IsValid() && uiTemplate.isLoaded)
                EditorSceneManager.CloseScene(uiTemplate, true);
            if (dormTemplate.IsValid() && dormTemplate.isLoaded)
                EditorSceneManager.CloseScene(dormTemplate, true);

            if (targetScene.IsValid() && targetScene.isLoaded)
                EditorSceneManager.SetActiveScene(targetScene);

            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    static void RunStep(string stepName, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("步骤失败：「" + stepName + "」\n" + ex.Message, ex);
        }
    }

    static int CopyNamedRoots(Scene templateScene, Scene targetScene, string[] names)
    {
        int copied = 0;
        foreach (string objectName in names)
        {
            if (FindByNameInScene(targetScene, objectName) != null)
                continue;

            var source = FindByNameInScene(templateScene, objectName);
            if (source == null)
            {
                Debug.LogWarning("[DormScene] 模板场景中未找到: " + objectName);
                continue;
            }

            var clone = UnityEngine.Object.Instantiate(source);
            if (clone == null)
            {
                Debug.LogWarning("[DormScene] 复制失败: " + objectName);
                continue;
            }

            clone.name = source.name;
            Undo.RegisterCreatedObjectUndo(clone, "Copy dorm system");
            EditorSceneManager.MoveGameObjectToScene(clone, targetScene);
            copied++;
        }
        return copied;
    }

    static void RemoveSceneDefaultCamera(Scene scene)
    {
        var cam = FindByNameInScene(scene, "Main Camera");
        if (cam != null)
            Undo.DestroyObjectImmediate(cam);
    }

    static void EnsureDormRoomSystem(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException("dorm 场景未加载，无法生成寝室标记。");

        DestroyLegacyMultiRoomObjects(scene);

        var stale = FindByNameInScene(scene, "DormRoomSystem");
        if (stale != null)
            Undo.DestroyObjectImmediate(stale);

        GameObject root = CreateSceneObject("DormRoomSystem", scene);
        if (root == null)
            throw new InvalidOperationException("无法创建 DormRoomSystem。");

        Vector3 center = ResolveDormModelCenter(scene);
        Vector3 spawnPos = center + Vector3.up * 1.2f;

        GameObject anchorGo = CreateSceneObject("DormSpawnAnchor", scene, root.transform);
        var anchor = EnsureComponent<DormRoomAnchor>(anchorGo);
        anchor.sharedInstanceSpawn = true;
        anchor.roomIndex = 0;
        anchorGo.transform.position = spawnPos;

        GameObject zoneGo = CreateSceneObject("DormPlacementZone", scene, root.transform);
        var box = EnsureComponent<BoxCollider>(zoneGo);
        box.isTrigger = true;
        box.size = DefaultSharedZoneSize;
        var zone = EnsureComponent<DormPlacementZone>(zoneGo);
        zone.sharedInstanceZone = true;
        zone.roomIndex = 0;
        zoneGo.transform.position = center + Vector3.up * (DefaultSharedZoneSize.y * 0.5f);

        EnsureSpawnPoint(scene, spawnPos);
        EnsureTutorialHint(scene, spawnPos);

        Selection.activeGameObject = zoneGo;
    }

    static void DestroyLegacyMultiRoomObjects(Scene scene)
    {
        for (int i = 0; i < 64; i++)
        {
            var a = FindByNameInScene(scene, "DormRoomAnchor_" + i);
            if (a != null)
                Undo.DestroyObjectImmediate(a);
            var z = FindByNameInScene(scene, "DormPlacementZone_" + i);
            if (z != null)
                Undo.DestroyObjectImmediate(z);
        }
    }

    static GameObject CreateSceneObject(string name, Scene scene, Transform parent = null)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        EditorSceneManager.MoveGameObjectToScene(go, scene);
        if (parent != null)
            go.transform.SetParent(parent, false);
        return go;
    }

    static T EnsureComponent<T>(GameObject go) where T : Component
    {
        if (go == null)
            throw new InvalidOperationException("无法向空物体添加组件 " + typeof(T).Name + "。");

        var comp = go.GetComponent<T>();
        if (comp == null)
            comp = go.AddComponent<T>();
        if (comp == null)
            throw new InvalidOperationException(
                "无法添加 " + typeof(T).Name + " 组件。请确认 DormMall 脚本无编译错误后再试。");

        Undo.RegisterCompleteObjectUndo(go, "Add " + typeof(T).Name);
        return comp;
    }

    static Vector3 ResolveDormModelCenter(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root == null || ShouldSkipForDormCenter(root.name))
                continue;

            if (root.name.Contains("道扬"))
                return root.transform.position;

            string lower = root.name.ToLowerInvariant();
            if (lower.Contains("dorm") && !lower.Contains("system"))
                return root.transform.position;
        }
        return DefaultDormCenter;
    }

    static bool ShouldSkipForDormCenter(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return true;
        for (int i = 0; i < DormModelCenterSkipNames.Length; i++)
        {
            if (string.Equals(objectName, DormModelCenterSkipNames[i], StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    static void EnsureSpawnPoint(Scene scene, Vector3 position)
    {
        var spawn = FindByNameInScene(scene, "PlayerSpawn");
        if (spawn == null)
        {
            spawn = new GameObject("PlayerSpawn");
            Undo.RegisterCreatedObjectUndo(spawn, "Create PlayerSpawn");
            EditorSceneManager.MoveGameObjectToScene(spawn, scene);
        }
        spawn.transform.position = position;
    }

    static void EnsureTutorialHint(Scene scene, Vector3 position)
    {
        var hint = FindByNameInScene(scene, "DormTutorialHint");
        if (hint == null)
        {
            hint = new GameObject("DormTutorialHint");
            Undo.RegisterCreatedObjectUndo(hint, "Create DormTutorialHint");
            EditorSceneManager.MoveGameObjectToScene(hint, scene);
            var comp = EnsureComponent<DormTutorialProximityHint>(hint);
            comp.hintText = "按 T 打开寝室商城，购买物品后可按 B 打开背包，在您的寝室区域内放置。";
            var sphere = EnsureComponent<SphereCollider>(hint);
            sphere.isTrigger = true;
            sphere.radius = 5f;
        }
        hint.transform.position = position;
    }

    static void ApplyQuestPreset(Scene scene)
    {
        var mgr = FindInScene<QuestSystemManager>(scene);
        if (mgr == null)
        {
            Debug.LogWarning("[DormScene] 未找到 QuestSystemManager，跳过任务链写入。");
            return;
        }
        Undo.RecordObject(mgr, "Apply dorm quest chain");
        mgr.ApplyLingCollegeBDormTutorialPresetChain();
        EditorUtility.SetDirty(mgr);
    }

    static void PointUppergardenDormPortalToDormScene()
    {
        const string upperPath = "Assets/scene/uppergarden.unity";
        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(upperPath))
        {
            Debug.LogWarning("[DormScene] 未找到 uppergarden，跳过传送门更新。");
            return;
        }

        var upperScene = EditorSceneManager.OpenScene(upperPath, OpenSceneMode.Additive);
        if (!upperScene.IsValid() || !upperScene.isLoaded)
        {
            Debug.LogWarning("[DormScene] 无法打开 uppergarden，跳过传送门更新。");
            return;
        }

        int changed = 0;
        foreach (var transfer in FindAllInScene<TeleportButton>(upperScene))
        {
            if (transfer == null || string.IsNullOrEmpty(transfer.targetSceneName))
                continue;

            string sceneKey = transfer.targetSceneName.Replace("\\", "/");
            bool isDormPortal = sceneKey.Contains("lingcollegeB", StringComparison.OrdinalIgnoreCase)
                || sceneKey.Contains("/dorm", StringComparison.OrdinalIgnoreCase)
                || string.Equals(sceneKey, "dorm", StringComparison.OrdinalIgnoreCase);
            if (!isDormPortal)
                continue;

            Undo.RecordObject(transfer, "Point dorm portal to dorm.scene");
            transfer.targetSceneName = "scene/dorm";
            transfer.targetPosition = LingCollegeBDormQuestPreset.DormTutorialWorldPosition;
            if (transfer.GetComponent<DormReturnAnchor>() == null)
                Undo.AddComponent<DormReturnAnchor>(transfer.gameObject);
            EditorUtility.SetDirty(transfer);
            changed++;
        }

        if (changed > 0)
        {
            EditorSceneManager.MarkSceneDirty(upperScene);
            Debug.Log("[DormScene] 已更新 uppergarden 中 " + changed + " 处传送目标为 scene/dorm（含 DormReturnAnchor）。");
        }

        EditorSceneManager.CloseScene(upperScene, true);
    }

    static GameObject FindByNameInScene(Scene scene, string name)
    {
        if (!scene.IsValid())
            return null;

        foreach (var root in scene.GetRootGameObjects())
        {
            if (root == null)
                continue;
            var found = FindChildRecursive(root.transform, name);
            if (found != null)
                return found.gameObject;
        }
        return null;
    }

    static Transform FindChildRecursive(Transform root, string name)
    {
        if (root == null)
            return null;
        if (string.Equals(root.name, name, StringComparison.Ordinal))
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChildRecursive(root.GetChild(i), name);
            if (found != null)
                return found;
        }
        return null;
    }

    static T FindInScene<T>(Scene scene) where T : Component
    {
        if (!scene.IsValid())
            return null;

        foreach (var item in Resources.FindObjectsOfTypeAll<T>())
        {
            if (item == null)
                continue;
            if (!item.gameObject.scene.IsValid())
                continue;
            if (item.gameObject.scene == scene)
                return item;
        }
        return null;
    }

    static List<T> FindAllInScene<T>(Scene scene) where T : Component
    {
        var list = new List<T>();
        if (!scene.IsValid())
            return list;

        foreach (var item in Resources.FindObjectsOfTypeAll<T>())
        {
            if (item == null)
                continue;
            if (!item.gameObject.scene.IsValid())
                continue;
            if (item.gameObject.scene == scene)
                list.Add(item);
        }
        return list;
    }
}
#endif
