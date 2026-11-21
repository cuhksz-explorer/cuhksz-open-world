using System.Collections;
using UnityEngine;

/// <summary>
/// 场景环境 / 背景音：淡入、循环播放。不会在 Awake 里立刻 Play，避免早于 Camera / CameraSwitcher 启用 AudioListener 而出现「无监听器」报警与开头卡一下。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SceneAmbientAudio : MonoBehaviour
{
    [Header("音频")]
    [Tooltip("要循环播放的音频（建议 Import 里勾选 Loop，或依赖 AudioSource.loop）")]
    [SerializeField] AudioClip clip;

    [Range(0f, 1f)]
    [Tooltip("淡入结束后的目标音量")]
    [SerializeField] float volume = 0.6f;

    [SerializeField] bool mute;

    [Header("淡入淡出")]
    [SerializeField] float fadeInDuration = 1.5f;

    [SerializeField] float fadeOutDuration = 1.2f;

    [Header("播放")]
    [SerializeField] bool playOnAwake = true;

    [Tooltip("在「默认多等一帧」之前再额外多等一帧")]
    [SerializeField] bool delayOneFrame;

    [Tooltip("若多帧后仍无任何已启用的 AudioListener，则在本物体上自动添加一个（无主相机监听器时的兜底）")]
    [SerializeField] int maxFramesWaitForListener = 16;

    AudioSource _source;
    Coroutine _fadeCoroutine;
    Coroutine _startupCoroutine;
    AudioListener _fallbackListener;

    float TargetVolume => mute ? 0f : volume;

    void Awake()
    {
        _source = GetComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = true;
        _source.spatialBlend = 0f;
        _source.mute = false;
        _source.volume = 0f;

        if (clip != null)
            _source.clip = clip;
    }

    void Start()
    {
        if (!playOnAwake || _source == null || _source.clip == null)
            return;

        _startupCoroutine = StartCoroutine(CoBeginPlayAfterListenerReady());
    }

    /// <summary>未勾选 Play On Awake 时，可手动调用。</summary>
    public void PlayAmbientWithFade()
    {
        if (_source == null || _source.clip == null)
            return;
        StopCoroutineSafe(ref _startupCoroutine);
        _startupCoroutine = StartCoroutine(CoBeginPlayAfterListenerReady());
    }

    void StopCoroutineSafe(ref Coroutine c)
    {
        if (c != null)
        {
            StopCoroutine(c);
            c = null;
        }
    }

    IEnumerator CoBeginPlayAfterListenerReady()
    {
        if (delayOneFrame)
            yield return null;

        // 再等一整帧：让本帧所有 Start（含 CameraSwitcher 启用第三人称 Listener）先执行完
        yield return null;

        int cap = Mathf.Max(0, maxFramesWaitForListener);
        for (int i = 0; i < cap && !HasEnabledAudioListenerInScene(); i++)
            yield return null;

        if (!HasEnabledAudioListenerInScene())
            EnsureFallbackAudioListener();

        if (_source != null && _source.clip != null)
        {
            _source.Play();
            RestartFadeIn();
        }

        _startupCoroutine = null;
    }

    static bool HasEnabledAudioListenerInScene()
    {
        var arr = Object.FindObjectsOfType<AudioListener>(true);
        for (int i = 0; i < arr.Length; i++)
        {
            AudioListener al = arr[i];
            if (al == null) continue;
            if (!al.enabled) continue;
            if (!al.gameObject.activeInHierarchy) continue;
            return true;
        }
        return false;
    }

    void EnsureFallbackAudioListener()
    {
        _fallbackListener = GetComponent<AudioListener>();
        if (_fallbackListener == null)
            _fallbackListener = gameObject.AddComponent<AudioListener>();
        _fallbackListener.enabled = true;
    }

    void RestartFadeIn()
    {
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }
        if (isActiveAndEnabled && _source != null && _source.isPlaying)
            _fadeCoroutine = StartCoroutine(CoFadeIn());
    }

    IEnumerator CoFadeIn()
    {
        float d = Mathf.Max(0.01f, fadeInDuration);
        float t = 0f;
        float from = _source != null ? _source.volume : 0f;

        while (t < d && _source != null)
        {
            t += Time.unscaledDeltaTime;
            _source.volume = Mathf.Clamp01(Mathf.Lerp(from, TargetVolume, t / d));
            yield return null;
        }

        if (_source != null)
            _source.volume = TargetVolume;
        _fadeCoroutine = null;
    }

    /// <summary>淡出到 0 后 Stop；可在切场景前由传送脚本调用。</summary>
    public void FadeOutAndStop(System.Action onComplete = null)
    {
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }
        StopCoroutineSafe(ref _startupCoroutine);

        if (_source == null || !_source.isPlaying)
        {
            onComplete?.Invoke();
            return;
        }
        _fadeCoroutine = StartCoroutine(CoFadeOutThenStop(onComplete));
    }

    IEnumerator CoFadeOutThenStop(System.Action onComplete)
    {
        float from = _source != null ? _source.volume : 0f;
        float d = Mathf.Max(0.01f, fadeOutDuration);
        float t = 0f;

        while (t < d && _source != null && _source.isPlaying)
        {
            t += Time.unscaledDeltaTime;
            _source.volume = Mathf.Lerp(from, 0f, t / d);
            yield return null;
        }

        if (_source != null)
        {
            _source.volume = 0f;
            _source.Stop();
        }
        _fadeCoroutine = null;
        onComplete?.Invoke();
    }

    void OnValidate()
    {
        if (!Application.isPlaying && _source == null)
            _source = GetComponent<AudioSource>();
        if (_source == null)
            return;
        _source.loop = true;
        _source.spatialBlend = 0f;
        _source.mute = false;
        if (clip != null)
            _source.clip = clip;
        if (!Application.isPlaying)
            _source.volume = volume;
    }

    /// <summary>运行时换曲：从 0 淡入并循环（此时场景中通常已有 Listener）。</summary>
    public void SetClipAndPlay(AudioClip newClip)
    {
        clip = newClip;
        if (_source == null) return;
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }
        StopCoroutineSafe(ref _startupCoroutine);

        _source.Stop();
        _source.clip = newClip;
        _source.volume = 0f;
        if (newClip != null)
        {
            if (!HasEnabledAudioListenerInScene())
                EnsureFallbackAudioListener();
            _source.Play();
            RestartFadeIn();
        }
    }

    public void SetVolume(float v)
    {
        volume = Mathf.Clamp01(v);
        if (_source == null) return;
        if (_fadeCoroutine == null && _source.isPlaying)
            _source.volume = TargetVolume;
    }

    void OnDestroy()
    {
        if (_fadeCoroutine != null)
            StopCoroutine(_fadeCoroutine);
        StopCoroutineSafe(ref _startupCoroutine);
    }
}
