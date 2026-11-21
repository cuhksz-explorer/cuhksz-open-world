using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 与 <see cref="InteractWithMouseAndMovementLock"/> 一致的交互流程：靠近显示提示、按 F 打开对话 Canvas、Esc 关闭；
/// 便于与寝室商城等 UI 共用同一套 InteractPrompt 样式与 SharedLabel 解析。
/// 对话内容仍走 LLMChatClient、头像相机与 NPC 情绪（原 CanvasInteractionUIManager 能力保留）。
/// </summary>
[DefaultExecutionOrder(550)]
public class CanvasInteractionUIManager : MonoBehaviour
{
    [Header("玩家")]
    [Tooltip("用于测距；为空时从 PlayerMovement 取 transform")]
    public Transform playerModelTransform;

    public PlayerMovement playerMovement;

    [Header("提示 UI（与 Interaction Mode2 一致）")]
    public RectTransform interactPromptPanel;

    public TextMeshProUGUI interactNameText;

    public TextMeshProUGUI interactTypeText;

    public Image interactPromptIcon;

    [Header("交互目标")]
    [Tooltip("优先使用；每项可配置名称、类型与触发 Transform")]
    public InteractProximityTarget[] proximityTargets;

    [Tooltip("未配置 proximityTargets 时，从这些 Transform 生成默认条目")]
    public Transform[] triggerObjects;

    [Tooltip("仅对 Trigger Objects 路径，或 Proximity Targets 中对应字段留空时生效；若 Proximity Targets 里已填写名称/类型，以列表为准。")]
    public string defaultDisplayName = "NPC";

    [Tooltip("同上；Proximity Targets 每行若已填写交互类型，不会被此处覆盖。")]
    public string defaultInteractionType = "对话";

    [Header("距离与按键")]
    public float iconTriggerDistance = 4f;

    public float distanceBuffer = 0.5f;

    public KeyCode activateKey = KeyCode.F;

    public bool showKeyHintOnTypeLine = true;

    [Header("对话 / LLM")]
    public GameObject dialoguePanel;

    [Tooltip("对话所在 Canvas；为空则从 dialoguePanel 上获取")]
    public Canvas dialogueTargetCanvas;

    [Header("无对话 Canvas 时仍用 F（走 InteractionNearestTriggerGate）")]
    [Tooltip("仅有测距+提示、未挂对话 Canvas 时：勾选后按 F 仍会 Submit（与 mode2 一致）。可绑定 SpawnMoveRotateDisappearSceneSwitch.OnButtonClick 等。")]
    public bool useActivateKeyWithoutDialogueCanvas;

    [Tooltip("若仅在代码里 AddListener，Inspector 持久事件数为 0 时请勾选。")]
    public bool proximityActivateKeyAllowRuntimeListeners;

    public UnityEvent onActivateKeyNoDialogueCanvas;

    public LLMChatClient_LocalQwen chatClient;

    public Button closeDialogueButton;

    [Header("头像相机")]
    public NPCPortraitCameraController portraitCamera;

    [Header("淡出")]
    public bool iconUseFade = true;

    public bool canvasUseFade = true;

    public float fadeDuration = 0.3f;

    [Header("调试")]
    [Tooltip("按 F 打开对话/交互 UI 时，在控制台打印本脚本所在物体与 Panel/Canvas。")]
    public bool logInteractionOpenToConsole = true;

    [Tooltip("为 true 时，若未绑定提示 Panel/TMP，运行时自动查找场景中的 InteractPrompt_Shared（与菜单一键配置一致），以便本物体参与共用提示并显示上面的默认名称/类型。")]
    public bool autoBindSharedInteractPromptFromScene = true;

    [Tooltip("勾选=恢复旧逻辑：必须已指定 dialogueTargetCanvas 才会执行测距与 Offer；公交站等若未绑对话 Canvas，整段靠近检测会被跳过。默认关闭；仅用于与「改前」对比排查。")]
    public bool legacyRequireDialogueCanvasForProximity = false;

    InteractProximityTarget[] _effectiveTargets;
    RectTransform _promptRoot;
    FirstPersonCamera _cachedFpsCamera;

    bool isPlayerInTriggerRange;
    bool isCanvasActive;
    CanvasGroup iconCanvasGroup;
    CanvasGroup targetCanvasGroup;
    float currentIconAlpha;
    float currentCanvasAlpha;
    int closestTargetIndex = -1;
    float _closestTriggerDistance = float.MaxValue;

    bool _chatInputFocusPending;

    /// <summary>对话面板是否处于打开（用于与其它脚本协调 Esc / 光标）。</summary>
    public bool IsDialogueOpen => DialoguePanelBlocksInteractPrompt();

    void Awake()
    {
        BindPlayerFromMovementScript();
    }

    void Start()
    {
        if (autoBindSharedInteractPromptFromScene)
        {
            InteractPromptSharedUiLocator.TryAutoBind(
                ref interactPromptPanel,
                ref interactNameText,
                ref interactTypeText);
        }

        InteractPromptSharedUiLocator.EnsureBoundToCanonicalSharedPrompt(
            ref interactPromptPanel,
            ref interactNameText,
            ref interactTypeText);

        BindPlayerFromMovementScript();
        BuildEffectiveTargets();
        ResolveDialogueCanvas();
        ResolvePromptRoot();
        InitInteractPrompt();
        InitTargetCanvas();

        if (closeDialogueButton)
            closeDialogueButton.onClick.AddListener(CloseDialogueFromExternal);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void BuildEffectiveTargets()
    {
        if (proximityTargets != null && proximityTargets.Length > 0)
        {
            _effectiveTargets = new InteractProximityTarget[proximityTargets.Length];
            for (int i = 0; i < proximityTargets.Length; i++)
            {
                var src = proximityTargets[i];
                if (src == null)
                {
                    _effectiveTargets[i] = null;
                    continue;
                }

                _effectiveTargets[i] = new InteractProximityTarget
                {
                    trigger = src.trigger,
                    displayName = string.IsNullOrWhiteSpace(src.displayName) ? defaultDisplayName : src.displayName,
                    interactionType = string.IsNullOrWhiteSpace(src.interactionType) ? defaultInteractionType : src.interactionType,
                    dialogueNpcRoot = src.dialogueNpcRoot
                };
            }
            return;
        }

        if (triggerObjects != null && triggerObjects.Length > 0)
        {
            _effectiveTargets = new InteractProximityTarget[triggerObjects.Length];
            for (int i = 0; i < triggerObjects.Length; i++)
            {
                _effectiveTargets[i] = new InteractProximityTarget
                {
                    trigger = triggerObjects[i],
                    displayName = defaultDisplayName,
                    interactionType = defaultInteractionType
                };
            }
            return;
        }

        if (playerMovement != null && playerMovement.targetNPCs != null && playerMovement.targetNPCs.Length > 0)
        {
            var arr = playerMovement.targetNPCs;
            _effectiveTargets = new InteractProximityTarget[arr.Length];
            for (int i = 0; i < arr.Length; i++)
            {
                _effectiveTargets[i] = new InteractProximityTarget
                {
                    trigger = arr[i],
                    displayName = defaultDisplayName,
                    interactionType = defaultInteractionType
                };
            }
            return;
        }

        _effectiveTargets = Array.Empty<InteractProximityTarget>();
    }

    void ResolveDialogueCanvas()
    {
        if (dialogueTargetCanvas != null)
            return;
        if (dialoguePanel != null)
            dialogueTargetCanvas = dialoguePanel.GetComponent<Canvas>();
        if (dialogueTargetCanvas == null && dialoguePanel != null)
            dialogueTargetCanvas = dialoguePanel.GetComponentInParent<Canvas>();
    }

    void ResolvePromptRoot()
    {
        _promptRoot = interactPromptPanel != null
            ? interactPromptPanel
            : (interactPromptIcon != null ? interactPromptIcon.rectTransform : null);
    }

    void Update()
    {
        BindPlayerFromMovementScript();
        if (playerModelTransform == null || playerMovement == null)
            return;

        // 默认：有触发目标即测距（不要求 dialogueTargetCanvas），否则公交站仅共用提示时也会跑靠近逻辑。
        // legacyRequireDialogueCanvasForProximity：恢复旧条件 dialogueTargetCanvas != null，用于对比测试。
        bool hasTargets = _effectiveTargets != null && _effectiveTargets.Length > 0;
        bool proximityAllowed = hasTargets
            && (!legacyRequireDialogueCanvasForProximity || dialogueTargetCanvas != null);
        if (proximityAllowed)
        {
            ReconcileInteractionStateIfUiWasClosedExternally();
            CheckPlayerDistanceToTriggers();
            if (dialogueTargetCanvas != null)
            {
                UpdateTargetCanvasVisibility();
                QueuePromptLabelOffer();
                HandleInputLogic();
                TryFocusChatInput();
            }
            else
            {
                QueuePromptLabelOffer();
                HandleInputLogic();
            }
        }
        else
        {
            isPlayerInTriggerRange = false;
            closestTargetIndex = -1;
            _closestTriggerDistance = float.MaxValue;
        }

        if (_promptRoot != null && !UsesSharedInteractPromptPanel())
        {
            if (UIInteractionState.BlocksWorldPrompt || GameplayUiHotkeyGuard.ShouldHideWorldInteractPrompt())
                UiLifecycleSafe.SetActiveIfAlive(_promptRoot.gameObject, false);
            else
                UpdatePromptPanel();
        }
    }

    void LateUpdate()
    {
        InteractPromptSharedUiLocator.EnsureBoundToCanonicalSharedPrompt(
            ref interactPromptPanel,
            ref interactNameText,
            ref interactTypeText);

        if (interactNameText == null && interactTypeText == null)
            return;
        InteractPromptSharedLabelFrame.ApplyBestToSharedTmp(interactNameText, interactTypeText);
    }

    bool UsesSharedInteractPromptPanel()
    {
        return InteractPromptSharedUiLocator.IsSharedPromptPanel(interactPromptPanel);
    }

    bool DialoguePanelBlocksInteractPrompt()
    {
        if (!isCanvasActive)
            return false;
        if (dialogueTargetCanvas == null)
            return false;
        if (!dialogueTargetCanvas.gameObject.activeInHierarchy || !dialogueTargetCanvas.enabled)
            return false;
        if (targetCanvasGroup != null)
            return targetCanvasGroup.alpha > 0.001f || targetCanvasGroup.interactable;
        return true;
    }

    void QueuePromptLabelOffer()
    {
        InteractPromptSharedUiLocator.EnsureBoundToCanonicalSharedPrompt(
            ref interactPromptPanel,
            ref interactNameText,
            ref interactTypeText);

        if (interactNameText == null && interactTypeText == null)
            return;

        if (GameplayUiHotkeyGuard.ShouldHideWorldInteractPrompt())
            return;

        if (!isPlayerInTriggerRange || DialoguePanelBlocksInteractPrompt() || closestTargetIndex < 0 ||
            closestTargetIndex >= _effectiveTargets.Length)
            return;

        var entry = _effectiveTargets[closestTargetIndex];
        string n = Loc.StoryTerm(entry.displayName ?? "");
        string t = FormatTypeLine(entry.interactionType);
        int groupId = interactNameText != null
            ? interactNameText.GetInstanceID()
            : interactTypeText.GetInstanceID();
        Vector3? anchor = InteractPromptSharedUiLocator.ResolveProximityWorldAnchor(
            entry?.trigger,
            entry?.dialogueNpcRoot);
        InteractPromptSharedLabelFrame.Offer(groupId, _closestTriggerDistance, n, t, anchor);
    }

    string FormatTypeLine(string typeLabel)
    {
        if (string.IsNullOrEmpty(typeLabel)) return "";
        typeLabel = Loc.StoryTerm(typeLabel);
        if (!showKeyHintOnTypeLine) return typeLabel;
        return Loc.F("[{0}] {1}", "[{0}] {1}", activateKey, typeLabel);
    }

    void BindPlayerFromMovementScript()
    {
        if (playerMovement == null)
            playerMovement = FindObjectOfType<PlayerMovement>(true);
        if (playerModelTransform == null && playerMovement != null)
            playerModelTransform = playerMovement.transform;
    }

    void CheckPlayerDistanceToTriggers()
    {
        Vector3 playerPos = playerModelTransform.position;

        // 进入与离开共用半径。若仅在内圈 iconTriggerDistance 判「进入」、在圈内再加 buffer 判「留下」，会出现：
        // 先在缓冲带内对话 → Esc 离开逻辑上出圈后，人仍站在同一位置，会因 d>iconTriggerDistance 永远无法再次进圈。
        float effectiveDistance = iconTriggerDistance + distanceBuffer;

        int best = -1;
        float bestD = float.MaxValue;
        for (int i = 0; i < _effectiveTargets.Length; i++)
        {
            var e = _effectiveTargets[i];
            if (e == null || e.trigger == null) continue;
            float d = Vector3.Distance(playerPos, e.trigger.position);
            if (d <= effectiveDistance && d < bestD)
            {
                bestD = d;
                best = i;
            }
        }

        bool inRange = best >= 0;
        closestTargetIndex = best;
        _closestTriggerDistance = inRange ? bestD : float.MaxValue;

        if (!inRange && isCanvasActive)
        {
            ReleaseDialogueUiFocus();
            isCanvasActive = false;
            _chatInputFocusPending = false;
            SnapDialogueCanvasFullyClosed();
            DisableInteractionMode();
        }

        isPlayerInTriggerRange = inRange;
    }

    void HandleInputLogic()
    {
        bool blockOtherGameplayInput = GameplayUiHotkeyGuard.ShouldBlockGameplayHotkeys()
                                       || GameplayUiHotkeyGuard.BlocksGameplayBecauseMultiplayerUi();

        if (isCanvasActive && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseDialogueFromExternal();
            return;
        }

        if (!isPlayerInTriggerRange || DialoguePanelBlocksInteractPrompt() || blockOtherGameplayInput)
            return;
        if (!Input.GetKeyDown(activateKey))
            return;

        float claimDistance = _closestTriggerDistance < float.MaxValue ? _closestTriggerDistance : iconTriggerDistance;

        if (dialogueTargetCanvas != null)
        {
            InteractionNearestTriggerGate.Submit(activateKey, claimDistance, GetInstanceID(), () =>
            {
                if (!this || !enabled || DialoguePanelBlocksInteractPrompt() || !isPlayerInTriggerRange)
                    return;
                isCanvasActive = true;
                _chatInputFocusPending = true;
                if (logInteractionOpenToConsole)
                    LogInteractionUiOpened();
                if (!dialogueTargetCanvas.gameObject.activeSelf)
                    UiLifecycleSafe.SetActiveIfAlive(dialogueTargetCanvas.gameObject, true);
                // 与 mode2 同理：先打开 Canvas，避免下一帧被 Reconcile 判成“外部关闭”。
                dialogueTargetCanvas.enabled = true;
                EnableInteractionMode();
                OpenDialogueForCurrentTarget();
            }, priority: 170);
            return;
        }

        if (!useActivateKeyWithoutDialogueCanvas || onActivateKeyNoDialogueCanvas == null)
            return;
        bool hasInspectorListeners = onActivateKeyNoDialogueCanvas.GetPersistentEventCount() > 0;
        if (!hasInspectorListeners && !proximityActivateKeyAllowRuntimeListeners)
            return;

        InteractionNearestTriggerGate.Submit(activateKey, claimDistance, GetInstanceID(), () =>
        {
            if (!this || !enabled || DialoguePanelBlocksInteractPrompt() || !isPlayerInTriggerRange)
                return;
            onActivateKeyNoDialogueCanvas.Invoke();
        }, priority: 170);
    }

    void LogInteractionUiOpened()
    {
        string host = gameObject != null
            ? $"{gameObject.name} (场景: {gameObject.scene.name})"
            : "(null)";
        string panelName = dialoguePanel != null ? dialoguePanel.name : "null";
        string canvasName = dialogueTargetCanvas != null ? dialogueTargetCanvas.gameObject.name : "null";
        string triggerInfo = "null";
        if (closestTargetIndex >= 0 && closestTargetIndex < _effectiveTargets.Length)
        {
            var e = _effectiveTargets[closestTargetIndex];
            if (e.trigger != null)
                triggerInfo = e.trigger.name;
        }

        Debug.Log(
            "[InteractionUI] CanvasInteractionUIManager 宿主物体: " + host +
            " | dialoguePanel: " + panelName +
            " | dialogueTargetCanvas: " + canvasName +
            " | 当前触发点 trigger: " + triggerInfo);
    }

    void OpenDialogueForCurrentTarget()
    {
        if (chatClient != null && dialogueTargetCanvas != null)
            chatClient.EnsureBoundUnderCanvas(dialogueTargetCanvas);

        if (dialoguePanel != null && !dialoguePanel.activeSelf)
            UiLifecycleSafe.SetActiveIfAlive(dialoguePanel, true);

        Transform currentNpc = null;
        if (closestTargetIndex >= 0 && closestTargetIndex < _effectiveTargets.Length)
        {
            var e = _effectiveTargets[closestTargetIndex];
            currentNpc = e.dialogueNpcRoot != null ? e.dialogueNpcRoot : e.trigger;
        }

        if (currentNpc != null)
        {
            if (portraitCamera != null)
            {
                Transform anchor = currentNpc.Find("PortraitAnchor");
                portraitCamera.SetTarget(anchor != null ? anchor : currentNpc);
            }

            if (chatClient != null)
            {
                var emo = currentNpc.GetComponent<NPCEmotionController>();
                if (emo != null)
                {
                    chatClient.npcEmotion = emo;
                    emo.TriggerStartConversation();
                }
            }
        }
        else if (chatClient != null && chatClient.npcEmotion != null)
        {
            chatClient.npcEmotion.TriggerStartConversation();
        }
    }

    void TryFocusChatInput()
    {
        if (!_chatInputFocusPending || !isCanvasActive || !DialoguePanelBlocksInteractPrompt() || chatClient == null || chatClient.inputField == null)
            return;
        if (canvasUseFade && targetCanvasGroup != null && currentCanvasAlpha < 0.99f)
            return;
        chatClient.inputField.ActivateInputField();
        _chatInputFocusPending = false;
    }

    void EnableInteractionMode()
    {
        if (_cachedFpsCamera == null)
            _cachedFpsCamera = FindObjectOfType<FirstPersonCamera>(true);
        if (_cachedFpsCamera != null)
            _cachedFpsCamera.SetCursorLocked(false);
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        if (playerMovement != null)
            playerMovement.enabled = false;
    }

    void DisableInteractionMode()
    {
        if (_cachedFpsCamera == null)
            _cachedFpsCamera = FindObjectOfType<FirstPersonCamera>(true);
        if (_cachedFpsCamera != null)
            _cachedFpsCamera.SetCursorLocked(true);
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        if (playerMovement != null)
            playerMovement.enabled = true;
    }

    public void ClearCachedFirstPersonCamera()
    {
        _cachedFpsCamera = null;
    }

    void InitInteractPrompt()
    {
        if (_promptRoot == null) return;
        if (UsesSharedInteractPromptPanel())
            return;

        UiLifecycleSafe.SetActiveIfAlive(_promptRoot.gameObject, false);

        if (iconUseFade)
        {
            iconCanvasGroup = _promptRoot.GetComponent<CanvasGroup>();
            if (iconCanvasGroup == null)
                iconCanvasGroup = _promptRoot.gameObject.AddComponent<CanvasGroup>();
            iconCanvasGroup.alpha = 0f;
            iconCanvasGroup.interactable = false;
            iconCanvasGroup.blocksRaycasts = false;
        }

        _promptRoot.localScale = Vector3.one;

        if (interactPromptIcon != null)
            UiLifecycleSafe.SetActiveIfAlive(interactPromptIcon.gameObject, interactPromptIcon.sprite != null);
    }

    void UpdatePromptPanel()
    {
        if (_promptRoot == null) return;
        GameObject promptGo = _promptRoot.gameObject;
        if (promptGo == null || !promptGo.scene.IsValid() || !promptGo.scene.isLoaded)
            return;

        int sharedGid = InteractPromptSharedLabelFrame.ResolveOfferGroupId(interactNameText, interactTypeText);
        bool externalOffer = sharedGid != 0 && InteractPromptSharedLabelFrame.HasPendingOfferForGroupId(sharedGid);
        bool shouldShow = (isPlayerInTriggerRange && !DialoguePanelBlocksInteractPrompt()) || externalOffer;

        if (iconUseFade && iconCanvasGroup != null)
        {
            if (shouldShow && currentIconAlpha < 1f)
            {
                UiLifecycleSafe.SetActiveIfAlive(promptGo, true);
                currentIconAlpha += Time.unscaledDeltaTime / fadeDuration;
                currentIconAlpha = Mathf.Clamp01(currentIconAlpha);
                iconCanvasGroup.alpha = currentIconAlpha;
            }
            else if (!shouldShow && currentIconAlpha > 0f)
            {
                currentIconAlpha -= Time.unscaledDeltaTime / fadeDuration;
                currentIconAlpha = Mathf.Clamp01(currentIconAlpha);
                iconCanvasGroup.alpha = currentIconAlpha;
                if (currentIconAlpha <= 0f)
                {
                    UiLifecycleSafe.SetActiveIfAlive(promptGo, false);
                    _promptRoot.localScale = Vector3.one;
                }
            }
            else if (shouldShow && currentIconAlpha >= 1f)
            {
                UiLifecycleSafe.SetActiveIfAlive(promptGo, true);
                iconCanvasGroup.alpha = 1f;
            }
        }
        else
        {
            UiLifecycleSafe.SetActiveIfAlive(promptGo, shouldShow);
            currentIconAlpha = shouldShow ? 1f : 0f;
            if (iconCanvasGroup != null)
                iconCanvasGroup.alpha = currentIconAlpha;
            if (!shouldShow)
                _promptRoot.localScale = Vector3.one;
        }

        if (interactPromptIcon != null && interactPromptIcon.sprite != null)
            interactPromptIcon.enabled = shouldShow && currentIconAlpha > 0.01f;

        if (_promptRoot != null)
            _promptRoot.localScale = Vector3.one;
    }

    void InitTargetCanvas()
    {
        if (dialogueTargetCanvas == null) return;
        dialogueTargetCanvas.enabled = false;
        if (dialoguePanel != null)
            UiLifecycleSafe.SetActiveIfAlive(dialoguePanel, false);

        targetCanvasGroup = dialogueTargetCanvas.GetComponent<CanvasGroup>();
        if (targetCanvasGroup == null)
            targetCanvasGroup = dialogueTargetCanvas.gameObject.AddComponent<CanvasGroup>();
        targetCanvasGroup.alpha = 0f;
        targetCanvasGroup.interactable = false;
        targetCanvasGroup.blocksRaycasts = false;
    }

    void UpdateTargetCanvasVisibility()
    {
        if (dialogueTargetCanvas == null) return;
        GameObject dgo = dialogueTargetCanvas.gameObject;
        if (dgo == null || !dgo.scene.IsValid() || !dgo.scene.isLoaded)
            return;

        bool shouldShow = isCanvasActive;

        if (targetCanvasGroup != null)
        {
            if (shouldShow && currentCanvasAlpha < 1f)
            {
                dialogueTargetCanvas.enabled = true;
                if (dialoguePanel != null && !dialoguePanel.activeSelf)
                    UiLifecycleSafe.SetActiveIfAlive(dialoguePanel, true);
                currentCanvasAlpha += Time.unscaledDeltaTime / fadeDuration;
                currentCanvasAlpha = Mathf.Clamp01(currentCanvasAlpha);
                targetCanvasGroup.alpha = currentCanvasAlpha;
                if (currentCanvasAlpha >= 1f)
                {
                    targetCanvasGroup.interactable = true;
                    targetCanvasGroup.blocksRaycasts = true;
                }
            }
            else if (!shouldShow && currentCanvasAlpha > 0f)
            {
                targetCanvasGroup.interactable = false;
                targetCanvasGroup.blocksRaycasts = false;
                currentCanvasAlpha -= Time.unscaledDeltaTime / fadeDuration;
                currentCanvasAlpha = Mathf.Clamp01(currentCanvasAlpha);
                targetCanvasGroup.alpha = currentCanvasAlpha;
                if (currentCanvasAlpha <= 0f)
                {
                    dialogueTargetCanvas.enabled = false;
                    if (dialoguePanel != null)
                        UiLifecycleSafe.SetActiveIfAlive(dialoguePanel, false);
                }
            }
        }
        else
        {
            dialogueTargetCanvas.enabled = shouldShow;
            if (dialoguePanel != null)
                UiLifecycleSafe.SetActiveIfAlive(dialoguePanel, shouldShow);
            currentCanvasAlpha = shouldShow ? 1f : 0f;
        }
    }

    public void CloseDialogueFromExternal()
    {
        if (!isCanvasActive) return;
        InteractionEscGate.SuppressFpsEscForThisFrame();
        ReleaseDialogueUiFocus();
        isCanvasActive = false;
        _chatInputFocusPending = false;
        SnapDialogueCanvasFullyClosed();
        DisableInteractionMode();
    }

    /// <summary>
    /// 关闭窗口时取消输入焦点，避免 EventSystem 仍指着已隐藏的 TMP_InputField，导致 F 被 UI 吞掉或逻辑仍认为在对话中。
    /// </summary>
    void ReleaseDialogueUiFocus()
    {
        if (chatClient != null && chatClient.inputField != null)
            chatClient.inputField.DeactivateInputField();

        var es = EventSystem.current;
        if (es != null)
            es.SetSelectedGameObject(null, null);
    }

    /// <summary>
    /// 若关闭按钮 / 动画等只 SetActive 了界面却未走 CloseDialogueFromExternal，isCanvasActive 会卡住，表现为无法再次按 F、提示也不出现。
    /// </summary>
    void ReconcileInteractionStateIfUiWasClosedExternally()
    {
        if (!isCanvasActive) return;

        bool canvasGone = dialogueTargetCanvas == null
                          || !dialogueTargetCanvas.gameObject.activeInHierarchy
                          || !dialogueTargetCanvas.enabled;
        bool panelGone = dialoguePanel != null && !dialoguePanel.activeInHierarchy;

        if (!canvasGone && !panelGone) return;

        InteractionEscGate.SuppressFpsEscForThisFrame();
        ReleaseDialogueUiFocus();
        isCanvasActive = false;
        _chatInputFocusPending = false;
        SnapDialogueCanvasFullyClosed();
        DisableInteractionMode();
    }

    /// <summary>
    /// 关闭对话 UI 时立即拉齐 alpha / SetActive，避免淡出未完成或与其它 Esc 处理冲突导致卡住。
    /// </summary>
    void SnapDialogueCanvasFullyClosed()
    {
        currentCanvasAlpha = 0f;
        if (dialogueTargetCanvas == null) return;
        GameObject dgo = dialogueTargetCanvas.gameObject;
        if (dgo == null || !dgo.scene.IsValid() || !dgo.scene.isLoaded)
            return;
        if (targetCanvasGroup != null)
        {
            targetCanvasGroup.alpha = 0f;
            targetCanvasGroup.interactable = false;
            targetCanvasGroup.blocksRaycasts = false;
        }
        dialogueTargetCanvas.enabled = false;
        if (dialoguePanel != null)
            UiLifecycleSafe.SetActiveIfAlive(dialoguePanel, false);
    }
}
