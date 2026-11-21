using UnityEngine;

/// <summary>
/// 场景加载后下一帧再次绑定共用交互面板，避免 Awake/Start 与 sceneLoaded 顺序导致引用仍指向已删除副本。
/// </summary>
[DefaultExecutionOrder(10000)]
sealed class InteractPromptSceneRebindRunner : MonoBehaviour
{
    static InteractPromptSceneRebindRunner _instance;
    int _rebindFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureInstance();
    }

    public static void ScheduleNextFrameRebind()
    {
        EnsureInstance();
        _instance._rebindFrame = Time.frameCount + 1;
    }

    static void EnsureInstance()
    {
        if (_instance != null)
            return;

        var go = new GameObject(nameof(InteractPromptSceneRebindRunner));
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<InteractPromptSceneRebindRunner>();
    }

    void LateUpdate()
    {
        InteractPromptSharedUiLocator.EnsureGameplayHudCanvasVisible();
        InteractPromptSharedLabelFrame.FinalizeSharedPanelForFrame();

        if (_rebindFrame < 0 || Time.frameCount < _rebindFrame)
            return;

        _rebindFrame = -1;
        InteractPromptSharedUiLocator.RefreshConsumersAfterSceneReady();
    }
}
