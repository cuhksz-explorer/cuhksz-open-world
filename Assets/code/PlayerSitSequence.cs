using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// 玩家坐下/站起：通过 OverrideController + Animator.Play 手动 scrub Humanoid 片段（与 WASD 同一套 Avatar 路径）。
/// </summary>
[DisallowMultipleComponent]
public class PlayerSitSequence : MonoBehaviour
{
    [Header("动画片段（可由 ChairSitClipSet 自动填入）")]
    public AnimationClip sitDownClip;
    public AnimationClip sitIdleClip;
    public AnimationClip standUpClip;

    [Header("专用控制器（可选备用）")]
    public RuntimeAnimatorController sitAnimatorController;

    [Header("站起后偏移（米）")]
    public float standUpBackOffset = 0.6f;

    [Header("过渡参数")]
    public float fallbackApproachDistance = 0.55f;
    public float minSitLerpDuration = 0.6f;

    [Header("坐姿偏移（米，相对 SeatAnchor）")]
    [Tooltip("沿座位朝向前移，修正 Mixamo 动画偏后。")]
    public float sitForwardOffset = ChairSitSeatPose.DefaultAnimForwardOffset;
    [Tooltip("上移，修正偏下/穿入椅面。")]
    public float sitUpOffset = ChairSitSeatPose.DefaultAnimUpOffset;

    [Header("调试")]
    public bool verboseLogs = true;

    Animator _animator;
    RuntimeAnimatorController _walkingController;
    AnimatorOverrideController _sitOverrideController;
    PlayerMovement _movement;
    KeyInputController _keyInput;
    CharacterController _characterController;
    Coroutine _sequenceRoutine;
    Coroutine _idleScrubRoutine;
    bool _isSitting;
    bool _savedApplyRootMotion;
    bool _hasSavedApplyRootMotion;
    PlayableGraph _playableGraph;

    Collider[] _chairCollidersIgnored;
    Collider _selfColliderForIgnore;
    static ChairSitClipSet _cachedClipSet;

    public bool IsSitting => _isSitting;
    public bool IsBusy => _sequenceRoutine != null;

    const string ResourcesFolder = "ChairSit";
    const string ClipSetResourcePath = "ChairSit/ChairSitClipSet";
    const string SitControllerResourcePath = "ChairSit/PlayerChairSit";
    const string IdleStateName = "idle";

    void Awake()
    {
        _movement = GetComponent<PlayerMovement>();
        _keyInput = GetComponent<KeyInputController>();
        _characterController = GetComponent<CharacterController>();
        _selfColliderForIgnore = _characterController as Collider ?? GetComponent<Collider>();
        TryResolveAnimator(force: true);
        EnsureConfigured();
    }

    public void EnsureConfigured()
    {
        TryResolveAnimator(force: true);
        if (TryApplyChairClipsFromResources())
            return;

        ApplyClipSetResources();
        TryFillMissingClipsFromResources();
    }

    bool TryApplyChairClipsFromResources()
    {
        if (!ChairSitClipCatalog.TryResolveClipsFromResources(
                out AnimationClip down,
                out AnimationClip idle,
                out AnimationClip up,
                ResourcesFolder))
            return false;

        sitDownClip = down;
        sitIdleClip = idle;
        standUpClip = up;
        return true;
    }

    void ApplyClipSetResources()
    {
        if (_cachedClipSet == null)
            _cachedClipSet = Resources.Load<ChairSitClipSet>(ClipSetResourcePath);

        if (_cachedClipSet == null)
            return;

        if (!ChairSitClipCatalog.IsClipSetBindingValid(_cachedClipSet))
            return;

        sitDownClip = _cachedClipSet.sitDownClip;
        sitIdleClip = _cachedClipSet.sitIdleClip;
        standUpClip = _cachedClipSet.standUpClip;
    }

    void TryFillMissingClipsFromResources()
    {
        if (!ChairSitClipCatalog.IsSitDownClip(sitDownClip)
            || !ChairSitClipCatalog.IsSitIdleClip(sitIdleClip)
            || !ChairSitClipCatalog.IsStandUpClip(standUpClip))
        {
            if (TryApplyChairClipsFromResources())
                return;
        }

        var clips = Resources.LoadAll<AnimationClip>(ResourcesFolder);
        if (clips == null || clips.Length == 0)
            return;

        if (!ChairSitClipCatalog.IsSitDownClip(sitDownClip))
            sitDownClip = ChairSitClipCatalog.FindExactClip(clips, "Sitting (2)")
                          ?? ChairSitClipCatalog.FindExactClip(clips, "Sitting (1)");
        if (!ChairSitClipCatalog.IsSitIdleClip(sitIdleClip))
            sitIdleClip = ChairSitClipCatalog.FindExactClip(clips, "Sitting Idle")
                          ?? ChairSitClipCatalog.FindExactClip(clips, "Sitting Talking");
        if (!ChairSitClipCatalog.IsStandUpClip(standUpClip))
        {
            standUpClip = ChairSitClipCatalog.FindExactClip(clips, "Sit To Stand (1)")
                          ?? ChairSitClipCatalog.FindExactClip(clips, "Sit To Stand")
                          ?? ChairSitClipCatalog.FindExactClip(clips, "Stand Up");
        }
    }

    void TryResolveAnimator(bool force = false)
    {
        if (!force && _animator != null)
            return;

        _animator = null;

        var visualRoot = transform.Find("PlayerCharacterVisual");
        if (visualRoot != null)
        {
            var animators = visualRoot.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                var candidate = animators[i];
                if (candidate == null || !candidate.enabled)
                    continue;
                if (candidate.avatar != null && candidate.avatar.isValid)
                {
                    _animator = candidate;
                    return;
                }
            }

            if (animators.Length > 0 && animators[0] != null)
            {
                _animator = animators[0];
                return;
            }
        }

        var rootAnimator = GetComponent<Animator>();
        if (rootAnimator != null && rootAnimator.enabled)
        {
            _animator = rootAnimator;
            return;
        }

        _animator = GetComponentInChildren<Animator>(true);
    }

    public bool TryBeginSit(Transform sitPoint, Transform approachPoint, Collider[] chairColliders)
    {
        if (_isSitting || _sequenceRoutine != null || sitPoint == null)
            return false;

        TryResolveAnimator(force: true);
        EnsureConfigured();

        if (_animator == null)
        {
            Debug.LogError("[PlayerSitSequence] 未找到 Animator。请确认 PlayerCharacterVisual 下模型有 Animator。", this);
            return false;
        }

        if (!ChairSitClipCatalog.IsSitDownClip(sitDownClip)
            || !ChairSitClipCatalog.IsSitIdleClip(sitIdleClip)
            || !ChairSitClipCatalog.IsStandUpClip(standUpClip))
        {
            Debug.LogError(
                "[PlayerSitSequence] 椅子动画绑定错误（需 Sitting (2) / Sitting Idle / Sit To Stand (1)）。" +
                $" 当前 Down={FormatClip(sitDownClip)} Idle={FormatClip(sitIdleClip)} Up={FormatClip(standUpClip)}。" +
                "请确认 Resources/ChairSit 下 FBX 存在，并运行 Tools → 椅子交互 → 重建 ClipSet。",
                this);
            LogBindingStatus("TryBeginSit FAILED");
            return false;
        }

        if (!BeginSitPlayback())
        {
            Debug.LogError("[PlayerSitSequence] 无法初始化坐下动画播放（OverrideController 创建失败）。", this);
            return false;
        }

        LogBindingStatus("TryBeginSit OK");
        _sequenceRoutine = StartCoroutine(CoSitDown(sitPoint, approachPoint, chairColliders));
        return true;
    }

    public bool TryBeginSit(Transform sitPoint) => TryBeginSit(sitPoint, null, null);

    public void RequestStandUp(Vector3? exitForward = null)
    {
        if (!_isSitting)
            return;
        if (_sequenceRoutine != null)
            StopCoroutine(_sequenceRoutine);
        _sequenceRoutine = StartCoroutine(CoStandUp(exitForward));
    }

    IEnumerator CoSitDown(Transform sitPoint, Transform approachPoint, Collider[] chairColliders)
    {
        LockPlayerControl();
        DisableRootMotionForSit();
        ResetMovementBools();

        Vector3 sitWorldPos = ResolveSitWorldPosition(sitPoint);
        Quaternion sitRot = sitPoint.rotation;
        Vector3 sitFwd = sitRot * Vector3.forward;
        sitFwd.y = 0f;
        if (sitFwd.sqrMagnitude < 0.01f) sitFwd = transform.forward;
        sitFwd.Normalize();

        Vector3 approachWorldPos;
        Quaternion approachRot;
        if (approachPoint != null)
        {
            approachWorldPos = approachPoint.position;
            approachRot = approachPoint.rotation;
        }
        else
        {
            approachWorldPos = sitWorldPos + sitFwd * Mathf.Max(0.2f, fallbackApproachDistance);
            approachWorldPos.y = sitPoint.position.y;
            approachRot = Quaternion.LookRotation(sitFwd, Vector3.up);
        }

        transform.SetPositionAndRotation(approachWorldPos, approachRot);

        _isSitting = true;
        ChairSitState.IsSitting = true;
        ChairSitCameraAlign.AlignForSitFacing(gameObject, approachRot);

        if (verboseLogs)
            Debug.Log($"[PlayerSitSequence] Scrub 播放 SitDown ← {sitDownClip.name} ({sitDownClip.length:F2}s)", this);

        float lerpDur = Mathf.Max(minSitLerpDuration, sitDownClip.length > 0f ? sitDownClip.length : 0.8f);
        yield return CoScrubClip(sitDownClip, "SitDown", lerpDur, t =>
        {
            float a = Mathf.Clamp01(t / lerpDur);
            float aSmooth = a * a * (3f - 2f * a);
            Quaternion rot = Quaternion.Slerp(approachRot, sitRot, aSmooth);
            transform.SetPositionAndRotation(
                Vector3.Lerp(approachWorldPos, sitWorldPos, aSmooth),
                rot);
            ChairSitCameraAlign.AlignForSitFacing(gameObject, rot);
        });

        transform.SetPositionAndRotation(sitWorldPos, sitRot);
        ChairSitCameraAlign.AlignForSitFacing(gameObject, sitRot);

        if (verboseLogs)
            Debug.Log($"[PlayerSitSequence] Scrub 循环 SitIdle ← {sitIdleClip.name} ({sitIdleClip.length:F2}s)", this);
        StartIdleScrubLoop();
        _chairCollidersIgnored = null;
        _sequenceRoutine = null;
    }

    IEnumerator CoStandUp(Vector3? exitForward)
    {
        StopIdleScrubLoop();

        Vector3 sitPosNow = transform.position;
        Quaternion sitRotNow = transform.rotation;
        Vector3 fwd = exitForward ?? (transform.rotation * Vector3.forward);
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.01f) fwd = transform.forward;
        fwd.Normalize();

        Vector3 standPos = sitPosNow + fwd * standUpBackOffset;
        standPos.y = sitPosNow.y;

        if (verboseLogs)
            Debug.Log($"[PlayerSitSequence] Scrub 播放 StandUp ← {standUpClip.name} ({standUpClip.length:F2}s)", this);

        float lerpDur = Mathf.Max(minSitLerpDuration, standUpClip.length > 0f ? standUpClip.length : 0.8f);
        yield return CoScrubClip(standUpClip, "StandUp", lerpDur, t =>
        {
            float a = Mathf.Clamp01(t / lerpDur);
            float aSmooth = a * a * (3f - 2f * a);
            transform.SetPositionAndRotation(
                Vector3.Lerp(sitPosNow, standPos, aSmooth),
                Quaternion.Slerp(sitRotNow, Quaternion.LookRotation(fwd, Vector3.up), aSmooth));
        });

        transform.SetPositionAndRotation(standPos, Quaternion.LookRotation(fwd, Vector3.up));

        IgnoreChairColliders(_chairCollidersIgnored, false);
        _chairCollidersIgnored = null;

        EndSitPlayback();
        RestoreRootMotion();
        UnlockPlayerControl();

        _isSitting = false;
        ChairSitState.IsSitting = false;
        _sequenceRoutine = null;
    }

    bool BeginSitPlayback()
    {
        if (_animator == null)
            return false;

        StopPlayableGraph();
        StopIdleScrubLoop();

        if (_walkingController == null)
            _walkingController = _animator.runtimeAnimatorController;
        if (_walkingController == null)
            return false;

        if (_sitOverrideController == null || _sitOverrideController.runtimeAnimatorController != _walkingController)
            _sitOverrideController = new AnimatorOverrideController(_walkingController);

        if (sitAnimatorController == null)
            sitAnimatorController = Resources.Load<RuntimeAnimatorController>(SitControllerResourcePath);

        _animator.enabled = true;
        _animator.speed = 1f;
        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        if (_animator.avatar == null || !_animator.avatar.isValid || !_animator.avatar.isHuman)
        {
            var referenceAvatar = PlayerCharacterAnimationUtility.LoadReferenceHumanoidAvatar();
            if (referenceAvatar != null)
                _animator.avatar = referenceAvatar;
        }

        _animator.runtimeAnimatorController = _sitOverrideController;
        ResetMovementBools();
        return true;
    }

    void EndSitPlayback()
    {
        StopIdleScrubLoop();
        StopPlayableGraph();

        if (_animator == null)
            return;

        if (_walkingController != null)
            _animator.runtimeAnimatorController = _walkingController;

        _animator.Rebind();
        _animator.Update(0f);
        ResetMovementBools();
    }

    void ApplyOverrideClip(AnimationClip clip)
    {
        if (_sitOverrideController == null || clip == null)
            return;

        var sourceClips = _sitOverrideController.animationClips;
        if (sourceClips == null || sourceClips.Length == 0)
            return;

        for (int i = 0; i < sourceClips.Length; i++)
        {
            var source = sourceClips[i];
            if (source != null)
                _sitOverrideController[source.name] = clip;
        }
    }

    bool ScrubClipAtNormalizedTime(AnimationClip clip, string sitControllerState, float normalizedTime)
    {
        if (_animator == null || clip == null)
            return false;

        ApplyOverrideClip(clip);

        // 始终用传入的 clip 做 Playable scrub；勿回退到 PlayerChairSit 的 SitDown/StandUp 状态，
        // 否则在状态机 Motion 与 ClipSet 不一致时会表现为坐下/站起动作颠倒。
        if (TryScrubViaPlayable(clip, normalizedTime))
            return true;

        return TryScrubViaIdleState(normalizedTime);
    }

    bool TryScrubViaIdleState(float normalizedTime)
    {
        int idleHash = Animator.StringToHash(IdleStateName);
        if (!_animator.HasState(0, idleHash))
            return false;

        _animator.Play(IdleStateName, 0, Mathf.Clamp01(normalizedTime));
        _animator.Update(0f);
        return true;
    }

    bool TryScrubViaPlayable(AnimationClip clip, float normalizedTime)
    {
        StopPlayableGraph();

        if (_animator.runtimeAnimatorController == null && _sitOverrideController != null)
            _animator.runtimeAnimatorController = _sitOverrideController;

        _animator.enabled = true;

        _playableGraph = PlayableGraph.Create("PlayerSitSequence");
        _playableGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        var output = AnimationPlayableOutput.Create(_playableGraph, "SitOutput", _animator);
        var clipPlayable = AnimationClipPlayable.Create(_playableGraph, clip);
        clipPlayable.SetApplyFootIK(true);
        float sampleTime = Mathf.Clamp01(normalizedTime) * clip.length;
        clipPlayable.SetTime(sampleTime);
        output.SetSourcePlayable(clipPlayable);
        _playableGraph.Play();
        _playableGraph.Evaluate(0f);
        _animator.Update(0f);
        return true;
    }

    void StopPlayableGraph()
    {
        if (_playableGraph.IsValid())
            _playableGraph.Destroy();
    }

    IEnumerator CoScrubClip(AnimationClip clip, string sitControllerState, float duration, Action<float> whileMoving)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            whileMoving?.Invoke(t);
            float nt = duration > 0.01f ? Mathf.Clamp01(t / duration) : 0f;
            ScrubClipAtNormalizedTime(clip, sitControllerState, nt);
            yield return null;
        }

        ScrubClipAtNormalizedTime(clip, sitControllerState, 1f);
    }

    void StartIdleScrubLoop()
    {
        StopIdleScrubLoop();
        _idleScrubRoutine = StartCoroutine(CoLoopIdleScrub());
    }

    void StopIdleScrubLoop()
    {
        if (_idleScrubRoutine == null)
            return;
        StopCoroutine(_idleScrubRoutine);
        _idleScrubRoutine = null;
    }

    IEnumerator CoLoopIdleScrub()
    {
        float time = 0f;
        while (_isSitting && _sequenceRoutine == null)
        {
            time += Time.deltaTime;
            float nt = sitIdleClip.length > 0.01f ? (time % sitIdleClip.length) / sitIdleClip.length : 0f;
            ScrubClipAtNormalizedTime(sitIdleClip, "SitIdle", nt);
            yield return null;
        }
    }

    Vector3 ResolveSitWorldPosition(Transform sitPoint)
    {
        if (sitPoint == null)
            return transform.position;
        return ChairSitSeatPose.ApplyAnimationOffset(
            sitPoint.position,
            sitPoint.rotation,
            ChairSitSeatPose.ResolveAnimForwardOffset(sitForwardOffset),
            ChairSitSeatPose.ResolveAnimUpOffset(sitUpOffset));
    }

    void LogBindingStatus(string context)
    {
        if (!verboseLogs)
            return;

        var sb = new StringBuilder();
        sb.Append("[PlayerSitSequence] ").Append(context).Append(" | ");
        sb.Append("Animator=").Append(_animator != null ? _animator.name : "null").Append(" | ");
        if (_animator != null)
        {
            sb.Append("Avatar=").Append(_animator.avatar != null && _animator.avatar.isValid ? "OK" : "MISSING").Append(" | ");
            sb.Append("HumanMotion=").Append(FormatHumanMotion(sitDownClip)).Append(" | ");
        }
        sb.Append("Down=").Append(FormatClip(sitDownClip)).Append(" | ");
        sb.Append("Idle=").Append(FormatClip(sitIdleClip)).Append(" | ");
        sb.Append("Up=").Append(FormatClip(standUpClip));

        if (sitDownClip != null && sitIdleClip != null && standUpClip != null)
            Debug.Log(sb.ToString(), this);
        else
            Debug.LogWarning(sb.ToString(), this);
    }

    static string FormatHumanMotion(AnimationClip clip)
    {
        if (clip == null)
            return "n/a";
        return clip.humanMotion ? "yes" : "no";
    }

    static string FormatClip(AnimationClip clip)
    {
        return clip != null ? clip.name + " " + clip.length.ToString("F2") + "s" : "MISSING";
    }

    void IgnoreChairColliders(Collider[] colliders, bool ignore)
    {
        if (colliders == null || colliders.Length == 0)
            return;
        if (_selfColliderForIgnore == null)
            _selfColliderForIgnore = _characterController as Collider ?? GetComponent<Collider>();
        if (_selfColliderForIgnore == null)
            return;

        for (int i = 0; i < colliders.Length; i++)
        {
            var c = colliders[i];
            if (c == null || c.isTrigger || !c.enabled) continue;
            Physics.IgnoreCollision(_selfColliderForIgnore, c, ignore);
        }
    }

    void DisableRootMotionForSit()
    {
        if (_animator == null)
            return;
        if (!_hasSavedApplyRootMotion)
        {
            _savedApplyRootMotion = _animator.applyRootMotion;
            _hasSavedApplyRootMotion = true;
        }
        _animator.applyRootMotion = false;
    }

    void RestoreRootMotion()
    {
        if (_animator != null && _hasSavedApplyRootMotion)
            _animator.applyRootMotion = _savedApplyRootMotion;
        _hasSavedApplyRootMotion = false;
    }

    void LockPlayerControl()
    {
        if (_characterController != null)
            _characterController.enabled = false;
        if (_movement != null)
            _movement.enabled = false;
        if (_keyInput != null)
            _keyInput.enabled = false;
    }

    void UnlockPlayerControl()
    {
        if (_characterController != null)
            _characterController.enabled = true;
        if (_movement != null)
            _movement.enabled = true;
        if (_keyInput != null)
            _keyInput.enabled = true;
    }

    void ResetMovementBools()
    {
        if (_animator == null)
            return;
        if (_keyInput != null)
        {
            TrySetBool(_keyInput.wParam, false);
            TrySetBool(_keyInput.aParam, false);
            TrySetBool(_keyInput.sParam, false);
            TrySetBool(_keyInput.dParam, false);
            TrySetBool(_keyInput.runParam, false);
        }
        else
        {
            TrySetBool("W", false);
            TrySetBool("A", false);
            TrySetBool("S", false);
            TrySetBool("D", false);
            TrySetBool("Run", false);
        }
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
        if (_isSitting)
        {
            IgnoreChairColliders(_chairCollidersIgnored, false);
            _chairCollidersIgnored = null;
            EndSitPlayback();
            RestoreRootMotion();
            UnlockPlayerControl();
            _isSitting = false;
            ChairSitState.IsSitting = false;
        }
    }
}

