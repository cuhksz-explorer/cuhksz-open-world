using TMPro;
using UnityEngine;

/// <summary>
/// 挂在大花坛附近：走近显示交互提示，按 F 打开制作人员名单（黑底白字，可滚动）。
/// </summary>
[DisallowMultipleComponent]
public class FlowerBedCreditsTrigger : MonoBehaviour
{
    [Header("交互")]
    [SerializeField] Transform triggerPoint;
    [SerializeField] float interactDistance = 4.5f;
    [SerializeField] KeyCode interactKey = KeyCode.F;
    [SerializeField] string displayName = "制作人员鸣谢";
    [SerializeField] string interactionType = "查看致谢";
    [SerializeField] int promptPriority = 160;

    [Header("提示 UI")]
    [SerializeField] bool useSharedInteractPrompt = true;
    [SerializeField] bool autoFindSharedPromptIfUnassigned = true;
    [SerializeField] RectTransform sharedPromptPanel;
    [SerializeField] TextMeshProUGUI sharedPromptNameText;
    [SerializeField] TextMeshProUGUI sharedPromptTypeText;

    [Header("Debug")]
    [SerializeField] bool drawGizmos = true;

    Transform _player;
    int _sharedPromptGroupId;

    Vector3 TriggerPosition => triggerPoint != null ? triggerPoint.position : transform.position;

    void Awake()
    {
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
        if (IsBrowsePanelOpen())
            return;

        if (_player == null)
        {
            var movement = FindObjectOfType<PlayerMovement>(true);
            if (movement != null)
                _player = movement.transform;
        }

        if (_player == null)
            return;

        float distance = Vector3.Distance(_player.position, TriggerPosition);
        if (distance > interactDistance)
            return;

        if (GameplayUiHotkeyGuard.ShouldBlockGameplayHotkeys()
            || GameplayUiHotkeyGuard.BlocksGameplayBecauseMultiplayerUi())
            return;

        QueueSharedPromptOffer(distance);

        if (Input.GetKeyDown(interactKey))
        {
            InteractionNearestTriggerGate.Submit(
                interactKey,
                distance,
                GetInstanceID(),
                OpenCreditsFromPrompt,
                promptPriority);
        }
    }

    void OpenCreditsFromPrompt()
    {
        GameCreditsScrollOverlay.ShowBrowsePanel();
    }

    void QueueSharedPromptOffer(float distance)
    {
        if (!useSharedInteractPrompt)
            return;

        if (_sharedPromptGroupId == 0
            || sharedPromptPanel == null
            || sharedPromptNameText == null
            || sharedPromptTypeText == null)
            RefreshSharedPromptBindings();
        if (_sharedPromptGroupId == 0)
            return;

        Vector3? anchor = InteractPromptSharedUiLocator.ResolveProximityWorldAnchor(triggerPoint, transform);
        InteractPromptSharedLabelFrame.Offer(
            _sharedPromptGroupId,
            distance,
            Loc.StoryTerm(displayName),
            FormatTypeLine(interactionType),
            anchor);
    }

    string FormatTypeLine(string typeLabel)
    {
        if (string.IsNullOrEmpty(typeLabel))
            return "";
        typeLabel = Loc.StoryTerm(typeLabel);
        return Loc.F("[{0}] {1}", "[{0}] {1}", interactKey, typeLabel);
    }

    void ResolveSharedPromptRefs() => RefreshSharedPromptBindings();

    static bool IsBrowsePanelOpen()
    {
        return GameCreditsScrollOverlay.IsBrowsePanelOpen;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.35f);
        Gizmos.DrawSphere(TriggerPosition, interactDistance);
    }
}
