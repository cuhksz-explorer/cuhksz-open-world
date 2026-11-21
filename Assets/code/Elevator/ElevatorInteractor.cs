using UnityEngine;
using TMPro;

/// <summary>
/// 单层电梯呼叫点：挂在每层门口的物体上，引用共用的 <see cref="ElevatorSystem"/> 即可从任意层传送到全部楼层。
/// </summary>
public class ElevatorInteractor : MonoBehaviour
{
    [Header("共用楼层表（必填）")]
    [Tooltip("整栋电梯的楼层配置，通常挂在父物体 Elevator_X 上")]
    public ElevatorSystem elevatorSystem;

    [Header("本层信息")]
    [Tooltip("当前呼叫点所在楼层号，用于提示「当前 X 层」；0 表示不显示")]
    public int currentFloorNumber;

    [Header("交互")]
    public Transform triggerPoint;
    public float interactDistance = 10f;
    public KeyCode interactKey = KeyCode.F;
    [SerializeField] string interactionTypeLabel = "乘坐电梯";
    public string InteractionTypeLabel => Loc.Choose(interactionTypeLabel, "Take elevator");

    [Header("传送")]
    [Tooltip("选择当前所在楼层时是否仍执行传送（一般关闭）")]
    public bool allowTeleportToSameFloor;

    [Header("共用交互提示")]
    public bool useSharedInteractPrompt = true;
    public bool autoFindSharedPromptIfUnassigned = true;
    public RectTransform sharedPromptPanel;
    public TextMeshProUGUI sharedPromptNameText;
    public TextMeshProUGUI sharedPromptTypeText;
    public int promptPriority = 165;

    Transform _player;
    int _sharedPromptGroupId;
    bool _inputOpen;

    public Vector3 TriggerPosition => triggerPoint != null ? triggerPoint.position : transform.position;

    void Awake()
    {
        ResolveElevatorSystem();
        ResolveSharedPromptRefs();
    }

    void OnEnable()
    {
        RefreshSharedPromptBindings();
    }

    public void RebindSharedPrompt(RectTransform panel, TextMeshProUGUI nameTmp, TextMeshProUGUI typeTmp)
    {
        sharedPromptPanel = panel;
        sharedPromptNameText = nameTmp;
        sharedPromptTypeText = typeTmp;
        useSharedInteractPrompt = true;
        RefreshSharedPromptBindings();
    }

    public void RefreshSharedPromptBindings()
    {
        _sharedPromptGroupId = 0;
        if (!useSharedInteractPrompt)
            return;

        if (autoFindSharedPromptIfUnassigned
            || sharedPromptPanel == null
            || sharedPromptNameText == null
            || sharedPromptTypeText == null)
        {
            InteractPromptSharedUiLocator.TryAutoBind(
                ref sharedPromptPanel,
                ref sharedPromptNameText,
                ref sharedPromptTypeText);
        }

        _sharedPromptGroupId = InteractPromptSharedLabelFrame.ResolveOfferGroupId(
            sharedPromptNameText,
            sharedPromptTypeText);
    }

    void Update()
    {
        if (_inputOpen)
        {
            if (!ElevatorFloorInputUI.Instance.IsOpen)
                _inputOpen = false;
            return;
        }

        if (GameplayUiHotkeyGuard.ShouldBlockGameplayHotkeys())
            return;

        if (!ResolveElevatorSystem() || !elevatorSystem.HasAnyFloor())
            return;

        if (_player == null)
        {
            var pm = FindObjectOfType<PlayerMovement>(true);
            if (pm != null)
                _player = pm.transform;
        }
        if (_player == null)
            return;

        float d = Vector3.Distance(_player.position, TriggerPosition);
        if (d > interactDistance)
            return;

        QueueSharedPromptOffer(d);

        if (!Input.GetKeyDown(interactKey))
            return;

        InteractionNearestTriggerGate.Submit(interactKey, d, GetInstanceID(), OpenFloorInput, promptPriority);
    }

    bool ResolveElevatorSystem()
    {
        if (elevatorSystem != null)
            return true;

        elevatorSystem = GetComponentInParent<ElevatorSystem>();
        return elevatorSystem != null;
    }

    void OpenFloorInput()
    {
        if (!ResolveElevatorSystem() || !elevatorSystem.HasAnyFloor())
        {
            Debug.LogWarning($"[Elevator] {name} 未绑定 ElevatorSystem 或楼层表为空。", this);
            return;
        }

        elevatorSystem.GetFloorRange(out int minFloor, out int maxFloor);
        _inputOpen = true;

        ElevatorFloorInputUI.Instance.Show(
            elevatorSystem.DisplayName,
            minFloor,
            maxFloor,
            currentFloorNumber,
            OnFloorConfirmed);
    }

    void OnFloorConfirmed(int floor)
    {
        _inputOpen = false;

        if (!allowTeleportToSameFloor && currentFloorNumber > 0 && floor == currentFloorNumber)
            return;

        var dest = elevatorSystem.FindFloor(floor);
        if (dest == null)
        {
            Debug.LogWarning($"[Elevator] 未找到楼层 {floor} 的配置（{elevatorSystem.name}）。", elevatorSystem);
            return;
        }

        InSceneTeleportLoading.Instance.TeleportTo(dest.ResolvePosition(), dest.ResolveRotation());
    }

    void QueueSharedPromptOffer(float distance)
    {
        if (!useSharedInteractPrompt || elevatorSystem == null)
            return;

        if (_sharedPromptGroupId == 0
            || sharedPromptPanel == null
            || sharedPromptNameText == null
            || sharedPromptTypeText == null)
            RefreshSharedPromptBindings();
        if (_sharedPromptGroupId == 0)
            return;

        string name = elevatorSystem.DisplayName;
        if (currentFloorNumber > 0)
            name = $"{name}（{currentFloorNumber}F）";

        string typeLine = string.IsNullOrEmpty(interactionTypeLabel)
            ? $"[{interactKey}]"
            : $"[{interactKey}] {InteractionTypeLabel}";

        Vector3? anchor = InteractPromptSharedUiLocator.ResolveProximityWorldAnchor(triggerPoint, transform);
        InteractPromptSharedLabelFrame.Offer(_sharedPromptGroupId, distance, name, typeLine, anchor);
    }

    void ResolveSharedPromptRefs() => RefreshSharedPromptBindings();

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.85f);
        Gizmos.DrawWireSphere(TriggerPosition, interactDistance);
    }
}
