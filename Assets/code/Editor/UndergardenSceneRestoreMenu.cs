#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 菜单入口：从备份恢复 undergarden，并重绑剧情 NPC / 主角 / 巴士等丢失的 GUID。
/// 实际逻辑在 <see cref="restore_undergarden_scene.py"/>，可在项目根执行：
/// py -3 Assets/code/Editor/restore_undergarden_scene.py
/// </summary>
public static class UndergardenSceneRestoreMenu
{
    const string MenuPath = "Tools/场景配置/undergarden 恢复场景并修复 GUID 引用";

    [MenuItem(MenuPath, false, 0)]
    public static void RunFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("undergarden 恢复", "请先退出 Play Mode。", "确定");
            return;
        }

        EditorUtility.DisplayDialog(
            "undergarden 恢复",
            "请在终端执行：\n\npy -3 Assets/code/Editor/restore_undergarden_scene.py\n\n"
            + "完成后回到 Unity，打开 undergarden.unity 并保存。\n"
            + "若仍有 Missing Prefab，再运行：\n"
            + "Tools → 场景配置 → 剧情NPC → 一键导入 NPC 角色模型",
            "确定");
    }
}
#endif
