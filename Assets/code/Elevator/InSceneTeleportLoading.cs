using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 同场景内带加载动画的传送（与 MapSystem 地图传送一致：全屏 loading + 随机视频 + 暂停时间）。
/// 优先复用场景中 MapSystem 的 loadingScreen / VideoPlayer；也可在 Inspector 单独配置。
/// </summary>
public class InSceneTeleportLoading : MonoBehaviour
{
    static InSceneTeleportLoading _instance;

    [Header("加载 UI（留空则自动从 MapSystem 复制）")]
    public GameObject loadingScreen;
    public Image loadingProgressBar;
    public VideoPlayer videoPlayer;
    public RawImage videoRawImage;
    public VideoClip[] teleportVideoClipVariants;
    public VideoClip teleportVideoClip;
    public float teleportationDuration = 2f;
    public AudioClip teleportSound;
    [Range(0.1f, 1f)] public float teleportSoundVolume = 0.8f;

    [Header("玩家与镜头")]
    public PlayerMovement playerMovement;
    public CameraFollow cameraFollow;

    AudioSource _audioSource;
    Coroutine _routine;

    public static InSceneTeleportLoading Instance
    {
        get
        {
            if (_instance != null)
                return _instance;
            _instance = FindObjectOfType<InSceneTeleportLoading>();
            if (_instance != null)
                return _instance;

            var go = new GameObject("[InSceneTeleportLoading]");
            _instance = go.AddComponent<InSceneTeleportLoading>();
            DontDestroyOnLoad(go);
            return _instance;
        }
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        AutoBindFromMapSystemIfNeeded();
        AutoFindPlayerAndCamera();
        InitAudio();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_instance == this)
            _instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        loadingScreen = null;
        loadingProgressBar = null;
        videoPlayer = null;
        videoRawImage = null;
        playerMovement = null;
        cameraFollow = null;
        AutoBindFromMapSystemIfNeeded();
        AutoFindPlayerAndCamera();
    }

    public void TeleportTo(Vector3 worldPosition, Quaternion? playerRotation = null)
    {
        if (_routine != null)
            StopCoroutine(_routine);
        _routine = StartCoroutine(CoTeleport(worldPosition, playerRotation));
    }

    public void CancelOngoing()
    {
        if (_routine == null)
            return;
        StopCoroutine(_routine);
        _routine = null;
        RestoreAfterCancel();
    }

    public static void CancelIfAny()
    {
        if (_instance != null)
            _instance.CancelOngoing();
    }

    void AutoBindFromMapSystemIfNeeded()
    {
        if (loadingScreen != null && videoPlayer != null)
            return;

        var map = FindObjectOfType<MapSystem>();
        if (map == null)
            return;

        if (loadingScreen == null) loadingScreen = map.loadingScreen;
        if (loadingProgressBar == null) loadingProgressBar = map.loadingProgressBar;
        if (videoPlayer == null) videoPlayer = map.videoPlayer;
        if (videoRawImage == null) videoRawImage = map.videoRawImage;
        if ((teleportVideoClipVariants == null || teleportVideoClipVariants.Length == 0)
            && map.teleportVideoClipVariants != null && map.teleportVideoClipVariants.Length > 0)
            teleportVideoClipVariants = map.teleportVideoClipVariants;
        if (teleportVideoClip == null) teleportVideoClip = map.teleportVideoClip;
        if (teleportationDuration <= 0f) teleportationDuration = map.teleportationDuration;
        if (teleportSound == null) teleportSound = map.teleportSound;
        if (playerMovement == null) playerMovement = map.playerMovement;
        if (cameraFollow == null) cameraFollow = map.cameraFollow;
    }

    void AutoFindPlayerAndCamera()
    {
        if (playerMovement == null)
            playerMovement = FindObjectOfType<PlayerMovement>();
        if (cameraFollow == null)
        {
            if (Camera.main != null)
                cameraFollow = Camera.main.GetComponent<CameraFollow>();
            if (cameraFollow == null)
                cameraFollow = FindObjectOfType<CameraFollow>();
        }
    }

    void InitAudio()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.loop = false;
        _audioSource.spatialBlend = 0f;
        _audioSource.playOnAwake = false;
        _audioSource.volume = teleportSoundVolume;
    }

    void PlayTeleportSound()
    {
        if (teleportSound == null || _audioSource == null)
            return;
        _audioSource.PlayOneShot(teleportSound, teleportSoundVolume);
    }

    IEnumerator CoTeleport(Vector3 worldPosition, Quaternion? playerRotation)
    {
        try
        {
            AutoFindPlayerAndCamera();
            PlayTeleportSound();

            if (loadingScreen == null)
            {
                ExecuteTeleport(worldPosition, playerRotation);
                yield break;
            }

            AutoBindFromMapSystemIfNeeded();
            TeleportLoadingUiLayout.ApplyFullscreen(loadingScreen, videoRawImage, loadingProgressBar);
            if (loadingScreen != null)
                loadingScreen.SetActive(true);
            else if (videoRawImage != null)
                videoRawImage.gameObject.SetActive(true);

            if (videoPlayer != null && videoRawImage != null)
            {
                VideoClip fallback = teleportVideoClip != null ? teleportVideoClip : videoPlayer.clip;
                VideoClip clipToPlay = LoadingVideoRandomUtil.PickRandomClip(teleportVideoClipVariants, fallback);
                if (clipToPlay != null)
                {
                    videoPlayer.clip = clipToPlay;
                    videoRawImage.gameObject.SetActive(true);
                    TeleportLoadingUiLayout.ApplyVideoCoverUv(
                        videoRawImage, (int)clipToPlay.width, (int)clipToPlay.height);
                    videoPlayer.Play();
                }

                float elapsed = 0f;
                if (clipToPlay != null)
                {
                    while (elapsed < teleportationDuration && videoPlayer.isPlaying)
                    {
                        if (IsDeathFlowActive())
                            yield break;
                        elapsed += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }
            }

            if (IsDeathFlowActive())
                yield break;

            if (playerMovement != null)
            {
                playerMovement.isMovementPaused = true;
                Time.timeScale = 0f;
                Time.fixedDeltaTime = 0.02f * Time.timeScale;
            }

            ExecuteTeleport(worldPosition, playerRotation);

            if (IsDeathFlowActive())
                yield break;

            if (loadingProgressBar != null)
            {
                float progress = 0f;
                while (progress < 1f)
                {
                    if (IsDeathFlowActive())
                        yield break;
                    progress += Time.unscaledDeltaTime / teleportationDuration;
                    loadingProgressBar.fillAmount = Mathf.Clamp01(progress);
                    yield return null;
                }
                loadingProgressBar.fillAmount = 1f;
            }
            else
            {
                float waited = 0f;
                while (waited < teleportationDuration)
                {
                    if (IsDeathFlowActive())
                        yield break;
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            if (IsDeathFlowActive())
                yield break;

            loadingScreen.SetActive(false);
            if (playerMovement != null)
                playerMovement.isMovementPaused = false;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;

            if (videoRawImage != null)
                videoRawImage.gameObject.SetActive(false);
            if (videoPlayer != null)
                videoPlayer.Stop();

            ResetPlayerStepSound();
        }
        finally
        {
            _routine = null;
        }
    }

    bool IsDeathFlowActive()
    {
        return playerMovement != null && playerMovement.IsDeathReviveFlowActive;
    }

    void ExecuteTeleport(Vector3 worldPosition, Quaternion? playerRotation)
    {
        if (playerMovement == null)
            return;

        playerMovement.Teleport(worldPosition);

        if (playerRotation.HasValue)
            playerMovement.transform.rotation = playerRotation.Value;
    }

    void ResetPlayerStepSound()
    {
        if (playerMovement != null && playerMovement.TryGetComponent<PlayerStepSound>(out var stepSound))
            stepSound.ResetStepSoundState();
    }

    void RestoreAfterCancel()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        if (loadingScreen != null)
            loadingScreen.SetActive(false);
        if (videoRawImage != null)
            videoRawImage.gameObject.SetActive(false);
        if (videoPlayer != null)
            videoPlayer.Stop();
        if (playerMovement != null)
            playerMovement.isMovementPaused = false;
    }
}
