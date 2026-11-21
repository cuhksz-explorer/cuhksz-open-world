#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 图书馆二楼：将 lib2f.blend 放入 Assets、导入材质、创建 libraryfloor2 场景并放置模型。
/// </summary>
public static class LibraryFloor2SceneSetup
{
    public const string SourceBlendPath = @"z:\微信\xwechat_files\wxid_iex8clusq8ee32_2373\msg\file\2026-05\lib2f(1).blend";
    public const string ModelAssetPath = LibraryFloor2DoubleSidedImport.ModelAssetPath;
    public const string ScenePath = "Assets/scene/libraryfloor2.scene";

    const string MenuPath = "Tools/场景/libraryfloor2 - 导入模型并创建场景";
    const string MenuImportSceneOnly = "Tools/场景/libraryfloor2 - 更新二楼模型与材质（仅本场景）";

    [MenuItem(MenuImportSceneOnly, false, 48)]
    public static void ImportNewBlendSceneOnlyFromMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("libraryfloor2", "请先退出 Play Mode。", "确定");
            return;
        }

        try
        {
            ImportNewBlendSceneOnly();
            EditorUtility.DisplayDialog("libraryfloor2", "二楼模型与材质已更新（仅 libraryfloor2）。", "确定");
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("libraryfloor2", $"更新失败：{ex.Message}", "确定");
        }
    }

    [MenuItem(MenuPath, false, 49)]
    public static void ImportAndCreateSceneFromMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("libraryfloor2", "请先退出 Play Mode。", "确定");
            return;
        }

        try
        {
            ImportAndCreateScene();
            EditorUtility.DisplayDialog("libraryfloor2", "场景与模型导入完成。", "确定");
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("libraryfloor2", $"导入失败：{ex.Message}", "确定");
        }
    }

    /// <summary>BatchMode: -executeMethod LibraryFloor2SceneSetup.ImportAndCreateScene</summary>
    public static void ImportAndCreateScene()
    {
        ImportNewBlendToLibraryFloor2(forceOverwriteBlend: false);
    }

    /// <summary>
    /// 从微信源 blend 导出 FBX 并仅更新 libraryfloor2 场景（不打开/修改其它场景）。
    /// BatchMode: -executeMethod LibraryFloor2SceneSetup.ImportNewBlendToLibraryFloor2
    /// </summary>
    public static void ImportNewBlendToLibraryFloor2()
    {
        ImportNewBlendToLibraryFloor2(forceOverwriteBlend: true, forceReexportFbx: false);
    }

    /// <summary>BatchMode: FBX 已导出后仅更新材质与 libraryfloor2 场景。</summary>
    public static void ImportNewBlendSceneOnly()
    {
        ImportNewBlendToLibraryFloor2(forceOverwriteBlend: false, forceReexportFbx: false);
    }

    const string ModelFbxGuid = "7aa5c8e04a22f6c47918ec4d53021da8";

    static void ImportNewBlendToLibraryFloor2(bool forceOverwriteBlend, bool forceReexportFbx = true)
    {
        EnsureModelMetaGuid(LibraryFloor2BlendExport.FbxAssetPath, ModelFbxGuid);
        EnsureBlendInAssets(forceOverwriteBlend);
        LibraryFloor2BlendExport.EnsureFbx(forceReexport: forceReexportFbx);
        LibraryFloor24MaterialBinder.BindImporterExternalMaterials(LibraryFloor2BlendExport.FbxAssetPath);
        LibraryFloor2DoubleSidedImport.ApplyImporterSettings(reimport: true);
        LibraryFloor2DoubleSidedImport.FixAllMaterialsAndRenderers();
        CreateOrUpdateScene();
        LibraryFloor2MaterialRemap.RematchAll(openSceneIfNeeded: false);
        var target = new LibraryFloor24MaterialBinder.ModelTarget(
            "Assets/scene/lib2f.fbx",
            ModelFbxGuid,
            ScenePath,
            "lib2f_外壳");
        LibraryFloor24MaterialBinder.RemapSceneRenderers(target);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[libraryfloor2] 新 blend 已导入，仅 libraryfloor2 场景已更新。");
    }

    static void EnsureModelMetaGuid(string assetPath, string guid)
    {
        string metaPath = assetPath + ".meta";
        if (!File.Exists(metaPath))
            return;

        string text = File.ReadAllText(metaPath);
        if (Regex.IsMatch(text, @"^guid: " + guid + @"$", RegexOptions.Multiline))
            return;

        text = Regex.Replace(text, @"^guid: .+$", "guid: " + guid, RegexOptions.Multiline);
        File.WriteAllText(metaPath, text);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
    }

    static void EnsureBlendInAssets(bool forceOverwrite = false)
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string destFull = Path.Combine(projectRoot, "Assets", "scene", "lib2f.blend");
        string destDir = Path.GetDirectoryName(destFull);
        if (!Directory.Exists(destDir))
            Directory.CreateDirectory(destDir);

        if (!File.Exists(SourceBlendPath))
            throw new FileNotFoundException($"未找到源模型：{SourceBlendPath}");

        if (!forceOverwrite && File.Exists(destFull))
        {
            var srcTime = File.GetLastWriteTimeUtc(SourceBlendPath);
            var dstTime = File.GetLastWriteTimeUtc(destFull);
            if (dstTime >= srcTime)
            {
                Debug.Log($"[libraryfloor2] blend 已是最新：{LibraryFloor2BlendExport.BlendAssetPath}");
                return;
            }
        }

        try
        {
            File.Copy(SourceBlendPath, destFull, overwrite: true);
            Debug.Log($"[libraryfloor2] 已更新 blend：{LibraryFloor2BlendExport.BlendAssetPath}");
        }
        catch (System.IO.IOException ex)
        {
            if (!File.Exists(destFull))
                throw;

            Debug.LogWarning(
                $"[libraryfloor2] blend 被占用，跳过覆盖，继续使用现有 {LibraryFloor2BlendExport.BlendAssetPath}：{ex.Message}");
        }

        AssetDatabase.ImportAsset(LibraryFloor2BlendExport.BlendAssetPath, ImportAssetOptions.ForceUpdate);
    }

    static void TryCreateHardLink(string dest, string source)
    {
        try
        {
            if (CreateHardLink(dest, source, System.IntPtr.Zero))
                Debug.Log($"[libraryfloor2] 已创建硬链接：{dest}");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[libraryfloor2] 硬链接失败，将尝试复制：{ex.Message}");
        }
    }

    [System.Runtime.InteropServices.DllImport("Kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, System.IntPtr lpSecurityAttributes);

    static void CreateOrUpdateScene()
    {
        var prefab = LoadModelPrefab();
        if (prefab == null)
            throw new System.InvalidOperationException(
                $"无法加载模型预制体：{ModelAssetPath}\n" +
                "请确认 lib2f.fbx 已导入（菜单：Tools > 场景 > libraryfloor2 - 从 blend 导出 FBX）。");

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
        instance.name = "lib2f";
        instance.transform.position = Vector3.zero;
        instance.transform.rotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        TryBuildEnvelopeShell();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        Debug.Log($"[libraryfloor2] 场景已保存：{ScenePath}");
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
            if (root.name == "lib2f" || root.name.Contains("图书馆2"))
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
            LibraryFloor2ShellSetup.BuildShellInActiveScene(
                LibraryFloor2ShellSetup.DefaultPadding,
                LibraryFloor2ShellSetup.DefaultThickness,
                LibraryFloor2ShellSetup.DefaultCellSize,
                includeCeiling: false);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[libraryfloor2] 外壳未自动生成（可在菜单手动生成）：{ex.Message}");
        }
    }

    static GameObject LoadModelPrefab()
    {
        var main = AssetDatabase.LoadMainAssetAtPath(ModelAssetPath) as GameObject;
        if (main != null)
            return main;

        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ModelAssetPath))
        {
            if (asset is GameObject go && go.transform.childCount > 0)
                return go;
        }

        return null;
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
