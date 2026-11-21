using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 在 GameServerSync 拉取并应用服务器出生点期间显示全屏加载层（可播循环视频）。
/// 在 Awake/Start 即开始 Prepare 与首帧预热，尽量让「加载锁」期间直接看到视频，而不是先黑屏再等解码。
/// </summary>
[DefaultExecutionOrder(-200)]
public class ServerSpawnLoadingOverlay : MonoBehaviour
{
    [Header("开关")]
    [SerializeField] bool enableOverlay = true;
    [Tooltip("与 GameServerSync 一致时建议勾选，避免进关卡后本脚本随场景卸载")]
    [SerializeField] bool dontDestroyOnLoadRoot = true;

    [Header("视频（Clip 优先；都空则仅黑屏，可配下方文字）")]
    [SerializeField] VideoClip loadingVideoClip;
    [Tooltip("放在 Assets/StreamingAssets/ 下的文件名，例如 Loading.mp4")]
    [SerializeField] string streamingAssetsVideoFileName;

    [Header("可选：解码完成前显示静图，避免任何黑屏间隙")]
    [SerializeField] Texture2D coverImageUntilVideoReady;

    [Header("显示")]
    [SerializeField] int canvasSortOrder = 32000;
    [Tooltip("防止同步极快时界面一闪；设为 0 可尽快关闭")]
    [SerializeField] float minDisplaySeconds = 0.4f;
    [SerializeField] float fadeOutSeconds = 0.5f;
    [Tooltip("若锁已释放但黑层仍停留超过该时长，则强制隐藏（防止事件漏掉导致黑屏）。")]
    [SerializeField] float orphanOverlayForceHideDelay = 1.0f;
    [SerializeField] bool loopVideo = true;
    [SerializeField] bool playVideoAudio;
    [SerializeField] string loadingText = "";
    [SerializeField] int videoRenderTextureWidth = 1920;
    [SerializeField] int videoRenderTextureHeight = 1080;

    GameObject _root;
    CanvasGroup _canvasGroup;
    RawImage _rawImage;
    VideoPlayer _videoPlayer;
    AudioSource _videoAudio;
    Text _label;
    RenderTexture _renderTexture;

    float _lockStartUnscaled;
    Coroutine _hideRoutine;
    Coroutine _primeRoutine;

    bool _prepared;
    bool _prepareInFlight;
    float _unlockSeenUnscaled = -1f;

    void Awake()
    {
        if (dontDestroyOnLoadRoot)
            DontDestroyOnLoad(gameObject);

        GameServerSync.OnServerSpawnSyncLockStarted += OnSpawnLockStarted;
        GameServerSync.OnServerSpawnSyncLockEnded += OnSpawnLockEnded;
        GameServerSync.OnAuthSuccess += OnAuthSuccessPrepareVideo;

        TryKickoffBackgroundPrepare();
    }

    void Start()
    {
        TryKickoffBackgroundPrepare();
    }

    void Update()
    {
        if (!enableOverlay || _root == null || !_root.activeSelf)
        {
            _unlockSeenUnscaled = -1f;
            return;
        }

        if (GameServerSync.BlocksInputForServerSpawn)
        {
            _unlockSeenUnscaled = -1f;
            return;
        }

        if (_unlockSeenUnscaled < 0f)
            _unlockSeenUnscaled = Time.unscaledTime;

        if (Time.unscaledTime - _unlockSeenUnscaled < Mathf.Max(0.1f, orphanOverlayForceHideDelay))
            return;

        HideImmediate();
        _unlockSeenUnscaled = -1f;
    }

    void OnDestroy()
    {
        GameServerSync.OnServerSpawnSyncLockStarted -= OnSpawnLockStarted;
        GameServerSync.OnServerSpawnSyncLockEnded -= OnSpawnLockEnded;
        GameServerSync.OnAuthSuccess -= OnAuthSuccessPrepareVideo;
        TeardownVideoResources();
    }

    void OnAuthSuccessPrepareVideo(string username, string token)
    {
        TryKickoffBackgroundPrepare();
    }

    void TryKickoffBackgroundPrepare()
    {
        if (!enableOverlay || !HasVideoSource()) return;
        EnsureUiBuilt();
        if (!_prepared && !_prepareInFlight)
            RequestVideoPrepare();
    }

    static bool HasVideoSource(VideoClip clip, string streamingName) =>
        clip != null || !string.IsNullOrWhiteSpace(streamingName);

    bool HasVideoSource() => HasVideoSource(loadingVideoClip, streamingAssetsVideoFileName);

    void OnSpawnLockStarted()
    {
        if (!enableOverlay) return;

        if (_hideRoutine != null)
        {
            StopCoroutine(_hideRoutine);
            _hideRoutine = null;
        }

        _lockStartUnscaled = Time.unscaledTime;
        _unlockSeenUnscaled = -1f;
        EnsureUiBuilt();
        ApplyCoverOrRtVisibility();

        _root.SetActive(true);
        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.interactable = true;

        if (!HasVideoSource())
        {
            if (_rawImage != null && coverImageUntilVideoReady != null)
            {
                _rawImage.texture = coverImageUntilVideoReady;
                _rawImage.enabled = true;
            }

            return;
        }

        if (_prepared)
        {
            _rawImage.texture = _renderTexture;
            _rawImage.enabled = true;
            _videoPlayer.time = 0d;
            _videoPlayer.isLooping = loopVideo;
            _videoPlayer.Play();
        }
        else
        {
            ApplyCoverOrRtVisibility();
            if (!_prepareInFlight)
                RequestVideoPrepare();
        }
    }

    void ApplyCoverOrRtVisibility()
    {
        if (_rawImage == null) return;
        if (coverImageUntilVideoReady != null && !_prepared)
        {
            _rawImage.texture = coverImageUntilVideoReady;
            _rawImage.enabled = true;
        }
        else if (_renderTexture != null)
        {
            _rawImage.texture = _renderTexture;
            _rawImage.enabled = HasVideoSource();
        }
    }

    void RequestVideoPrepare()
    {
        if (_videoPlayer == null || !HasVideoSource()) return;

        if (_primeRoutine != null)
        {
            StopCoroutine(_primeRoutine);
            _primeRoutine = null;
        }

        _videoPlayer.prepareCompleted -= OnVideoPrepareCompleted;
        _videoPlayer.prepareCompleted += OnVideoPrepareCompleted;
        _prepareInFlight = true;
        _prepared = false;

        if (_videoPlayer.isPlaying)
            _videoPlayer.Stop();

        ApplySourceToVideoPlayer();

        _videoPlayer.isLooping = loopVideo;
        _videoPlayer.Prepare();
    }

    void ApplySourceToVideoPlayer()
    {
        if (loadingVideoClip != null)
        {
            _videoPlayer.source = VideoSource.VideoClip;
            _videoPlayer.clip = loadingVideoClip;
            _videoPlayer.url = string.Empty;
        }
        else
        {
            _videoPlayer.source = VideoSource.Url;
            _videoPlayer.clip = null;
            _videoPlayer.url = Path.Combine(Application.streamingAssetsPath, streamingAssetsVideoFileName.Trim());
        }

        if (playVideoAudio)
        {
            _videoPlayer.controlledAudioTrackCount = 1;
            _videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            _videoPlayer.EnableAudioTrack(0, true);
            _videoPlayer.SetTargetAudioSource(0, _videoAudio);
        }
        else
            _videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
    }

    void OnVideoPrepareCompleted(VideoPlayer vp)
    {
        vp.prepareCompleted -= OnVideoPrepareCompleted;
        _prepareInFlight = false;
        _prepared = true;

        if (_root != null && _root.activeSelf)
        {
            if (_rawImage != null)
            {
                _rawImage.texture = _renderTexture;
                _rawImage.enabled = true;
            }

            vp.time = 0d;
            vp.isLooping = loopVideo;
            vp.Play();
            return;
        }

        if (_primeRoutine != null)
            StopCoroutine(_primeRoutine);
        _primeRoutine = StartCoroutine(CoPrimeFirstFrameWhileUiHidden(vp));
    }

    IEnumerator CoPrimeFirstFrameWhileUiHidden(VideoPlayer vp)
    {
        vp.time = 0d;
        vp.isLooping = loopVideo;
        vp.Play();
        yield return null;

        if (_root != null && _root.activeSelf)
        {
            _primeRoutine = null;
            yield break;
        }

        vp.Pause();
        _primeRoutine = null;
    }

    void OnSpawnLockEnded()
    {
        if (!enableOverlay || _root == null) return;
        _unlockSeenUnscaled = Time.unscaledTime;

        if (_hideRoutine != null)
            StopCoroutine(_hideRoutine);
        _hideRoutine = StartCoroutine(CoFadeOutAndHide());
    }

    IEnumerator CoFadeOutAndHide()
    {
        float shown = Time.unscaledTime - _lockStartUnscaled;
        float wait = minDisplaySeconds - shown;
        if (wait > 0f)
            yield return new WaitForSecondsRealtime(wait);

        if (fadeOutSeconds <= 0.001f)
        {
            HideImmediate();
            yield break;
        }

        float t = 0f;
        while (t < fadeOutSeconds)
        {
            t += Time.unscaledDeltaTime;
            _canvasGroup.alpha = 1f - Mathf.Clamp01(t / fadeOutSeconds);
            yield return null;
        }

        HideImmediate();
    }

    void HideImmediate()
    {
        if (_primeRoutine != null)
        {
            StopCoroutine(_primeRoutine);
            _primeRoutine = null;
        }

        if (_videoPlayer != null)
        {
            _videoPlayer.prepareCompleted -= OnVideoPrepareCompleted;
            if (_videoPlayer.isPlaying)
                _videoPlayer.Stop();
        }

        _prepareInFlight = false;
        _prepared = false;

        if (_root != null)
            _root.SetActive(false);

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }

        _hideRoutine = null;

        if (enableOverlay && HasVideoSource())
            RequestVideoPrepare();
    }

    void EnsureUiBuilt()
    {
        if (_root != null) return;

        _root = new GameObject("ServerSpawnLoadingOverlay_UI");
        _root.transform.SetParent(transform, false);

        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortOrder;

        _root.AddComponent<GraphicRaycaster>();

        var scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        _canvasGroup = _root.AddComponent<CanvasGroup>();

        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(_root.transform, false);
        var bg = bgGo.AddComponent<Image>();
        bg.color = Color.black;
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        var rawGo = new GameObject("VideoRaw");
        rawGo.transform.SetParent(_root.transform, false);
        _rawImage = rawGo.AddComponent<RawImage>();
        _rawImage.color = Color.white;
        var rawRt = _rawImage.rectTransform;
        rawRt.anchorMin = Vector2.zero;
        rawRt.anchorMax = Vector2.one;
        rawRt.offsetMin = Vector2.zero;
        rawRt.offsetMax = Vector2.zero;

        var vpGo = new GameObject("VideoPlayer");
        vpGo.transform.SetParent(_root.transform, false);
        _videoPlayer = vpGo.AddComponent<VideoPlayer>();
        _videoPlayer.playOnAwake = false;
        _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        _videoPlayer.waitForFirstFrame = true;
        _videoPlayer.skipOnDrop = true;

        _videoAudio = vpGo.AddComponent<AudioSource>();
        _videoAudio.playOnAwake = false;
        if (playVideoAudio)
        {
            _videoPlayer.controlledAudioTrackCount = 1;
            _videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            _videoPlayer.EnableAudioTrack(0, true);
            _videoPlayer.SetTargetAudioSource(0, _videoAudio);
        }
        else
            _videoPlayer.audioOutputMode = VideoAudioOutputMode.None;

        _renderTexture = new RenderTexture(
            Mathf.Max(16, videoRenderTextureWidth),
            Mathf.Max(16, videoRenderTextureHeight),
            0);
        _videoPlayer.targetTexture = _renderTexture;

        if (!string.IsNullOrEmpty(loadingText))
        {
            var textGo = new GameObject("LoadingLabel");
            textGo.transform.SetParent(_root.transform, false);
            _label = textGo.AddComponent<Text>();
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.text = loadingText;
            _label.fontSize = 36;
            _label.alignment = TextAnchor.LowerCenter;
            _label.color = new Color(1f, 1f, 1f, 0.85f);
            var tr = _label.rectTransform;
            tr.anchorMin = new Vector2(0f, 0f);
            tr.anchorMax = new Vector2(1f, 0.12f);
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
        }

        _root.SetActive(false);
    }

    void TeardownVideoResources()
    {
        if (_primeRoutine != null)
        {
            StopCoroutine(_primeRoutine);
            _primeRoutine = null;
        }

        if (_hideRoutine != null)
        {
            StopCoroutine(_hideRoutine);
            _hideRoutine = null;
        }

        if (_videoPlayer != null)
        {
            _videoPlayer.prepareCompleted -= OnVideoPrepareCompleted;
            _videoPlayer.Stop();
            _videoPlayer.targetTexture = null;
        }

        if (_rawImage != null)
            _rawImage.texture = null;

        if (_renderTexture != null)
        {
            _renderTexture.Release();
            Destroy(_renderTexture);
            _renderTexture = null;
        }

        if (_root != null)
        {
            Destroy(_root);
            _root = null;
        }
    }
}
