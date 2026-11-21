#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>按名称索引 Assets/scene/Materials 中带贴图的材质（图书馆 2/4 楼共用）。</summary>
public static class LibraryMaterialIndex
{
    public const string SharedMaterialsFolder = "Assets/scene/Materials";

    static readonly string[] ExtraFolders =
    {
        "Assets/scene/lib2f Materials",
        "Assets/scene/图书馆四楼 Materials",
        "Assets/scene/图书馆1楼 Materials",
        "Assets/Meshes/Materials",
    };

    public static Dictionary<string, Material> BuildMapping()
    {
        var index = new Dictionary<string, List<Material>>();
        foreach (string folder in GetSearchFolders())
        {
            if (!AssetDatabase.IsValidFolder(folder))
                continue;

            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat == null)
                    continue;
                Add(index, mat);
            }
        }

        var mapping = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in index)
            mapping[pair.Key] = PickBest(pair.Value);
        return mapping;
    }

    public static IEnumerable<string> GetSearchFolders()
    {
        yield return SharedMaterialsFolder;
        foreach (string f in ExtraFolders)
            yield return f;
    }

    public static bool TryResolve(string materialName, Dictionary<string, Material> mapping, out Material result)
    {
        result = null;
        if (string.IsNullOrEmpty(materialName) || mapping == null)
            return false;

        string norm = Normalize(materialName);
        if (mapping.TryGetValue(norm, out result) && result != null)
            return true;

        string baseName = StripNumericSuffix(norm);
        if (baseName != norm && mapping.TryGetValue(baseName, out result) && result != null)
            return true;

        foreach (var pair in mapping)
        {
            if (pair.Key.StartsWith(baseName, StringComparison.OrdinalIgnoreCase)
                || baseName.StartsWith(pair.Key, StringComparison.OrdinalIgnoreCase))
            {
                result = pair.Value;
                return true;
            }
        }

        return false;
    }

    public static int Score(Material mat)
    {
        if (mat == null)
            return -1;
        int score = 0;
        if (GetMainTexture(mat) != null)
            score += 10;
        if (mat.HasProperty("_BumpMap") && mat.GetTexture("_BumpMap") != null)
            score += 4;
        if (mat.HasProperty("_MetallicGlossMap") && mat.GetTexture("_MetallicGlossMap") != null)
            score += 3;
        if (mat.shader != null && mat.shader.name.Contains("LibraryDoubleSided"))
            score -= 3;
        return score;
    }

    public static Texture GetMainTexture(Material mat)
    {
        if (mat == null)
            return null;
        if (mat.HasProperty("_MainTex"))
        {
            var t = mat.GetTexture("_MainTex");
            if (t != null)
                return t;
        }
        if (mat.HasProperty("_BaseMap"))
        {
            var t = mat.GetTexture("_BaseMap");
            if (t != null)
                return t;
        }
        return mat.mainTexture;
    }

    static void Add(Dictionary<string, List<Material>> index, Material mat)
    {
        string key = Normalize(mat.name);
        if (!index.TryGetValue(key, out var list))
        {
            list = new List<Material>();
            index[key] = list;
        }
        if (!list.Contains(mat))
            list.Add(mat);
    }

    static Material PickBest(List<Material> list)
    {
        Material best = list[0];
        int bestScore = Score(best);
        for (int i = 1; i < list.Count; i++)
        {
            int s = Score(list[i]);
            if (s > bestScore)
            {
                bestScore = s;
                best = list[i];
            }
        }
        return best;
    }

    public static string Normalize(string name) =>
        Regex.Replace(name ?? string.Empty, @"\s+", " ").Trim().ToLowerInvariant();

    public static string StripNumericSuffix(string norm) =>
        Regex.Replace(norm, @"\.(\d+)$", "");
}
#endif
