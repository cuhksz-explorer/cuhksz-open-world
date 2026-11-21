#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// 图书馆1楼 FBX：导入/菜单一键将所有材质改为双面渲染（含 Legacy/Autodesk 等不支持 _Cull 的 Shader）。
/// </summary>
public static class LibraryFloor1DoubleSidedImport
{
    public const string BlendAssetPath = "Assets/scene/图书馆1楼.blend";
    public const string FbxAssetPath = BlendAssetPath;
    const string MaterialsFolder = "Assets/scene/图书馆1楼 Materials";
    const string MenuReimport = "Tools/Mesh/图书馆1楼 - Reimport 并修复双面材质";
    const string MenuFix = "Tools/Mesh/图书馆1楼 - 修复单面可见（双面材质）";
    const string MenuFixScene = "Tools/Mesh/图书馆1楼 - 修复当前场景实例";

    static Shader _doubleSidedShader;

    static Shader DoubleSidedShader
    {
        get
        {
            if (_doubleSidedShader == null)
                _doubleSidedShader = Shader.Find("Custom/LibraryDoubleSided");
            return _doubleSidedShader;
        }
    }

    [MenuItem(MenuReimport, false, 109)]
    public static void ReimportAndFixFromMenu()
    {
        ApplyImporterSettings(reimport: true);
        int count = FixAllMaterialsAndRenderers();
        count += FixRenderersInOpenScenes();
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog(
            "图书馆1楼",
            $"Reimport 完成，已修复 {count} 个材质/Renderer 为双面渲染。",
            "确定");
    }

    [MenuItem(MenuFix, false, 110)]
    public static void FixFromMenu()
    {
        int count = FixAllMaterialsAndRenderers();
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog(
            "图书馆1楼",
            count > 0
                ? $"已修复 {count} 个材质为双面渲染。"
                : "未找到可修复材质，请先 Reimport FBX。",
            "确定");
    }

    [MenuItem(MenuFixScene, false, 111)]
    public static void FixCurrentSceneInstances()
    {
        int count = FixRenderersInOpenScenes();
        EditorSceneManager.MarkAllScenesDirty();
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("图书馆1楼", $"已修复当前场景中 {count} 个 Renderer 的材质引用。", "确定");
    }

    public static void FixAndReimport()
    {
        ApplyImporterSettings(reimport: true);
        FixAllMaterialsAndRenderers();
        FixRenderersInOpenScenes();
        AssetDatabase.SaveAssets();
    }

    public static bool IsLibraryFloor1Path(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return false;
        string normalized = assetPath.Replace('\\', '/');
        return normalized == BlendAssetPath
            || normalized.StartsWith(MaterialsFolder, System.StringComparison.OrdinalIgnoreCase);
    }

    public static void ApplyImporterAndReimport() => ApplyImporterSettings(reimport: true);

    public static void ApplyImporterSettings(bool reimport)
    {
        var importer = AssetImporter.GetAtPath(BlendAssetPath) as ModelImporter;
        if (importer == null)
            return;

        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.External;
        importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
        importer.materialSearch = ModelImporterMaterialSearch.Everywhere;

        if (reimport)
            importer.SaveAndReimport();
    }

    public static int FixAllMaterialsAndRenderers()
    {
        EnsureMaterialsFolder();
        var fixedMaterials = new HashSet<Material>();
        int count = 0;

        foreach (string path in GetMaterialSearchPaths())
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is not Material mat || fixedMaterials.Contains(mat))
                    continue;
                if (MigrateMaterial(mat))
                {
                    EditorUtility.SetDirty(mat);
                    fixedMaterials.Add(mat);
                    count++;
                }
            }
        }

        var prefab = AssetDatabase.LoadMainAssetAtPath(BlendAssetPath) as GameObject;
        if (prefab != null)
            count += FixRenderersOnHierarchy(prefab, fixedMaterials);

        return count;
    }

    static IEnumerable<string> GetMaterialSearchPaths()
    {
        yield return BlendAssetPath;
        if (AssetDatabase.IsValidFolder(MaterialsFolder))
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialsFolder }))
                yield return AssetDatabase.GUIDToAssetPath(guid);
        }
    }

    static void EnsureMaterialsFolder()
    {
        if (!AssetDatabase.IsValidFolder(MaterialsFolder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/scene"))
                return;
            AssetDatabase.CreateFolder("Assets/scene", "图书馆1楼 Materials");
        }
    }

    public static bool MigrateMaterial(Material mat)
    {
        if (mat == null)
            return false;

        bool changed = false;
        mat.doubleSidedGI = true;

        if (mat.HasProperty("_Cull") && mat.GetInt("_Cull") != (int)CullMode.Off)
        {
            mat.SetInt("_Cull", (int)CullMode.Off);
            changed = true;
        }

        if (mat.HasProperty("_CullMode") && mat.GetInt("_CullMode") != (int)CullMode.Off)
        {
            mat.SetInt("_CullMode", (int)CullMode.Off);
            changed = true;
        }

        if (mat.HasProperty("_DoubleSidedEnable") && mat.GetFloat("_DoubleSidedEnable") < 0.5f)
        {
            mat.SetFloat("_DoubleSidedEnable", 1f);
            changed = true;
        }

        if (mat.HasProperty("_DoubleSided") && mat.GetFloat("_DoubleSided") < 0.5f)
        {
            mat.SetFloat("_DoubleSided", 1f);
            changed = true;
        }

        string shaderName = mat.shader != null ? mat.shader.name : string.Empty;
        bool needsRemap = !mat.HasProperty("_Cull")
            && !mat.HasProperty("_CullMode")
            && !shaderName.Contains("LibraryDoubleSided");

        if (needsRemap && DoubleSidedShader != null)
        {
            Texture mainTex = GetMainTexture(mat);
            Color color = GetMainColor(mat);
            float smooth = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness")
                : mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : 0.5f;
            float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;

            mat.shader = DoubleSidedShader;
            if (mainTex != null)
                mat.SetTexture("_MainTex", mainTex);
            mat.SetColor("_Color", color);
            mat.SetFloat("_Glossiness", smooth);
            mat.SetFloat("_Metallic", metallic);
            changed = true;
        }

        if (mat.HasProperty("_Cull") && mat.GetInt("_Cull") != (int)CullMode.Off)
        {
            mat.SetInt("_Cull", (int)CullMode.Off);
            changed = true;
        }

        return changed;
    }

    static Texture GetMainTexture(Material mat)
    {
        if (mat.HasProperty("_MainTex"))
        {
            var tex = mat.GetTexture("_MainTex");
            if (tex != null) return tex;
        }
        if (mat.HasProperty("_BaseMap"))
        {
            var tex = mat.GetTexture("_BaseMap");
            if (tex != null) return tex;
        }
        return mat.mainTexture;
    }

    static Color GetMainColor(Material mat)
    {
        if (mat.HasProperty("_Color"))
            return mat.GetColor("_Color");
        if (mat.HasProperty("_BaseColor"))
            return mat.GetColor("_BaseColor");
        return Color.white;
    }

    static int FixRenderersInOpenScenes()
    {
        var fixedMaterials = new HashSet<Material>();
        int count = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;
            foreach (var root in scene.GetRootGameObjects())
                count += FixRenderersOnHierarchy(root, fixedMaterials);
        }
        return count;
    }

    static int FixRenderersOnHierarchy(GameObject root, HashSet<Material> fixedMaterials)
    {
        int count = 0;
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer == null)
                continue;

            Material[] mats = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                Material mat = mats[i];
                if (mat == null || !IsLibraryMaterial(mat))
                    continue;

                if (!fixedMaterials.Contains(mat))
                {
                    if (MigrateMaterial(mat))
                    {
                        EditorUtility.SetDirty(mat);
                        fixedMaterials.Add(mat);
                        count++;
                    }
                }

                changed = true;
            }

            if (changed)
                EditorUtility.SetDirty(renderer);
        }
        return count;
    }

    static bool IsLibraryMaterial(Material mat)
    {
        string path = AssetDatabase.GetAssetPath(mat);
        if (string.IsNullOrEmpty(path))
            return false;
        path = path.Replace('\\', '/');
        return path == BlendAssetPath || path.StartsWith(MaterialsFolder, System.StringComparison.OrdinalIgnoreCase);
    }
}

public class LibraryFloor1DoubleSidedModelImport : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        if (!LibraryFloor1DoubleSidedImport.IsLibraryFloor1Path(assetPath))
            return;
        if (assetPath.Replace('\\', '/') != LibraryFloor1DoubleSidedImport.BlendAssetPath)
            return;

        var importer = (ModelImporter)assetImporter;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.External;
        importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
        importer.materialSearch = ModelImporterMaterialSearch.Everywhere;
        importer.addCollider = false;
    }

    void OnPostprocessMaterial(Material material)
    {
        if (!LibraryFloor1DoubleSidedImport.IsLibraryFloor1Path(assetPath))
            return;
        LibraryFloor1DoubleSidedImport.MigrateMaterial(material);
    }

    void OnPostprocessModel(GameObject root)
    {
        if (assetPath.Replace('\\', '/') != LibraryFloor1DoubleSidedImport.BlendAssetPath)
            return;

        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = renderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null)
                    LibraryFloor1DoubleSidedImport.MigrateMaterial(mats[i]);
            }
        }

        LibraryFloor1PhysicsBootstrap.RemoveDuplicateColliders(root.transform);
        LibraryFloor1PhysicsBootstrap.EnsureColliders(root.transform);

        if (root.GetComponent<LibraryFloor1PhysicsBootstrap>() == null)
            root.AddComponent<LibraryFloor1PhysicsBootstrap>();
    }
}
#endif
