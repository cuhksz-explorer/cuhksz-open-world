using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 场景传送公共逻辑（供 TeleportButton、寝室传送等复用）。
/// </summary>
public static class SceneTeleportHelper
{
    public struct LoadUiOptions
    {
        public bool useProgressBar;
        public Slider loadProgressSlider;
        public TMPro.TextMeshProUGUI progressText;
        public bool useLoadVideo;
        public RectTransform videoPanel;
        public RawImage videoRawImage;
        public VideoClip[] loadVideoClipVariants;
        public VideoClip loadVideoClip;
        public bool loopVideo;
        public float videoVolume;
    }

    public static string NormalizeSceneName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return string.Empty;

        string normalized = sceneName.Trim().Replace('\\', '/');
        if (normalized.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase))
            normalized = normalized.Substring(0, normalized.Length - ".unity".Length);
        if (normalized.EndsWith(".scene", System.StringComparison.OrdinalIgnoreCase))
            normalized = normalized.Substring(0, normalized.Length - ".scene".Length);

        int slash = normalized.LastIndexOf('/');
        if (slash >= 0 && slash < normalized.Length - 1)
            normalized = normalized.Substring(slash + 1);

        return normalized;
    }

    public static bool SceneNameMatches(string loadedSceneName, string expectedSceneName)
    {
        return string.Equals(
            NormalizeSceneName(loadedSceneName),
            NormalizeSceneName(expectedSceneName),
            System.StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryFindLocalPlayer(out GameObject player)
    {
        player = null;
        try
        {
            player = GameObject.FindWithTag("Player");
        }
        catch (UnityException)
        {
            player = null;
        }

        if (player != null)
            return true;

        var movement = Object.FindObjectOfType<PlayerMovement>();
        if (movement == null)
            return false;

        player = movement.gameObject;
        return true;
    }

    public static void ApplyPlayerTransform(Vector3 position, Vector3 rotationEuler)
    {
        if (!TryFindLocalPlayer(out var player))
        {
            Debug.LogWarning("[SceneTeleport] 未找到主角，无法设置传送坐标。");
            return;
        }

        var movement = player.GetComponent<PlayerMovement>();
        if (movement != null)
            movement.Teleport(position);
        else
            player.transform.position = position;

        player.transform.rotation = Quaternion.Euler(rotationEuler);
    }

    public static async Task LoadSceneAsync(string sceneName, LoadUiOptions ui, Object logContext = null)
    {
        string resolved = NormalizeSceneName(sceneName);
        if (string.IsNullOrEmpty(resolved))
        {
            Debug.LogError("[SceneTeleport] 场景名为空。", logContext);
            return;
        }

        bool useCrossSceneOverlay = ui.useLoadVideo &&
            LoadingVideoRandomUtil.HasAnyClip(ui.loadVideoClipVariants, ui.loadVideoClip);

        try
        {
            if (useCrossSceneOverlay)
                await TeleportSceneLoadOverlay.PrepareAndShowAsync(
                    ui.loadVideoClipVariants, ui.loadVideoClip, ui.loopVideo, ui.videoVolume);

            if (ui.useProgressBar)
                await LoadSceneWithProgress(resolved, ui, logContext);
            else
                await LoadSceneWithoutProgress(resolved, logContext);
        }
        finally
        {
            TeleportSceneLoadOverlay.Hide();
        }
    }

    static async Task LoadSceneWithProgress(string sceneName, LoadUiOptions ui, Object logContext)
    {
        var asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        if (asyncLoad == null)
        {
            Debug.LogError(BuildSceneLoadError(sceneName), logContext);
            return;
        }

        asyncLoad.allowSceneActivation = false;
        while (!asyncLoad.isDone)
        {
            float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
            if (ui.useProgressBar && ui.loadProgressSlider != null)
            {
                ui.loadProgressSlider.value = progress;
                if (ui.progressText != null)
                    ui.progressText.text = $"{Mathf.RoundToInt(progress * 100)}%";
            }

            if (progress >= 1.0f)
                asyncLoad.allowSceneActivation = true;

            await Task.Yield();
        }
    }

    static async Task LoadSceneWithoutProgress(string sceneName, Object logContext)
    {
        var asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        if (asyncLoad == null)
        {
            Debug.LogError(BuildSceneLoadError(sceneName), logContext);
            return;
        }

        while (!asyncLoad.isDone)
            await Task.Yield();
    }

    public static string BuildSceneLoadError(string sceneName)
    {
        return $"[SceneTeleport] 无法加载场景 \"{sceneName}\"。请确认已加入 Build Settings。";
    }
}
