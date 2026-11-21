#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 仅 libraryfloor2 / libraryfloor4：FBX 导入后 Reimport + 材质匹配。不触碰 libraryfloor1。
/// </summary>
public static class LibraryFloor24FbxSetup
{
    const string MenuPath = "Tools/图书馆/二楼+四楼 修复材质颜色+Reimport（不动一楼）";
    const string MenuPathLegacy = "Tools/图书馆/二楼+四楼 FBX Reimport并匹配材质（不动一楼）";

    [MenuItem(MenuPath, false, 55)]
    [MenuItem(MenuPathLegacy, false, 56)]
    public static void RunFromMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("图书馆 2/4", "请先退出 Play Mode。", "确定");
            return;
        }

        EnsureMeta("Assets/scene/lib2f.fbx", "7aa5c8e04a22f6c47918ec4d53021da8");
        EnsureMeta("Assets/scene/图书馆四楼.fbx", "0fc0c835acb8857428e7673f5c1b0a62");

        int maps2 = LibraryFloor24MaterialBinder.BindImporterExternalMaterials("Assets/scene/lib2f.fbx");
        int maps4 = LibraryFloor24MaterialBinder.BindImporterExternalMaterials("Assets/scene/图书馆四楼.fbx");

        int n2 = LibraryFloor2MaterialRemap.RematchAll(openSceneIfNeeded: true);
        int n4 = LibraryFloor4MaterialRemap.RematchAll(openSceneIfNeeded: false);

        var t2 = new LibraryFloor24MaterialBinder.ModelTarget(
            "Assets/scene/lib2f.fbx", "7aa5c8e04a22f6c47918ec4d53021da8",
            "Assets/scene/libraryfloor2.scene", "lib2f_外壳");
        var t4 = new LibraryFloor24MaterialBinder.ModelTarget(
            "Assets/scene/图书馆四楼.fbx", "0fc0c835acb8857428e7673f5c1b0a62",
            "Assets/scene/libraryfloor4.scene", "图书馆四楼_外壳");
        int r2 = LibraryFloor24MaterialBinder.RemapSceneRenderers(t2);
        int r4 = LibraryFloor24MaterialBinder.RemapSceneRenderers(t4);

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog(
            "图书馆 2/4",
            $"完成。\n外部映射 lib2f={maps2} 四楼={maps4}\n材质槽 lib2f={n2} 四楼={n4}\nRenderer lib2f={r2} 四楼={r4}\n\nlibraryfloor1 未修改。",
            "确定");
    }

    static void EnsureMeta(string assetPath, string guid)
    {
        string metaPath = assetPath + ".meta";
        if (!System.IO.File.Exists(metaPath))
            return;

        string text = System.IO.File.ReadAllText(metaPath);
        if (!System.Text.RegularExpressions.Regex.IsMatch(
                text,
                @"^guid: " + guid + @"$",
                System.Text.RegularExpressions.RegexOptions.Multiline))
        {
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"^guid: .+$",
                "guid: " + guid,
                System.Text.RegularExpressions.RegexOptions.Multiline);
            System.IO.File.WriteAllText(metaPath, text);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }
    }
}
#endif
