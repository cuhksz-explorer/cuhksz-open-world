using UnityEngine;

namespace GameQuestSystem
{
    /// <summary>
    /// GUID 迁移后部分场景的「QuestSystemManager」物体误挂 StoryQuestRuntime，导致任务进度无法检测。
    /// 在场景加载后若仍无 Instance，则修复或创建 QuestSystemManager。
    /// </summary>
    public partial class QuestSystemManager
    {
        const string ManagerObjectName = "QuestSystemManager";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void RepairMiswiredManagerAfterSceneLoad()
        {
            if (Instance != null)
                return;

            if (TryRepairMisnamedQuestManagerObject())
                return;

            var existing = Object.FindObjectOfType<QuestSystemManager>(true);
            if (existing != null)
                return;

            var go = new GameObject(ManagerObjectName);
            go.AddComponent<QuestSystemManager>();
        }

        static bool TryRepairMisnamedQuestManagerObject()
        {
            var storyRuntimes = Object.FindObjectsOfType<StoryQuestRuntime>(true);
            for (int i = 0; i < storyRuntimes.Length; i++)
            {
                var runtime = storyRuntimes[i];
                if (runtime == null)
                    continue;

                var go = runtime.gameObject;
                if (!string.Equals(go.name, ManagerObjectName, System.StringComparison.Ordinal))
                    continue;
                if (go.GetComponent<QuestSystemManager>() != null)
                    continue;

                Object.Destroy(runtime);
                go.AddComponent<QuestSystemManager>();
                return true;
            }

            return Instance != null;
        }
    }
}
