#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 一键修复 Lingdrom / dorm / libraryfloor2 / libraryfloor4 贴图与 GUID（不碰 floor1）。
/// BatchMode: -executeMethod SceneTextureGuidRepairBatch.ExecuteBatch
/// </summary>
public static class SceneTextureGuidRepairBatch
{
    const string MenuPath = "Tools/场景/一键修复 贴图与GUID（Lingdrom+dorm+图书馆2/4楼）";

    [MenuItem(MenuPath, false, 1)]
    public static void RepairFromMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("场景修复", "请先退出 Play Mode。", "确定");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "场景修复",
                "将修复：\n• Lingdrom（道扬）\n• dorm（第八书院宿舍）\n• libraryfloor2\n• libraryfloor4\n\n不修改 libraryfloor1。继续？",
                "继续",
                "取消"))
            return;

        int total = RunAll();
        EditorUtility.DisplayDialog("场景修复", $"完成，共更新 {total} 个 Renderer 材质槽。", "确定");
    }

    public static void ExecuteBatch()
    {
        RunAll();
        AssetDatabase.SaveAssets();
        EditorApplication.Exit(0);
    }

    static int RunAll()
    {
        int total = 0;

        Debug.Log("[SceneRepair] === libraryfloor2 ===");
        LibraryFloor2SceneSetup.ImportNewBlendSceneOnly();
        total += LibraryFloor24MaterialBinder.RemapSceneRenderers(
            new LibraryFloor24MaterialBinder.ModelTarget(
                "Assets/scene/lib2f.fbx",
                "7aa5c8e04a22f6c47918ec4d53021da8",
                "Assets/scene/libraryfloor2.scene",
                "lib2f_外壳"));

        Debug.Log("[SceneRepair] === libraryfloor4 ===");
        total += LibraryFloor4MaterialRemap.RematchAndReimport();
        total += LibraryFloor24MaterialBinder.RemapSceneRenderers(
            new LibraryFloor24MaterialBinder.ModelTarget(
                "Assets/scene/图书馆四楼.fbx",
                "0fc0c835acb8857428e7673f5c1b0a62",
                "Assets/scene/libraryfloor4.scene",
                "图书馆四楼_外壳"));

        Debug.Log("[SceneRepair] === Lingdrom ===");
        total += LingdromDaoyangRepair.RepairAll();

        Debug.Log("[SceneRepair] === dorm ===");
        total += DormSceneMaterialRepair.RepairAll();

        AssetDatabase.SaveAssets();
        Debug.Log($"[SceneRepair] 全部完成，共更新 {total} 个 Renderer。");
        return total;
    }
}
#endif
