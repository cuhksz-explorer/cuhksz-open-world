#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// After restoring historical .meta GUIDs on library floor models, force reimport so
/// embedded materials/meshes are rebuilt under the correct asset GUID.
/// </summary>
public static class LibraryFloorReimportMenu
{
    static readonly string[] ModelPaths =
    {
        "Assets/scene/图书馆1楼.blend",
        "Assets/scene/lib2f.blend",
        "Assets/scene/图书馆四楼.blend",
    };

    [MenuItem("Tools/GUID Repair/Reimport Library Floor Models")]
    public static void ReimportLibraryFloorModels()
    {
        foreach (var path in ModelPaths)
        {
            if (System.IO.File.Exists(path))
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                UnityEngine.Debug.Log($"[GUID Repair] Reimported: {path}");
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[GUID Repair] Missing: {path}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        UnityEngine.Debug.Log("[GUID Repair] Library floor model reimport finished.");
    }
}

#endif
