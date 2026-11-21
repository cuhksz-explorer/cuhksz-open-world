using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家挂机待机动画：10 秒无键盘/鼠标操作后，随机循环播放已配置的待机片段之一。
/// </summary>
[DisallowMultipleComponent]
public class PlayerAfkIdleDance : MonoBehaviour
{
    public static bool IsAfkDancing { get; private set; }

    [Header("资源（可由菜单 Tools/待机动画/一键配置 自动填入）")]
    public RuntimeAnimatorController afkDanceController;

    [Header("触发条件")]
    public float idleSeconds = 10f;
    [Tooltip("鼠标移动低于该值视为未操作")]
    public float mouseMoveThreshold = 0.02f;

    [Header("Animator 状态名（与 PlayerAfkDance.controller 一致，由一键配置生成）")]
    public string[] danceStateNames;

    [Tooltip("与 danceStateNames 一一对应，用于校验片段时长（由一键配置填入）")]
    public AnimationClip[] afkDanceClips;

    Animator _animator;
    RuntimeAnimatorController _walkingController;
    KeyInputController _keyInput;
    float _idleTimer;
    bool _isDancing;
    int _currentDanceIndex = -1;
    readonly List<int> _validDanceIndices = new List<int>(16);

    const float MinPlayableClipLength = 0.5f;

    void Awake()
    {
        _animator = GetComponentInChildren<Animator>(true);
        _keyInput = GetComponent<KeyInputController>();
        if (_animator != null)
            _walkingController = _animator.runtimeAnimatorController;

        RebuildValidDanceIndices();
    }

    void RebuildValidDanceIndices()
    {
        _validDanceIndices.Clear();
        if (afkDanceController == null || danceStateNames == null)
            return;

        int n = danceStateNames.Length;
        for (int i = 0; i < n; i++)
        {
            float len = GetClipLengthForSlot(i);
            if (len >= MinPlayableClipLength)
                _validDanceIndices.Add(i);
        }
    }

    float GetClipLengthForSlot(int slotIndex)
    {
        if (afkDanceClips != null && slotIndex >= 0 && slotIndex < afkDanceClips.Length && afkDanceClips[slotIndex] != null)
            return afkDanceClips[slotIndex].length;

        if (danceStateNames != null && slotIndex >= 0 && slotIndex < danceStateNames.Length)
            return MinPlayableClipLength;

        return 0f;
    }

    void Update()
    {
        if (!CanRunAfkLogic())
        {
            if (_isDancing)
                StopAfkDance();
            _idleTimer = 0f;
            return;
        }

        if (HasAnyInput())
        {
            _idleTimer = 0f;
            if (_isDancing)
                StopAfkDance();
            return;
        }

        if (_isDancing)
            return;

        _idleTimer += Time.deltaTime;
        if (_idleTimer >= idleSeconds)
            StartRandomAfkDance();
    }

    bool CanRunAfkLogic()
    {
        if (_animator == null || afkDanceController == null)
            return false;
        if (ChairSitState.BlocksGameplay)
            return false;
        if (VehicleController.IsAnyVehicleDriven)
            return false;
        if (GameplayUiHotkeyGuard.BlocksGameplayBecauseMultiplayerUi())
            return false;

        var sitSeq = GetComponent<PlayerSitSequence>();
        if (sitSeq != null && (sitSeq.IsSitting || sitSeq.IsBusy))
            return false;

        return true;
    }

    bool HasAnyInput()
    {
        if (Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.01f
            || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.01f)
            return true;

        if (Mathf.Abs(Input.GetAxis("Mouse X")) > mouseMoveThreshold
            || Mathf.Abs(Input.GetAxis("Mouse Y")) > mouseMoveThreshold)
            return true;

        if (Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f
            || Mathf.Abs(Input.mouseScrollDelta.x) > 0.01f)
            return true;

        if (Input.anyKey || Input.anyKeyDown)
            return true;

        return false;
    }

    void StartRandomAfkDance()
    {
        if (_animator == null || afkDanceController == null)
            return;

        RebuildValidDanceIndices();
        if (_validDanceIndices.Count == 0)
            return;

        if (_walkingController == null)
            _walkingController = _animator.runtimeAnimatorController;

        int pick = Random.Range(0, _validDanceIndices.Count);
        _currentDanceIndex = _validDanceIndices[pick];

        if (_keyInput != null)
            _keyInput.enabled = false;

        ResetMovementBools();
        _animator.runtimeAnimatorController = afkDanceController;

        string stateName = danceStateNames != null && _currentDanceIndex < danceStateNames.Length
            ? danceStateNames[_currentDanceIndex]
            : "Dance" + _currentDanceIndex;

        if (!_animator.HasState(0, Animator.StringToHash(stateName)))
        {
            Debug.LogWarning($"[待机动画] Animator 无状态 {stateName}，请运行 Tools/待机动画/一键配置");
            if (_keyInput != null)
                _keyInput.enabled = true;
            if (_walkingController != null)
                _animator.runtimeAnimatorController = _walkingController;
            return;
        }

        _animator.CrossFade(stateName, 0.2f, 0, 0f);
        _isDancing = true;
        IsAfkDancing = true;
        _idleTimer = 0f;
    }

    void StopAfkDance()
    {
        if (_animator != null && _walkingController != null)
            _animator.runtimeAnimatorController = _walkingController;

        if (_keyInput != null)
            _keyInput.enabled = true;

        ResetMovementBools();
        _isDancing = false;
        IsAfkDancing = false;
        _currentDanceIndex = -1;
        _idleTimer = 0f;
    }

    void ResetMovementBools()
    {
        if (_animator == null || _keyInput == null)
            return;

        TrySetBool(_keyInput.wParam, false);
        TrySetBool(_keyInput.aParam, false);
        TrySetBool(_keyInput.sParam, false);
        TrySetBool(_keyInput.dParam, false);
        TrySetBool(_keyInput.runParam, false);
    }

    void TrySetBool(string param, bool value)
    {
        if (_animator == null || string.IsNullOrEmpty(param))
            return;
        for (int i = 0; i < _animator.parameterCount; i++)
        {
            var p = _animator.GetParameter(i);
            if (p.name == param && p.type == AnimatorControllerParameterType.Bool)
            {
                _animator.SetBool(param, value);
                return;
            }
        }
    }

    void OnDisable()
    {
        if (_isDancing)
            StopAfkDance();
    }
}
