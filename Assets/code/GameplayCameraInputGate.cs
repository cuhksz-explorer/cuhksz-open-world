using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 独占 UI 打开时统一解锁光标并暂停环视；关闭后恢复 suppressMouseLook 与光标锁定。
/// </summary>
public static class GameplayCameraInputGate
{
    static int _suppressMouseLookCacheFrame = -1;
    static int _restoreAfterModalUiFrame = -1;
    static bool _modalUiWasBlocking;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void BootstrapModalUiWatcher()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureModalUiWatcher();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive)
            return;
        EnsureModalUiWatcher();
    }

    static void EnsureModalUiWatcher()
    {
        if (ModalUiWatcher.Instance != null)
            return;
        var go = new GameObject("[GameplayCameraInputGate]");
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<ModalUiWatcher>();
    }

    /// <summary>在相机 Update/LateUpdate 开头调用，处理 UI 开关边沿。</summary>
    public static void SyncModalUiCameraInput()
    {
        bool blocking = GameplayUiHotkeyGuard.BlocksCameraMouseLook();
        if (blocking)
        {
            _modalUiWasBlocking = true;
            EnsureCursorUnlockedForModalUi();
            return;
        }

        if (_modalUiWasBlocking)
        {
            _modalUiWasBlocking = false;
            RestoreGameplayCameraAfterModalUi();
        }
    }

    public static void EnsureCursorUnlockedForModalUi()
    {
        if (!GameplayUiHotkeyGuard.BlocksCameraMouseLook())
            return;

        if (GameplayCursorLock.IsLocked)
            GameplayCursorLock.UnlockForModalUi();

        ApplyFirstPersonSuppressMouseLook();
    }

    static void RestoreGameplayCameraAfterModalUi()
    {
        if (_restoreAfterModalUiFrame == Time.frameCount)
            return;
        _restoreAfterModalUiFrame = Time.frameCount;

        var fpsCameras = Object.FindObjectsOfType<FirstPersonCamera>(true);
        for (int i = 0; i < fpsCameras.Length; i++)
        {
            var fps = fpsCameras[i];
            if (fps != null)
                fps.suppressMouseLook = false;
        }

        var switchers = Object.FindObjectsOfType<CameraSwitcher>(true);
        for (int i = 0; i < switchers.Length; i++)
        {
            var sw = switchers[i];
            if (sw == null)
                continue;
            sw.RestoreFirstPersonMouseLookIfActive();
            sw.RestoreThirdPersonMouseLookIfActive();
        }

        // 关闭独占 UI 后应立刻恢复环视；勿用 TryApplyGameplayDefaultLock（UI 内 SetLocked(false) 会误记 Esc 解锁）。
        GameplayCursorLock.LockForGameplayView();
    }

    static void ApplyFirstPersonSuppressMouseLook()
    {
        if (_suppressMouseLookCacheFrame == Time.frameCount)
            return;
        _suppressMouseLookCacheFrame = Time.frameCount;

        var fpsCameras = Object.FindObjectsOfType<FirstPersonCamera>(true);
        for (int i = 0; i < fpsCameras.Length; i++)
        {
            var fps = fpsCameras[i];
            if (fps == null || !fps.IsActive)
                continue;
            fps.suppressMouseLook = true;
            if (GameplayCursorLock.IsLocked)
                GameplayCursorLock.UnlockForModalUi();
            fps.ReconcileWalkingControlState();
        }
    }

    sealed class ModalUiWatcher : MonoBehaviour
    {
        public static ModalUiWatcher Instance { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void LateUpdate()
        {
            SyncModalUiCameraInput();
        }
    }
}
