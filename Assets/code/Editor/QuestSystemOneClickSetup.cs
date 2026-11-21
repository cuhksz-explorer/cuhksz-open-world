using GameQuestSystem;
using UnityEditor;
using UnityEngine;

public static class QuestSystemOneClickSetup
{
    [MenuItem("Tools/Quest System/One Click Setup")]
    public static void CreateQuestSystem()
    {
        var manager = Object.FindObjectOfType<QuestSystemManager>();
        if (manager == null)
        {
            var managerGo = new GameObject("QuestSystemManager");
            manager = managerGo.AddComponent<QuestSystemManager>();
        }

        var ui = Object.FindObjectOfType<QuestSystemUI>();
        if (ui == null)
        {
            var uiGo = new GameObject("QuestSystemUI");
            ui = uiGo.AddComponent<QuestSystemUI>();
        }

        var builder = ui.GetComponent<QuestSystemAutoUIBuilder>();
        if (builder == null)
            builder = ui.gameObject.AddComponent<QuestSystemAutoUIBuilder>();

        builder.OneClickBuildQuestUI();
        Selection.activeGameObject = manager.gameObject;
        Debug.Log("[QuestSystem] One Click Setup 完成。");
    }
}
