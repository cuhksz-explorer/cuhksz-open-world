using TMPro;
using UnityEngine;

public class VehicleDealerNPC : MonoBehaviour
{
    [Header("控制模式")]
    [Tooltip("开启后，本脚本不再走旧版 E + 独立 Canvas，而是兼容代理到 InteractableNPC（统一 F 交互 / 共享提示）。")]
    public bool disableSelfControl = true;

    [Header("统一交互（系统二替换系统一）")]
    public InteractableNPC interactableNpc;
    public string dealerDisplayName = "载具商人";
    public string interactionTypeName = "选车";
    public KeyCode unifiedInteractKey = KeyCode.F;
    public int interactPriority = 45;
    public bool useSharedPrompt = true;
    public bool autoFindSharedPromptIfUnassigned = true;
    public RectTransform sharedPromptPanel;
    public TextMeshProUGUI sharedPromptNameText;
    public TextMeshProUGUI sharedPromptTypeText;

    [Header("玩家与交互")]
    public Transform playerTransform;       // 玩家的 Transform（车辆根物体）
    public float interactDistance = 3.5f;   // 触发距离
    public KeyCode interactKey = KeyCode.E; // 旧版交互键

    [Header("UI 引用")]
    public GameObject interactPromptCanvas; // 靠近时显示的小提示 Canvas
    public GameObject vehicleShopCanvas;    // 全屏选车界面 Canvas

    [Header("提示文字（可选）")]
    public string promptMessage = "按 E 选车";

    bool isPlayerInRange;
    int sharedPromptGroupId;

    void Start()
    {
        ResolveReferences();
        if (disableSelfControl)
            DisableCompanionMode2();
        if (interactPromptCanvas != null)
            interactPromptCanvas.SetActive(false);
        HideShopCanvas();
    }

    void OnEnable()
    {
        if (disableSelfControl)
            DisableCompanionMode2();
    }

    void OnDisable()
    {
        if (interactPromptCanvas != null)
            interactPromptCanvas.SetActive(false);
        HideShopCanvas();
    }

    void LateUpdate()
    {
        if (!disableSelfControl || !useSharedPrompt)
            return;

        ResolveSharedPrompt();
        if (sharedPromptGroupId != 0)
            InteractPromptSharedLabelFrame.ApplyBestToSharedTmp(sharedPromptNameText, sharedPromptTypeText);
    }

    void Update()
    {
        ResolveReferences();

        if (disableSelfControl)
        {
            HandleUnifiedFlow();
            return;
        }

        HandleLegacyFlow();
    }

    void HandleUnifiedFlow()
    {
        if (playerTransform == null || interactableNpc == null)
        {
            HideLegacyPrompt();
            return;
        }

        if (UIInteractionState.IsNpcShopOpen)
        {
            HideLegacyPrompt();
            return;
        }

        float distance = Vector3.Distance(transform.position, playerTransform.position);
        bool inRange = distance <= interactDistance;
        if (!inRange)
        {
            HideLegacyPrompt();
            return;
        }

        OfferUnifiedPrompt(distance);

        if (GameplayUiHotkeyGuard.ShouldBlockGameplayHotkeys()
            || GameplayUiHotkeyGuard.BlocksGameplayBecauseMultiplayerUi())
            return;

        if (Input.GetKeyDown(unifiedInteractKey))
        {
            float claimDistance = distance;
            InteractionNearestTriggerGate.Submit(unifiedInteractKey, claimDistance, GetInstanceID(), () =>
            {
                if (!this || !enabled || interactableNpc == null || playerTransform == null)
                    return;

                float latestDistance = Vector3.Distance(transform.position, playerTransform.position);
                if (latestDistance > interactDistance)
                    return;

                interactableNpc.OpenShop();
                HideLegacyPrompt();
            }, interactPriority);
        }
    }

    void HandleLegacyFlow()
    {
        if (playerTransform == null)
            return;

        float distance = Vector3.Distance(transform.position, playerTransform.position);
        bool wasInRange = isPlayerInRange;
        isPlayerInRange = distance <= interactDistance;

        if (isPlayerInRange && !wasInRange)
        {
            if (interactPromptCanvas != null)
                interactPromptCanvas.SetActive(true);
        }
        else if (!isPlayerInRange && wasInRange)
        {
            HideLegacyPrompt();
            HideShopCanvas();
        }

        if (isPlayerInRange && Input.GetKeyDown(interactKey))
            OpenLegacyShop();
    }

    void OfferUnifiedPrompt(float distance)
    {
        string typeLine = $"[{unifiedInteractKey}] {Loc.Choose(interactionTypeName, "Select vehicle")}";
        if (useSharedPrompt)
        {
            ResolveSharedPrompt();
            if (sharedPromptGroupId != 0)
            {
                InteractPromptSharedLabelFrame.Offer(
                    sharedPromptGroupId,
                    distance,
                    Loc.Choose(dealerDisplayName, "Vehicle Dealer"),
                    typeLine,
                    transform.position);
                HideLegacyPrompt();
                return;
            }
        }

        if (interactPromptCanvas != null)
            interactPromptCanvas.SetActive(true);
    }

    void HideLegacyPrompt()
    {
        if (interactPromptCanvas != null)
            interactPromptCanvas.SetActive(false);
    }

    void OpenLegacyShop()
    {
        if (vehicleShopCanvas == null)
            return;
        if (interactPromptCanvas != null)
            interactPromptCanvas.SetActive(false);

        VehicleShopUI shopUI = vehicleShopCanvas.GetComponentInChildren<VehicleShopUI>(true);
        if (shopUI != null)
            shopUI.Show();
        else
            vehicleShopCanvas.SetActive(true);
    }

    void HideShopCanvas()
    {
        if (vehicleShopCanvas == null)
            return;

        VehicleShopUI shopUI = vehicleShopCanvas.GetComponentInChildren<VehicleShopUI>(true);
        if (shopUI != null)
            shopUI.CloseShop();
        else
            vehicleShopCanvas.SetActive(false);
    }

    void ResolveReferences()
    {
        if (interactableNpc == null)
            interactableNpc = GetComponent<InteractableNPC>();
        if (interactableNpc == null)
            interactableNpc = GetComponentInParent<InteractableNPC>();

        if (vehicleShopCanvas == null && interactableNpc != null)
            vehicleShopCanvas = interactableNpc.vehicleShopUI;

        if (playerTransform == null)
        {
            var player = FindObjectOfType<PlayerMovement>(true);
            if (player != null)
                playerTransform = player.transform;
        }
    }

    void ResolveSharedPrompt()
    {
        if (!useSharedPrompt)
        {
            sharedPromptGroupId = 0;
            return;
        }

        if (autoFindSharedPromptIfUnassigned)
            InteractPromptSharedUiLocator.TryAutoBind(ref sharedPromptPanel, ref sharedPromptNameText, ref sharedPromptTypeText);

        sharedPromptGroupId = InteractPromptSharedLabelFrame.ResolveOfferGroupId(sharedPromptNameText, sharedPromptTypeText);
    }

    /// <summary>统一 F 流程下禁用同物体上的 mode2，避免与 VehicleDealerNPC 重复 Offer / 抢 F 键。</summary>
    void DisableCompanionMode2()
    {
        var mode2 = GetComponent<InteractWithMouseAndMovementLock>();
        if (mode2 != null && mode2.enabled)
            mode2.enabled = false;
    }
}