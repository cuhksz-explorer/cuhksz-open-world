using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Threading.Tasks;
using DormMall;

/// <summary>
/// 场景 UI 传送按钮：切换场景并将玩家移动到目标坐标。
/// 需挂在带 Button 的物体上；目标场景须加入 Build Settings。
/// </summary>
public partial class TeleportButton : MonoBehaviour
{
    [Header("=== 目标场景与落点 ===")]
    [Tooltip("要加载的场景名（须已在 Build Settings 中）")]
    public string targetSceneName;

    [Tooltip("进入目标场景后玩家的世界坐标")]
    public Vector3 targetPosition;

    [Tooltip("进入目标场景后玩家的欧拉角旋转")]
    public Vector3 targetRotation = Vector3.zero;

    [Tooltip("落点是否向下射线贴地（会覆盖你填的 Y 高度；一般保持关闭，直接用 targetPosition）")]
    public bool snapToGround = false;

    [Header("=== 加载进度条（可选） ===")]
    [Tooltip("是否显示加载进度条")]
    public bool useProgressBar = false;

    [Tooltip("进度条 UI Slider，不填则仅后台加载")]
    public Slider loadProgressSlider;

    [Tooltip("进度百分比文字，可选")]
    public TMPro.TextMeshProUGUI progressText;

    [Header("=== 加载视频（可选） ===")]
    [Tooltip("加载时是否播放视频")]
    public bool useLoadVideo = false;

    [Tooltip("视频面板 RectTransform")]
    public RectTransform videoPanel;

    [Tooltip("用于显示视频的 RawImage")]
    public RawImage videoRawImage;

    [Tooltip("加载时播放的视频片段")]
    public VideoClip loadVideoClip;

    [Tooltip("视频是否循环播放")]
    public bool loopVideo = true;

    [Tooltip("视频音量（0-1）")]
    [Range(0, 1)] public float videoVolume = 0.5f;

    private Button _teleportBtn;
    private VideoPlayer _videoPlayer;
    private AudioSource _videoAudioSource;

    private void Awake()
    {
        _teleportBtn = GetComponent<Button>();
        if (_teleportBtn == null)
        {
            Debug.LogError("TeleportButton 需要同物体上有 Button 组件。", this);
            return;
        }

        _teleportBtn.onClick.AddListener(OnTeleportClick);

        if (useProgressBar)
        {
            if (loadProgressSlider != null)
            {
                loadProgressSlider.gameObject.SetActive(false);
                loadProgressSlider.value = 0;
            }
        }

        InitVideoPlayer();
    }

    private void InitVideoPlayer()
    {
        if (!useLoadVideo) return;

        if (videoPanel == null)
        {
            Debug.LogError("已启用加载视频，但未指定 Video Panel。", this);
            useLoadVideo = false;
            return;
        }

        if (videoRawImage == null)
        {
            Debug.LogError("已启用加载视频，但未指定 Video RawImage。", this);
            useLoadVideo = false;
            return;
        }

        if (loadVideoClip == null)
        {
            Debug.LogWarning("未指定加载视频 Video Clip。", this);
        }

        _videoPlayer = videoPanel.gameObject.AddComponent<VideoPlayer>();
        _videoAudioSource = videoPanel.gameObject.AddComponent<AudioSource>();

        _videoPlayer.playOnAwake = false;
        _videoPlayer.isLooping = loopVideo;
        _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        _videoPlayer.targetCameraAlpha = 0;
        _videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        _videoPlayer.SetTargetAudioSource(0, _videoAudioSource);

        _videoAudioSource.volume = videoVolume;

        if (loadVideoClip != null)
        {
            _videoPlayer.clip = loadVideoClip;
        }

        CreateVideoRenderTexture();
        videoPanel.gameObject.SetActive(false);
    }

    private void CreateVideoRenderTexture()
    {
        if (_videoPlayer == null || videoRawImage == null) return;

        int width = Screen.width;
        int height = Screen.height;

        RenderTexture renderTexture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32);
        renderTexture.filterMode = FilterMode.Bilinear;
        renderTexture.wrapMode = TextureWrapMode.Clamp;

        _videoPlayer.targetTexture = renderTexture;
        videoRawImage.texture = renderTexture;
        videoRawImage.uvRect = new Rect(0, 0, 1, 1);
    }

    private async void OnTeleportClick()
    {
        if (dormRole == DormTeleportRole.ExitDorm)
        {
            await RunExitDormAsync();
            return;
        }

        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError("TeleportButton 未设置 targetSceneName。", this);
            return;
        }

        if (dormRole == DormTeleportRole.EnterDorm || DormSceneConfig.IsDormScene(targetSceneName))
            RecordDormReturnPoint(this);

        TeleportDataManager dataManager = FindObjectOfType<TeleportDataManager>();
        if (dataManager == null)
        {
            GameObject managerObj = new GameObject("[TeleportDataManager]");
            dataManager = managerObj.AddComponent<TeleportDataManager>();
            Debug.Log("已自动创建 TeleportDataManager。", managerObj);
        }

        string resolvedSceneName = SceneTeleportHelper.NormalizeSceneName(targetSceneName);
        dataManager.SetTeleportTarget(
            resolvedSceneName,
            targetPosition,
            Quaternion.Euler(targetRotation),
            snapToGround
        );

        bool useCrossSceneOverlay = useLoadVideo && loadVideoClip != null;
        try
        {
            if (useCrossSceneOverlay)
                await TeleportSceneLoadOverlay.PrepareAndShowAsync(null, loadVideoClip, loopVideo, videoVolume);

            ShowLoadingUI(skipInSceneVideo: useCrossSceneOverlay);
            await SceneTeleportHelper.LoadSceneAsync(resolvedSceneName, BuildLoadUiOptions(), this);
            CrossSceneTeleportApplicator.TryApplyPendingNow(resolvedSceneName);
        }
        finally
        {
            TeleportSceneLoadOverlay.Hide();
            HideLoadingUI();
        }
    }

    private void ShowLoadingUI(bool skipInSceneVideo = false)
    {
        if (!skipInSceneVideo && useLoadVideo && videoPanel != null && _videoPlayer != null)
        {
            TeleportLoadingUiLayout.ApplyFullscreen(videoPanel.gameObject, videoRawImage);
            videoPanel.gameObject.SetActive(true);

            if (loadVideoClip != null)
            {
                _videoPlayer.clip = loadVideoClip;
                TeleportLoadingUiLayout.ApplyVideoCoverUv(
                    videoRawImage, (int)loadVideoClip.width, (int)loadVideoClip.height);
                _videoPlayer.Play();
            }
            else
            {
                Debug.LogWarning("未指定加载视频，跳过播放。", this);
            }
        }

        if (useProgressBar && loadProgressSlider != null)
        {
            loadProgressSlider.gameObject.SetActive(true);
            loadProgressSlider.value = 0;
            if (progressText != null)
            {
                progressText.text = "0%";
            }
        }

        if (_teleportBtn != null)
        {
            _teleportBtn.interactable = false;
        }
    }

    private void HideLoadingUI()
    {
        if (useLoadVideo && videoPanel != null && _videoPlayer != null)
        {
            _videoPlayer.Stop();
            videoPanel.gameObject.SetActive(false);
        }

        if (useProgressBar && loadProgressSlider != null)
        {
            loadProgressSlider.gameObject.SetActive(false);
        }

        if (_teleportBtn != null)
        {
            _teleportBtn.interactable = true;
        }
    }

}
