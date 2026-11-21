#if UNITY_EDITOR
using GameQuestSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 队友资源（液态水 / 剧情 NPC 游走）菜单入口。单独文件，避免其它 Editor 编译失败导致菜单不显示。
/// </summary>
public static class TeammateResourceMenus
{
    const string WaterPrefabRoot = "Assets/KriptoFX/Water2.0/Prefabs/Water/PC/Unity5";

    // ---------- 剧情 NPC 模型 / 游走 ----------

    [MenuItem("Tools/Quest System/设置所有剧情NPC交互距离为6", false, 13)]
    [MenuItem("Tools/场景配置/设置所有剧情NPC交互距离为6", false, 13)]
    public static void SetAllStoryNpcInteractDistanceToSix()
    {
        const float distance = 6f;
        int prefabs = 0;
        int scenes = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/NPCs/Story" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            bool dirty = false;
            foreach (var npc in root.GetComponentsInChildren<StoryQuestNpc>(true))
            {
                if (npc == null || Mathf.Approximately(npc.interactDistance, distance))
                    continue;
                npc.interactDistance = distance;
                dirty = true;
            }

            if (dirty)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                prefabs++;
            }

            PrefabUtility.UnloadPrefabContents(root);
        }

        foreach (var npc in UnityEngine.Object.FindObjectsOfType<StoryQuestNpc>(true))
        {
            if (npc == null || Mathf.Approximately(npc.interactDistance, distance))
                continue;
            npc.interactDistance = distance;
            EditorUtility.SetDirty(npc);
            scenes++;
        }

        foreach (var npc in UnityEngine.Object.FindObjectsOfType<NpcDialogueInteractor>(true))
        {
            if (npc == null || Mathf.Approximately(npc.interactDistance, distance))
                continue;
            npc.interactDistance = distance;
            EditorUtility.SetDirty(npc);
            scenes++;
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkAllScenesDirty();
        EditorUtility.DisplayDialog(
            "剧情 NPC 交互距离",
            $"已将交互距离设为 {distance} 米。\n更新预制体 {prefabs} 个，场景实例 {scenes} 个。",
            "确定");
    }

    // 一键导入菜单见 StoryNpcSketchfabImportMenu（Tools/Quest System/剧情NPC/…）

    [MenuItem("Tools/场景配置/剧情NPC启用游走规则", false, 16)]
    [MenuItem("Tools/Quest System/剧情NPC/为场景剧情NPC启用游走规则", false, 16)]
    [MenuItem("Tools/剧情NPC/为场景剧情NPC启用游走规则", false, 16)]
    public static void SetupStoryNpcMovement()
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
            "剧情 NPC",
            $"已为 {count} 个剧情 NPC 配置游走。\n单人居中会移动，多人同场景时全部静止。",
            "确定");
    }

    [MenuItem("Tools/Quest System/导入队友资源/说明", false, 14)]
    public static void ShowTeammateResourceHelp()
    {
        EditorUtility.DisplayDialog(
            "队友资源",
            "一键导入 NPC 模型：Tools/Quest System/剧情NPC/一键导入 NPC 角色模型\n\n" +
            "平静踩踏水面：选中静态水面或 LiquidWater → Tools/场景配置/一键布置平静踩踏水面\n\n" +
            "剧情 NPC 游走：Tools/场景配置/剧情NPC启用游走规则\n\n" +
            "资源路径：\n· Assets/KriptoFX/Water2.0\n· Assets/NPC\n· Assets/code/NPC/RandomWalkNPC.cs",
            "确定");
    }
}
#endif
