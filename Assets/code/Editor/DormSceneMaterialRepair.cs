#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// dorm.scene：第八书院宿舍 FBX 材质修复，仅修改 dorm.scene。
/// </summary>
public static class DormSceneMaterialRepair
{
    public const string ScenePath = "Assets/scene/dorm.scene";
    public const string ModelAssetPath = "Assets/Meshes/第八书院宿舍.fbx";
    public const string ModelGuid = "905a1df5f37c2d544a127974b46bb788";
    const string MenuPath = "Tools/场景/dorm - 修复第八书院宿舍材质（仅本场景）";

    static readonly string[] LegacyDormGuids =
    {
        "4c1ae1eec102bc54ea9b8dc29f1c9d1c",
        "ad8f009f4b9a87942be2e6a3b0a67ee7",
        "03bcd8a1124fca6449cd1afcc12d2b53",
    };

    [MenuItem(MenuPath, false, 5)]
    public static void RepairFromMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("dorm", "请先退出 Play Mode。", "确定");
            return;
        }

        try
        {
            int count = RepairAll();
            EditorUtility.DisplayDialog("dorm", $"第八书院宿舍材质已修复，更新 {count} 个 Renderer。", "确定");
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("dorm", ex.Message, "确定");
        }
    }

    /// <summary>BatchMode: -executeMethod DormSceneMaterialRepair.RepairAll</summary>
    public static int RepairAll()
    {
        EnsureModelImporter();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var mapping = BuildDormMaterialMapping();
        int updated = 0;

        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (!IsDormBuildingRoot(root))
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
        Debug.Log($"[dorm] 材质修复完成，更新 {updated} 个 Renderer。");
        return updated;
    }

    static void EnsureModelImporter()
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

    static Dictionary<string, Material> BuildDormMaterialMapping()
    {
        var mapping = LibraryMaterialIndex.BuildMapping();
        string[] extra = { "Assets/Meshes/Materials", "Assets/texture/Materials" };
        foreach (string folder in extra)
        {
            if (!AssetDatabase.IsValidFolder(folder))
                continue;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat == null)
                    continue;
                string key = mat.name;
                if (!mapping.ContainsKey(key))
                    mapping[key] = mat;
            }
        }

        return mapping;
    }

    static bool IsDormBuildingRoot(GameObject go)
    {
        if (!PrefabUtility.IsPartOfPrefabInstance(go))
            return false;
        if (PrefabUtility.GetNearestPrefabInstanceRoot(go) != go)
            return false;
        string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
        return path == ModelAssetPath;
    }

    static bool RemapRenderer(Renderer renderer, Dictionary<string, Material> mapping)
    {
        Material[] slots = renderer.sharedMaterials;
        bool changed = false;

        for (int i = 0; i < slots.Length; i++)
        {
            Material src = slots[i];
            if (IsLegacyDormMaterial(src))
            {
                slots[i] = null;
                src = null;
                changed = true;
            }

            if (src == null)
            {
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ModelAssetPath))
                {
                    if (asset is not Material embeddedMat || AssetDatabase.IsMainAsset(embeddedMat))
                        continue;
                    if (!LibraryMaterialIndex.TryResolve(embeddedMat.name, mapping, out Material replacement) || replacement == null)
                        continue;
                    slots[i] = replacement;
                    changed = true;
                    break;
                }
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

    static bool IsLegacyDormMaterial(Material mat)
    {
        if (mat == null)
            return false;
        string path = AssetDatabase.GetAssetPath(mat);
        if (string.IsNullOrEmpty(path))
            return false;
        return path.Replace('\\', '/').Contains("LegacyDorm/", System.StringComparison.OrdinalIgnoreCase);
    }
}
#endif
