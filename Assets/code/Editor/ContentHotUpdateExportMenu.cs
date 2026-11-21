#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using GameQuestSystem;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 将 Content1StoryDatabase 导出为 GameServer 可热更新的 JSON + manifest。
/// </summary>
public static class ContentHotUpdateExportMenu
{
    public const string DatabasePath = "Assets/code/GameQuestSystem/Content1StoryDatabase.asset";

    [MenuItem("Tools/Quest System/导出剧情内容到 GameServer (热更新)")]
    public static void ExportStoryContentToGameServer()
    {
        EditorUtility.DisplayDialog(
            "已改用网页后台发布",
            "客户端整包更新请使用 GameServer 管理后台：\n\n" +
            "1. 启动 GameServer\n" +
            "2. 浏览器打开 http://服务器IP:8765/admin/\n" +
            "3. 上传 Unity Build 输出的 zip 包\n\n" +
            "Unity 内 AssetBundle 热更新菜单已停用。",
            "确定");
    }

    [MenuItem("Tools/Quest System/仅导出剧情 JSON (已停用)")]
    public static void ExportStoryJsonOnly_Disabled()
    {
        ExportStoryContentToGameServer();
    }
}
#endif
