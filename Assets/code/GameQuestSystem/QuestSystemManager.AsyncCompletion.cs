using System;
using GameControls;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using DormMall;

namespace GameQuestSystem
{
    public partial class QuestSystemManager
    {
        void ProcessAllAcceptedQuestsInUpdate()
        {
            ProcessOpenSettingsQuestChecks();
            ProcessChairSitQuestChecks();

            if (GameplayUiHotkeyGuard.ShouldBlockGameplayHotkeys())
                return;

            bool checkDistanceThisFrame = autoCheckDistanceCompletion && Time.time >= _nextDistanceCheckTime;
            if (checkDistanceThisFrame)
                _nextDistanceCheckTime = Time.time + Mathf.Max(0.05f, autoCheckInterval);

            for (int i = 0; i < questChains.Count; i++)
            {
                var quest = questChains[i];
                if (!IsQuestRunnable(quest))
                    continue;

                var prog = FindProgress(quest.questId);
                var sub = GetCurrentSubTaskForQuest(quest.questId);
                if (prog == null || sub == null)
                    continue;

                if (!string.IsNullOrEmpty(sub.targetSceneName) &&
                    !SceneNameMatches(SceneManager.GetActiveScene().name, sub.targetSceneName))
                    continue;

                if (sub.triggerType == QuestTriggerType.ReachLocation)
                {
                    if (!checkDistanceThisFrame || playerTransform == null)
                        continue;

                    var target = ResolveTargetPosition(sub);
                    float d = Vector3.Distance(playerTransform.position, target);
                    float completeRadius = Mathf.Max(5f, sub.autoCompleteDistance);
                    if (d <= completeRadius)
                        CompleteSubTaskForQuest(quest.questId);
                }
                else if (sub.triggerType == QuestTriggerType.ManualEvent)
                {
                    TryHandleBuiltInManualEventForQuest(quest.questId, sub);
                }
            }
        }

        public void CompleteActiveSubTask()
        {
            if (CoopSessionState.IsSessionGuest)
            {
                CoopQuestRelay.Instance?.RequestCompleteActiveSubTask();
                return;
            }

            if (!string.IsNullOrEmpty(_trackedQuestId))
                CompleteSubTaskForQuest(_trackedQuestId);
        }

        public void CompleteSubTaskForQuest(string questId)
        {
            if (CoopSessionState.IsSessionGuest)
            {
                if (string.Equals(questId, _trackedQuestId, StringComparison.Ordinal))
                    CoopQuestRelay.Instance?.RequestCompleteActiveSubTask();
                return;
            }

            CompleteSubTaskForQuestCore(questId);
        }

        void CompleteSubTaskForQuestCore(string questId)
        {
            var quest = FindQuestDefinition(questId);
            var prog = FindProgress(questId);
            var sub = GetCurrentSubTaskForQuest(questId);
            if (quest == null || prog == null || sub == null)
                return;

            int current = Mathf.Clamp(prog.currentSubTaskIndex, 0, quest.subTasks.Count - 1);
            if (prog.subTaskCompleted != null && current < prog.subTaskCompleted.Length)
                prog.subTaskCompleted[current] = true;
            if (prog.subTaskCounter != null && current < prog.subTaskCounter.Length)
                prog.subTaskCounter[current] = Mathf.Max(
                    prog.subTaskCounter[current],
                    quest.subTasks[current].targetCount);

            AddGold(sub.rewardGold, "quest_subtask");

            if (string.Equals(questId, NewUserTutorialQuestPreset.QuestId, StringComparison.Ordinal))
                ResetNewUserTutorialTransientFlags();

            bool isLast = current >= quest.subTasks.Count - 1;
            if (isLast)
            {
                prog.questCompleted = true;
                AddGold(quest.finalRewardGold, "quest_chain");
                if (verboseLog)
                    Debug.Log("[QuestSystem] 任务完成: " + quest.questName + " (" + quest.category + ")");
                if (string.Equals(_trackedQuestId, questId, StringComparison.Ordinal))
                    EnsureDefaultTrackedQuestIfEmpty();
            }
            else
            {
                prog.currentSubTaskIndex = current + 1;
                if (verboseLog)
                    Debug.Log("[QuestSystem] 子任务完成: " + sub.subTaskName + " @ " + quest.questName);
            }

            RaiseQuestChanged();
            MarkPendingDirtyAndPersistLocal();
            StartCoroutine(CoSaveStateToServer());
        }

        void CompleteActiveSubTaskCore() => CompleteSubTaskForQuest(_trackedQuestId);

        public void ServerApplyRelayCompleteActiveSubTask(NetworkConnectionToClient sender)
        {
            if (!NetworkServer.active) return;
            if (!ServerValidateReachForRelay(sender))
                return;
            CompleteSubTaskForQuestCore(_trackedQuestId);
        }

        bool ServerValidateReachForRelay(NetworkConnectionToClient conn)
        {
            var sub = GetTrackedSubTask();
            if (sub == null) return false;
            if (sub.triggerType != QuestTriggerType.ReachLocation)
                return true;
            if (conn == null || conn.identity == null)
                return false;
            if (!string.IsNullOrEmpty(sub.targetSceneName))
            {
                var currentScene = SceneManager.GetActiveScene().name;
                if (!SceneNameMatches(currentScene, sub.targetSceneName))
                    return false;
            }

            Vector3 target = ResolveTargetPosition(sub);
            float r = Mathf.Max(5f, sub.autoCompleteDistance) + 3f;
            return Vector3.Distance(conn.identity.transform.position, target) <= r;
        }

        public void ReportEvent(string eventId)
        {
            if (CoopSessionState.IsSessionGuest)
            {
                CoopQuestRelay.Instance?.ReportQuestEvent(eventId);
                return;
            }

            ApplyReportEventCore(eventId);
        }

        void ApplyReportEventCore(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId))
                return;

            EvaluateSideQuestAcceptTriggers(QuestAcceptTriggerType.QuestEvent, eventId);

            for (int i = 0; i < questChains.Count; i++)
            {
                var quest = questChains[i];
                if (!IsQuestRunnable(quest))
                    continue;

                var sub = GetCurrentSubTaskForQuest(quest.questId);
                if (sub == null || sub.triggerType != QuestTriggerType.ManualEvent)
                    continue;
                if (!SubTaskSceneMatchesActiveScene(sub) && !StoryEventMatchesDocumentScene(eventId))
                    continue;
                if (!string.Equals((sub.triggerEventId ?? "").Trim(), eventId.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                CompleteSubTaskForQuest(quest.questId);
            }
        }

        /// <summary>
        /// 剧情对话结束时兜底：若当前追踪子任务的事件与完成事件一致，则直接推进（避免场景 targetSceneName 与数据库不同步）。
        /// ReportEvent 已成功推进时，当前子任务已变，此处不会重复完成。
        /// </summary>
        public void TryCompleteTrackedStoryDialogue(string completeEventId)
        {
            if (CoopSessionState.IsSessionGuest)
                return;
            if (string.IsNullOrWhiteSpace(completeEventId) || string.IsNullOrWhiteSpace(_trackedQuestId))
                return;

            var sub = GetTrackedSubTask();
            if (sub == null || sub.triggerType != QuestTriggerType.ManualEvent)
                return;
            if (!string.Equals((sub.triggerEventId ?? "").Trim(), completeEventId.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                return;

            CompleteSubTaskForQuestCore(_trackedQuestId);
        }

        bool StoryEventMatchesDocumentScene(string completeEventId)
        {
            var doc = FindStoryDocumentByCompleteEvent(completeEventId);
            if (doc == null || string.IsNullOrWhiteSpace(doc.targetSceneName))
                return false;
            return SceneNameMatches(SceneManager.GetActiveScene().name, doc.targetSceneName);
        }

        public void ServerApplyRelayReportEvent(string eventId)
        {
            if (!NetworkServer.active) return;
            ApplyReportEventCore(eventId);
        }

        public void ReportCounter(string counterId, int add = 1)
        {
            if (CoopSessionState.IsSessionGuest)
            {
                CoopQuestRelay.Instance?.ReportQuestCounter(counterId, add);
                return;
            }

            ApplyReportCounterCore(counterId, add);
        }

        void ApplyReportCounterCore(string counterId, int add)
        {
            if (add <= 0 || string.IsNullOrWhiteSpace(counterId))
                return;

            for (int i = 0; i < questChains.Count; i++)
            {
                var quest = questChains[i];
                if (!IsQuestRunnable(quest))
                    continue;

                var progress = FindProgress(quest.questId);
                var sub = GetCurrentSubTaskForQuest(quest.questId);
                if (progress == null || sub == null || sub.triggerType != QuestTriggerType.Counter)
                    continue;
                if (!string.Equals((sub.triggerEventId ?? "").Trim(), counterId.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                int idx = Mathf.Clamp(progress.currentSubTaskIndex, 0, quest.subTasks.Count - 1);
                if (progress.subTaskCounter == null || progress.subTaskCounter.Length != quest.subTasks.Count)
                    progress.subTaskCounter = new int[quest.subTasks.Count];
                progress.subTaskCounter[idx] += add;
                int need = Mathf.Max(1, sub.targetCount);
                RaiseQuestChanged();
                MarkPendingDirtyAndPersistLocal();
                if (progress.subTaskCounter[idx] >= need)
                    CompleteSubTaskForQuest(quest.questId);
                else
                    StartCoroutine(CoSaveStateToServer());
            }
        }

        public void ServerApplyRelayReportCounter(string counterId, int add)
        {
            if (!NetworkServer.active) return;
            ApplyReportCounterCore(counterId, add);
        }

        void OnAnyPurchaseCompleted(int productId, string productName, string category, int price, int coinsAfter)
        {
            for (int i = 0; i < questChains.Count; i++)
            {
                var quest = questChains[i];
                if (!IsQuestRunnable(quest))
                    continue;

                var sub = GetCurrentSubTaskForQuest(quest.questId);
                if (sub == null || sub.triggerType != QuestTriggerType.PurchaseProduct)
                    continue;
                if (!SubTaskSceneMatchesActiveScene(sub))
                    continue;

                string configured = (sub.triggerEventId ?? "").Trim();
                if (string.IsNullOrEmpty(configured))
                {
                    CompleteSubTaskForQuest(quest.questId);
                    continue;
                }

                bool matchId = int.TryParse(configured, out var cfgId) && cfgId == productId;
                bool matchName = string.Equals(configured, productName ?? "", StringComparison.OrdinalIgnoreCase);
                bool matchCategory = string.Equals(configured, category ?? "", StringComparison.OrdinalIgnoreCase);
                if (matchId || matchName || matchCategory)
                    CompleteSubTaskForQuest(quest.questId);
            }
        }

        /// <summary>
        /// 手机/设置 UI 打开时会屏蔽 <see cref="GameplayUiHotkeyGuard.ShouldBlockGameplayHotkeys"/>，
        /// 须在屏蔽判断之前检测 open_settings / settings_open_close，否则相关新手任务无法完成。
        /// </summary>
        void ProcessOpenSettingsQuestChecks()
        {
            bool settingsOpen = IsSettingsLikePanelOpen();

            for (int i = 0; i < questChains.Count; i++)
            {
                var quest = questChains[i];
                if (!IsQuestRunnable(quest))
                    continue;

                var sub = GetCurrentSubTaskForQuest(quest.questId);
                if (sub == null || sub.triggerType != QuestTriggerType.ManualEvent)
                    continue;
                if (!SubTaskSceneMatchesActiveScene(sub))
                    continue;

                string evt = (sub.triggerEventId ?? "").Trim();
                if (string.Equals(evt, "open_settings", StringComparison.OrdinalIgnoreCase))
                {
                    if (settingsOpen)
                        CompleteSubTaskForQuest(quest.questId);
                    continue;
                }

                if (evt == "settings_open_close" || evt == "tutorial_settings_close")
                {
                    if (!_tutorialSettingsPanelOpened)
                    {
                        if (settingsOpen)
                            _tutorialSettingsPanelOpened = true;
                    }
                    else if (!settingsOpen)
                    {
                        _tutorialSettingsPanelOpened = false;
                        CompleteSubTaskForQuest(quest.questId);
                    }
                }
            }
        }

        /// <summary>
        /// 坐下时 <see cref="ChairSitState.BlocksGameplay"/> 会令
        /// <see cref="GameplayUiHotkeyGuard.ShouldBlockGameplayHotkeys"/> 为 true，
        /// 须在屏蔽判断之前检测 sit_on_chair，否则「坐下休息」新手步骤无法完成。
        /// </summary>
        void ProcessChairSitQuestChecks()
        {
            if (!ChairSitState.IsSitting && !InteractableChair.IsSitting)
                return;

            for (int i = 0; i < questChains.Count; i++)
            {
                var quest = questChains[i];
                if (!IsQuestRunnable(quest))
                    continue;

                var sub = GetCurrentSubTaskForQuest(quest.questId);
                if (sub == null || sub.triggerType != QuestTriggerType.ManualEvent)
                    continue;
                if (!SubTaskSceneMatchesActiveScene(sub))
                    continue;

                string evt = (sub.triggerEventId ?? "").Trim();
                if (string.Equals(evt, "sit_on_chair", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(evt, "tutorial_chair_sit", StringComparison.OrdinalIgnoreCase))
                    CompleteSubTaskForQuest(quest.questId);
            }
        }

        void TryHandleBuiltInManualEventForQuest(string questId, QuestSubTask sub)
        {
            if (sub == null || string.IsNullOrWhiteSpace(sub.triggerEventId))
                return;

            string evt = sub.triggerEventId.Trim().ToLowerInvariant();

            if (evt == "learn_wasd" || evt == "tutorial_wasd")
            {
                if (GameInput.GetKeyDown(GameControlAction.MoveForward)
                    || GameInput.GetKeyDown(GameControlAction.MoveBack)
                    || GameInput.GetKeyDown(GameControlAction.MoveLeft)
                    || GameInput.GetKeyDown(GameControlAction.MoveRight))
                    CompleteSubTaskForQuest(questId);
                return;
            }

            if (evt == "learn_sprint" || evt == "tutorial_sprint")
            {
                if (GameInput.GetKeyDown(GameControlAction.Run))
                    CompleteSubTaskForQuest(questId);
                return;
            }

            if (evt == "learn_jump" || evt == "tutorial_jump")
            {
                if (GameInput.GetKeyDown(GameControlAction.Jump))
                    CompleteSubTaskForQuest(questId);
                return;
            }

            if (evt == "learn_crouch" || evt == "tutorial_crouch")
            {
                if (GameInput.GetKeyDown(GameControlAction.Crouch))
                    CompleteSubTaskForQuest(questId);
                return;
            }

            if (evt == "learn_mouse_look" || evt == "tutorial_mouse_look")
            {
                if (Mathf.Abs(Input.GetAxisRaw("Mouse X")) > 0.12f || Mathf.Abs(Input.GetAxisRaw("Mouse Y")) > 0.12f)
                    CompleteSubTaskForQuest(questId);
                return;
            }

            if (evt == "learn_camera_v" || evt == "tutorial_camera_v")
            {
                if (GameInput.GetKeyDown(GameControlAction.ToggleCamera))
                    CompleteSubTaskForQuest(questId);
                return;
            }

            if (evt == "open_settings" || evt == "camera_and_settings" || evt == "tutorial_v_settings")
            {
                if (evt == "camera_and_settings" || evt == "tutorial_v_settings")
                {
                    if (GameInput.GetKeyDown(GameControlAction.ToggleCamera))
                        _tutorialVPressed = true;
                    if (_tutorialVPressed && IsSettingsLikePanelOpen())
                    {
                        _tutorialVPressed = false;
                        CompleteSubTaskForQuest(questId);
                    }
                    return;
                }

                if (IsSettingsLikePanelOpen())
                    CompleteSubTaskForQuest(questId);
                return;
            }

            if (evt == "press_f" || evt == "intercom_f")
            {
                if (GameInput.GetKeyDown(GameControlAction.Interact))
                    CompleteSubTaskForQuest(questId);
                return;
            }

            if (evt == "sit_on_chair" || evt == "tutorial_chair_sit")
            {
                if (ChairSitState.IsSitting || InteractableChair.IsSitting)
                    CompleteSubTaskForQuest(questId);
                return;
            }

            if (evt == "inventory_open_close" || evt == "backpack_open_close")
            {
                var inv = InventoryUI.Instance;
                bool isOpen = inv != null && inv.inventoryPanel != null && inv.inventoryPanel.activeSelf;
                if (!_tutorialInventoryOpened)
                {
                    if (GameInput.GetKeyDown(GameControlAction.OpenInventory) || isOpen)
                        _tutorialInventoryOpened = true;
                }
                else if (!isOpen)
                {
                    _tutorialInventoryOpened = false;
                    CompleteSubTaskForQuest(questId);
                }

                return;
            }

            if (evt == "quest_journal_toggle" || evt == "tutorial_quest_j")
            {
                var questUi = UnityEngine.Object.FindObjectOfType<QuestSystemUI>(true);
                bool open = questUi != null && questUi.IsFullPanelOpen;
                if (!_tutorialQuestJournalOpened)
                {
                    if (GameInput.GetKeyDown(GameControlAction.OpenQuest) || open)
                        _tutorialQuestJournalOpened = true;
                }
                else if (!open)
                {
                    _tutorialQuestJournalOpened = false;
                    CompleteSubTaskForQuest(questId);
                }

                return;
            }

            if (evt == "map_toggle" || evt == "tutorial_map_m")
            {
                var map = UnityEngine.Object.FindObjectOfType<MapSystem>(true);
                bool open = map != null && map.IsMapWindowOpen;
                if (!_tutorialMapOpened)
                {
                    if (GameInput.GetKeyDown(GameControlAction.OpenMap) || open)
                        _tutorialMapOpened = true;
                }
                else if (!open)
                {
                    _tutorialMapOpened = false;
                    CompleteSubTaskForQuest(questId);
                }

                return;
            }

            if (evt == "social_panel_toggle" || evt == "tutorial_social_kc")
            {
                var room = UnityEngine.Object.FindObjectOfType<MultiplayerRoomCodePanelController>(true);
                var chat = UnityEngine.Object.FindObjectOfType<MultiplayerFriendChatSidebarController>(true);
                bool socialOpen = (room != null && room.IsPanelOpen) || (chat != null && chat.IsOpen);
                if (!_tutorialSocialOpened)
                {
                    if (GameInput.GetKeyDown(GameControlAction.OpenMultiplayer)
                        || GameInput.GetKeyDown(GameControlAction.OpenFriends)
                        || socialOpen)
                        _tutorialSocialOpened = true;
                }
                else if (!socialOpen)
                {
                    _tutorialSocialOpened = false;
                    CompleteSubTaskForQuest(questId);
                }

                return;
            }

            if (evt == "learn_sprint_jump" || evt == "tutorial_shift_space")
            {
                if (GameInput.GetKeyDown(GameControlAction.Run) || GameInput.GetKey(GameControlAction.Run))
                    _tutorialShiftPressed = true;
                if (GameInput.GetKeyDown(GameControlAction.Jump) || GameInput.GetKey(GameControlAction.Jump))
                    _tutorialSpacePressed = true;
                if (_tutorialShiftPressed && _tutorialSpacePressed)
                {
                    _tutorialShiftPressed = false;
                    _tutorialSpacePressed = false;
                    CompleteSubTaskForQuest(questId);
                }

                return;
            }

            if (evt == "dorm_mall_open")
            {
                if (IsAnyDormMallUiOpenInHierarchy())
                    CompleteSubTaskForQuest(questId);
                return;
            }

            if (evt == "inventory_has_item" || evt == "backpack_has_item")
            {
                if (InventoryData.GetItems().Count > 0)
                    CompleteSubTaskForQuest(questId);
                return;
            }

            if (evt == "placement_mode_enter" || evt == "placement_mode_active")
            {
                if (PlacementModeController.Instance != null &&
                    PlacementModeController.Instance.IsPlacementActive)
                    CompleteSubTaskForQuest(questId);
                return;
            }

            const string enterScenePrefix = "enter_scene:";
            if (evt.StartsWith(enterScenePrefix, StringComparison.Ordinal))
            {
                string targetScene = evt.Substring(enterScenePrefix.Length).Trim();
                if (!string.IsNullOrEmpty(targetScene) &&
                    SceneNameMatches(SceneManager.GetActiveScene().name, targetScene))
                    CompleteSubTaskForQuest(questId);
            }
        }

        void EvaluateSceneArrivalAndAutoCompleteIfNeeded()
        {
            for (int i = 0; i < questChains.Count; i++)
            {
                var quest = questChains[i];
                if (!IsQuestRunnable(quest))
                    continue;

                var sub = GetCurrentSubTaskForQuest(quest.questId);
                if (sub == null || !SubTaskSceneMatchesActiveScene(sub))
                    continue;

                if (sub.triggerType == QuestTriggerType.ManualEvent)
                {
                    string evt = (sub.triggerEventId ?? "").Trim().ToLowerInvariant();
                    const string enterScenePrefix = "enter_scene:";
                    if (evt.StartsWith(enterScenePrefix, StringComparison.Ordinal))
                        CompleteSubTaskForQuest(quest.questId);
                    continue;
                }

                if (sub.triggerType == QuestTriggerType.ReachLocation && playerTransform != null)
                {
                    float completeRadius = Mathf.Max(5f, sub.autoCompleteDistance);
                    float d = Vector3.Distance(playerTransform.position, ResolveTargetPosition(sub));
                    if (d <= completeRadius)
                        CompleteSubTaskForQuest(quest.questId);
                }
            }
        }

        public int GetDisplaySubTaskIndexForQuest(string questId)
        {
            var p = FindProgress(questId);
            return p == null ? 0 : p.currentSubTaskIndex + 1;
        }

        public int GetTotalSubTaskCountForQuest(string questId)
        {
            var q = FindQuestDefinition(questId);
            return q == null || q.subTasks == null ? 0 : q.subTasks.Count;
        }

        public int GetCounterCurrentForQuest(string questId)
        {
            var quest = FindQuestDefinition(questId);
            var progress = FindProgress(questId);
            if (quest == null || progress == null)
                return 0;
            int idx = Mathf.Clamp(progress.currentSubTaskIndex, 0, quest.subTasks.Count - 1);
            if (progress.subTaskCounter == null || idx >= progress.subTaskCounter.Length)
                return 0;
            return Mathf.Max(0, progress.subTaskCounter[idx]);
        }

        public float GetDistanceToQuestTarget(string questId)
        {
            TryResolveRefs();
            var sub = GetCurrentSubTaskForQuest(questId);
            if (sub == null || playerTransform == null)
                return -1f;
            if (!TryGetQuestGuideWorldPosition(sub, out Vector3 target))
                return -1f;
            return Vector3.Distance(playerTransform.position, target);
        }
    }
}
