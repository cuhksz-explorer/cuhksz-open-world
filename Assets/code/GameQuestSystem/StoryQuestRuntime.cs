using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameQuestSystem
{
    [DefaultExecutionOrder(570)]
    public class StoryQuestRuntime : MonoBehaviour
    {
        public static StoryQuestRuntime Instance { get; private set; }

        [SerializeField] private StoryDialogueDatabase database;
        [SerializeField] private StoryDialogueUI dialogueUI;
        [SerializeField] private bool dontDestroyOnLoad = true;

        public StoryDialogueDatabase Database => database;

        StoryQuestNpc[] _cachedNpcs;
        int _cachedNpcSceneHandle = -1;
        bool _refreshScheduled;
        Transform _cachedPlayerTransform;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (dontDestroyOnLoad)
            {
                if (transform.parent != null)
                    transform.SetParent(null, true);
                DontDestroyOnLoad(gameObject);
            }

            if (dialogueUI == null)
                dialogueUI = FindObjectOfType<StoryDialogueUI>(true);
            if (dialogueUI == null)
                dialogueUI = gameObject.AddComponent<StoryDialogueUI>();

            SceneManager.sceneLoaded += OnSceneLoaded;
            StartCoroutine(CoSyncAfterSceneReady());
        }

        void Start()
        {
            if (database != null)
                Content1StoryDocxLoader.HydrateDatabaseIfNeeded(database);

            var manager = QuestSystemManager.Instance;
            if (manager != null)
            {
                manager.OnQuestChanged += OnTrackedQuestOrProgressChanged;
                manager.OnTrackedQuestChanged += OnTrackedQuestIdChanged;
                manager.OnPlayerStateLoaded += OnTrackedQuestOrProgressChanged;
            }

            SyncActiveStoryNpcForCurrentScene();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            var manager = QuestSystemManager.Instance;
            if (manager != null)
            {
                manager.OnQuestChanged -= OnTrackedQuestOrProgressChanged;
                manager.OnTrackedQuestChanged -= OnTrackedQuestIdChanged;
                manager.OnPlayerStateLoaded -= OnTrackedQuestOrProgressChanged;
            }
        }

        void OnTrackedQuestOrProgressChanged() => SyncActiveStoryNpcForCurrentScene();

        void OnTrackedQuestIdChanged(string _) => SyncActiveStoryNpcForCurrentScene();

        public void SetDatabase(StoryDialogueDatabase storyDatabase)
        {
            database = storyDatabase;
            SyncActiveStoryNpcForCurrentScene();
        }

        public bool CanStartDocument(string documentId)
        {
            if (string.IsNullOrWhiteSpace(documentId))
                return false;

            var manager = QuestSystemManager.Instance;
            if (manager == null)
                return GetDocument(documentId) != null;

            var active = manager.GetTrackedSubTask();
            if (active == null || active.triggerType != QuestTriggerType.ManualEvent)
                return false;

            var doc = GetDocument(documentId);
            if (doc != null)
            {
                return string.Equals(active.triggerEventId, doc.completeEventId, System.StringComparison.OrdinalIgnoreCase);
            }

            return MatchesTrackedStorySubTask(active, documentId);
        }

        public bool TryStartDocument(string documentId, StoryQuestNpc sourceNpc = null)
        {
            var doc = ResolveDocument(documentId);
            if (doc == null || !CanStartDocument(documentId))
                return false;

            if (dialogueUI == null)
                dialogueUI = FindObjectOfType<StoryDialogueUI>(true);
            if (dialogueUI == null)
                dialogueUI = gameObject.AddComponent<StoryDialogueUI>();

            if (GetComponent<NpcDialogueLookAtDriver>() == null)
                gameObject.AddComponent<NpcDialogueLookAtDriver>();

            dialogueUI.Open(database, doc.documentId, doc.documentId, this, sourceNpc);
            return true;
        }

        public StoryDialogueDocument GetDocument(string documentId)
        {
            return database != null ? database.FindDocument(documentId) : null;
        }

        StoryDialogueDocument ResolveDocument(string documentId)
        {
            var doc = GetDocument(documentId);
            if (doc != null)
                return doc;

            var active = QuestSystemManager.Instance?.GetTrackedSubTask();
            if (active == null || !MatchesTrackedStorySubTask(active, documentId))
                return null;

            return CreateStubDocument(documentId, active);
        }

        public void CompleteRootDocument(string rootDocumentId, StoryQuestNpc sourceNpc = null)
        {
            var doc = ResolveDocument(rootDocumentId);
            if (doc == null)
                return;

            var manager = QuestSystemManager.Instance;
            if (!string.IsNullOrWhiteSpace(doc.completeEventId))
            {
                manager?.ReportEvent(doc.completeEventId);
                manager?.TryCompleteTrackedStoryDialogue(doc.completeEventId);
            }

            if (!string.IsNullOrWhiteSpace(rootDocumentId))
                QuestSideQuestAcceptListener.ReportNpcDialogue(rootDocumentId);
            if (sourceNpc != null && !string.IsNullOrWhiteSpace(sourceNpc.NpcIdForQuestTriggers))
                QuestSideQuestAcceptListener.ReportNpcDialogue(sourceNpc.NpcIdForQuestTriggers);

            RefreshStoryNpcVisibility();
        }

        void EnsureDialogueUi()
        {
            if (dialogueUI == null)
                dialogueUI = FindObjectOfType<StoryDialogueUI>(true);
            if (dialogueUI == null)
                dialogueUI = gameObject.AddComponent<StoryDialogueUI>();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            InvalidateNpcCache();
            _cachedPlayerTransform = null;
            StartCoroutine(CoSyncAfterSceneReady());
        }

        void InvalidateNpcCache()
        {
            _cachedNpcs = null;
            _cachedNpcSceneHandle = -1;
        }

        StoryQuestNpc[] GetCachedStoryNpcs()
        {
            int handle = SceneManager.GetActiveScene().handle;
            if (_cachedNpcs == null || _cachedNpcSceneHandle != handle)
            {
                _cachedNpcs = FindObjectsOfType<StoryQuestNpc>(true);
                _cachedNpcSceneHandle = handle;
            }

            return _cachedNpcs;
        }

        /// <summary>供任务引导 UI 查询当前场景内剧情 NPC 位置。</summary>
        public StoryQuestNpc[] GetCachedStoryNpcsForGuide()
        {
            return GetCachedStoryNpcs();
        }

        public Transform GetPlayerTransform()
        {
            if (_cachedPlayerTransform != null)
                return _cachedPlayerTransform;

            var pm = FindObjectOfType<PlayerMovement>(true);
            if (pm != null)
                _cachedPlayerTransform = pm.transform;
            return _cachedPlayerTransform;
        }

        System.Collections.IEnumerator CoSyncAfterSceneReady()
        {
            yield return null;
            SyncActiveStoryNpcForCurrentScene();
        }

        /// <summary>当前追踪任务对应的剧情文档 ID；非剧情子任务时返回 null。</summary>
        public string GetTrackedStoryDocumentId()
        {
            var manager = QuestSystemManager.Instance;
            if (manager == null || database == null || database.documents == null)
                return null;

            var active = manager.GetTrackedSubTask();
            if (active == null || active.triggerType != QuestTriggerType.ManualEvent)
                return null;
            if (string.IsNullOrWhiteSpace(active.triggerEventId))
                return null;

            string docId = FindDocumentIdByCompleteEvent(active.triggerEventId);
            if (!string.IsNullOrEmpty(docId))
                return docId;

            if (!string.IsNullOrWhiteSpace(active.subTaskName))
                return active.subTaskName.Trim();

            return null;
        }

        string FindDocumentIdByCompleteEvent(string completeEventId)
        {
            if (string.IsNullOrWhiteSpace(completeEventId) || database?.documents == null)
                return null;

            for (int i = 0; i < database.documents.Count; i++)
            {
                var candidate = database.documents[i];
                if (candidate != null &&
                    string.Equals(candidate.completeEventId, completeEventId, System.StringComparison.OrdinalIgnoreCase))
                    return candidate.documentId;
            }

            return null;
        }

        /// <summary>仅显示当前追踪任务可对话的 NPC；对话进行中保留发言 NPC 与当前文档 NPC。</summary>
        public void RefreshStoryNpcVisibility()
        {
            if (_refreshScheduled)
                return;
            _refreshScheduled = true;
            StartCoroutine(CoRefreshStoryNpcVisibilityDeferred());
        }

        System.Collections.IEnumerator CoRefreshStoryNpcVisibilityDeferred()
        {
            yield return null;
            _refreshScheduled = false;
            RefreshStoryNpcVisibilityImmediate();
        }

        void RefreshStoryNpcVisibilityImmediate()
        {
            string currentScene = SceneManager.GetActiveScene().name;
            var npcs = GetCachedStoryNpcs();

            for (int i = 0; i < npcs.Length; i++)
            {
                var npc = npcs[i];
                if (npc == null)
                    continue;

                bool show = ShouldShowStoryNpcInScene(npc, currentScene);

                if (npc.gameObject.activeSelf != show)
                    npc.gameObject.SetActive(show);

                if (show)
                    npc.ApplyTriggerOnlyAppearanceIfNeeded();
            }
        }

        bool ShouldShowStoryNpcInScene(StoryQuestNpc npc, string currentScene)
        {
            if (npc == null || string.IsNullOrWhiteSpace(npc.documentId))
                return false;

            var doc = ResolveDocument(npc.documentId) ?? GetDocument(npc.documentId);
            if (doc == null)
                return false;

            string targetScene = StoryDocumentLocationResolver.ResolveTargetSceneName(database, doc);
            if (string.IsNullOrWhiteSpace(targetScene))
                return false;

            if (!string.Equals(
                    NormalizeSceneName(currentScene),
                    NormalizeSceneName(targetScene),
                    System.StringComparison.OrdinalIgnoreCase))
                return false;

            if (IsNpcRelevantDuringDialogue(npc))
                return true;

            return CanStartDocument(npc.documentId);
        }

        bool IsNpcRelevantDuringDialogue(StoryQuestNpc npc)
        {
            if (npc == null)
                return false;

            if (dialogueUI == null)
                dialogueUI = FindObjectOfType<StoryDialogueUI>(true);
            if (dialogueUI == null || !dialogueUI.IsOpen)
                return false;

            if (dialogueUI.ActiveSourceNpc == npc)
                return true;

            string currentDocId = dialogueUI.CurrentDocumentId;
            return !string.IsNullOrWhiteSpace(currentDocId) &&
                   string.Equals(npc.documentId, currentDocId, System.StringComparison.OrdinalIgnoreCase);
        }

        void SyncActiveStoryNpcForCurrentScene()
        {
            RefreshStoryNpcVisibilityImmediate();

            if (database == null || database.documents == null)
                return;

            string activeDocId = GetTrackedStoryDocumentId();
            if (string.IsNullOrEmpty(activeDocId))
                return;

            var doc = ResolveDocument(activeDocId);
            if (doc == null)
                return;

            string currentScene = SceneManager.GetActiveScene().name;
            if (!string.IsNullOrEmpty(doc.targetSceneName) &&
                !string.Equals(NormalizeSceneName(currentScene), NormalizeSceneName(doc.targetSceneName), System.StringComparison.OrdinalIgnoreCase))
                return;

            var existing = GetCachedStoryNpcs();
            for (int i = 0; i < existing.Length; i++)
            {
                var npc = existing[i];
                if (npc == null ||
                    !string.Equals(npc.documentId, doc.documentId, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!StoryNpcSpawnHelper.IsRuntimePlaceholder(npc.gameObject))
                    return;

                Object.Destroy(npc.gameObject);
                InvalidateNpcCache();
                break;
            }

            StoryNpcSpawnHelper.SpawnStoryNpc(
                database,
                doc,
                doc.fallbackTargetPosition + ResolveSpawnOffset(doc.documentId));
            InvalidateNpcCache();
            RefreshStoryNpcVisibilityImmediate();
        }

        static Vector3 ResolveSpawnOffset(string documentId)
        {
            if (ComputerRoomStoryLocations.UsesConfirmedPosition(documentId))
                return Vector3.zero;
            if (UndergardenStoryLocations.UsesConfirmedPosition(documentId))
                return Vector3.zero;
            if (documentId == "M0-2-1")
                return new Vector3(-2f, 0f, 0f);
            if (documentId == "M0-1-3" || documentId == "M0-3-1")
                return new Vector3(2f, 0f, 0f);
            return new Vector3(1.5f, 0f, 1.5f);
        }

        static string NormalizeSceneName(string sceneRef)
        {
            if (string.IsNullOrWhiteSpace(sceneRef))
                return "";
            string s = sceneRef.Trim().Replace('\\', '/');
            int slash = s.LastIndexOf('/');
            if (slash >= 0 && slash < s.Length - 1)
                s = s.Substring(slash + 1);
            if (s.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase))
                s = s.Substring(0, s.Length - 6);
            return s.Trim();
        }

        static bool MatchesTrackedStorySubTask(QuestSubTask active, string documentId)
        {
            if (active == null || string.IsNullOrWhiteSpace(documentId))
                return false;

            if (!string.Equals(active.subTaskName, documentId, System.StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.IsNullOrWhiteSpace(active.triggerEventId))
                return true;

            return string.Equals(
                active.triggerEventId,
                BuildCompleteEventId(documentId),
                System.StringComparison.OrdinalIgnoreCase);
        }

        static string BuildCompleteEventId(string documentId)
        {
            if (string.IsNullOrWhiteSpace(documentId))
                return "";

            string sanitized = Regex.Replace(documentId.Trim().ToLowerInvariant(), @"[^\p{L}\p{Nd}]+", "_").Trim('_');
            return "content1_story_complete_" + sanitized;
        }

        static StoryDialogueDocument CreateStubDocument(string documentId, QuestSubTask active)
        {
            string chapterId = documentId;
            int dash = documentId.IndexOf('-');
            if (dash > 0)
                chapterId = documentId.Substring(0, dash);

            return new StoryDialogueDocument
            {
                documentId = documentId,
                title = documentId,
                chapterId = chapterId,
                category = QuestCategory.Main,
                isRootTask = true,
                completeEventId = string.IsNullOrWhiteSpace(active.triggerEventId)
                    ? BuildCompleteEventId(documentId)
                    : active.triggerEventId,
                npcId = GuessNpcId(documentId),
                targetSceneName = active.targetSceneName ?? "",
                fallbackTargetPosition = active.fallbackTargetPosition,
                rewardGold = active.rewardGold,
                finishLocationName = active.finishLocationName ?? "",
                pages = new List<StoryDialoguePage>
                {
                    new StoryDialoguePage
                    {
                        pageNumber = 1,
                        speakerName = "",
                        text = "（剧情文本尚未导入数据库。请在 Unity 编辑器运行 Tools → Quest System → 导入 content1 剧情任务。）"
                    }
                },
                choices = new List<StoryDialogueChoice>()
            };
        }

        static string GuessNpcId(string documentId)
        {
            switch (documentId)
            {
                case "M0-1-1":
                case "M0-1-3":
                    return "npc_\u8FCE\u65B0\u5B66\u957F";
                case "M0-2-1":
                    return "npc_\u6C88\u9752\u79BE";
                case "M0-3-1":
                    return "story_trigger";
                default:
                    return "story_trigger";
            }
        }
    }
}
