#if UNITY_EDITOR
using GameQuestSystem;
using UnityEditor;
using UnityEngine;

public static class Content1StoryDatabaseHydrateMenu
{
    const string DatabasePath = "Assets/code/GameQuestSystem/Content1StoryDatabase.asset";

    [MenuItem("Tools/Quest System/刷新剧情数据库（从 docx）", false, 61)]
    public static void RefreshStoryDatabaseFromDocx()
    {
        var database = AssetDatabase.LoadAssetAtPath<StoryDialogueDatabase>(DatabasePath);
        if (database == null)
        {
            EditorUtility.DisplayDialog("刷新剧情数据库", "未找到 Content1StoryDatabase.asset。", "确定");
            return;
        }

        Content1StoryDocxLoader.SyncDatabaseFromContentFolder(database);
        database.generatedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();

        int pageCount = 0;
        if (database.documents != null)
        {
            for (int i = 0; i < database.documents.Count; i++)
            {
                var doc = database.documents[i];
                if (doc?.pages != null)
                    pageCount += doc.pages.Count;
            }
        }

        Debug.Log("[Content1Story] 已从 docx 刷新剧情文档 " + (database.documents?.Count ?? 0) +
                  " 个，共 " + pageCount + " 页。");
        EditorUtility.DisplayDialog(
            "刷新剧情数据库",
            "已从 Assets/content1 导入/刷新剧情。\n文档数：" + (database.documents?.Count ?? 0) +
            "\n总页数：" + pageCount,
            "确定");
    }
}
#endif
