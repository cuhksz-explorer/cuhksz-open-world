#if UNITY_EDITOR
using GameQuestSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 为剧情 NPC 配置游走：单人居所动，多人场景静止。
/// </summary>
public static class StoryNpcMovementMenu
{
    // 菜单入口见 TeammateResourceMenus（避免与同名 MenuItem 冲突导致整组 Tools 菜单注册失败）
    public static void SetupStoryNpcMovementInOpenScenes()
    {
        int count = 0;
        foreach (var npc in UnityEngine.Object.FindObjectsOfType<StoryQuestNpc>(true))
        {
            if (npc == null || string.IsNullOrWhiteSpace(npc.documentId))
                continue;

            var movement = npc.GetComponent<StoryQuestNpcMovement>();
            if (movement == null)
                movement = Undo.AddComponent<StoryQuestNpcMovement>(npc.gameObject);

            if (npc.GetComponent<NavMeshAgent>() == null)
                Undo.AddComponent<NavMeshAgent>(npc.gameObject);

            if (npc.GetComponent<RandomWalkNPC>() == null)
                Undo.AddComponent<RandomWalkNPC>(npc.gameObject);

            EditorUtility.SetDirty(npc.gameObject);
            count++;
        }

        StoryQuestNpcMovementRegistry.RefreshScenePolicy();
        EditorSceneManager.MarkAllScenesDirty();

        EditorUtility.DisplayDialog(
            "剧情 NPC 游走",
            $"已为 {count} 个剧情 NPC 添加游走组件。\n\n规则：当前场景仅 1 个有效剧情 NPC 时会随机移动；2 个及以上时全部静止。",
            "确定");
    }

    [MenuItem("Tools/Quest System/导入队友资源/导入 NPC 与液态水说明", false, 50)]
    static void ShowImportHelp()
    {
        EditorUtility.DisplayDialog(
            "队友资源",
            "已导入到工程：\n" +
            "· NPC：Assets/NPC（游走逻辑见 Assets/code/NPC/RandomWalkNPC.cs）\n" +
            "· 液态水：Assets/KriptoFX/Water2.0\n\n" +
            "平静踩踏水面：Tools/场景配置/一键布置平静踩踏水面\n" +
            "（或 Tools/Quest System/环境/…）\n" +
            "剧情 NPC：Tools/Quest System/剧情NPC/为场景剧情NPC启用游走规则",
            "确定");
    }

}
#endif
