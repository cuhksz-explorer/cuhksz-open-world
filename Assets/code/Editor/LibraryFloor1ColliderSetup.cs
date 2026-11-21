#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 图书馆1楼：每个网格仅一个非凸 MeshCollider（不与 FBX addCollider 重复叠加）。
/// </summary>
public static class LibraryFloor1ColliderSetup
{
    public const string FbxAssetPath = "Assets/scene/图书馆1楼.blend";
    public const string ScenePath = "Assets/scene/libraryfloor1.scene";

    const string MenuSetup = "Tools/Physics/图书馆1楼 - 添加 MeshCollider（当前场景）";
    const string MenuSetupScene = "Tools/Physics/图书馆1楼 - 打开场景并添加 MeshCollider";
    const string MenuDedupe = "Tools/Physics/图书馆1楼 - 移除重复 MeshCollider";

    [MenuItem(MenuDedupe, false, 29)]
    public static void RemoveDuplicatesFromMenu()
    {
        int removed = DedupeInLoadedScenes(saveScenes: true);
        EditorUtility.DisplayDialog("图书馆1楼", $"已移除 {removed} 个重复 MeshCollider。", "确定");
    }

    [MenuItem(MenuSetup, false, 30)]
    public static void SetupInCurrentSceneFromMenu()
    {
        int removed = DedupeInLoadedScenes(saveScenes: false);
        int added = EnsureInLoadedScenes(saveScenes: true);
        EditorUtility.DisplayDialog(
            "图书馆1楼 MeshCollider",
            $"移除重复: {removed}\n补全碰撞体: {added}",
            "确定");
    }

    [MenuItem(MenuSetupScene, false, 31)]
    public static void OpenSceneAndSetupFromMenu()
    {
        if (!System.IO.File.Exists(ScenePath))
        {
            EditorUtility.DisplayDialog("图书馆1楼 MeshCollider", $"场景不存在：{ScenePath}", "确定");
            return;
        }

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        DisableFbxAutoColliders(reimport: true);
        int removed = DedupeInLoadedScenes(saveScenes: false);
        int added = EnsureInLoadedScenes(saveScenes: true);
        EditorUtility.DisplayDialog(
            "图书馆1楼 MeshCollider",
            $"Reimport 完成。\n移除重复: {removed}\n补全碰撞体: {added}",
            "确定");
    }

    public static void SetupAndSave()
    {
        DisableFbxAutoColliders(reimport: true);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        DedupeInLoadedScenes(saveScenes: false);
        EnsureInLoadedScenes(saveScenes: true);
        AssetDatabase.SaveAssets();
    }

    static void DisableFbxAutoColliders(bool reimport)
    {
        var importer = AssetImporter.GetAtPath(FbxAssetPath) as ModelImporter;
        if (importer == null)
            return;

        importer.addCollider = false;
        if (reimport)
            importer.SaveAndReimport();
    }

    static List<GameObject> GetLibraryRoots()
    {
        var roots = FindLibraryRootsInLoadedScenes();
        if (roots.Count == 0)
            roots = FindLibraryRootsFromSelection();
        return roots;
    }

    static int DedupeInLoadedScenes(bool saveScenes)
    {
        var roots = GetLibraryRoots();
        if (roots.Count == 0)
            return 0;

        Undo.IncrementCurrentGroup();
        int removed = 0;
        foreach (var root in roots)
            removed += LibraryFloor1PhysicsBootstrap.RemoveDuplicateColliders(root.transform);

        Undo.SetCurrentGroupName("Library Floor1 Dedupe Colliders");
        SaveScenesIfNeeded(saveScenes);
        return removed;
    }

    static int EnsureInLoadedScenes(bool saveScenes)
    {
        DisableFbxAutoColliders(reimport: false);

        var roots = GetLibraryRoots();
        if (roots.Count == 0)
            return 0;

        Undo.IncrementCurrentGroup();
        int added = 0;
        foreach (var root in roots)
        {
            added += LibraryFloor1PhysicsBootstrap.EnsureColliders(root.transform);
            if (root.GetComponent<LibraryFloor1PhysicsBootstrap>() == null)
                Undo.AddComponent<LibraryFloor1PhysicsBootstrap>(root);
        }

        Undo.SetCurrentGroupName("Library Floor1 MeshColliders");
        SaveScenesIfNeeded(saveScenes);
        Debug.Log($"[图书馆1楼 MeshCollider] 补全 {added} 个，根节点 {roots.Count} 个。");
        return added;
    }

    static void SaveScenesIfNeeded(bool saveScenes)
    {
        if (!saveScenes)
            return;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded && scene.isDirty)
                EditorSceneManager.SaveScene(scene);
        }
    }

    static List<GameObject> FindLibraryRootsInLoadedScenes()
    {
        var roots = new List<GameObject>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;

            foreach (var root in scene.GetRootGameObjects())
                CollectLibraryRootsRecursive(root.transform, roots);
        }

        return roots;
    }

    static List<GameObject> FindLibraryRootsFromSelection()
    {
        return Selection.gameObjects
            .Where(go => go != null && IsLibraryHierarchy(go))
            .Distinct()
            .ToList();
    }

    static void CollectLibraryRootsRecursive(Transform t, List<GameObject> roots)
    {
        if (t == null)
            return;

        if (IsLibraryHierarchy(t.gameObject))
        {
            roots.Add(t.gameObject);
            return;
        }

        for (int i = 0; i < t.childCount; i++)
            CollectLibraryRootsRecursive(t.GetChild(i), roots);
    }

    static bool IsLibraryHierarchy(GameObject go)
    {
        if (go == null || EditorUtility.IsPersistent(go))
            return false;

        if (go.name.Contains("图书馆1楼") || go.name.Contains("图书馆1"))
            return true;

        if (PrefabUtility.IsPartOfPrefabInstance(go))
        {
            string instancePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            if (instancePath == FbxAssetPath)
                return PrefabUtility.GetNearestPrefabInstanceRoot(go) == go;
        }

        return false;
    }
}
#endif
