using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 跨场景传送时在最上层全屏播放加载视频（DontDestroyOnLoad）。
/// 原场景里的 UI 会随 LoadScene 被卸掉，必须把视频挂在此层才能在「加载过程中」一直可见。
/// </summary>
[DefaultExecutionOrder(-100)]
public class TeleportSceneLoadOverlay : MonoBehaviour
{
    const int CanvasSortOrder = 32000;

    static TeleportSceneLoadOverlay _instance;

    GameObject _root;
    CanvasGroup _canvasGroup;
    RawImage _raw;
    VideoPlayer _vp;
    AudioSource _audio;
    AudioListener _overlayListener;
    RenderTexture _rt;

    bool _showing;

    public static bool IsShowing => _instance != null && _instance._showing;

    /// <summary>若配置了任意 Clip，则准备并显示全屏视频；否则立即完成的 Task。</summary>
    public static Task PrepareAndShowAsync(VideoClip[] variants, VideoClip fallback, bool loop, float volume)
    {
        if (!LoadingVideoRandomUtil.HasAnyClip(variants, fallback))
            return Task.CompletedTask;

        VideoClip clip = LoadingVideoRandomUtil.PickRandomClip(variants, fallback);
        if (clip == null)
            return Task.CompletedTask;

        EnsureInstance();
        var tcs = new TaskCompletionSource<bool>();
        _instance.StartCoroutine(_instance.CoPrepareAndShow(clip, loop, volume, () => tcs.TrySetResult(true)));
        return tcs.Task;
    }

    public static void Hide()
    {
        if (_instance != null)
            _instance.ApplyHide();
    }

    static void EnsureInstance()
    {
        if (_instance != null)
            return;

        var go = new GameObject("[TeleportSceneLoadOverlay]");
        _instance = go.AddComponent<TeleportSceneLoadOverlay>();
        DontDestroyOnLoad(go);
    }

    IEnumerator CoPrepareAndShow(VideoClip clip, bool loop, float volume, Action onDone)
    {
        BuildUiIfNeeded();

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        _showing = true;
        int w = clip != null && clip.width > 32 ? (int)clip.width : 1920;
        int h = clip != null && clip.height > 32 ? (int)clip.height : 1080;
        EnsureRenderTexture(clip);

        _vp.Stop();
        _vp.clip = clip;
        _vp.isLooping = loop;
        _vp.playOnAwake = false;
        _audio.volume = volume;

        _root.SetActive(true);
        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;

        _vp.Prepare();
        while (!_vp.isPrepared)
            yield return null;

        if (_overlayListener != null)
            _overlayListener.enabled = true;

        TeleportLoadingUiLayout.ApplyFullscreen(_root, _raw);
        TeleportLoadingUiLayout.ApplyVideoCoverUv(_raw, w, h);

        _vp.Play();
        onDone?.Invoke();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!_showing)
            return;

        if (mode == LoadSceneMode.Additive)
            return;

        ApplyHide();
    }

    void ApplyHide()
    {
        if (!_showing && (_root == null || !_root.activeSelf))
            return;

        SceneManager.sceneLoaded -= OnSceneLoaded;

        _showing = false;
        if (_overlayListener != null)
            _overlayListener.enabled = false;

        if (_canvasGroup != null)
            _canvasGroup.blocksRaycasts = false;

        if (_vp != null)
        {
            _vp.Stop();
            _vp.clip = null;
        }

        if (_root != null)
            _root.SetActive(false);
    }

    void BuildUiIfNeeded()
    {
        if (_root != null)
            return;

        _root = new GameObject("OverlayRoot");
        _root.transform.SetParent(transform, false);

        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortOrder;

        _root.AddComponent<GraphicRaycaster>();
        _canvasGroup = _root.AddComponent<CanvasGroup>();

        var scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var imgGo = new GameObject("VideoRaw");
        imgGo.transform.SetParent(_root.transform, false);
        _raw = imgGo.AddComponent<RawImage>();
        _raw.color = Color.white;
        var rt = _raw.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _vp = _root.AddComponent<VideoPlayer>();
        _vp.playOnAwake = false;
        _vp.renderMode = VideoRenderMode.RenderTexture;
        _vp.audioOutputMode = VideoAudioOutputMode.AudioSource;
        _audio = _root.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _vp.SetTargetAudioSource(0, _audio);

        // LoadScene 卸载旧场景时，原 Main Camera 上的 AudioListener 会一起消失，Video 的 AudioSource 会瞬间「无人收听」导致断音与 Console 报警。
        _overlayListener = _root.AddComponent<AudioListener>();
        _overlayListener.enabled = false;

        _root.SetActive(false);
    }

    void EnsureRenderTexture(VideoClip clip)
    {
        int w = clip != null && clip.width > 32 ? (int)clip.width : 1920;
        int h = clip != null && clip.height > 32 ? (int)clip.height : 1080;

        if (_rt != null && _rt.width == w && _rt.height == h)
        {
            _vp.targetTexture = _rt;
            _raw.texture = _rt;
            return;
        }

        if (_rt != null)
        {
            _rt.Release();
            Destroy(_rt);
        }

        _rt = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32);
        _rt.filterMode = FilterMode.Bilinear;
        _vp.targetTexture = _rt;
        _raw.texture = _rt;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_rt != null)
        {
            _rt.Release();
            Destroy(_rt);
        }

        if (_instance == this)
            _instance = null;
    }
}
