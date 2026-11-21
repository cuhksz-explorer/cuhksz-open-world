using UnityEngine;

namespace GameQuestSystem
{
    /// <summary>
    /// M0 主线整章完成后，触发支线接取事件（仅一次）。
    /// </summary>
    public class Content1ChapterSideQuestBridge : MonoBehaviour
    {
        const string M0QuestId = "content1_m0";

        static Content1ChapterSideQuestBridge _instance;
        static bool _m0AcceptFired;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() => EnsureExists();

        public static void EnsureExists()
        {
            if (_instance != null)
                return;
            var go = new GameObject("[Content1ChapterSideQuestBridge]");
            _instance = go.AddComponent<Content1ChapterSideQuestBridge>();
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

        QuestSystemManager _subscribedManager;

        void OnEnable()
        {
            TrySubscribe();
            TryFireM0Complete();
        }

        void Update()
        {
            if (_subscribedManager == null)
                TrySubscribe();
        }

        void OnDisable()
        {
            if (_subscribedManager != null)
            {
                _subscribedManager.OnQuestChanged -= OnQuestChanged;
                _subscribedManager = null;
            }
        }

        void TrySubscribe()
        {
            var mgr = QuestSystemManager.Instance;
            if (mgr == null || mgr == _subscribedManager)
                return;
            if (_subscribedManager != null)
                _subscribedManager.OnQuestChanged -= OnQuestChanged;
            _subscribedManager = mgr;
            _subscribedManager.OnQuestChanged += OnQuestChanged;
        }

        void OnQuestChanged() => TryFireM0Complete();

        void TryFireM0Complete()
        {
            if (_m0AcceptFired)
                return;

            var mgr = QuestSystemManager.Instance;
            if (mgr == null || !mgr.IsQuestCompleted(M0QuestId))
                return;

            _m0AcceptFired = true;
            QuestSideQuestAcceptListener.EnsureExists();
            QuestSideQuestAcceptListener.ReportAcceptEvent(LlmWanderingNpcQuestPreset.M0CompleteAcceptEventId);
            Debug.Log("[Quest] M0 已完成，触发支线接取: " + LlmWanderingNpcQuestPreset.M0CompleteAcceptEventId);
        }
    }
}
