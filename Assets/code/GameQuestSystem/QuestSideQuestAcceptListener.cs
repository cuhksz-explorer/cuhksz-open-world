using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameQuestSystem
{
    /// <summary>
    /// 监听场景加载等全局事件，评估支线接取条件。
    /// </summary>
    public class QuestSideQuestAcceptListener : MonoBehaviour
    {
        static QuestSideQuestAcceptListener _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            EnsureExists();
        }

        public static void EnsureExists()
        {
            if (_instance != null)
                return;
            var go = new GameObject("[QuestSideQuestAcceptListener]");
            _instance = go.AddComponent<QuestSideQuestAcceptListener>();
            DontDestroyOnLoad(go);
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive)
                return;
            QuestSystemManager.Instance?.EvaluateSideQuestAcceptTriggers(
                QuestAcceptTriggerType.EnterScene,
                scene.name);
        }

        /// <summary>NPC 对话结束时调用。</summary>
        public static void ReportNpcDialogue(string npcId)
        {
            QuestSystemManager.Instance?.EvaluateSideQuestAcceptTriggers(
                QuestAcceptTriggerType.NpcDialogue,
                npcId);
        }

        /// <summary>Canvas 首次打开时由 SideQuestCanvasOpenTrigger 调用。</summary>
        public static void ReportCanvasOpen(string canvasObjectName)
        {
            QuestSystemManager.Instance?.EvaluateSideQuestAcceptTriggers(
                QuestAcceptTriggerType.CanvasOpen,
                canvasObjectName);
        }

        /// <summary>自定义事件接取支线（与 ReportEvent 区分，仅用于接取）。</summary>
        public static void ReportAcceptEvent(string eventId)
        {
            QuestSystemManager.Instance?.EvaluateSideQuestAcceptTriggers(
                QuestAcceptTriggerType.QuestEvent,
                eventId);
        }
    }
}
