#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lingdrom：道扬.fbx 导入、材质匹配，仅修改 Lingdrom.unity。
/// </summary>
public static class LingdromDaoyangRepair
{
    public const string ScenePath = "Assets/scene/Lingdrom.unity";
    public const string ModelAssetPath = "Assets/scene/道扬.fbx";
    public const string ModelGuid = "45a559bbd50c46a0a8badcc38a2a9ab4";
    const string MenuPath = "Tools/场景/Lingdrom - 修复道扬模型与材质（仅本场景）";

    [MenuItem(MenuPath, false, 4)]
    public static void RepairFromMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Lingdrom", "请先退出 Play Mode。", "确定");
            return;
        }

        try
        {
            int count = RepairAll();
            EditorUtility.DisplayDialog("Lingdrom", $"道扬模型与材质已修复，更新 {count} 个 Renderer。", "确定");
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Lingdrom", ex.Message, "确定");
        }
    }

    /// <summary>BatchMode: -executeMethod LingdromDaoyangRepair.RepairAll</summary>
    public static int RepairAll()
    {
        EnsureModelMetaGuid();
        BindImporterAndReimport();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var mapping = LibraryMaterialIndex.BuildMapping();
        if (mapping.Count == 0)
            throw new System.InvalidOperationException("材质索引为空。");

        int updated = 0;
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (!IsDaoyangRoot(root))
                continue;

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                    continue;
                if (RemapRenderer(renderer, mapping))
                    updated++;
            }
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log($"[Lingdrom] 道扬修复完成，更新 {updated} 个 Renderer。");
        return updated;
    }

    static void EnsureModelMetaGuid()
    {
        string metaPath = ModelAssetPath + ".meta";
        if (!File.Exists(GetAbsolutePath(metaPath)))
            throw new FileNotFoundException("未找到：" + ModelAssetPath);

        string text = File.ReadAllText(metaPath);
        if (!Regex.IsMatch(text, @"^guid: " + ModelGuid + @"$", RegexOptions.Multiline))
        {
            text = Regex.Replace(text, @"^guid: .+$", "guid: " + ModelGuid, RegexOptions.Multiline);
            File.WriteAllText(metaPath, text);
        }
    }

    static void BindImporterAndReimport()
    {
        var importer = AssetImporter.GetAtPath(ModelAssetPath) as ModelImporter;
        if (importer == null)
            throw new System.InvalidOperationException("无 ModelImporter：" + ModelAssetPath);

        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.External;
        importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
        importer.materialSearch = ModelImporterMaterialSearch.Everywhere;
        importer.SaveAndReimport();
    }

    static bool IsDaoyangRoot(GameObject go)
    {
        if (!PrefabUtility.IsPartOfPrefabInstance(go))
            return go.name.Contains("道扬");
        if (PrefabUtility.GetNearestPrefabInstanceRoot(go) != go)
            return false;
        string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
        return path == ModelAssetPath;
    }

    static bool RemapRenderer(Renderer renderer, System.Collections.Generic.Dictionary<string, Material> mapping)
    {
        Material[] slots = renderer.sharedMaterials;
        bool changed = false;

        for (int i = 0; i < slots.Length; i++)
        {
            Material src = slots[i];
            if (src == null)
            {
                if (TryFillSlot(renderer, i, mapping, slots))
                    changed = true;
                continue;
            }

            string path = AssetDatabase.GetAssetPath(src);
            bool embedded = path.Replace('\\', '/').Equals(ModelAssetPath, System.StringComparison.OrdinalIgnoreCase);
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

    static bool TryFillSlot(
        Renderer renderer,
        int index,
        System.Collections.Generic.Dictionary<string, Material> mapping,
        Material[] slots)
    {
        // Fallback: use first textured material if slot is empty (pink fix).
        foreach (var pair in mapping)
        {
            if (pair.Value == null || LibraryMaterialIndex.GetMainTexture(pair.Value) == null)
                continue;
            slots[index] = pair.Value;
            return true;
        }

        return false;
    }

    static string GetAbsolutePath(string assetPath) =>
        Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? string.Empty,
            assetPath.Replace('/', Path.DirectorySeparatorChar));
}
#endif
