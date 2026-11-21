#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 通过 Editor API 修正 libraryfloor2 默认出生点与 undergarden 传送落点（不手改 .unity YAML）。
/// </summary>
public static class LibraryFloor2TeleportSpawnSetup
{
    const string MenuPath = "Tools/场景配置/libraryfloor2 - 修正 undergarden 传送落点";
    const string LibraryFloor2ScenePath = "Assets/scene/libraryfloor2.scene";
    const string UndergardenScenePath = "Assets/scene/undergarden.unity";

    static readonly Vector3 EntranceXZ = new Vector3(3.38000011f, 0f, -10.1700001f);

    [MenuItem(MenuPath, false, 5)]
    public static void FixFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("libraryfloor2 传送落点", "请先退出 Play Mode。", "确定");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        try
        {
            var report = FixScenes();
            EditorUtility.DisplayDialog("libraryfloor2 传送落点", report, "确定");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("libraryfloor2 传送落点", $"修复失败：{ex.Message}", "确定");
        }
    }

    public static string FixScenes()
    {
        var floor2Scene = EditorSceneManager.OpenScene(LibraryFloor2ScenePath, OpenSceneMode.Single);
        Vector3 spawnPosition = ResolveFloorSpawnPosition(floor2Scene);
        int playerFixed = FixPlayerAndRespawn(floor2Scene, spawnPosition);
        EditorSceneManager.SaveScene(floor2Scene);

        int teleportButtonsUpdated = 0;
        var undergardenScene = EditorSceneManager.OpenScene(UndergardenScenePath, OpenSceneMode.Single);
        foreach (var teleport in FindAllInScene<TeleportButton>(undergardenScene))
        {
            if (teleport == null || !SceneTeleportHelper.SceneNameMatches(teleport.targetSceneName, "libraryfloor2"))
                continue;

            Undo.RecordObject(teleport, "Fix undergarden -> libraryfloor2 teleport");
            teleport.targetSceneName = "libraryfloor2";
            teleport.targetPosition = spawnPosition;
            EditorUtility.SetDirty(teleport);
            teleportButtonsUpdated++;
        }

        UpdateEntryRegistryAsset(spawnPosition);
        EditorSceneManager.SaveScene(undergardenScene);
        AssetDatabase.SaveAssets();

        return
            $"修复完成。\n\n" +
            $"libraryfloor2 落点：{spawnPosition}\n" +
            $"修正玩家/复活点：{playerFixed}\n" +
            $"更新 undergarden 传送按钮：{teleportButtonsUpdated}\n" +
            $"已同步 SceneTeleportEntryRegistry.asset";
    }

    static Vector3 ResolveFloorSpawnPosition(Scene scene)
    {
        EditorSceneManager.SetActiveScene(scene);
        Physics.SyncTransforms();

        var origin = new Vector3(EntranceXZ.x, 200f, EntranceXZ.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 400f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point + Vector3.up * 0.05f;

        return new Vector3(EntranceXZ.x, 0.56f, EntranceXZ.z);
    }

    static int FixPlayerAndRespawn(Scene scene, Vector3 spawnPosition)
    {
        int changed = 0;
        var player = FindInScene<PlayerMovement>(scene);
        if (player != null)
        {
            Undo.RecordObject(player.transform, "Fix libraryfloor2 player spawn");
            player.transform.position = spawnPosition;
            player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var respawn = EnsureRespawnPoint(scene, spawnPosition);
            Undo.RecordObject(player, "Bind libraryfloor2 respawn");
            player.respawnPoint = respawn;
            EditorUtility.SetDirty(player);
            changed++;
        }

        return changed;
    }

    static Transform EnsureRespawnPoint(Scene scene, Vector3 position)
    {
        var existing = FindByNameInScene(scene, "RespawnPoint");
        if (existing != null)
        {
            Undo.RecordObject(existing.transform, "Move RespawnPoint");
            existing.transform.position = position;
            return existing.transform;
        }

        var go = new GameObject("RespawnPoint");
        Undo.RegisterCreatedObjectUndo(go, "Create RespawnPoint");
        go.transform.position = position;
        SceneManager.MoveGameObjectToScene(go, scene);
        return go.transform;
    }

    static void UpdateEntryRegistryAsset(Vector3 spawnPosition)
    {
        var registry = AssetDatabase.LoadAssetAtPath<SceneTeleportEntryRegistry>(
            "Assets/code/Resources/SceneTeleportEntryRegistry.asset");
        if (registry == null || registry.entries == null)
            return;

        bool changed = false;
        for (int i = 0; i < registry.entries.Length; i++)
        {
            var entry = registry.entries[i];
            if (!SceneTeleportHelper.SceneNameMatches(entry.sourceSceneName, "undergarden") ||
                !SceneTeleportHelper.SceneNameMatches(entry.targetSceneName, "libraryfloor2"))
                continue;

            entry.position = spawnPosition;
            entry.snapToGround = true;
            registry.entries[i] = entry;
            changed = true;
        }

        if (!changed)
            return;

        EditorUtility.SetDirty(registry);
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

    static System.Collections.Generic.List<T> FindAllInScene<T>(Scene scene) where T : Component
    {
        var list = new System.Collections.Generic.List<T>();
        foreach (var item in Resources.FindObjectsOfTypeAll<T>())
        {
            if (item != null && item.gameObject.scene == scene)
                list.Add(item);
        }

        return list;
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
}
#endif
