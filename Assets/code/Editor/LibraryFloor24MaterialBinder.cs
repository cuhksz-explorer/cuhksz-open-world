#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 图书馆 2/4 楼：强制 FBX 使用外部 Materials/ 材质（带贴图颜色），并修正场景 Renderer。
/// 不修改 libraryfloor1。
/// </summary>
public static class LibraryFloor24MaterialBinder
{
    static readonly ModelTarget[] Targets =
    {
        new ModelTarget(
            "Assets/scene/lib2f.fbx",
            "7aa5c8e04a22f6c47918ec4d53021da8",
            "Assets/scene/libraryfloor2.scene",
            "lib2f_外壳"),
        new ModelTarget(
            "Assets/scene/图书馆四楼.fbx",
            "0fc0c835acb8857428e7673f5c1b0a62",
            "Assets/scene/libraryfloor4.scene",
            "图书馆四楼_外壳"),
    };

    public readonly struct ModelTarget
    {
        public readonly string ModelPath;
        public readonly string ModelGuid;
        public readonly string ScenePath;
        public readonly string ShellRootName;

        public ModelTarget(string modelPath, string modelGuid, string scenePath, string shellRootName)
        {
            ModelPath = modelPath;
            ModelGuid = modelGuid;
            ScenePath = scenePath;
            ShellRootName = shellRootName;
        }
    }

    public static int BindImporterExternalMaterials(string modelAssetPath)
    {
        FixMetaImporterBlock(modelAssetPath);

        var mapping = LibraryMaterialIndex.BuildMapping();
        if (mapping.Count == 0)
            throw new InvalidOperationException("Materials 索引为空。");

        var importer = AssetImporter.GetAtPath(modelAssetPath) as ModelImporter;
        if (importer == null)
            throw new InvalidOperationException("无 ModelImporter：" + modelAssetPath);

        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.External;
        importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
        importer.materialSearch = ModelImporterMaterialSearch.Everywhere;

        int added = TryApplyExternalObjectMappings(importer, modelAssetPath, mapping);

        importer.SaveAndReimport();
        Debug.Log($"[LibraryMaterial] {modelAssetPath} Reimport（External+按名搜索 Materials/），显式映射 {added} 条");
        return added;
    }

    static int TryApplyExternalObjectMappings(
        ModelImporter importer,
        string modelAssetPath,
        Dictionary<string, Material> mapping)
    {
        MethodInfo addMapping = typeof(ModelImporter).GetMethod(
            "AddExternalObjectMapping",
            BindingFlags.Instance | BindingFlags.Public,
            null,
            new[] { typeof(AssetImporter.SourceAssetIdentifier), typeof(UnityEngine.Object) },
            null);

        int added = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(modelAssetPath))
        {
            if (asset is not Material sub || AssetDatabase.IsMainAsset(sub))
                continue;

            if (!seen.Add(sub.name))
                continue;

            if (!LibraryMaterialIndex.TryResolve(sub.name, mapping, out Material external) || external == null)
                continue;

            if (addMapping != null)
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), sub.name);
                addMapping.Invoke(importer, new object[] { id, external });
                added++;
            }
        }

        return added;
    }

    public static int RemapSceneRenderers(ModelTarget target)
    {
        if (!File.Exists(GetAbsolutePath(target.ScenePath)))
            return 0;

        EditorSceneManager.OpenScene(target.ScenePath, OpenSceneMode.Single);
        var mapping = LibraryMaterialIndex.BuildMapping();
        int updated = 0;

        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (!IsModelRoot(root, target))
                continue;

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || IsShell(renderer, target.ShellRootName))
                    continue;
                if (RemapRenderer(renderer, mapping, target.ModelPath))
                    updated++;
            }
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        return updated;
    }

    static bool RemapRenderer(Renderer renderer, Dictionary<string, Material> mapping, string modelPath)
    {
        Material[] slots = renderer.sharedMaterials;
        bool changed = false;

        for (int i = 0; i < slots.Length; i++)
        {
            Material src = slots[i];
            if (src == null)
                continue;

            string path = AssetDatabase.GetAssetPath(src);
            bool embedded = path.Replace('\\', '/').Equals(modelPath, StringComparison.OrdinalIgnoreCase);
            bool noColor = LibraryMaterialIndex.GetMainTexture(src) == null;

            if (!embedded && !noColor)
                continue;

            if (!LibraryMaterialIndex.TryResolve(src.name, mapping, out Material dst) || dst == null)
                continue;

            if (dst == src)
                continue;

            slots[i] = dst;
            changed = true;
        }

        if (changed)
        {
            renderer.sharedMaterials = slots;
            EditorUtility.SetDirty(renderer);
        }

        return changed;
    }

    static void FixMetaImporterBlock(string modelAssetPath)
    {
        string metaPath = GetAbsolutePath(modelAssetPath + ".meta");
        if (!File.Exists(metaPath))
            return;

        string text = File.ReadAllText(metaPath);
        text = Regex.Replace(
            text,
            @"  materials:\n    materialImportMode: \d+\n    materialName: \d+\n    materialSearch: \d+\n    materialLocation: \d+\n",
            "  materials:\n    materialImportMode: 1\n    materialName: 1\n    materialSearch: 2\n    materialLocation: 1\n");
        File.WriteAllText(metaPath, text);
    }

    static bool IsModelRoot(GameObject go, ModelTarget target)
    {
        if (!PrefabUtility.IsPartOfPrefabInstance(go))
            return false;
        if (PrefabUtility.GetNearestPrefabInstanceRoot(go) != go)
            return false;
        string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
        return path == target.ModelPath;
    }

    static bool IsShell(Renderer renderer, string shellRootName)
    {
        Transform t = renderer.transform;
        while (t != null)
        {
            if (!string.IsNullOrEmpty(shellRootName) && t.name == shellRootName)
                return true;
            if (t.name.StartsWith("Shell_"))
                return true;
            t = t.parent;
        }
        return false;
    }

    static string GetAbsolutePath(string assetPath) =>
        Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? string.Empty,
            assetPath.Replace('/', Path.DirectorySeparatorChar));
}
#endif
