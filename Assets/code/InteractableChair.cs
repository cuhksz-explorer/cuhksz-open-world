using TMPro;
using UnityEngine;

/// <summary>
/// 椅子/长椅交互：靠近时使用项目统一交互 UI 显示 [F] 坐下，再由 <see cref="PlayerSitSequence"/> 播放坐下/站起动画。
/// </summary>
public class InteractableChair : MonoBehaviour
{
    [Header("交互")]
    public string displayName = "长椅";
    [Tooltip("座位锚点：角色最终位置与朝向严格对齐到该 Transform 的 Position / Forward。")]
    public Transform seatAnchor;
    public Transform sitPoint;
    [Tooltip("坐下动画起点（椅子前方的地面位置），由 Setup 自动生成。空时退回 sitPoint。")]
    public Transform approachPoint;
    public float interactDistance = 2.8f;
    public float distanceBuffer = 0.5f;
    public KeyCode interactKey = KeyCode.F;
    public KeyCode standKey = KeyCode.F;
    public KeyCode cancelKey = KeyCode.Escape;
    public int interactPriority = 45;

    [Header("站起")]
    public float standUpBackOffset = 0.75f;

    [Header("坐姿微调（米）")]
    [Tooltip("沿 SeatAnchor.forward 额外前移，修正 Mixamo 坐下动画偏后。")]
    public float sitForwardOffset = ChairSitSeatPose.DefaultAnimForwardOffset;
    [Tooltip("相对 SeatAnchor 额外上移，修正偏下/穿入椅面。")]
    public float sitUpOffset = ChairSitSeatPose.DefaultAnimUpOffset;

    static InteractableChair s_occupiedChair;
    static InteractableChair s_promptHolder;
    static float s_promptHolderDistance = float.MaxValue;
    static int s_promptFrame = -1;

    PlayerMovement _cachedPlayer;
    TextMeshProUGUI _sharedNameTmp;
    TextMeshProUGUI _sharedTypeTmp;
    int _sharedOfferGroupId;

    public static bool IsSitting => s_occupiedChair != null;

    void Awake()
    {
        sitForwardOffset = ChairSitSeatPose.ResolveAnimForwardOffset(sitForwardOffset);
        sitUpOffset = ChairSitSeatPose.ResolveAnimUpOffset(sitUpOffset);

        if (seatAnchor == null)
        {
            var child = transform.Find("SeatAnchor");
            if (child != null)
                seatAnchor = child;
        }

        if (sitPoint == null)
        {
            var child = transform.Find("ChairSitPoint");
            if (child != null)
                sitPoint = child;
        }
        if (seatAnchor == null)
            seatAnchor = CreateRuntimeSeatAnchor();

        ResolveSharedInteractPromptRefs();
    }

    void Update()
    {
        // 每帧重置一次 holder 竞争（多椅子时让最近的赢）。
        if (s_promptFrame != Time.frameCount)
        {
            s_promptFrame = Time.frameCount;
            s_promptHolderDistance = float.MaxValue;
            s_promptHolder = null;
        }

        if (s_occupiedChair == this)
        {
            if (!IsPlayerStillSeatedOnThisChair())
            {
                s_occupiedChair = null;
            }
            else
            {
                UpdateWhileSitting();
                return;
            }
        }

        if (s_occupiedChair != null)
            return;

        UpdateProximity();

    }

    void UpdateProximity()
    {
        PlayerMovement player = GetActivePlayer();
        Transform seat = GetSeatAnchor();
        if (player == null || !player.gameObject.activeInHierarchy || seat == null)
            return;

        float distance = Vector3.Distance(player.transform.position, seat.position);
        bool inRange = distance <= interactDistance + distanceBuffer;
        if (!inRange)
            return;

        // 使用项目原有的 InteractPrompt_Shared 交互提示。
        if (s_promptHolder == null
            || s_promptHolder == this
            || distance + 0.001f < s_promptHolderDistance)
        {
            s_promptHolder = this;
            s_promptHolderDistance = distance;
            OfferSharedPrompt(distance, GetDisplayName(), FormatSitTypeLine());
        }

        if (GameplayUiHotkeyGuard.ShouldBlockGameplayHotkeys()
            || GameplayUiHotkeyGuard.BlocksGameplayBecauseMultiplayerUi())
            return;

        if (!Input.GetKeyDown(interactKey))
            return;

        InteractionNearestTriggerGate.Submit(interactKey, distance, GetInstanceID(), () =>
        {
            if (!this || !enabled || s_occupiedChair != null)
                return;

            PlayerMovement latest = GetActivePlayer();
            Transform latestSeat = GetSeatAnchor();
            if (latest == null || latestSeat == null)
                return;

            float latestDistance = Vector3.Distance(latest.transform.position, latestSeat.position);
            if (latestDistance > interactDistance + distanceBuffer)
                return;

            BeginSit(latest);
        }, interactPriority);
    }

    void BeginSit(PlayerMovement player)
    {
        Transform seat = GetSeatAnchor();
        if (player == null || seat == null)
            return;

        var sitSeq = player.GetComponent<PlayerSitSequence>();
        if (sitSeq == null)
            sitSeq = player.gameObject.AddComponent<PlayerSitSequence>();

        sitSeq.standUpBackOffset = standUpBackOffset;
        sitSeq.sitForwardOffset = ChairSitSeatPose.ResolveAnimForwardOffset(sitForwardOffset);
        sitSeq.sitUpOffset = ChairSitSeatPose.ResolveAnimUpOffset(sitUpOffset);
        sitSeq.EnsureConfigured();
        if (!sitSeq.TryBeginSit(seat, approachPoint, GetChairColliders()))
        {
            Debug.LogWarning(
                "[InteractableChair] 坐下未开始：动画未绑定或玩家正忙。请查看 Console 中 [PlayerSitSequence] / [椅子交互] 日志。",
                this);
            return;
        }

        s_occupiedChair = this;
    }

    Collider[] GetChairColliders()
    {
        return GetComponentsInChildren<Collider>(true);
    }

    void UpdateWhileSitting()
    {
        var player = GetActivePlayer();
        if (player == null)
            return;

        var sitSeq = player.GetComponent<PlayerSitSequence>();
        if (sitSeq == null || sitSeq.IsBusy)
            return;

        if (HasMovementOrStandUpInput())
            EndSit();
    }

    /// <summary>
    /// 仅移动/站起相关按键触发站起；鼠标环顾、滚轮、V 切换视角等不触发。
    /// </summary>
    bool HasMovementOrStandUpInput()
    {
        if (Input.GetKeyDown(cancelKey) || Input.GetKeyDown(standKey))
            return true;

        if (Input.GetKeyDown(KeyCode.Space))
            return true;

        if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl))
            return true;

        return Input.GetKeyDown(KeyCode.W)
               || Input.GetKeyDown(KeyCode.A)
               || Input.GetKeyDown(KeyCode.S)
               || Input.GetKeyDown(KeyCode.D);
    }

    void EndSit()
    {
        PlayerMovement player = GetActivePlayer();
        if (player != null)
        {
            var sitSeq = player.GetComponent<PlayerSitSequence>();
            if (sitSeq != null && (sitSeq.IsSitting || sitSeq.IsBusy))
            {
                Vector3 flatForward = GetSeatForward();
                flatForward.y = 0f;
                if (flatForward.sqrMagnitude < 0.01f)
                    flatForward = -player.transform.forward;
                sitSeq.RequestStandUp(flatForward.normalized);
            }
        }

        s_occupiedChair = null;
    }

    void OnDisable()
    {
        if (s_promptHolder == this)
            s_promptHolder = null;
    }

    string GetDisplayName()
    {
        if (string.IsNullOrEmpty(displayName) || displayName == "长椅")
            return Loc.Choose("长椅", "Bench");
        return displayName;
    }

    string FormatSitTypeLine() => $"[{interactKey}] {Loc.Choose("坐下", "Sit")}";

    string FormatStandTypeLine() => $"[{standKey}] {Loc.Choose("站起", "Stand")}";

    Transform GetSeatAnchor()
    {
        if (seatAnchor != null)
            return seatAnchor;
        var found = transform.Find("SeatAnchor");
        if (found != null)
            seatAnchor = found;
        if (seatAnchor == null)
            seatAnchor = CreateRuntimeSeatAnchor();
        return seatAnchor;
    }

    Vector3 GetSeatForward()
    {
        Transform seat = GetSeatAnchor();
        if (seat != null)
            return seat.forward;
        return transform.forward;
    }

    Transform CreateRuntimeSeatAnchor()
    {
        var go = new GameObject("SeatAnchor_Runtime");
        go.transform.SetParent(transform, false);

        if (TryComputeSeatPose(out Vector3 pos, out Quaternion rot, out Vector3 approachPos))
        {
            go.transform.SetPositionAndRotation(pos, rot);
            if (approachPoint != null && approachPoint.name == "ChairApproachPoint")
            {
                approachPoint.SetPositionAndRotation(approachPos, rot);
            }
            else if (approachPoint == null)
            {
                var approachGo = new GameObject("ChairApproachPoint_Runtime");
                approachGo.transform.SetParent(transform, false);
                approachGo.transform.SetPositionAndRotation(approachPos, rot);
                approachPoint = approachGo.transform;
            }
        }
        else
        {
            if (sitPoint != null)
                go.transform.SetPositionAndRotation(sitPoint.position, sitPoint.rotation);
            else
            {
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
            }
        }

        return go.transform;
    }

    bool TryComputeSeatPose(out Vector3 position, out Quaternion rotation, out Vector3 approachPosition)
    {
        position = transform.position;
        rotation = transform.rotation;
        approachPosition = transform.position;

        var renderers = GetComponentsInChildren<Renderer>(true);
        return ChairSitSeatPose.TryComputeFromRenderers(transform, renderers, out position, out rotation, out approachPosition);
    }

    void OfferSharedPrompt(float distance, string name, string typeLine)
    {
        if (_sharedOfferGroupId == 0 || _sharedNameTmp == null && _sharedTypeTmp == null)
            ResolveSharedInteractPromptRefs();
        if (_sharedOfferGroupId == 0)
            return;

        Transform seat = GetSeatAnchor();
        Vector3? anchor = seat != null ? seat.position : (Vector3?)null;
        InteractPromptSharedLabelFrame.Offer(_sharedOfferGroupId, distance, name, typeLine, anchor);
    }

    bool IsPlayerStillSeatedOnThisChair()
    {
        if (ChairSitState.IsSitting)
            return true;

        var player = GetActivePlayer();
        if (player == null)
            return false;

        var sitSeq = player.GetComponent<PlayerSitSequence>();
        return sitSeq != null && (sitSeq.IsSitting || sitSeq.IsBusy);
    }

    void LateUpdate()
    {
        if (_sharedNameTmp == null && _sharedTypeTmp == null)
            return;
        InteractPromptSharedLabelFrame.ApplyBestToSharedTmp(_sharedNameTmp, _sharedTypeTmp);
    }

    PlayerMovement GetActivePlayer()
    {
        if (_cachedPlayer != null)
            return _cachedPlayer;
        _cachedPlayer = FindObjectOfType<PlayerMovement>(true);
        return _cachedPlayer;
    }

    void ResolveSharedInteractPromptRefs()
    {
        RectTransform panel = null;
        InteractPromptSharedUiLocator.TryAutoBind(ref panel, ref _sharedNameTmp, ref _sharedTypeTmp);
        _sharedOfferGroupId = InteractPromptSharedLabelFrame.ResolveOfferGroupId(_sharedNameTmp, _sharedTypeTmp);
    }

    void OnDrawGizmosSelected()
    {
        Transform seat = seatAnchor != null ? seatAnchor : sitPoint;
        if (seat == null)
            return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(seat.position, 0.2f);
        Gizmos.DrawLine(seat.position, seat.position + seat.forward * 0.5f);
    }
}

/// <summary>
/// 根据长椅 Renderer 包围盒计算 SeatAnchor 位置与朝向（座面高度 + 朝前偏移）。
/// </summary>
public static class ChairSitSeatPose
{
    public const float SeatHeightRatio = 0.58f;
    public const float SeatForwardBiasRatio = 0.56f;
    public const float DefaultAnimForwardOffset = 0.72f;
    public const float DefaultAnimUpOffset = 0.40f;
    public const float ApproachExtraDistance = 0.45f;

    static readonly float[] LegacyAnimForwardOffsets = { 0.30f, 0.52f };
    static readonly float[] LegacyAnimUpOffsets = { 0.08f, 0.26f };

    public static float ResolveAnimForwardOffset(float serialized)
    {
        for (int i = 0; i < LegacyAnimForwardOffsets.Length; i++)
        {
            if (Mathf.Approximately(serialized, LegacyAnimForwardOffsets[i]))
                return DefaultAnimForwardOffset;
        }
        return serialized;
    }

    public static float ResolveAnimUpOffset(float serialized)
    {
        for (int i = 0; i < LegacyAnimUpOffsets.Length; i++)
        {
            if (Mathf.Approximately(serialized, LegacyAnimUpOffsets[i]))
                return DefaultAnimUpOffset;
        }
        return serialized;
    }

    public static bool TryComputeFromRenderers(
        Transform benchRoot,
        Renderer[] renderers,
        out Vector3 seatPosition,
        out Quaternion seatRotation,
        out Vector3 approachPosition)
    {
        seatPosition = benchRoot != null ? benchRoot.position : Vector3.zero;
        seatRotation = benchRoot != null ? benchRoot.rotation : Quaternion.identity;
        approachPosition = seatPosition;

        if (benchRoot == null || renderers == null || renderers.Length == 0)
            return false;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return TryComputeFromBounds(benchRoot, bounds, out seatPosition, out seatRotation, out approachPosition);
    }

    public static bool TryComputeFromBounds(
        Transform benchRoot,
        Bounds bounds,
        out Vector3 seatPosition,
        out Quaternion seatRotation,
        out Vector3 approachPosition)
    {
        seatPosition = benchRoot.position;
        seatRotation = benchRoot.rotation;
        approachPosition = seatPosition;

        if (benchRoot == null)
            return false;

        Vector3 localRight = benchRoot.right;
        localRight.y = 0f;
        Vector3 localForward = benchRoot.forward;
        localForward.y = 0f;
        if (localRight.sqrMagnitude < 0.01f)
            localRight = Vector3.right;
        if (localForward.sqrMagnitude < 0.01f)
            localForward = Vector3.forward;
        localRight.Normalize();
        localForward.Normalize();

        float rightExtent = Mathf.Abs(Vector3.Dot(bounds.extents, Abs(localRight)));
        float forwardExtent = Mathf.Abs(Vector3.Dot(bounds.extents, Abs(localForward)));
        Vector3 fwdLocal = rightExtent >= forwardExtent ? Vector3.forward : Vector3.right;
        Vector3 flatForward = benchRoot.TransformDirection(fwdLocal);
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.01f)
            flatForward = benchRoot.forward;
        flatForward.Normalize();
        Vector3 seatForward = -flatForward;

        float halfDepth = Mathf.Max(0.25f, rightExtent >= forwardExtent ? forwardExtent : rightExtent);
        float seatY = bounds.min.y + bounds.size.y * SeatHeightRatio;
        float forwardBias = halfDepth * SeatForwardBiasRatio;

        seatPosition = new Vector3(bounds.center.x, seatY, bounds.center.z) + seatForward * forwardBias;
        seatRotation = Quaternion.LookRotation(seatForward, Vector3.up);
        approachPosition = seatPosition + seatForward * (halfDepth + ApproachExtraDistance);
        approachPosition.y = bounds.min.y;
        return true;
    }

    public static Vector3 ApplyAnimationOffset(Vector3 seatPosition, Quaternion seatRotation, float forwardOffset, float upOffset)
    {
        Vector3 pos = seatPosition;
        if (forwardOffset != 0f)
            pos += seatRotation * Vector3.forward * forwardOffset;
        if (upOffset != 0f)
            pos += Vector3.up * upOffset;
        return pos;
    }

    static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
}
