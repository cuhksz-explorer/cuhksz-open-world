using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using Mirror;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using DormMall;
using PhoneUI;

namespace GameQuestSystem
{
    public partial class QuestSystemManager : MonoBehaviour
    {
        public static QuestSystemManager Instance { get; private set; }
        const string LocalPendingSaveFile = "quest_pending_save_v1.json";
        const string LocalProgressSaveFile = "quest_local_progress_v1.json";
        const string LocalSaveFolderName = "quest_saves_by_user";

        [Header("基础")]
        [SerializeField] private GameServerSync gameServerSync;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private bool dontDestroyOnLoad = true;
        [SerializeField] private bool autoCreateProgressWhenEmpty = true;
        [SerializeField] private bool enableLocalProgressSave = true;

        [Header("任务配置")]
        [SerializeField] private List<QuestChainDefinition> questChains = new List<QuestChainDefinition>();

        [Header("金币配置")]
        [SerializeField] private int initialGold = 200;
        [SerializeField] private bool verboseLog = true;

        [Header("自动检测")]
        [SerializeField] private bool autoCheckDistanceCompletion = true;
        [SerializeField] private float autoCheckInterval = 0.25f;

        [Serializable]
        class PlayerStateResponse
        {
            public bool ok;
            public int dorm_gold;
            public string quest_state_json;
            public bool has_state;
        }

        [Serializable]
        class SaveStateResponse
        {
            public bool ok;
        }

        [Serializable]
        class LocalPendingSave
        {
            public string token;
            public string username;
            public int gold;
            public string questStateJson;
            public long savedAtUnix;
        }

        public event Action OnQuestChanged;
        public event Action<int> OnGoldChanged;
        public event Action OnPlayerStateLoaded;

        private readonly List<QuestChainProgress> _progressList = new List<QuestChainProgress>();
        private int _currentGold;
        private float _nextDistanceCheckTime;
        private bool _tutorialInventoryOpened;
        private bool _tutorialShiftPressed;
        private bool _tutorialSpacePressed;
        private bool _tutorialVPressed;
        private bool _tutorialQuestJournalOpened;
        private bool _tutorialMapOpened;
        private bool _tutorialSettingsPanelOpened;
        private bool _tutorialSocialOpened;
        private bool _localPendingDirty;
        private int _playerStateLoadSeq;
        private bool _playerStateLoadInProgress;
        private bool _playerStateReady;
        private long _lastAppliedStateUnix;
        private bool _forceFreshProgressOnNextServerLoad;

        public int CurrentGold => _currentGold;
        public IReadOnlyList<QuestChainDefinition> QuestChains => questChains;
        public bool IsPlayerStateReady => _playerStateReady;
        public bool IsPlayerStateLoadInProgress => _playerStateLoadInProgress;

        /// <summary>
        /// 将寝室教学链作为支线加入（不覆盖其它主线）。
        /// </summary>
        public void ApplyLingCollegeBDormTutorialPresetChain()
        {
            AddOrReplaceQuestChain(LingCollegeBDormQuestPreset.CreateChain());
        }

        /// <summary>
        /// 添加「寻找游走 LLM NPC」支线（M0 整章完成后接取）。
        /// </summary>
        public void ApplyLlmWanderingNpcSideQuestPreset()
        {
            AddOrReplaceQuestChain(LlmWanderingNpcQuestPreset.CreateChain());
        }

        public void AddOrReplaceQuestChain(QuestChainDefinition chain)
        {
            if (chain == null)
                return;
            if (questChains == null)
                questChains = new List<QuestChainDefinition>();

            for (int i = questChains.Count - 1; i >= 0; i--)
            {
                if (questChains[i] != null &&
                    string.Equals(questChains[i].questId, chain.questId, StringComparison.Ordinal))
                {
                    questChains[i] = chain;
                    EnsureProgressIntegrity();
                    RaiseQuestChanged();
                    return;
                }
            }

            questChains.Add(chain);
            EnsureProgressIntegrity();
            RaiseQuestChanged();
        }

        public void RemoveQuestChainById(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId) || questChains == null)
                return;

            questChains.RemoveAll(q =>
                q != null && string.Equals(q.questId, questId, StringComparison.Ordinal));

            _progressList.RemoveAll(p =>
                p != null && string.Equals(p.questId, questId, StringComparison.Ordinal));

            if (string.Equals(_trackedQuestId, questId, StringComparison.Ordinal))
                _trackedQuestId = "";

            EnsureProgressIntegrity();
            EnsureDefaultTrackedQuestIfEmpty();
            RaiseQuestChanged();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // 菜单/Loading 等先加载的实例往往是空 Quest Chains；当前场景里配好的链在「重复」物体上，直接 Destroy 会丢掉。
                if (questChains != null && questChains.Count > 0)
                    Instance.AdoptQuestChainsFromScene(questChains);
                if (storyQuestDatabase != null)
                    Instance.AssignStoryDatabaseIfEmpty(storyQuestDatabase);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (dontDestroyOnLoad)
            {
                // Unity 只会把根物体可靠迁移到 DontDestroyOnLoad 场景。
                // 任务管理器在部分场景里挂在父物体下，不先脱离父级会在切场景时被卸载，
                // 导致下一场景的空 QuestSystemManager 接管并显示“暂无可追踪任务”。
                if (transform.parent != null)
                    transform.SetParent(null, true);
                DontDestroyOnLoad(gameObject);
            }

            TryResolveRefs();
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloadedPersist;
            Application.quitting += OnApplicationQuittingPersist;
        }

        /// <summary>
        /// 从当前场景的管理器合并任务链定义到持久化的 Instance（新号先进菜单再进关卡时必需）。
        /// </summary>
        public void AdoptQuestChainsFromScene(IReadOnlyList<QuestChainDefinition> sceneChains)
        {
            if (sceneChains == null || sceneChains.Count == 0)
                return;

            if (questChains == null)
                questChains = new List<QuestChainDefinition>();

            for (int i = 0; i < sceneChains.Count; i++)
            {
                if (sceneChains[i] != null)
                    AddOrReplaceQuestChain(sceneChains[i]);
            }

            EnsureProgressIntegrity();
            EnsureDefaultTrackedQuestIfEmpty();
            RaiseQuestChanged();
            if (_playerStateReady && gameServerSync != null && gameServerSync.IsLoggedIn && !_playerStateLoadInProgress)
                StartCoroutine(CoLoadStateFromServer());
            EnsureStoryQuestInfrastructure();
            QuestGroundGuidePath.EnsureExists();
            if (verboseLog)
                Debug.Log("[QuestSystem] 已从场景合并任务链定义，共 " + questChains.Count + " 条。");
        }

        void Start()
        {
            GameServerSync.OnAuthSuccess += OnAuthSuccess;
            GameServerSync.OnRegisterSuccess += OnRegisterAccountSuccess;
            GameServerSync.OnLoggedOut += OnLoggedOut;
            DormMallUI.OnAnyPurchaseCompleted += OnAnyPurchaseCompleted;
            ResourceHotUpdateService.OnResourcesReady += OnResourcesHotUpdated;

            TryApplyCachedStoryHotUpdate();
            EnsureStoryQuestInfrastructure();
            QuestGroundGuidePath.EnsureExists();
            if (enableContentHotUpdate)
                StartCoroutine(CoFetchStoryHotUpdate());

            if (questChains == null || questChains.Count == 0)
                Debug.LogWarning("[QuestSystem] Quest Chains 为空，任务栏不会显示有效任务。请先在 QuestSystemManager 中配置任务。");

            if (gameServerSync != null && gameServerSync.IsLoggedIn)
                StartCoroutine(CoLoadStateFromServer());
            else if (autoCreateProgressWhenEmpty && _progressList.Count == 0)
            {
                InitializeDefaultLocalState();
                FinalizePlayerStateLoad();
            }
            else if (_progressList.Count > 0)
                FinalizePlayerStateLoad();
        }

        void OnSceneLoaded(Scene _, LoadSceneMode __)
        {
            TryResolveRefs(forceRefreshPlayer: true);
            QuestTeleportGuideResolver.InvalidateCache();
            EnsureStoryQuestInfrastructure();
            QuestGroundGuidePath.EnsureExists();
            EnsureDefaultTrackedQuestIfEmpty();
            EvaluateSceneArrivalAndAutoCompleteIfNeeded();
            PersistLocalPendingSnapshotIfDirty();
            RaiseQuestChanged();
        }

        void OnSceneUnloadedPersist(Scene _)
        {
            PersistLocalPendingSnapshotIfDirty();
        }

        void OnApplicationQuittingPersist()
        {
            PersistLocalPendingSnapshotIfDirty();
        }

        void OnDestroy()
        {
            ResourceHotUpdateService.OnResourcesReady -= OnResourcesHotUpdated;
            if (Instance == this)
                Instance = null;
            GameServerSync.OnAuthSuccess -= OnAuthSuccess;
            GameServerSync.OnRegisterSuccess -= OnRegisterAccountSuccess;
            GameServerSync.OnLoggedOut -= OnLoggedOut;
            DormMallUI.OnAnyPurchaseCompleted -= OnAnyPurchaseCompleted;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloadedPersist;
            Application.quitting -= OnApplicationQuittingPersist;
            PersistLocalPendingSnapshotIfDirty();
        }

        void OnApplicationPause(bool pause)
        {
            if (pause)
                PersistLocalPendingSnapshotIfDirty();
        }

        void OnApplicationQuit()
        {
            PersistLocalPendingSnapshotIfDirty();
        }

        void OnLoggedOut()
        {
            StopAllCoroutines();
            _playerStateLoadInProgress = false;
            _playerStateReady = false;
            _lastAppliedStateUnix = 0;
            _forceFreshProgressOnNextServerLoad = false;
            _localPendingDirty = false;
            string username = gameServerSync != null ? gameServerSync.SavedUsername : "";
            string token = gameServerSync != null ? gameServerSync.AuthTokenForExtensions : "";
            InitializeDefaultLocalState();
            ClearLocalQuestSavesForUser(username, token);
        }

        void OnRegisterAccountSuccess(string username)
        {
            _forceFreshProgressOnNextServerLoad = true;
            string token = gameServerSync != null ? gameServerSync.AuthTokenForExtensions : "";
            ClearLocalQuestSavesForUser(username, token);
            if (!_playerStateLoadInProgress)
            {
                InitializeDefaultLocalState();
                FinalizePlayerStateLoad();
            }
        }

        static bool QuestLoadTokenStillCurrent(GameServerSync sync, string tokenAtStart)
        {
            if (sync == null)
                return string.IsNullOrEmpty(tokenAtStart);
            return string.Equals(tokenAtStart ?? "", sync.AuthTokenForExtensions ?? "", StringComparison.Ordinal);
        }

        void Update()
        {
            TryResolveRefs();
            ProcessAllAcceptedQuestsInUpdate();
        }

        void OnAuthSuccess(string username, string token)
        {
            StartCoroutine(CoLoadStateFromServer());
        }

        void TryResolveRefs(bool forceRefreshPlayer = false)
        {
            if (gameServerSync == null)
                gameServerSync = GameServerSync.Instance;

            if (forceRefreshPlayer || !IsPlayerTransformAlive())
            {
                playerTransform = null;
                var pm = FindObjectOfType<PlayerMovement>(true);
                if (pm != null)
                    playerTransform = pm.transform;
            }
        }

        bool IsPlayerTransformAlive()
        {
            return playerTransform != null;
        }

        public bool IsNewUserTutorialActive()
        {
            var prog = FindProgress(NewUserTutorialQuestPreset.QuestId);
            if (prog == null || prog.questCompleted)
                return false;
            return GetCurrentSubTaskForQuest(NewUserTutorialQuestPreset.QuestId) != null;
        }

        public QuestSubTask GetNewUserTutorialCurrentSubTask()
        {
            return GetCurrentSubTaskForQuest(NewUserTutorialQuestPreset.QuestId);
        }

        void ResetNewUserTutorialTransientFlags()
        {
            _tutorialInventoryOpened = false;
            _tutorialShiftPressed = false;
            _tutorialSpacePressed = false;
            _tutorialVPressed = false;
            _tutorialQuestJournalOpened = false;
            _tutorialMapOpened = false;
            _tutorialSettingsPanelOpened = false;
            _tutorialSocialOpened = false;
        }

        public float GetDistanceToActiveTarget()
        {
            return GetDistanceToQuestTarget(_trackedQuestId);
        }

        /// <summary>
        /// 供引导 UI 等使用：当前子任务可追踪时返回世界坐标。
        /// 同场景指向任务目标；跨场景指向当前场景内最近的传送入口（含公交站）。
        /// </summary>
        public bool TryGetActiveQuestReachLocationWorldPosition(out Vector3 worldPosition)
        {
            return TryGetQuestGuideWorldPosition(GetTrackedSubTask(), out worldPosition);
        }

        public bool TryGetQuestGuideWorldPosition(QuestSubTask sub, out Vector3 worldPosition)
        {
            worldPosition = default;
            if (sub == null)
                return false;
            if (!CanGuideToSubTaskTarget(sub))
                return false;

            if (SubTaskSceneMatchesActiveScene(sub))
            {
                worldPosition = ResolveTargetPosition(sub);
                return true;
            }

            TryResolveRefs();
            if (playerTransform == null)
                return false;
            if (!TryResolveQuestDestinationScene(sub, out string destinationScene))
                return false;

            return QuestTeleportGuideResolver.TryGetNearestToScene(
                destinationScene,
                playerTransform.position,
                out worldPosition);
        }

        public bool TryGetSubTaskTargetSceneName(QuestSubTask sub, out string sceneName)
        {
            return TryResolveQuestDestinationScene(sub, out sceneName);
        }

        public bool IsSubTaskTargetInCurrentScene(QuestSubTask sub)
        {
            return SubTaskSceneMatchesActiveScene(sub);
        }

        bool CanGuideToSubTaskTarget(QuestSubTask sub)
        {
            return CanGuideToSubTaskTargetInternal(sub);
        }

        public bool CanGuideToSubTaskTargetPublic(QuestSubTask sub)
        {
            return CanGuideToSubTaskTargetInternal(sub);
        }

        bool CanGuideToSubTaskTargetInternal(QuestSubTask sub)
        {
            if (sub == null) return false;
            if (sub.triggerType != QuestTriggerType.ReachLocation &&
                sub.triggerType != QuestTriggerType.ManualEvent)
                return false;

            if (SubTaskSceneMatchesActiveScene(sub))
            {
                if (TryResolveStoryNpcGuidePosition(sub, out _))
                    return true;
                if (sub.targetTransform != null)
                    return true;
                return sub.fallbackTargetPosition != Vector3.zero;
            }

            if (!TryResolveQuestDestinationScene(sub, out string destinationScene))
                return false;

            TryResolveRefs();
            if (playerTransform == null)
                return false;

            return QuestTeleportGuideResolver.TryGetNearestToScene(
                destinationScene,
                playerTransform.position,
                out _);
        }

        bool TryResolveQuestDestinationScene(QuestSubTask sub, out string destinationScene)
        {
            destinationScene = null;
            if (sub == null)
                return false;

            if (!string.IsNullOrWhiteSpace(sub.targetSceneName))
            {
                destinationScene = NormalizeSceneName(sub.targetSceneName);
                return !string.IsNullOrEmpty(destinationScene);
            }

            string docId = ResolveStoryDocumentId(sub);
            if (string.IsNullOrWhiteSpace(docId))
                return false;

            var runtime = StoryQuestRuntime.Instance;
            var doc = runtime != null ? runtime.GetDocument(docId) : null;
            if (doc == null || string.IsNullOrWhiteSpace(doc.targetSceneName))
                return false;

            destinationScene = NormalizeSceneName(doc.targetSceneName);
            return !string.IsNullOrEmpty(destinationScene);
        }

        string ResolveStoryDocumentId(QuestSubTask sub)
        {
            if (sub == null)
                return null;

            if (!string.IsNullOrWhiteSpace(sub.subTaskName))
                return sub.subTaskName.Trim();

            if (string.IsNullOrWhiteSpace(sub.triggerEventId))
                return null;

            var runtime = StoryQuestRuntime.Instance;
            var db = runtime?.Database;
            if (db?.documents == null)
                return null;

            for (int i = 0; i < db.documents.Count; i++)
            {
                var doc = db.documents[i];
                if (doc != null &&
                    string.Equals(doc.completeEventId, sub.triggerEventId, StringComparison.OrdinalIgnoreCase))
                    return doc.documentId;
            }

            return null;
        }

        bool TryResolveStoryNpcGuidePosition(QuestSubTask sub, out Vector3 worldPosition)
        {
            worldPosition = default;
            if (sub == null || sub.triggerType != QuestTriggerType.ManualEvent)
                return false;

            var runtime = StoryQuestRuntime.Instance;
            if (runtime == null)
                return false;

            string docId = ResolveStoryDocumentId(sub);
            if (string.IsNullOrWhiteSpace(docId))
                return false;

            var npcs = runtime.GetCachedStoryNpcsForGuide();
            for (int i = 0; i < npcs.Length; i++)
            {
                var npc = npcs[i];
                if (npc == null || !npc.gameObject.activeInHierarchy)
                    continue;
                if (!string.Equals(npc.documentId, docId, StringComparison.OrdinalIgnoreCase))
                    continue;
                worldPosition = npc.TriggerPosition;
                return true;
            }

            return false;
        }

        public int GetDisplaySubTaskIndex()
        {
            return GetDisplaySubTaskIndexForQuest(_trackedQuestId);
        }

        public int GetTotalSubTaskCount()
        {
            return GetTotalSubTaskCountForQuest(_trackedQuestId);
        }

        public int GetActiveCounterCurrent()
        {
            return GetCounterCurrentForQuest(_trackedQuestId);
        }

        const char CoopQuestSnapshotSep = '\u001e';

        /// <summary>房主侧序列化：金币 + 分隔符 + <see cref="QuestSavePayload"/> 的 Json（与 HTTP 存档结构一致）。</summary>
        public string SerializeCoopSnapshotForNet()
        {
            var payload = new QuestSavePayload
            {
                questProgressList = new List<QuestChainProgress>(_progressList),
                trackedQuestId = GetTrackedQuestIdForSave()
            };
            string questPart = JsonUtility.ToJson(payload);
            return _currentGold.ToString(CultureInfo.InvariantCulture) + CoopQuestSnapshotSep + questPart;
        }

        /// <summary>客端应用房主快照；不写 HTTP。</summary>
        public void TryApplyCoopHostSnapshotFromJson(string bundled)
        {
            if (string.IsNullOrEmpty(bundled)) return;
            int sep = bundled.IndexOf(CoopQuestSnapshotSep);
            if (sep <= 0 || sep >= bundled.Length - 1) return;
            string goldPart = bundled.Substring(0, sep);
            string questPart = bundled.Substring(sep + 1);
            if (!int.TryParse(goldPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out int gold))
                return;

            QuestSavePayload payload = null;
            try
            {
                payload = JsonUtility.FromJson<QuestSavePayload>(questPart);
            }
            catch
            {
                return;
            }
            if (payload == null) return;

            _currentGold = Mathf.Max(0, gold);
            OnGoldChanged?.Invoke(_currentGold);
            _progressList.Clear();
            if (payload.questProgressList != null)
            {
                for (int i = 0; i < payload.questProgressList.Count; i++)
                    _progressList.Add(payload.questProgressList[i]);
            }
            ApplyTrackedQuestIdFromSave(payload.trackedQuestId);
            EnsureProgressIntegrity();
            EnsureDefaultTrackedQuestIfEmpty();
            RaiseQuestChanged();
        }

        /// <summary>联机客端断线后从服务器恢复本人任务与金币。</summary>
        public void ReloadPersonalStateAfterCoop()
        {
            StopAllCoroutines();
            _playerStateLoadInProgress = false;
            StartCoroutine(CoLoadStateFromServer());
        }

        static bool SubTaskSceneMatchesActiveScene(QuestSubTask sub)
        {
            if (sub == null || string.IsNullOrEmpty(sub.targetSceneName))
                return true;
            return SceneNameMatches(SceneManager.GetActiveScene().name, sub.targetSceneName);
        }

        static bool SceneNameMatches(string currentSceneName, string expectedSceneName)
        {
            string a = NormalizeSceneName(currentSceneName);
            string b = NormalizeSceneName(expectedSceneName);
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return false;
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        static string NormalizeSceneName(string sceneRef)
        {
            if (string.IsNullOrWhiteSpace(sceneRef))
                return "";
            string s = sceneRef.Trim().Replace('\\', '/');
            int slash = s.LastIndexOf('/');
            if (slash >= 0 && slash < s.Length - 1)
                s = s.Substring(slash + 1);
            if (s.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(0, s.Length - 6);
            return s.Trim();
        }

        static bool IsAnyDormMallUiOpenInHierarchy()
        {
            return DormMallUI.IsAnyMainShopPanelOpenInHierarchy();
        }

        static bool IsSettingsLikePanelOpen()
        {
            if (PhoneSessionController.IsSettingsPageOpen)
                return true;

            var curve = UnityEngine.Object.FindObjectOfType<CurvePanelController>(true);
            if (curve != null && curve.curvePanel != null && curve.curvePanel.activeInHierarchy)
                return true;

            var qualityControllers = UnityEngine.Object.FindObjectsOfType<QualityController>(true);
            for (int i = 0; i < qualityControllers.Length; i++)
            {
                var q = qualityControllers[i];
                if (q != null && q.qualityPanel != null && q.qualityPanel.activeInHierarchy)
                    return true;
            }

            return false;
        }

        public void AddGold(int amount, string reason)
        {
            _currentGold = Mathf.Max(0, _currentGold + amount);
            OnGoldChanged?.Invoke(_currentGold);
            MarkPendingDirtyAndPersistLocal();
            if (verboseLog)
                Debug.Log("[QuestSystem] 金币变化: " + amount + ", 当前=" + _currentGold + ", reason=" + reason);
        }

        public bool TryConsumeGold(int amount, string reason)
        {
            if (CoopSessionState.IsSessionGuest)
                return false;
            if (amount <= 0) return true;
            if (_currentGold < amount) return false;
            AddGold(-amount, reason);
            MarkPendingDirtyAndPersistLocal();
            StartCoroutine(CoSaveStateToServer());
            return true;
        }

        public void ForceSaveNow()
        {
            StartCoroutine(CoSaveStateToServer());
        }

        Vector3 ResolveTargetPosition(QuestSubTask sub)
        {
            if (sub == null)
                return Vector3.zero;
            if (TryResolveStoryNpcGuidePosition(sub, out Vector3 npcPos))
                return npcPos;
            if (sub.targetTransform != null)
                return sub.targetTransform.position;

            return sub.fallbackTargetPosition;
        }

        void RaiseQuestChanged()
        {
            OnQuestChanged?.Invoke();
        }

        void InitializeDefaultLocalState()
        {
            _progressList.Clear();
            _trackedQuestId = "";
            for (int i = 0; i < questChains.Count; i++)
            {
                var def = questChains[i];
                if (def == null)
                    continue;
                bool accepted = def.category == QuestCategory.Main;
                _progressList.Add(new QuestChainProgress(def, accepted));
            }

            _currentGold = initialGold;
            OnGoldChanged?.Invoke(_currentGold);
            EnsureProgressIntegrity();
            EnsureDefaultTrackedQuestIfEmpty();
            RaiseQuestChanged();
        }

        QuestChainProgress FindProgress(string questId)
        {
            for (int i = 0; i < _progressList.Count; i++)
            {
                if (string.Equals(_progressList[i].questId, questId, StringComparison.Ordinal))
                    return _progressList[i];
            }
            return null;
        }

        IEnumerator CoLoadStateFromServer()
        {
            TryResolveRefs();
            int seq = ++_playerStateLoadSeq;
            string tokenAtStart = gameServerSync != null ? gameServerSync.AuthTokenForExtensions : "";
            _playerStateLoadInProgress = true;
            _playerStateReady = false;

            if (gameServerSync == null || !gameServerSync.IsLoggedIn)
            {
                if (QuestLoadTokenStillCurrent(gameServerSync, tokenAtStart) &&
                    _progressList.Count == 0 &&
                    autoCreateProgressWhenEmpty)
                    InitializeDefaultLocalState();
                FinalizePlayerStateLoad(seq, tokenAtStart);
                yield break;
            }

            bool loadedFromServer = false;
            long serverAppliedUnix = 0;

            string url = gameServerSync.BaseUrlTrimmed + "/api/player_state?token=" +
                         UnityWebRequest.EscapeURL(gameServerSync.AuthTokenForExtensions);
            using (var req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();

                if (seq != _playerStateLoadSeq)
                    yield break;

                if (!QuestLoadTokenStillCurrent(gameServerSync, tokenAtStart))
                {
                    FinalizePlayerStateLoad(seq, tokenAtStart);
                    yield break;
                }

                if (req.result != UnityWebRequest.Result.Success)
                {
                    if (verboseLog)
                        Debug.LogWarning("[QuestSystem] 拉取玩家状态失败: " + req.error);
                    if (!TryRestoreProgressWithFallbacks(allowKeepExisting: false) ||
                        IsLoadedProgressSuspicious())
                    {
                        ClearLocalQuestSavesForCurrentUser();
                        InitializeDefaultLocalState();
                    }
                    FinalizePlayerStateLoad(seq, tokenAtStart);
                    yield break;
                }

                PlayerStateResponse resp = null;
                try { resp = JsonUtility.FromJson<PlayerStateResponse>(req.downloadHandler.text); }
                catch { }

                if (!QuestLoadTokenStillCurrent(gameServerSync, tokenAtStart))
                {
                    FinalizePlayerStateLoad(seq, tokenAtStart);
                    yield break;
                }

                if (resp == null || !resp.ok)
                {
                    if (!TryRestoreProgressWithFallbacks(allowKeepExisting: false) ||
                        IsLoadedProgressSuspicious())
                    {
                        ClearLocalQuestSavesForCurrentUser();
                        InitializeDefaultLocalState();
                    }
                    FinalizePlayerStateLoad(seq, tokenAtStart);
                    yield break;
                }

                if (!string.IsNullOrEmpty(resp.quest_state_json))
                {
                    try
                    {
                        var payload = JsonUtility.FromJson<QuestSavePayload>(resp.quest_state_json);
                        if (payload != null && payload.questProgressList != null && payload.questProgressList.Count > 0)
                        {
                            loadedFromServer = ApplyQuestProgressPayload(
                                payload,
                                Mathf.Max(0, resp.dorm_gold),
                                payload.trackedQuestId,
                                DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                        }
                    }
                    catch
                    {
                    }
                }

                if (!loadedFromServer)
                {
                    if (_forceFreshProgressOnNextServerLoad)
                    {
                        _forceFreshProgressOnNextServerLoad = false;
                        ClearLocalQuestSavesForCurrentUser();
                        InitializeDefaultLocalState();
                    }
                    else if (string.IsNullOrWhiteSpace(resp?.quest_state_json))
                    {
                        if (!TryRestoreProgressWithFallbacks(allowKeepExisting: false) ||
                            IsLoadedProgressSuspicious())
                        {
                            ClearLocalQuestSavesForCurrentUser();
                            InitializeDefaultLocalState();
                        }
                    }
                    else if (!TryRestoreProgressWithFallbacks(allowKeepExisting: false))
                    {
                        InitializeDefaultLocalState();
                    }
                }
                else
                {
                    serverAppliedUnix = _lastAppliedStateUnix;
                    PersistLocalProgressSnapshot();
                }

                if (seq != _playerStateLoadSeq)
                    yield break;

                if (IsLoadedProgressSuspicious())
                {
                    if (verboseLog)
                        Debug.LogWarning("[QuestSystem] 检测到异常任务进度（主线已推进但新手教学未完成），已重置为默认进度。");
                    ClearLocalQuestSavesForCurrentUser();
                    InitializeDefaultLocalState();
                    StartCoroutine(CoSaveStateToServer());
                }
                else
                {
                    TryApplyPendingLocalSnapshotIfNewer(serverAppliedUnix);
                    if (IsLoadedProgressSuspicious())
                    {
                        if (verboseLog)
                            Debug.LogWarning("[QuestSystem] 本地兜底任务进度异常，已重置为默认进度。");
                        ClearLocalQuestSavesForCurrentUser();
                        InitializeDefaultLocalState();
                        StartCoroutine(CoSaveStateToServer());
                    }
                }

                FinalizePlayerStateLoad(seq, tokenAtStart);
            }
        }

        void FinalizePlayerStateLoad(int seq, string tokenAtStart)
        {
            if (seq != _playerStateLoadSeq)
                return;
            if (!QuestLoadTokenStillCurrent(gameServerSync, tokenAtStart))
                return;

            FinalizePlayerStateLoad();
        }

        void FinalizePlayerStateLoad()
        {
            _playerStateLoadInProgress = false;
            _playerStateReady = true;
            EnsureProgressIntegrity();
            EnsureDefaultTrackedQuestIfEmpty();
            EvaluateSideQuestAcceptTriggers(QuestAcceptTriggerType.EnterScene, SceneManager.GetActiveScene().name);
            RaiseQuestChanged();
            OnPlayerStateLoaded?.Invoke();
        }

        bool TryRestoreProgressWithFallbacks(bool allowKeepExisting)
        {
            if (TryLoadLocalProgressSnapshotForCurrentUser(requireTokenMatch: true))
                return true;
            if (TryLoadLocalProgressSnapshotForCurrentUser(requireTokenMatch: false))
                return true;
            if (TryLoadPendingSnapshotForRestore())
                return true;
            if (allowKeepExisting && _progressList.Count > 0)
                return true;
            return false;
        }

        bool ApplyQuestProgressPayload(QuestSavePayload payload, int gold, string trackedQuestId, long savedAtUnix)
        {
            if (payload == null || payload.questProgressList == null || payload.questProgressList.Count == 0)
                return false;

            _progressList.Clear();
            for (int i = 0; i < payload.questProgressList.Count; i++)
                _progressList.Add(payload.questProgressList[i]);
            _currentGold = Mathf.Max(0, gold);
            ApplyTrackedQuestIdFromSave(trackedQuestId);
            _lastAppliedStateUnix = savedAtUnix;
            OnGoldChanged?.Invoke(_currentGold);
            return true;
        }

        bool TryLoadPendingSnapshotForRestore()
        {
            if (gameServerSync == null || !gameServerSync.IsLoggedIn)
                return false;

            string path = GetLocalPendingSavePath(gameServerSync.SavedUsername, gameServerSync.AuthTokenForExtensions);
            if (!File.Exists(path))
                return false;

            LocalPendingSave local = null;
            try { local = JsonUtility.FromJson<LocalPendingSave>(File.ReadAllText(path)); }
            catch { return false; }

            if (local == null || string.IsNullOrEmpty(local.questStateJson))
                return false;
            if (string.IsNullOrWhiteSpace(gameServerSync.SavedUsername))
                return false;
            if (string.IsNullOrWhiteSpace(local.username))
                return false;
            if (!string.IsNullOrEmpty(local.token) &&
                !string.Equals(local.token, gameServerSync.AuthTokenForExtensions ?? "", StringComparison.Ordinal))
                return false;
            if (!string.IsNullOrEmpty(local.username) &&
                !string.IsNullOrEmpty(gameServerSync.SavedUsername) &&
                !string.Equals(local.username, gameServerSync.SavedUsername, StringComparison.OrdinalIgnoreCase))
                return false;

            try
            {
                var payload = JsonUtility.FromJson<QuestSavePayload>(local.questStateJson);
                if (!ApplyQuestProgressPayload(payload, local.gold, payload?.trackedQuestId, local.savedAtUnix))
                    return false;
                if (IsLoadedProgressSuspicious())
                    return false;
                if (verboseLog)
                    Debug.Log("[QuestSystem] 已从 pending 本地档恢复任务进度。");
                return true;
            }
            catch (Exception ex)
            {
                if (verboseLog)
                    Debug.LogWarning("[QuestSystem] 读取 pending 本地档失败: " + ex.Message);
                return false;
            }
        }

        void TryApplyPendingLocalSnapshotIfNewer(long serverAppliedUnix = 0)
        {
            if (gameServerSync == null || !gameServerSync.IsLoggedIn)
                return;

            string path = GetLocalPendingSavePath(gameServerSync.SavedUsername, gameServerSync.AuthTokenForExtensions);
            if (!File.Exists(path))
                return;

            LocalPendingSave local = null;
            try { local = JsonUtility.FromJson<LocalPendingSave>(File.ReadAllText(path)); }
            catch { return; }
            if (local == null || string.IsNullOrEmpty(local.questStateJson))
                return;
            if (!string.IsNullOrEmpty(local.token) &&
                !string.Equals(local.token, gameServerSync.AuthTokenForExtensions ?? "", StringComparison.Ordinal))
                return;
            if (!string.IsNullOrEmpty(local.username) &&
                !string.IsNullOrEmpty(gameServerSync.SavedUsername) &&
                !string.Equals(local.username, gameServerSync.SavedUsername, StringComparison.OrdinalIgnoreCase))
                return;

            long baseline = Math.Max(serverAppliedUnix, _lastAppliedStateUnix);
            if (baseline > 0 && local.savedAtUnix > 0 && local.savedAtUnix <= baseline)
                return;

            try
            {
                var payload = JsonUtility.FromJson<QuestSavePayload>(local.questStateJson);
                if (!ApplyQuestProgressPayload(payload, local.gold, payload?.trackedQuestId, local.savedAtUnix))
                    return;

                EnsureProgressIntegrity();
                EnsureDefaultTrackedQuestIfEmpty();
                OnGoldChanged?.Invoke(_currentGold);
                RaiseQuestChanged();
                StartCoroutine(CoSaveStateToServer());
                if (verboseLog)
                    Debug.Log("[QuestSystem] 已应用较新的 pending 本地任务进度。");
            }
            catch (Exception ex)
            {
                if (verboseLog)
                    Debug.LogWarning("[QuestSystem] 应用 pending 本地档失败: " + ex.Message);
            }
        }

        void EnsureProgressIntegrity()
        {
            for (int i = 0; i < questChains.Count; i++)
            {
                var def = questChains[i];
                if (def == null)
                    continue;

                var p = FindProgress(def.questId);
                if (p == null)
                {
                    bool accepted = def.category == QuestCategory.Main;
                    _progressList.Add(new QuestChainProgress(def, accepted));
                    p = FindProgress(def.questId);
                }

                if (def.category == QuestCategory.Main && !p.accepted)
                    p.accepted = true;

                if (def.category == QuestCategory.Side && !p.accepted)
                {
                    if (p.questCompleted || p.currentSubTaskIndex > 0)
                        p.accepted = true;
                    else if (p.subTaskCompleted != null)
                    {
                        for (int s = 0; s < p.subTaskCompleted.Length; s++)
                        {
                            if (p.subTaskCompleted[s])
                            {
                                p.accepted = true;
                                break;
                            }
                        }
                    }
                }

                int count = def.subTasks != null ? def.subTasks.Count : 0;
                if (p.subTaskCompleted == null || p.subTaskCompleted.Length != count)
                {
                    var arr = new bool[Mathf.Max(0, count)];
                    if (p.subTaskCompleted != null)
                    {
                        int n = Mathf.Min(arr.Length, p.subTaskCompleted.Length);
                        Array.Copy(p.subTaskCompleted, arr, n);
                    }
                    p.subTaskCompleted = arr;
                }
                if (p.subTaskCounter == null || p.subTaskCounter.Length != count)
                {
                    var c = new int[Mathf.Max(0, count)];
                    if (p.subTaskCounter != null)
                    {
                        int n = Mathf.Min(c.Length, p.subTaskCounter.Length);
                        Array.Copy(p.subTaskCounter, c, n);
                    }
                    p.subTaskCounter = c;
                }
                p.currentSubTaskIndex = Mathf.Clamp(p.currentSubTaskIndex, 0, Mathf.Max(0, count - 1));
            }
        }

        IEnumerator CoSaveStateToServer()
        {
            TryResolveRefs();
            if (CoopSessionState.BlockGuestCloudWrites)
                yield break;
            if (gameServerSync == null || !gameServerSync.IsLoggedIn)
                yield break;

            var payload = new QuestSavePayload
            {
                questProgressList = new List<QuestChainProgress>(_progressList),
                trackedQuestId = GetTrackedQuestIdForSave()
            };
            string questStateJson = JsonUtility.ToJson(payload);
            string body = BuildPlayerStateBody(gameServerSync.AuthTokenForExtensions, _currentGold, questStateJson);

            using (var req = new UnityWebRequest(gameServerSync.BaseUrlTrimmed + "/api/player_state", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success && verboseLog)
                    Debug.LogWarning("[QuestSystem] 保存玩家状态失败: " + req.error);
                else if (req.result == UnityWebRequest.Result.Success)
                    ClearLocalPendingSnapshot();
            }
        }

        string BuildPlayerStateBody(string token, int gold, string questStateJson)
        {
            return
                "{" +
                "\"token\":\"" + JsonEscape(token) + "\"," +
                "\"dorm_gold\":" + gold.ToString(CultureInfo.InvariantCulture) + "," +
                "\"quest_state_json\":\"" + JsonEscape(questStateJson) + "\"" +
                "}";
        }

        void MarkPendingDirtyAndPersistLocal()
        {
            _localPendingDirty = true;
            PersistLocalPendingSnapshotIfDirty();
        }

        void PersistLocalPendingSnapshotIfDirty()
        {
            if (!_localPendingDirty)
                return;
            if (CoopSessionState.BlockGuestCloudWrites)
                return;

            PersistLocalProgressSnapshot();

            if (gameServerSync == null || !gameServerSync.IsLoggedIn)
            {
                _localPendingDirty = false;
                return;
            }

            try
            {
                var payload = new QuestSavePayload
                {
                    questProgressList = new List<QuestChainProgress>(_progressList),
                    trackedQuestId = GetTrackedQuestIdForSave()
                };
                var save = new LocalPendingSave
                {
                    token = gameServerSync.AuthTokenForExtensions ?? "",
                    username = gameServerSync.SavedUsername ?? "",
                    gold = _currentGold,
                    questStateJson = JsonUtility.ToJson(payload),
                    savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };
                string json = JsonUtility.ToJson(save);
                string path = GetLocalPendingSavePath();
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(path, json);
                _localPendingDirty = false;
            }
            catch (Exception ex)
            {
                if (verboseLog)
                    Debug.LogWarning("[QuestSystem] 写本地兜底存档失败: " + ex.Message);
            }
        }

        void PersistLocalProgressSnapshot()
        {
            if (!enableLocalProgressSave)
                return;
            if (gameServerSync == null || !gameServerSync.IsLoggedIn)
                return;
            try
            {
                var payload = new QuestSavePayload
                {
                    questProgressList = new List<QuestChainProgress>(_progressList),
                    trackedQuestId = GetTrackedQuestIdForSave()
                };
                var save = new LocalPendingSave
                {
                    token = gameServerSync != null && gameServerSync.IsLoggedIn ? gameServerSync.AuthTokenForExtensions ?? "" : "",
                    username = gameServerSync != null && gameServerSync.IsLoggedIn ? gameServerSync.SavedUsername ?? "" : "",
                    gold = _currentGold,
                    questStateJson = JsonUtility.ToJson(payload),
                    savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };
                string json = JsonUtility.ToJson(save);
                string path = GetLocalProgressSavePath();
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(path, json);
                if (verboseLog)
                    Debug.Log("[QuestSystem] 已保存本地任务进度: " + path);
            }
            catch (Exception ex)
            {
                if (verboseLog)
                    Debug.LogWarning("[QuestSystem] 写本地任务进度失败: " + ex.Message);
            }
        }

        bool TryLoadLocalProgressSnapshotForCurrentUser(bool requireTokenMatch)
        {
            if (!enableLocalProgressSave)
                return false;
            if (gameServerSync == null || !gameServerSync.IsLoggedIn)
                return false;

            return TryLoadLocalProgressSnapshot(
                gameServerSync.AuthTokenForExtensions ?? "",
                gameServerSync.SavedUsername ?? "",
                requireTokenMatch);
        }

        bool TryLoadLocalProgressSnapshot(string expectedToken, string expectedUsername, bool requireTokenMatch)
        {
            string path = GetLocalProgressSavePath(expectedUsername, expectedToken);
            if (!File.Exists(path))
                return false;

            LocalPendingSave local = null;
            try { local = JsonUtility.FromJson<LocalPendingSave>(File.ReadAllText(path)); }
            catch { return false; }

            if (local == null || string.IsNullOrEmpty(local.questStateJson))
                return false;
            if (string.IsNullOrWhiteSpace(expectedUsername))
                return false;
            if (string.IsNullOrWhiteSpace(local.username))
                return false;
            if (requireTokenMatch && !string.IsNullOrEmpty(local.token) &&
                !string.Equals(local.token, expectedToken ?? "", StringComparison.Ordinal))
                return false;
            if (requireTokenMatch && !string.IsNullOrEmpty(local.username) &&
                !string.IsNullOrEmpty(expectedUsername) &&
                !string.Equals(local.username, expectedUsername, StringComparison.OrdinalIgnoreCase))
                return false;
            if (!requireTokenMatch && !string.IsNullOrEmpty(expectedUsername) &&
                !string.IsNullOrEmpty(local.username) &&
                !string.Equals(local.username, expectedUsername, StringComparison.OrdinalIgnoreCase))
                return false;

            try
            {
                var payload = JsonUtility.FromJson<QuestSavePayload>(local.questStateJson);
                if (!ApplyQuestProgressPayload(payload, local.gold, payload?.trackedQuestId, local.savedAtUnix))
                    return false;
                if (IsLoadedProgressSuspicious())
                    return false;
                EnsureProgressIntegrity();
                EnsureDefaultTrackedQuestIfEmpty();
                RaiseQuestChanged();
                if (verboseLog)
                    Debug.Log("[QuestSystem] 已加载本地任务进度: " + path);
                return true;
            }
            catch (Exception ex)
            {
                if (verboseLog)
                    Debug.LogWarning("[QuestSystem] 加载本地任务进度失败: " + ex.Message);
                return false;
            }
        }

        void ClearLocalPendingSnapshot()
        {
            if (gameServerSync == null || !gameServerSync.IsLoggedIn)
                return;
            string path = GetLocalPendingSavePath(gameServerSync.SavedUsername, gameServerSync.AuthTokenForExtensions);
            if (File.Exists(path))
                File.Delete(path);
        }

        string GetLocalPendingSavePath()
        {
            string username = gameServerSync != null ? gameServerSync.SavedUsername : "";
            string token = gameServerSync != null ? gameServerSync.AuthTokenForExtensions : "";
            return GetLocalPendingSavePath(username, token);
        }

        string GetLocalProgressSavePath()
        {
            string username = gameServerSync != null ? gameServerSync.SavedUsername : "";
            string token = gameServerSync != null ? gameServerSync.AuthTokenForExtensions : "";
            return GetLocalProgressSavePath(username, token);
        }

        static string GetLocalPendingSavePath(string username, string token)
        {
            return Path.Combine(GetLocalSaveFolder(), BuildPerUserSaveFileName(LocalPendingSaveFile, username, token));
        }

        static string GetLocalProgressSavePath(string username, string token)
        {
            return Path.Combine(GetLocalSaveFolder(), BuildPerUserSaveFileName(LocalProgressSaveFile, username, token));
        }

        static string GetLocalSaveFolder()
        {
            string dir = Path.Combine(Application.persistentDataPath, LocalSaveFolderName);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }

        static string BuildPerUserSaveFileName(string baseFileName, string username, string token)
        {
            string key = SanitizeSaveKey(!string.IsNullOrWhiteSpace(username) ? username : token);
            if (string.IsNullOrEmpty(key))
                key = "guest";
            return Path.GetFileNameWithoutExtension(baseFileName) + "_" + key + Path.GetExtension(baseFileName);
        }

        static string SanitizeSaveKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";
            return Regex.Replace(value.Trim(), @"[^\w\-.@]+", "_");
        }

        void ClearLocalQuestSavesForCurrentUser()
        {
            if (gameServerSync == null || !gameServerSync.IsLoggedIn)
                return;
            ClearLocalQuestSavesForUser(gameServerSync.SavedUsername, gameServerSync.AuthTokenForExtensions);
        }

        static void ClearLocalQuestSavesForUser(string username, string token)
        {
            TryDeleteLocalSaveFile(GetLocalProgressSavePath(username ?? "", token ?? ""));
            TryDeleteLocalSaveFile(GetLocalPendingSavePath(username ?? "", token ?? ""));
            if (!string.IsNullOrWhiteSpace(username))
            {
                TryDeleteLocalSaveFile(GetLocalProgressSavePath(username, ""));
                TryDeleteLocalSaveFile(GetLocalPendingSavePath(username, ""));
            }
        }

        static void TryDeleteLocalSaveFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;
            try { File.Delete(path); }
            catch { }
        }

        /// <summary>
        /// 主线章节已推进但新手教学未完成，或任务被标记完成却没有任何子任务进度，视为脏存档。
        /// </summary>
        bool IsLoadedProgressSuspicious()
        {
            if (IsQuestProgressStructurallyInvalid(NewUserTutorialQuestPreset.QuestId))
                return true;

            if (!IsQuestCompleted(NewUserTutorialQuestPreset.QuestId))
            {
                for (int i = 0; i < questChains.Count; i++)
                {
                    var def = questChains[i];
                    if (def == null || def.category != QuestCategory.Main)
                        continue;
                    if (string.Equals(def.questId, NewUserTutorialQuestPreset.QuestId, StringComparison.Ordinal))
                        continue;
                    if (IsQuestCompleted(def.questId) || IsQuestProgressStructurallyInvalid(def.questId))
                        return true;
                }

                return false;
            }

            int invalidCompletedMain = 0;
            for (int i = 0; i < questChains.Count; i++)
            {
                var def = questChains[i];
                if (def == null || def.category != QuestCategory.Main)
                    continue;
                if (!IsQuestCompleted(def.questId))
                    continue;
                if (IsQuestProgressStructurallyInvalid(def.questId))
                    invalidCompletedMain++;
            }

            return invalidCompletedMain >= 2;
        }

        bool IsQuestProgressStructurallyInvalid(string questId)
        {
            var def = FindQuestDefinition(questId);
            var prog = FindProgress(questId);
            if (def == null || prog == null || !prog.questCompleted)
                return false;

            int subCount = def.subTasks != null ? def.subTasks.Count : 0;
            if (subCount <= 0)
                return false;

            if (prog.currentSubTaskIndex > 0)
                return false;

            if (prog.subTaskCompleted != null)
            {
                for (int i = 0; i < prog.subTaskCompleted.Length; i++)
                {
                    if (prog.subTaskCompleted[i])
                        return false;
                }
            }

            return true;
        }

        static string JsonEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

#if UNITY_EDITOR
        [UnityEngine.ContextMenu("Quest 预设：添加 dorm 寝室支线（不覆盖其它任务）")]
        void Editor_ApplyLingCollegeBDormQuestPreset()
        {
            AddOrReplaceQuestChain(LingCollegeBDormQuestPreset.CreateChain());
            UnityEditor.EditorUtility.SetDirty(this);
        }

        [UnityEngine.ContextMenu("Quest 预设：从列表移除 dorm 寝室支线")]
        void Editor_RemoveDormSideQuest()
        {
            RemoveQuestChainById(LingCollegeBDormQuestPreset.QuestId);
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
