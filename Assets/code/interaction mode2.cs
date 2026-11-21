using System;
using GameControls;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// ????????????????????? + ??????????????????? Canvas??ESC ????
/// ???? <see cref="proximityTargets"/>???????? <see cref="triggerObjects"/> ?????????/?????
/// ??????????????????????????????? <see cref="UpdatePromptPanel"/> ????????????????????
/// </summary>
[DefaultExecutionOrder(550)]
public class InteractWithMouseAndMovementLock : MonoBehaviour
{
    [Header("???????")]
    [Tooltip("??????????????????????????? CharacterController ???")]
    public Transform playerModelTransform;

    [Tooltip("???????????????????????????????")]
    public PlayerMovement playerMovement;

    [Header("??????? UI")]
    [Tooltip("???????????????????????????????")]
    public RectTransform interactPromptPanel;

    [Tooltip("????? TMP")]
    public TextMeshProUGUI interactNameText;

    [Tooltip("????? TMP????? [F] ??")]
    public TextMeshProUGUI interactTypeText;

    [Tooltip("???????? Sprite ????????????")]
    public Image interactPromptIcon;

    [Tooltip("?? F ?????????? Canvas")]
    public Canvas targetCanvas;

    [Header("无 targetCanvas 时仍用 F（走 InteractionNearestTriggerGate）")]
    [Tooltip("公交站等仅测距+提示、没有可打开 Canvas 时：勾选后按 F 仍会 Submit 到门控（与载具/联机等同键仲裁）。请在下方绑定事件，例如 SpawnMoveRotateDisappearSceneSwitch.OnButtonClick。")]
    public bool useActivateKeyWithoutTargetCanvas;

    [Tooltip("若仅在代码里对 onActivateKeyNoTargetCanvas.AddListener，Inspector 持久事件数为 0，请勾选此项，否则不会 Submit（避免空事件仍抢走 F）。")]
    public bool proximityActivateKeyAllowRuntimeListeners;

    [Tooltip("无 targetCanvas 且在范围内按激活键时调用（经 LateUpdate 门控执行）。可绑定公交车传送脚本的 OnButtonClick。")]
    public UnityEvent onActivateKeyNoTargetCanvas;

    [Header("???????????")]
    [Tooltip("?????????????????????????????????????????????? InteractPromptSharedResolver??")]
    public bool useSharedPromptResolution;

    [Header("??????????????")]
    public InteractProximityTarget[] proximityTargets;

    [Header("??????????????")]
    [Tooltip("??????? Proximity Targets ???????")]
    public Transform[] triggerObjects;

    [Tooltip("?? triggerObjects ?????????")]
    public string defaultDisplayName = "交互点";

    [Tooltip("同上；Proximity Targets 每行若已填写交互类型，不会被此处覆盖。")]
    public string defaultInteractionType = "交互";

    [Header("????????")]
    [Tooltip("????????????????")]
    public float iconTriggerDistance = 3.5f;

    [Tooltip("?????????????????????????[??")]
    public float distanceBuffer = 0.5f;

    public KeyCode activateKey = KeyCode.F;

    [Tooltip("??????????? [????]")]
    public bool showKeyHintOnTypeLine = true;

    [Header("????")]
    public bool iconUseFade = true;
    public bool canvasUseFade = true;
    public float fadeDuration = 0.3f;

    [Header("Debug")]
    [Tooltip("Log to Console when interaction Canvas opens (host GameObject and targetCanvas).")]
    public bool logInteractionOpenToConsole = true;

    [Tooltip("If prompt Panel/TMP are empty, find InteractPrompt_Shared at runtime (same as Tools menu setup).")]
    public bool autoBindSharedInteractPromptFromScene = true;

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

    void Awake()
    {
        if (autoBindSharedInteractPromptFromScene)
        {
            InteractPromptSharedUiLocator.TryAutoBind(
                ref interactPromptPanel,
                ref interactNameText,
                ref interactTypeText);
        }

        BuildEffectiveTargets();
        BindPlayerFromMovementScript();
    }

    void Start()
    {
        InteractPromptSharedUiLocator.EnsureBoundToCanonicalSharedPrompt(
            ref interactPromptPanel,
            ref interactNameText,
            ref interactTypeText);

        InitMouseState();
        BindPlayerFromMovementScript();
        ResolvePromptRoot();
        InitInteractPrompt();
        InitTargetCanvas();
        BusStopCanvasLocalizer.LocalizeCanvas(targetCanvas);
        CheckReferences();
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

        _effectiveTargets = Array.Empty<InteractProximityTarget>();
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

        // 测距 / Offer 只依赖触发点与 TMP；勿要求 targetCanvas 已赋值，否则 Canvas 丢引用时整段逻辑被跳过，靠近也无提示。
        bool hasTargets = _effectiveTargets != null && _effectiveTargets.Length > 0;
        if (hasTargets)
        {
            ReconcileCanvasStateIfHiddenExternally();
            CheckPlayerDistanceToTriggers();
            if (targetCanvas != null)
            {
                UpdateTargetCanvasVisibility();
                QueuePromptLabelOffer();
            }
            else
                QueuePromptLabelOffer();

            // 无 targetCanvas 时也必须跑 HandleInputLogic，才会向 InteractionNearestTriggerGate 提交 F（否则只有提示、按键无响应）。
            HandleInputLogic();
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

    /// <summary>
    /// 若仅用 isCanvasActive，在「面板已淡出/关闭但标志未同步」时会永远挡住 Offer 与底部提示。
    /// 用 Canvas 实际可见性判断交互是否仍占用提示条。
    /// </summary>
    bool InteractionPanelBlocksInteractPrompt()
    {
        if (!isCanvasActive)
            return false;
        // 公交站等 useActivateKeyWithoutTargetCanvas 无 targetCanvas，不应因此挡住共用提示。
        if (targetCanvas == null)
            return false;
        if (!targetCanvas.gameObject.activeInHierarchy || !targetCanvas.enabled)
            return false;
        if (targetCanvasGroup != null)
            return targetCanvasGroup.alpha > 0.001f || targetCanvasGroup.interactable;
        return true;
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

    /// <summary>
    /// ??? Interact ???????? TMP ????????? Update ???????/??????????????????????????
    /// ????????????????????? LateUpdate ??????????????
    /// </summary>
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

        if (!isPlayerInTriggerRange || InteractionPanelBlocksInteractPrompt() || closestTargetIndex < 0 ||
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
        return Loc.F("[{0}] {1}", "[{0}] {1}", GameInput.FormatChord(GameControlAction.Interact), typeLabel);
    }

    #region ?????????

    void InitMouseState()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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

    #endregion

    void BindPlayerFromMovementScript()
    {
        if (playerModelTransform != null)
            return;
        if (playerMovement == null)
            playerMovement = FindObjectOfType<PlayerMovement>(true);
        if (playerMovement != null)
        {
            playerModelTransform = playerMovement.transform;
            Debug.Log($"[Interact] ??? PlayerMovement ?? Transform??{playerModelTransform.name}");
        }
    }

    void CheckReferences()
    {
        if (playerModelTransform == null)
            Debug.LogError("[Interact] ?????? playerModelTransform??");
        if (playerMovement == null)
            Debug.LogError("[Interact] ????? PlayerMovement??");
        if (_promptRoot == null)
            Debug.LogError("[Interact] ?????? interactPromptPanel ?? interactPromptIcon??");
        if (interactNameText == null && interactTypeText == null)
            Debug.LogWarning("[Interact] interactNameText / interactTypeText ??????");
        if (targetCanvas == null)
            Debug.LogError("[Interact] ?????? targetCanvas??");
        if (_effectiveTargets == null || _effectiveTargets.Length == 0)
            Debug.LogWarning("[Interact] ?????? Proximity Targets ?? Trigger Objects??");
        if (useSharedPromptResolution)
            Debug.LogWarning("[Interact]?????????????????????????? Use Shared Prompt Resolution??????? InteractPrompt_Shared ??? InteractPromptSharedResolver ?????????????? CanvasGroup????");
    }

    void CheckPlayerDistanceToTriggers()
    {
        Vector3 playerPos = playerModelTransform.position;

        // ?? CanvasInteractionUIManager ?????????????????????????????????????????
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

        bool isInRange = best >= 0;
        closestTargetIndex = best;
        _closestTriggerDistance = isInRange ? bestD : float.MaxValue;

        if (!isInRange && isCanvasActive)
        {
            ReleaseMode2UiFocus();
            isCanvasActive = false;
            SnapTargetCanvasFullyClosed();
            DisableInteractionMode();
        }

        isPlayerInTriggerRange = isInRange;
    }

    void LogInteractionUiOpened()
    {
        string host = gameObject != null
            ? $"{gameObject.name} (????: {gameObject.scene.name})"
            : "(null)";
        string canvasName = targetCanvas != null ? targetCanvas.gameObject.name : "null";
        string triggerInfo = "null";
        if (closestTargetIndex >= 0 && closestTargetIndex < _effectiveTargets.Length)
        {
            var e = _effectiveTargets[closestTargetIndex];
            if (e.trigger != null)
                triggerInfo = e.trigger.name;
        }

        Debug.Log(
            "[InteractionUI] InteractWithMouseAndMovementLock ????????: " + host +
            " | targetCanvas (Panel): " + canvasName +
            " | ????????? trigger: " + triggerInfo);
    }

    void HandleInputLogic()
    {
        bool blockOtherGameplayInput = GameplayUiHotkeyGuard.ShouldBlockGameplayHotkeys()
                                       || GameplayUiHotkeyGuard.BlocksGameplayBecauseMultiplayerUi();

        if (isCanvasActive && Input.GetKeyDown(KeyCode.Escape))
        {
            InteractionEscGate.SuppressFpsEscForThisFrame();
            ReleaseMode2UiFocus();
            isCanvasActive = false;
            SnapTargetCanvasFullyClosed();
            DisableInteractionMode();
            return;
        }

        if (!isPlayerInTriggerRange || InteractionPanelBlocksInteractPrompt() || blockOtherGameplayInput)
            return;
        if (!GameInput.GetKeyDown(GameControlAction.Interact))
            return;

        var gateKey = GameInput.GetPrimaryKey(GameControlAction.Interact);
        float claimDistance = _closestTriggerDistance < float.MaxValue ? _closestTriggerDistance : iconTriggerDistance;

        if (targetCanvas != null)
        {
            InteractionNearestTriggerGate.Submit(gateKey, claimDistance, GetInstanceID(), () =>
            {
                if (!this || !enabled || InteractionPanelBlocksInteractPrompt() || !isPlayerInTriggerRange)
                    return;
                isCanvasActive = true;
                BusStopCanvasLocalizer.LocalizeCanvas(targetCanvas);
                if (logInteractionOpenToConsole)
                    LogInteractionUiOpened();
                if (targetCanvas != null && !targetCanvas.gameObject.activeSelf)
                    UiLifecycleSafe.SetActiveIfAlive(targetCanvas.gameObject, true);
                // 关键：仲裁改到 LateUpdate 后，下一帧 Reconcile 会先检查 enabled；
                // 这里先置 true，避免被误判为“外部关闭”而立刻打断。
                if (targetCanvas != null)
                    targetCanvas.enabled = true;
                EnableInteractionMode();
            }, priority: 160);
            return;
        }

        if (!useActivateKeyWithoutTargetCanvas || onActivateKeyNoTargetCanvas == null)
            return;
        bool hasInspectorListeners = onActivateKeyNoTargetCanvas.GetPersistentEventCount() > 0;
        if (!hasInspectorListeners && !proximityActivateKeyAllowRuntimeListeners)
            return;

        InteractionNearestTriggerGate.Submit(gateKey, claimDistance, GetInstanceID(), () =>
        {
            if (!this || !enabled || InteractionPanelBlocksInteractPrompt() || !isPlayerInTriggerRange)
                return;
            onActivateKeyNoTargetCanvas.Invoke();
        }, priority: 160);
    }

    bool UsesSharedInteractPromptPanel()
    {
        return InteractPromptSharedUiLocator.IsSharedPromptPanel(interactPromptPanel);
    }

    void InitInteractPrompt()
    {
        if (_promptRoot == null) return;
        // 共用底栏由 InteractPromptSharedLabelFrame + Locator 统一显隐，勿在此 Disable/淡入淡出（多实例会互相关掉）。
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

        bool shouldShow = ShouldShowInteractPromptPanel();

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
                    ResetPromptAnimation();
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
            if (!shouldShow) ResetPromptAnimation();
        }

        if (interactPromptIcon != null && interactPromptIcon.sprite != null)
            interactPromptIcon.enabled = shouldShow && currentIconAlpha > 0.01f;

        if (_promptRoot != null)
            _promptRoot.localScale = Vector3.one;
    }

    void ResetPromptAnimation()
    {
        if (_promptRoot != null)
            _promptRoot.localScale = Vector3.one;
    }

    int ResolveSharedPromptGroupId()
    {
        if (interactNameText == null && interactTypeText == null && autoBindSharedInteractPromptFromScene)
        {
            RectTransform panelIgnored = interactPromptPanel;
            InteractPromptSharedUiLocator.TryAutoBind(
                ref panelIgnored,
                ref interactNameText,
                ref interactTypeText);
        }

        return InteractPromptSharedLabelFrame.ResolveOfferGroupId(interactNameText, interactTypeText);
    }

    /// <summary>
    /// 共用 InteractPrompt_Shared 时：本帧任意交互器 Offer 过也应显示（避免多个 InteractWithMouseAndMovementLock 互相把面板关掉）。
    /// </summary>
    bool ShouldShowInteractPromptPanel()
    {
        if (GameplayUiHotkeyGuard.ShouldHideWorldInteractPrompt())
            return false;

        int sharedGid = ResolveSharedPromptGroupId();
        bool globalOffer = sharedGid != 0 &&
                           (InteractPromptSharedLabelFrame.HasOfferThisFrame(sharedGid) ||
                            InteractPromptSharedLabelFrame.HasPendingOfferForGroupId(sharedGid));
        bool localOffer = isPlayerInTriggerRange && !InteractionPanelBlocksInteractPrompt();
        return globalOffer || localOffer;
    }

    void SnapTargetCanvasFullyClosed()
    {
        if (targetCanvas == null) return;
        currentCanvasAlpha = 0f;
        if (targetCanvasGroup != null)
        {
            targetCanvasGroup.alpha = 0f;
            targetCanvasGroup.interactable = false;
            targetCanvasGroup.blocksRaycasts = false;
        }
        targetCanvas.enabled = false;
        UiLifecycleSafe.SetActiveIfAlive(targetCanvas.gameObject, false);
    }

    void InitTargetCanvas()
    {
        if (targetCanvas == null) return;
        // 强制整面板隐藏，避免“Canvas.enabled=false 但子层仍可见”的常亮情况。
        UiLifecycleSafe.SetActiveIfAlive(targetCanvas.gameObject, false);
        targetCanvas.enabled = false;

        targetCanvasGroup = targetCanvas.GetComponent<CanvasGroup>();
        if (targetCanvasGroup == null)
            targetCanvasGroup = targetCanvas.gameObject.AddComponent<CanvasGroup>();
        targetCanvasGroup.alpha = 0f;
        targetCanvasGroup.interactable = false;
        targetCanvasGroup.blocksRaycasts = false;
    }

    void UpdateTargetCanvasVisibility()
    {
        bool shouldShowCanvas = isCanvasActive;

        if (targetCanvasGroup != null)
        {
            if (shouldShowCanvas && currentCanvasAlpha < 1f)
            {
                if (!targetCanvas.gameObject.activeSelf)
                    UiLifecycleSafe.SetActiveIfAlive(targetCanvas.gameObject, true);
                targetCanvas.enabled = true;
                currentCanvasAlpha += Time.unscaledDeltaTime / fadeDuration;
                currentCanvasAlpha = Mathf.Clamp01(currentCanvasAlpha);
                targetCanvasGroup.alpha = currentCanvasAlpha;
                if (currentCanvasAlpha >= 1f)
                {
                    targetCanvasGroup.interactable = true;
                    targetCanvasGroup.blocksRaycasts = true;
                }
            }
            else if (!shouldShowCanvas && currentCanvasAlpha > 0f)
            {
                targetCanvasGroup.interactable = false;
                targetCanvasGroup.blocksRaycasts = false;
                currentCanvasAlpha -= Time.unscaledDeltaTime / fadeDuration;
                currentCanvasAlpha = Mathf.Clamp01(currentCanvasAlpha);
                targetCanvasGroup.alpha = currentCanvasAlpha;
                if (currentCanvasAlpha <= 0f)
                {
                    targetCanvas.enabled = false;
                    UiLifecycleSafe.SetActiveIfAlive(targetCanvas.gameObject, false);
                }
            }
        }
        else
        {
            if (shouldShowCanvas && !targetCanvas.gameObject.activeSelf)
                UiLifecycleSafe.SetActiveIfAlive(targetCanvas.gameObject, true);
            targetCanvas.enabled = shouldShowCanvas;
            currentCanvasAlpha = shouldShowCanvas ? 1f : 0f;
            if (!shouldShowCanvas)
                UiLifecycleSafe.SetActiveIfAlive(targetCanvas.gameObject, false);
        }
    }

    /// <summary>
    /// ????????????? Canvas ??????????
    /// </summary>
    public void CloseCanvasExternally()
    {
        if (!isCanvasActive) return;
        InteractionEscGate.SuppressFpsEscForThisFrame();
        ReleaseMode2UiFocus();
        isCanvasActive = false;
        SnapTargetCanvasFullyClosed();
        DisableInteractionMode();
    }

    void ReleaseMode2UiFocus()
    {
        var es = EventSystem.current;
        if (es == null) return;
        es.SetSelectedGameObject(null, null);
    }

    void ReconcileCanvasStateIfHiddenExternally()
    {
        if (!isCanvasActive || targetCanvas == null) return;
        if (targetCanvas.gameObject.activeInHierarchy && targetCanvas.enabled) return;

        InteractionEscGate.SuppressFpsEscForThisFrame();
        ReleaseMode2UiFocus();
        isCanvasActive = false;
        currentCanvasAlpha = 0f;
        SnapTargetCanvasFullyClosed();
        DisableInteractionMode();
    }

    /// <summary>
    /// ??????????? Canvas??
    /// </summary>
    /// <summary>交互面板是否仍实际占用输入/挡住世界交互（与 isCanvasActive 不同：后者在淡出后可能未同步）。</summary>
    public bool IsInteractionCanvasOpen => InteractionPanelBlocksInteractPrompt();

    public void ClearCachedFirstPersonCamera()
    {
        _cachedFpsCamera = null;
    }
}

/// <summary>
/// 多系统共用同一套 InteractNameText / InteractTypeText（公交站、载具、联机近距等）时，按距离合并本帧最佳 Offer，在 LateUpdate 写入 TMP。
/// 改动原因简述：
/// 1) Offer 内每帧首次调用时 Clear：避免上帧残留键导致「人走远了仍显示旧提示」。
/// 2) Apply 开头若本帧无任何 Offer（f != s_offerFrame）则 Clear：玩家已离开且无人再 Offer 时，在 LateUpdate 清掉字典，避免 HasPendingOffer / 文案残留。
/// 3) 文案写入每帧只执行一次；面板显隐每帧以 Offer 字典为准同步（避免先执行的 LateUpdate 无 Offer 却把面板关掉）。
/// </summary>
static class InteractPromptSharedLabelFrame
{
    struct Row
    {
        public float BestDistance;
        public string Name;
        public string TypeLine;
        public Vector3 WorldAnchor;
        public bool HasWorldAnchor;
    }

    static readonly System.Collections.Generic.Dictionary<int, Row> s_offers =
        new System.Collections.Generic.Dictionary<int, Row>(8);

    static readonly System.Collections.Generic.HashSet<int> s_appliedThisFrame =
        new System.Collections.Generic.HashSet<int>();

    static int s_frame = -1;

    /// <summary>与 Offer 使用相同的 groupId（优先 Name TMP 的 InstanceID）。</summary>
    public static int ResolveOfferGroupId(TextMeshProUGUI nameField, TextMeshProUGUI typeField)
    {
        if (nameField != null) return nameField.GetInstanceID();
        if (typeField != null) return typeField.GetInstanceID();
        return 0;
    }

    static int ResolveCanonicalOfferGroupId()
    {
        if (!InteractPromptSharedUiLocator.TryGetCanonical(out _, out var nameTmp, out var typeTmp))
            return 0;
        return ResolveOfferGroupId(nameTmp, typeTmp);
    }

    /// <summary>本帧是否已有系统对同一 TMP 组提交过 Offer（在 LateUpdate Apply 之前有效）。</summary>
    public static bool HasPendingOfferForGroupId(int nameOrTypeTmpInstanceId)
    {
        if (nameOrTypeTmpInstanceId == 0) return false;
        return s_offers.TryGetValue(nameOrTypeTmpInstanceId, out var row)
            && row.BestDistance < float.MaxValue - 1f;
    }

    /// <summary>本帧是否已调用过 Offer()（用于 Update 中在 Offer 之前判断全局提示）。</summary>
    public static bool HasOfferThisFrame(int nameOrTypeTmpInstanceId)
    {
        if (nameOrTypeTmpInstanceId == 0) return false;
        return Time.frameCount == s_offerFrame && HasPendingOfferForGroupId(nameOrTypeTmpInstanceId);
    }

    static int s_offerFrame = -1;

    public static void Offer(int nameOrTypeTmpInstanceId, float distanceToTrigger, string displayName, string typeLine)
    {
        Offer(nameOrTypeTmpInstanceId, distanceToTrigger, displayName, typeLine, null);
    }

    /// <param name="worldAnchor">交互目标世界坐标（一般为 trigger/NPC 脚底）；为 null 时保持屏幕底部默认位置。</param>
    public static void Offer(
        int nameOrTypeTmpInstanceId,
        float distanceToTrigger,
        string displayName,
        string typeLine,
        Vector3? worldAnchor)
    {
        int canonicalGroupId = ResolveCanonicalOfferGroupId();
        if (canonicalGroupId != 0)
            nameOrTypeTmpInstanceId = canonicalGroupId;

        int f = Time.frameCount;
        if (f != s_offerFrame)
        {
            s_offerFrame = f;
            s_offers.Clear();
        }

        if (!s_offers.TryGetValue(nameOrTypeTmpInstanceId, out var row) || distanceToTrigger < row.BestDistance)
        {
            row.BestDistance = distanceToTrigger;
            row.Name = displayName ?? "";
            row.TypeLine = typeLine ?? "";
            if (worldAnchor.HasValue)
            {
                row.WorldAnchor = worldAnchor.Value;
                row.HasWorldAnchor = true;
            }
            // 更近的 Offer 未带锚点时保留上一候选的 WorldAnchor，避免椅子等近距离提示盖掉 NPC/载具的屏幕跟随。
            s_offers[nameOrTypeTmpInstanceId] = row;
        }
    }

    public static bool TryGetActiveWorldAnchor(int nameOrTypeTmpInstanceId, out Vector3 worldAnchor)
    {
        worldAnchor = default;
        if (Time.frameCount != s_offerFrame)
            return false;

        if (TryGetBestOfferThisFrame(out var bestRow) && bestRow.HasWorldAnchor)
        {
            worldAnchor = bestRow.WorldAnchor;
            return true;
        }

        if (nameOrTypeTmpInstanceId == 0)
            return false;
        if (!s_offers.TryGetValue(nameOrTypeTmpInstanceId, out var row) || !row.HasWorldAnchor)
            return false;
        worldAnchor = row.WorldAnchor;
        return true;
    }

    static bool TryGetBestOfferThisFrame(out Row bestRow)
    {
        bestRow = default;
        if (Time.frameCount != s_offerFrame || s_offers.Count == 0)
            return false;

        float bestDistance = float.MaxValue;
        bool found = false;
        foreach (var kv in s_offers)
        {
            if (kv.Value.BestDistance >= float.MaxValue - 1f)
                continue;
            if (!found || kv.Value.BestDistance < bestDistance)
            {
                bestDistance = kv.Value.BestDistance;
                bestRow = kv.Value;
                found = true;
            }
        }

        return found;
    }

    static bool HasAnyActiveOfferThisFrame()
    {
        return TryGetBestOfferThisFrame(out _);
    }

    /// <summary>在所有交互器 LateUpdate 之后统一写入 canonical 共用面板，避免多脚本 Apply 互相把面板关掉。</summary>
    public static void FinalizeSharedPanelForFrame()
    {
        InteractPromptSharedUiLocator.EnsureGameplayHudCanvasVisible();
        if (!InteractPromptSharedUiLocator.TryFindShared(out _, out var nameTmp, out var typeTmp))
            return;

        int f = Time.frameCount;
        if (f != s_offerFrame)
            s_offers.Clear();

        if (UIInteractionState.BlocksWorldPrompt || GameplayUiHotkeyGuard.ShouldHideWorldInteractPrompt())
        {
            if (nameTmp != null) nameTmp.text = "";
            if (typeTmp != null) typeTmp.text = "";
            InteractPromptSharedUiLocator.SetSharedPanelVisible(nameTmp, typeTmp, false);
            return;
        }

        if (!TryGetBestOfferThisFrame(out var bestRow))
        {
            if (nameTmp != null) nameTmp.text = "";
            if (typeTmp != null) typeTmp.text = "";
            InteractPromptSharedUiLocator.SetSharedPanelVisible(nameTmp, typeTmp, false);
            return;
        }

        if (nameTmp != null) nameTmp.text = bestRow.Name;
        if (typeTmp != null) typeTmp.text = bestRow.TypeLine;
        InteractPromptSharedUiLocator.SetSharedPanelVisible(nameTmp, typeTmp, true);
    }

    public static void ApplyBestToSharedTmp(TextMeshProUGUI nameField, TextMeshProUGUI typeField)
    {
        int f = Time.frameCount;
        // 本帧若没有任何 Offer()，字典里仍是上一帧残留；先清空再应用，避免一直显示旧文案。
        if (f != s_offerFrame)
            s_offers.Clear();

        if (f != s_frame)
        {
            s_frame = f;
            s_appliedThisFrame.Clear();
        }

        int id = nameField != null ? nameField.GetInstanceID() : typeField.GetInstanceID();
        if (id == 0)
            return;

        if (UIInteractionState.BlocksWorldPrompt || GameplayUiHotkeyGuard.ShouldHideWorldInteractPrompt())
        {
            if (!s_appliedThisFrame.Contains(id))
            {
                s_appliedThisFrame.Add(id);
                if (nameField != null) nameField.text = "";
                if (typeField != null) typeField.text = "";
            }
            return;
        }

        bool hasOffer = s_offers.TryGetValue(id, out var row) && row.BestDistance < float.MaxValue - 1f;
        if (!hasOffer && InteractPromptSharedUiLocator.TryResolveSharedPanel(nameField, typeField, out _))
            hasOffer = HasAnyActiveOfferThisFrame();

        if (!s_appliedThisFrame.Contains(id))
        {
            s_appliedThisFrame.Add(id);
            if (!hasOffer)
            {
                if (nameField != null) nameField.text = "";
                if (typeField != null) typeField.text = "";
            }
            else if (s_offers.TryGetValue(id, out row))
            {
                if (nameField != null) nameField.text = row.Name;
                if (typeField != null) typeField.text = row.TypeLine;
            }
            else if (TryGetBestOfferThisFrame(out var bestRow))
            {
                if (nameField != null) nameField.text = bestRow.Name;
                if (typeField != null) typeField.text = bestRow.TypeLine;
            }
        }
    }
}
