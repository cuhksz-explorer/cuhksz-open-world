#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 图书馆四楼：将 .blend 放入 Assets、导入材质、创建 libraryfloor4 场景并放置模型。
/// </summary>
public static class LibraryFloor4SceneSetup
{
    public const string SourceBlendPath = @"z:\PHY1001 FINAL\图书馆四楼.blend";
    public const string ModelAssetPath = LibraryFloor4DoubleSidedImport.ModelAssetPath;
    public const string ScenePath = "Assets/scene/libraryfloor4.scene";

    const string MenuPath = "Tools/场景/libraryfloor4 - 导入模型并创建场景";

    [MenuItem(MenuPath, false, 50)]
    public static void ImportAndCreateSceneFromMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("libraryfloor4", "请先退出 Play Mode。", "确定");
            return;
        }

        try
        {
            ImportAndCreateScene();
            EditorUtility.DisplayDialog("libraryfloor4", "场景与模型导入完成。", "确定");
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("libraryfloor4", $"导入失败：{ex.Message}", "确定");
        }
    }

    /// <summary>BatchMode: -executeMethod LibraryFloor4SceneSetup.ImportAndCreateScene</summary>
    public static void ImportAndCreateScene()
    {
        EnsureModelInAssets();
        LibraryFloor4DoubleSidedImport.FixAndReimport();
        CreateOrUpdateScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[libraryfloor4] 导入与场景创建完成。");
    }

    static void EnsureModelInAssets()
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string destFull = Path.Combine(projectRoot, "Assets", "scene", "图书馆四楼.blend");
        string destDir = Path.GetDirectoryName(destFull);
        if (!Directory.Exists(destDir))
            Directory.CreateDirectory(destDir);

        if (File.Exists(destFull))
        {
            Debug.Log($"[libraryfloor4] 模型已存在：{ModelAssetPath}");
            return;
        }

        if (!File.Exists(SourceBlendPath))
            throw new FileNotFoundException($"未找到源模型：{SourceBlendPath}");

        TryCreateHardLink(destFull, SourceBlendPath);
        if (!File.Exists(destFull))
            File.Copy(SourceBlendPath, destFull, overwrite: false);

        AssetDatabase.ImportAsset(ModelAssetPath, ImportAssetOptions.ForceUpdate);
        Debug.Log($"[libraryfloor4] 已链接/复制模型到 {ModelAssetPath}");
    }

    static void TryCreateHardLink(string dest, string source)
    {
        try
        {
            if (CreateHardLink(dest, source, System.IntPtr.Zero))
                Debug.Log($"[libraryfloor4] 已创建硬链接：{dest}");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[libraryfloor4] 硬链接失败，将尝试复制：{ex.Message}");
        }
    }

    [System.Runtime.InteropServices.DllImport("Kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, System.IntPtr lpSecurityAttributes);

    static void CreateOrUpdateScene()
    {
        var prefab = AssetDatabase.LoadMainAssetAtPath(ModelAssetPath) as GameObject;
        if (prefab == null)
            throw new System.InvalidOperationException($"无法加载模型预制体：{ModelAssetPath}");

        Scene scene;
        bool isNew = !File.Exists(ScenePath);
        if (isNew)
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
        else
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            RemoveExistingModelRoots();
        }

        EnsureSceneDefaults();
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        if (instance == null)
            instance = Object.Instantiate(prefab);
        instance.name = "图书馆四楼";
        instance.transform.position = Vector3.zero;
        instance.transform.rotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        TryBuildEnvelopeShell();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        Debug.Log($"[libraryfloor4] 场景已保存：{ScenePath}");
    }

    static void AddSceneToBuildSettings(string scenePath)
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        string normalized = scenePath.Replace('\\', '/');
        foreach (var entry in scenes)
        {
            if (entry.path.Replace('\\', '/') == normalized)
                return;
        }

        scenes.Add(new EditorBuildSettingsScene(normalized, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    static void RemoveExistingModelRoots()
    {
        var toRemove = new System.Collections.Generic.List<GameObject>();
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name.Contains("图书馆四楼") || root.name.Contains("图书馆4"))
                toRemove.Add(root);
            else if (PrefabUtility.IsPartOfPrefabInstance(root))
            {
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
                if (path == ModelAssetPath)
                    toRemove.Add(root);
            }
        }

        foreach (var go in toRemove)
            Object.DestroyImmediate(go);
    }

    static void TryBuildEnvelopeShell()
    {
        try
        {
            LibraryFloor4ShellSetup.BuildShellInActiveScene(
                LibraryFloor4ShellSetup.DefaultPadding,
                LibraryFloor4ShellSetup.DefaultThickness,
                LibraryFloor4ShellSetup.DefaultCellSize,
                includeCeiling: false);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[libraryfloor4] 外壳未自动生成（可在菜单手动生成）：{ex.Message}");
        }
    }

    static void EnsureSceneDefaults()
    {
        if (Object.FindObjectOfType<Light>() == null)
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        if (Camera.main == null && Object.FindObjectOfType<Camera>() == null)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0f, 2f, -8f);
            camGo.transform.rotation = Quaternion.Euler(10f, 0f, 0f);
        }
    }
}
#endif
