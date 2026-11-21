using UnityEngine;

namespace GameQuestSystem
{
    /// <summary>
    /// 全部主线任务完成后，自动播放一次滚动片尾（每位玩家仅一次）。
    /// </summary>
    public class MainStoryCreditsBridge : MonoBehaviour
    {
        const string PrefsPrefix = "GameCredits_MainStoryRoll_v1_";

        static MainStoryCreditsBridge _instance;
        static bool _rollTriggeredThisSession;

        QuestSystemManager _subscribedManager;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() => EnsureExists();

        public static void EnsureExists()
        {
            if (_instance != null)
                return;
            var go = new GameObject("[MainStoryCreditsBridge]");
            _instance = go.AddComponent<MainStoryCreditsBridge>();
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
            TrySubscribe();
            TryShowMainStoryRoll();
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

        void OnQuestChanged() => TryShowMainStoryRoll();

        void TryShowMainStoryRoll()
        {
            if (_rollTriggeredThisSession || HasShownForCurrentUser())
                return;

            var mgr = QuestSystemManager.Instance;
            if (mgr == null || !AreAllMainStoryQuestsCompleted(mgr))
                return;

            _rollTriggeredThisSession = true;
            MarkShownForCurrentUser();
            GameCreditsScrollOverlay.ShowMainStoryRoll();
            Debug.Log("[Credits] 主线已全部完成，播放制作组滚动片尾。");
        }

        public static bool AreAllMainStoryQuestsCompleted(QuestSystemManager mgr)
        {
            if (mgr == null)
                return false;
            if (!mgr.IsQuestCompleted(NewUserTutorialQuestPreset.QuestId))
                return false;

            bool foundMain = false;
            foreach (var def in mgr.QuestChains)
            {
                if (def == null || def.category != QuestCategory.Main)
                    continue;
                foundMain = true;
                if (!mgr.IsQuestCompleted(def.questId))
                    return false;
            }

            return foundMain;
        }

        static string BuildPrefsKey()
        {
            var sync = GameServerSync.Instance;
            string user = sync != null ? sync.SavedUsername : "";
            if (string.IsNullOrWhiteSpace(user))
                user = "local";
            return PrefsPrefix + user.Trim();
        }

        static bool HasShownForCurrentUser()
        {
            return PlayerPrefs.GetInt(BuildPrefsKey(), 0) == 1;
        }

        static void MarkShownForCurrentUser()
        {
            PlayerPrefs.SetInt(BuildPrefsKey(), 1);
            PlayerPrefs.Save();
        }
    }
}
