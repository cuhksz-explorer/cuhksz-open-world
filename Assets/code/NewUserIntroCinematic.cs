using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 新账号注册成功后，在首次进入游戏场景时全屏播放一次开场视频（本地记录，换设备会再播）。
/// 右上角「跳过」→ 居中二次确认（参考崩坏三）。登录不会触发。
/// </summary>
[DefaultExecutionOrder(-50)]
public class NewUserIntroCinematic : MonoBehaviour
{
    public static NewUserIntroCinematic Instance { get; private set; }

    /// <summary>开场结束或确认跳过后触发（可用于衔接新手任务等）。</summary>
    public static event Action OnIntroFinishedOrSkipped;

    public static bool IsPlayingIntro { get; private set; }

    const string PrefsPrefix = "NewUserIntroWatched_v1_";

    [Header("视频（二选一：Clip 优先）")]
    [SerializeField] VideoClip introVideoClip;
    [Tooltip("使用 StreamingAssets 下文件名，例如 Intro.mp4（各平台路径由 Unity 处理）")]
    [SerializeField] string streamingAssetsVideoFileName;

    [Header("UI（可由一键生成自动绑定）")]
    [SerializeField] Canvas rootCanvas;
    [SerializeField] CanvasGroup rootCanvasGroup;
    [SerializeField] RawImage videoRawImage;
    [SerializeField] VideoPlayer videoPlayer;
    [SerializeField] AudioSource videoAudioSource;
    [SerializeField] Button skipButton;
    [SerializeField] GameObject confirmPanel;
    [SerializeField] Button confirmSkipButton;
    [SerializeField] Button cancelSkipButton;
    [SerializeField] TMP_Text confirmTitleText;
    [SerializeField] TMP_Text confirmBodyText;
    [SerializeField] TMP_Text confirmYesText;
    [SerializeField] TMP_Text confirmNoText;

    [Header("文案")]
    [SerializeField] string skipButtonLabel = "跳过";
    [SerializeField] string confirmTitle = "跳过动画？";
    [SerializeField] string confirmBody = "当前为重要剧情演出，确认要跳过吗？";
    [SerializeField] string confirmYesLabel = "确认跳过";
    [SerializeField] string confirmNoLabel = "返回";

    [Header("场景")]
    [Tooltip("在这些场景加载时不会尝试播放（仍在加载/菜单等）")]
    [SerializeField] string[] excludedSceneNames = { "Loadingscene", "MainMenu", "Menu" };

    [Header("行为")]
    [SerializeField] bool dontDestroyOnLoad = true;
    [SerializeField] bool pauseTimeScaleWhilePlaying = true;
    [SerializeField] int canvasSortOrder = 32750;
    [SerializeField] int renderTextureWidth = 1920;
    [SerializeField] int renderTextureHeight = 1080;
    [Tooltip("编辑器内无注册流程时，勾选后首次进 Play 会尝试播放（调试用）")]
    [SerializeField] bool editorSimulatePendingIntro;

    [SerializeField] bool verboseLog = true;

    RenderTexture _rt;
    string _pendingUsername;
    bool _playRoutineActive;
    bool _userChoseSkip;
    float _savedTimeScale = 1f;
    Coroutine _playCo;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        if (rootCanvas != null)
            rootCanvas.sortingOrder = canvasSortOrder;

        HydratePendingFromPrefs();

        GameServerSync.OnRegisterSuccess += OnRegisterSuccess;
        SceneManager.sceneLoaded += OnSceneLoaded;

        ApplyLocalizedLabels();
        Loc.OnLanguageChanged += ApplyLocalizedLabels;
        SetRootVisible(false);
        if (confirmPanel != null)
            confirmPanel.SetActive(false);
    }

    void Start()
    {
        HydratePendingFromPrefs();
#if UNITY_EDITOR
        if (editorSimulatePendingIntro && string.IsNullOrEmpty(_pendingUsername))
        {
            var sync = GameServerSync.Instance;
            if (sync != null && !string.IsNullOrEmpty(sync.SavedUsername) && !HasWatchedIntro(sync.SavedUsername))
                _pendingUsername = sync.SavedUsername;
            else
                OnRegisterSuccess("EditorIntroTest");
        }
#endif
        // 若 sceneLoaded 早于本物体生成或未触发，进场景后首帧再试一次
        TryBeginIntroIfNeeded(SceneManager.GetActiveScene());
    }

    void OnDestroy()
    {
        Loc.OnLanguageChanged -= ApplyLocalizedLabels;
        GameServerSync.OnRegisterSuccess -= OnRegisterSuccess;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this)
            Instance = null;
        ReleaseRt();
    }

    void OnRegisterSuccess(string username)
    {
        if (string.IsNullOrEmpty(username)) return;
        if (HasWatchedIntro(username))
            return;
        _pendingUsername = username;
        PlayerPrefs.SetString(GameServerSync.PrefsIntroCinematicPendingUserKey, username);
        PlayerPrefs.Save();
        if (verboseLog)
            Debug.Log($"[NewUserIntro] 注册成功，将在进入游戏场景后播放开场：{username}");
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HydratePendingFromPrefs();
        TryBeginIntroIfNeeded(scene);
    }

    /// <summary>从 PlayerPrefs 恢复待播用户名（注册时可能尚未生成本组件，或切场景时实例曾销毁）。</summary>
    void HydratePendingFromPrefs()
    {
        string fromPrefs = PlayerPrefs.GetString(GameServerSync.PrefsIntroCinematicPendingUserKey, "");
        if (string.IsNullOrEmpty(fromPrefs)) return;

        var sync = GameServerSync.Instance;
        if (sync != null && sync.IsLoggedIn &&
            !string.Equals(fromPrefs, sync.SavedUsername, StringComparison.Ordinal))
        {
            ClearPendingPrefs();
            if (verboseLog)
                Debug.Log("[NewUserIntro] 待播用户名与当前登录不一致，已清除待播标记。");
            return;
        }

        if (HasWatchedIntro(fromPrefs))
        {
            ClearPendingPrefs();
            return;
        }

        if (string.IsNullOrEmpty(_pendingUsername))
            _pendingUsername = fromPrefs;
    }

    static void ClearPendingPrefs()
    {
        PlayerPrefs.DeleteKey(GameServerSync.PrefsIntroCinematicPendingUserKey);
        PlayerPrefs.Save();
    }

    void TryBeginIntroIfNeeded(Scene scene)
    {
        HydratePendingFromPrefs();
        if (string.IsNullOrEmpty(_pendingUsername)) return;
        if (HasWatchedIntro(_pendingUsername))
        {
            _pendingUsername = null;
            ClearPendingPrefs();
            return;
        }

        if (IsSceneExcluded(scene.name))
            return;

        if (!HasPlayableSource())
        {
            if (verboseLog)
                Debug.LogWarning("[NewUserIntro] 未配置 VideoClip 或 StreamingAssets 视频，无法播放开场。请赋值后再测；已清除待播以免反复提示。");
            _pendingUsername = null;
            ClearPendingPrefs();
            return;
        }

        if (_playRoutineActive) return;
        if (verboseLog)
            Debug.Log($"[NewUserIntro] 尝试播放开场，场景={scene.name}，用户={_pendingUsername}");
        _playCo = StartCoroutine(CoPlayIntro());
    }

    bool HasPlayableSource()
    {
        if (introVideoClip != null) return true;
        return !string.IsNullOrWhiteSpace(streamingAssetsVideoFileName);
    }

    bool IsSceneExcluded(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || excludedSceneNames == null) return false;
        for (int i = 0; i < excludedSceneNames.Length; i++)
        {
            var n = excludedSceneNames[i];
            if (!string.IsNullOrEmpty(n) && string.Equals(n.Trim(), sceneName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    IEnumerator CoPlayIntro()
    {
        _playRoutineActive = true;
        IsPlayingIntro = true;
        _userChoseSkip = false;

        if (pauseTimeScaleWhilePlaying)
        {
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        EnsureVideoInfrastructure();
        if (videoPlayer == null || videoRawImage == null)
        {
            Debug.LogError("[NewUserIntro] VideoPlayer/RawImage 无效，取消开场。");
            RestoreTimeScale();
            _pendingUsername = null;
            ClearPendingPrefs();
            IsPlayingIntro = false;
            _playRoutineActive = false;
            _playCo = null;
            yield break;
        }

        SetupVideoSource();
        WireUiCallbacks();
        ApplyLocalizedLabels();

        SetRootVisible(true);
        if (confirmPanel != null)
            confirmPanel.SetActive(false);

        videoPlayer.Prepare();
        float wait = 0f;
        while (!videoPlayer.isPrepared && wait < 20f)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!videoPlayer.isPrepared)
        {
            Debug.LogError("[NewUserIntro] 视频准备失败或超时，已跳过开场并标记为已观看（避免反复重试）。");
            MarkWatched(_pendingUsername);
            _pendingUsername = null;
            ClearPendingPrefs();
            SetRootVisible(false);
            videoPlayer.Stop();
            RestoreTimeScale();
            IsPlayingIntro = false;
            _playRoutineActive = false;
            _playCo = null;
            OnIntroFinishedOrSkipped?.Invoke();
            yield break;
        }

        if (videoRawImage != null && _rt != null)
            videoRawImage.texture = _rt;

        bool finished = false;
        void OnLoopPoint(VideoPlayer vp) => finished = true;
        videoPlayer.loopPointReached += OnLoopPoint;

        videoPlayer.Play();

        while (videoPlayer.isPlaying && !finished && !_userChoseSkip)
            yield return null;

        videoPlayer.loopPointReached -= OnLoopPoint;
        videoPlayer.Stop();

        MarkWatched(_pendingUsername);
        _pendingUsername = null;
        ClearPendingPrefs();

        SetRootVisible(false);
        RestoreTimeScale();

        IsPlayingIntro = false;
        _playRoutineActive = false;
        _playCo = null;

        OnIntroFinishedOrSkipped?.Invoke();
    }

    void RestoreTimeScale()
    {
        if (!pauseTimeScaleWhilePlaying) return;
        Time.timeScale = Mathf.Approximately(_savedTimeScale, 0f) ? 1f : _savedTimeScale;
    }

    void EnsureVideoInfrastructure()
    {
        if (videoPlayer == null && rootCanvas != null)
            videoPlayer = rootCanvas.GetComponentInChildren<VideoPlayer>(true);
        if (videoRawImage == null && rootCanvas != null)
            videoRawImage = rootCanvas.GetComponentInChildren<RawImage>(true);

        if (videoPlayer == null || videoRawImage == null)
        {
            Debug.LogError("[NewUserIntro] 缺少 VideoPlayer 或 RawImage。");
            return;
        }

        ReleaseRt();
        _rt = new RenderTexture(renderTextureWidth, renderTextureHeight, 0);
        videoPlayer.targetTexture = _rt;
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;

        if (videoAudioSource == null)
            videoAudioSource = videoPlayer.GetComponent<AudioSource>();
        if (videoAudioSource == null)
            videoAudioSource = gameObject.AddComponent<AudioSource>();
        videoAudioSource.playOnAwake = false;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        videoPlayer.EnableAudioTrack(0, true);
        videoPlayer.SetTargetAudioSource(0, videoAudioSource);
    }

    void SetupVideoSource()
    {
        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.clip = null;
        videoPlayer.url = "";

        if (introVideoClip != null)
        {
            videoPlayer.clip = introVideoClip;
        }
        else if (!string.IsNullOrWhiteSpace(streamingAssetsVideoFileName))
        {
            videoPlayer.source = VideoSource.Url;
            string name = streamingAssetsVideoFileName.Trim();
#if UNITY_ANDROID && !UNITY_EDITOR
            videoPlayer.url = Path.Combine(Application.streamingAssetsPath, name);
#else
            string path = Path.Combine(Application.streamingAssetsPath, name);
            if (!File.Exists(path))
            {
                Debug.LogError($"[NewUserIntro] StreamingAssets 中找不到视频：{path}");
                return;
            }
            videoPlayer.url = path;
#endif
        }
    }

    void WireUiCallbacks()
    {
        if (skipButton != null)
        {
            skipButton.onClick.RemoveListener(OnSkipClicked);
            skipButton.onClick.AddListener(OnSkipClicked);
        }
        if (confirmSkipButton != null)
        {
            confirmSkipButton.onClick.RemoveListener(OnConfirmSkip);
            confirmSkipButton.onClick.AddListener(OnConfirmSkip);
        }
        if (cancelSkipButton != null)
        {
            cancelSkipButton.onClick.RemoveListener(OnCancelSkip);
            cancelSkipButton.onClick.AddListener(OnCancelSkip);
        }
    }

    void ApplyLocalizedLabels()
    {
        if (skipButton != null)
        {
            var tmp = skipButton.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) tmp.text = Loc.Choose(skipButtonLabel, "Skip");
        }
        if (confirmTitleText != null) confirmTitleText.text = Loc.Choose(confirmTitle, "Skip cutscene?");
        if (confirmBodyText != null) confirmBodyText.text = Loc.Choose(confirmBody, "This is an important story scene. Skip anyway?");
        if (confirmYesText != null) confirmYesText.text = Loc.Choose(confirmYesLabel, "Skip");
        if (confirmNoText != null) confirmNoText.text = Loc.Choose(confirmNoLabel, "Back");
    }

    void OnSkipClicked()
    {
        if (confirmPanel != null)
            confirmPanel.SetActive(true);
    }

    void OnCancelSkip()
    {
        if (confirmPanel != null)
            confirmPanel.SetActive(false);
    }

    void OnConfirmSkip()
    {
        _userChoseSkip = true;
        if (confirmPanel != null)
            confirmPanel.SetActive(false);
    }

    void SetRootVisible(bool v)
    {
        // 不要用 SetActive(false) 关掉整棵 Canvas：脚本与协程挂在同一物体上会失效，导致无法播片。
        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = v ? 1f : 0f;
            rootCanvasGroup.interactable = v;
            rootCanvasGroup.blocksRaycasts = v;
        }
        else if (rootCanvas != null)
            rootCanvas.gameObject.SetActive(v);
    }

    void ReleaseRt()
    {
        if (_rt != null)
        {
            if (videoPlayer != null && videoPlayer.targetTexture == _rt)
                videoPlayer.targetTexture = null;
            Destroy(_rt);
            _rt = null;
        }
    }

    public static bool HasWatchedIntro(string username)
    {
        string key = PrefsKeyForUser(username);
        return !string.IsNullOrEmpty(key) && PlayerPrefs.GetInt(key, 0) == 1;
    }

    static void MarkWatched(string username)
    {
        string key = PrefsKeyForUser(username);
        if (string.IsNullOrEmpty(key)) return;
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
    }

    static string PrefsKeyForUser(string username)
    {
        if (string.IsNullOrEmpty(username)) return null;
        using (var sha1 = SHA1.Create())
        {
            var bytes = Encoding.UTF8.GetBytes(username);
            var hash = sha1.ComputeHash(bytes);
            var sb = new StringBuilder(16);
            for (int i = 0; i < 8; i++)
                sb.Append(hash[i].ToString("x2"));
            return PrefsPrefix + sb;
        }
    }

    /// <summary>调试：清除当前用户观看记录并标记待播放（仅本地）。</summary>
    [ContextMenu("Debug/Clear watched flag for SavedUsername")]
    void DebugClearWatched()
    {
        var sync = GameServerSync.Instance;
        string u = sync != null ? sync.SavedUsername : "";
        if (string.IsNullOrEmpty(u)) return;
        PlayerPrefs.DeleteKey(PrefsKeyForUser(u));
        PlayerPrefs.Save();
        Debug.Log($"[NewUserIntro] 已清除观看记录：{u}");
    }
}
