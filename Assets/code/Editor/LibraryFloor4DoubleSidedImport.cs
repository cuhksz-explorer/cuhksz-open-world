#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// 图书馆四楼 .blend：导入/菜单一键将所有材质改为双面渲染（含 Legacy/Autodesk 等不支持 _Cull 的 Shader）。
/// </summary>
public static class LibraryFloor4DoubleSidedImport
{
    public const string BlendAssetPath = "Assets/scene/图书馆四楼.blend";
    public const string ModelAssetPath = "Assets/scene/图书馆四楼.fbx";
    const string MaterialsFolder = "Assets/scene/图书馆四楼 Materials";
    const string MenuReimport = "Tools/Mesh/图书馆四楼 - Reimport 并修复双面材质";
    const string MenuRematchReimport = "Tools/Mesh/图书馆四楼 - Reimport 并重新匹配材质";
    const string MenuRematch = "Tools/Mesh/图书馆四楼 - 重新匹配材质";
    const string MenuFix = "Tools/Mesh/图书馆四楼 - 修复单面可见（双面材质）";
    const string MenuFixScene = "Tools/Mesh/图书馆四楼 - 修复当前场景实例";

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

    [MenuItem(MenuRematchReimport, false, 117)]
    public static void ReimportAndRematchMaterialsFromMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("图书馆四楼材质", "请先退出 Play Mode。", "确定");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "图书馆四楼材质",
                "Reimport 会改写模型与材质，可能破坏已布置场景。\n\n强烈建议先执行：\nTools → 场景 → libraryfloor4 - 备份当前场景\n\n是否仍要继续？",
                "继续 Reimport",
                "取消"))
            return;

        int count = LibraryFloor4MaterialRemap.RematchAndReimport();
        EditorUtility.DisplayDialog("图书馆四楼材质", $"Reimport 与材质匹配已完成，更新 {count} 个材质槽。", "确定");
    }

    [MenuItem(MenuRematch, false, 116)]
    public static void RematchMaterialsFromMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("图书馆四楼材质", "请先退出 Play Mode。", "确定");
            return;
        }

        int count = LibraryFloor4MaterialRemap.RematchAll(openSceneIfNeeded: false);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();
        EditorUtility.DisplayDialog("图书馆四楼材质", $"已重新匹配 {count} 个 Renderer 材质槽。", "确定");
    }

    [MenuItem(MenuReimport, false, 119)]
    public static void ReimportAndFixFromMenu()
    {
        ApplyImporterSettings(reimport: true);
        int count = FixAllMaterialsAndRenderers();
        count += FixRenderersInOpenScenes();
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog(
            "图书馆四楼",
            $"Reimport 完成，已修复 {count} 个材质/Renderer 为双面渲染。",
            "确定");
    }

    [MenuItem(MenuFix, false, 120)]
    public static void FixFromMenu()
    {
        int count = FixAllMaterialsAndRenderers();
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog(
            "图书馆四楼",
            count > 0
                ? $"已修复 {count} 个材质为双面渲染。"
                : "未找到可修复材质，请先 Reimport 模型。",
            "确定");
    }

    [MenuItem(MenuFixScene, false, 121)]
    public static void FixCurrentSceneInstances()
    {
        int count = FixRenderersInOpenScenes();
        EditorSceneManager.MarkAllScenesDirty();
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("图书馆四楼", $"已修复当前场景中 {count} 个 Renderer 的材质引用。", "确定");
    }

    /// <summary>BatchMode: -executeMethod LibraryFloor4DoubleSidedImport.FixAndReimport</summary>
    public static void FixAndReimport()
    {
        ApplyImporterSettings(reimport: true);
        FixAllMaterialsAndRenderers();
        FixRenderersInOpenScenes();
        AssetDatabase.SaveAssets();
    }

    public static bool IsLibraryFloor4Path(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return false;
        string normalized = assetPath.Replace('\\', '/');
        return normalized == ModelAssetPath
            || normalized == BlendAssetPath
            || normalized.StartsWith(MaterialsFolder, System.StringComparison.OrdinalIgnoreCase);
    }

    public static void ApplyImporterSettings(bool reimport)
    {
        var importer = AssetImporter.GetAtPath(ModelAssetPath) as ModelImporter;
        if (importer == null)
            return;

        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.External;
        importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
        importer.materialSearch = ModelImporterMaterialSearch.Everywhere;
        importer.addCollider = false;

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

        var prefab = AssetDatabase.LoadMainAssetAtPath(ModelAssetPath) as GameObject;
        if (prefab != null)
            count += FixRenderersOnHierarchy(prefab, fixedMaterials);

        return count;
    }

    static IEnumerable<string> GetMaterialSearchPaths()
    {
        yield return ModelAssetPath;
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
            AssetDatabase.CreateFolder("Assets/scene", "图书馆四楼 Materials");
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
        bool hasSurfaceMaps = GetMainTexture(mat) != null
            || (mat.HasProperty("_BumpMap") && mat.GetTexture("_BumpMap") != null)
            || (mat.HasProperty("_MetallicGlossMap") && mat.GetTexture("_MetallicGlossMap") != null);
        bool needsRemap = !hasSurfaceMaps
            && !mat.HasProperty("_Cull")
            && !mat.HasProperty("_CullMode")
            && !shaderName.Contains("LibraryDoubleSided");

        if (needsRemap && DoubleSidedShader != null)
        {
            Texture mainTex = GetMainTexture(mat);
            Texture bump = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
            Color color = GetMainColor(mat);
            float smooth = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness")
                : mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : 0.5f;
            float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
            float bumpScale = mat.HasProperty("_BumpScale") ? mat.GetFloat("_BumpScale") : 1f;

            mat.shader = DoubleSidedShader;
            if (mainTex != null)
                mat.SetTexture("_MainTex", mainTex);
            if (bump != null && mat.HasProperty("_BumpMap"))
                mat.SetTexture("_BumpMap", bump);
            if (mat.HasProperty("_BumpScale"))
                mat.SetFloat("_BumpScale", bumpScale);
            mat.SetColor("_Color", color);
            mat.SetFloat("_Glossiness", smooth);
            mat.SetFloat("_Metallic", metallic);
            changed = true;
        }
        else if (hasSurfaceMaps && shaderName.Contains("LibraryDoubleSided"))
        {
            changed |= RestoreMapsOnDoubleSided(mat);
        }

        if (mat.HasProperty("_Cull") && mat.GetInt("_Cull") != (int)CullMode.Off)
        {
            mat.SetInt("_Cull", (int)CullMode.Off);
            changed = true;
        }

        return changed;
    }

    static bool RestoreMapsOnDoubleSided(Material mat)
    {
        bool changed = false;
        if (GetMainTexture(mat) == null && mat.HasProperty("_MainTex"))
        {
            var fallback = mat.GetTexture("_BaseMap");
            if (fallback != null)
            {
                mat.SetTexture("_MainTex", fallback);
                changed = true;
            }
        }

        return changed;
    }

    public static Texture GetMainTexture(Material mat)
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
        return path == ModelAssetPath || path.StartsWith(MaterialsFolder, System.StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// 图书馆四楼：按材质名重新匹配完整 PBR 材质（与 LibraryFloor4DoubleSidedImport 同文件，确保菜单可注册）。
/// </summary>
public static class LibraryFloor4MaterialRemap
{
    public const string ModelAssetPath = LibraryFloor4DoubleSidedImport.ModelAssetPath;
    const string ShellRootName = "图书馆四楼_外壳";
    public const string MaterialsFolder = "Assets/scene/图书馆四楼 Materials";
    const string Floor4ScenePath = "Assets/scene/libraryfloor4.scene";

    static readonly string[] SearchFolders =
    {
        MaterialsFolder,
        "Assets/scene/Materials",
        "Assets/scene/图书馆1楼 Materials",
        "Assets/Meshes/Materials",
    };

    public static int RematchAndReimport()
    {
        LibraryFloor4DoubleSidedImport.FixAndReimport();
        int count = RematchAll(openSceneIfNeeded: true);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[图书馆四楼材质] Reimport + 匹配完成，更新 {count} 个材质槽。");
        return count;
    }

    public static void TryUpgradeMaterialOnImport(Material material)
    {
        if (material == null)
            return;

        var index = BuildMaterialIndex();
        if (index.Count == 0)
            return;

        var mapping = BuildNameMapping(index);
        if (!TryResolveMaterial(material.name, mapping, out Material matched))
            return;

        if (ScoreMaterial(matched) <= ScoreMaterial(material))
            return;

        string originalName = material.name;
        material.CopyPropertiesFromMaterial(matched);
        material.shader = matched.shader;
        material.name = originalName;
        LibraryFloor4DoubleSidedImport.MigrateMaterial(material);
    }

    public static int RematchAll(bool openSceneIfNeeded)
    {
        if (openSceneIfNeeded && File.Exists(Floor4ScenePath))
            EditorSceneManager.OpenScene(Floor4ScenePath, OpenSceneMode.Single);

        var index = BuildMaterialIndex();
        if (index.Count == 0)
            throw new System.InvalidOperationException("材质索引为空，请确认 Assets/scene/Materials 存在。");

        var mapping = BuildNameMapping(index);
        int updated = 0;

        GameObject modelRoot = FindModelRoot();
        if (modelRoot != null)
        {
            foreach (var renderer in modelRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || IsShellRenderer(renderer))
                    continue;
                updated += RemapRenderer(renderer, mapping);
            }
        }

        foreach (var mat in LoadBlendMaterials())
            TryCopyToFloor4Folder(mat, mapping);

        return updated;
    }

    static int RemapRenderer(Renderer renderer, Dictionary<string, Material> mapping)
    {
        Material[] mats = renderer.sharedMaterials;
        bool changed = false;

        for (int i = 0; i < mats.Length; i++)
        {
            Material src = mats[i];
            if (src == null)
                continue;

            if (!TryResolveMaterial(src.name, mapping, out Material dst))
                continue;

            if (dst == src)
                continue;

            mats[i] = dst;
            changed = true;
        }

        if (changed)
        {
            renderer.sharedMaterials = mats;
            EditorUtility.SetDirty(renderer);
        }

        return changed ? 1 : 0;
    }

    public static bool TryResolveMaterial(string materialName, Dictionary<string, Material> mapping, out Material result)
    {
        result = null;
        if (string.IsNullOrEmpty(materialName) || mapping == null)
            return false;

        string norm = NormalizeName(materialName);
        if (mapping.TryGetValue(norm, out result))
            return result != null;

        string baseName = StripNumericSuffix(norm);
        if (baseName != norm && mapping.TryGetValue(baseName, out result))
            return result != null;

        foreach (var pair in mapping)
        {
            if (pair.Key.StartsWith(baseName, System.StringComparison.OrdinalIgnoreCase)
                || baseName.StartsWith(pair.Key, System.StringComparison.OrdinalIgnoreCase))
            {
                result = pair.Value;
                return true;
            }
        }

        return false;
    }

    static Dictionary<string, Material> BuildNameMapping(Dictionary<string, List<Material>> index)
    {
        var mapping = new Dictionary<string, Material>();
        foreach (var pair in index)
            mapping[pair.Key] = PickBestMaterial(pair.Value);
        return mapping;
    }

    static Dictionary<string, List<Material>> BuildMaterialIndex()
    {
        var index = new Dictionary<string, List<Material>>();
        foreach (string folder in SearchFolders)
        {
            if (!AssetDatabase.IsValidFolder(folder))
                continue;

            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                    continue;
                AddToIndex(index, mat);
            }
        }

        return index;
    }

    static void AddToIndex(Dictionary<string, List<Material>> index, Material mat)
    {
        string norm = NormalizeName(mat.name);
        if (!index.TryGetValue(norm, out var list))
        {
            list = new List<Material>();
            index[norm] = list;
        }

        if (!list.Contains(mat))
            list.Add(mat);
    }

    static Material PickBestMaterial(List<Material> candidates)
    {
        Material best = candidates[0];
        int bestScore = ScoreMaterial(best);
        for (int i = 1; i < candidates.Count; i++)
        {
            int score = ScoreMaterial(candidates[i]);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidates[i];
            }
        }

        return best;
    }

    static int ScoreMaterial(Material mat)
    {
        if (mat == null)
            return -1;

        int score = 0;
        if (GetMainTexture(mat) != null)
            score += 8;
        if (mat.HasProperty("_BumpMap") && mat.GetTexture("_BumpMap") != null)
            score += 4;
        if (mat.HasProperty("_MetallicGlossMap") && mat.GetTexture("_MetallicGlossMap") != null)
            score += 3;
        if (mat.HasProperty("_OcclusionMap") && mat.GetTexture("_OcclusionMap") != null)
            score += 1;
        if (mat.shader != null && mat.shader.name.Contains("LibraryDoubleSided"))
            score -= 2;
        return score;
    }

    static IEnumerable<Material> LoadBlendMaterials()
    {
        var seen = new HashSet<Material>();
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ModelAssetPath))
        {
            if (asset is Material mat && seen.Add(mat))
                yield return mat;
        }
    }

    static void TryCopyToFloor4Folder(Material src, Dictionary<string, Material> mapping)
    {
        if (!TryResolveMaterial(src.name, mapping, out Material matched))
            return;

        if (AssetDatabase.GetAssetPath(matched).StartsWith(MaterialsFolder, System.StringComparison.OrdinalIgnoreCase))
            return;

        if (!AssetDatabase.IsValidFolder(MaterialsFolder))
        {
            if (AssetDatabase.IsValidFolder("Assets/scene"))
                AssetDatabase.CreateFolder("Assets/scene", "图书馆四楼 Materials");
        }

        string safeName = Regex.Replace(src.name, @"[^\w\u4e00-\u9fff\-\. ]", "_");
        string dstPath = $"{MaterialsFolder}/{safeName}.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(dstPath) != null)
            return;

        var copy = new Material(matched) { name = src.name };
        AssetDatabase.CreateAsset(copy, dstPath);
    }

    static GameObject FindModelRoot()
    {
        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            var scene = SceneManager.GetSceneAt(s);
            if (!scene.isLoaded)
                continue;

            foreach (var root in scene.GetRootGameObjects())
            {
                if (IsModelRoot(root))
                    return root;
            }
        }

        return null;
    }

    static bool IsModelRoot(GameObject go)
    {
        if (go == null)
            return false;

        if (go.name.Contains("图书馆四楼") || go.name.Contains("图书馆4"))
            return true;

        if (PrefabUtility.IsPartOfPrefabInstance(go))
        {
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            if (path == ModelAssetPath && PrefabUtility.GetNearestPrefabInstanceRoot(go) == go)
                return true;
        }

        return false;
    }

    static bool IsShellRenderer(Renderer renderer)
    {
        Transform t = renderer.transform;
        while (t != null)
        {
            if (t.name == ShellRootName || t.name.StartsWith("Shell_"))
                return true;
            t = t.parent;
        }

        return false;
    }

    static string NormalizeName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;
        return name.Trim().ToLowerInvariant();
    }

    static string StripNumericSuffix(string norm)
    {
        return Regex.Replace(norm, @"\.(\d+)$", "");
    }

    static Texture GetMainTexture(Material mat)
    {
        if (mat == null)
            return null;
        if (mat.HasProperty("_MainTex"))
        {
            var tex = mat.GetTexture("_MainTex");
            if (tex != null)
                return tex;
        }
        if (mat.HasProperty("_BaseMap"))
        {
            var tex = mat.GetTexture("_BaseMap");
            if (tex != null)
                return tex;
        }
        return mat.mainTexture;
    }
}

public class LibraryFloor4DoubleSidedModelImport : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        if (!LibraryFloor4DoubleSidedImport.IsLibraryFloor4Path(assetPath))
            return;
        if (assetPath.Replace('\\', '/') != LibraryFloor4DoubleSidedImport.ModelAssetPath)
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
        if (!LibraryFloor4DoubleSidedImport.IsLibraryFloor4Path(assetPath))
            return;

        LibraryFloor4MaterialRemap.TryUpgradeMaterialOnImport(material);
        if (LibraryFloor4DoubleSidedImport.GetMainTexture(material) != null)
            LibraryFloor4DoubleSidedImport.MigrateMaterial(material);
    }

    void OnPostprocessModel(GameObject root)
    {
        if (assetPath.Replace('\\', '/') != LibraryFloor4DoubleSidedImport.ModelAssetPath)
            return;

        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = renderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null)
                    LibraryFloor4DoubleSidedImport.MigrateMaterial(mats[i]);
            }
        }

        LibraryFloor1PhysicsBootstrap.RemoveDuplicateColliders(root.transform);
        LibraryFloor1PhysicsBootstrap.EnsureColliders(root.transform);

        if (root.GetComponent<LibraryFloor1PhysicsBootstrap>() == null)
            root.AddComponent<LibraryFloor1PhysicsBootstrap>();
    }
}
#endif
