using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runtime-only performance policy. This script does not touch scene assets,
/// lighting setup, models, materials, or serialized UI configuration.
/// </summary>
public sealed class PerformanceRuntimeBootstrap : MonoBehaviour
{
    const int TargetFps = 120;
    const string DiagnosticsPrefsKey = "PerfDiagnosticsEnabled";
    const float DiagnosticsInterval = 2f;

    static PerformanceRuntimeBootstrap _instance;

    int _frameCount;
    float _elapsed;
    float _worstDelta;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        ApplyFramePolicy();

        if (_instance != null)
            return;

        var go = new GameObject(nameof(PerformanceRuntimeBootstrap));
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<PerformanceRuntimeBootstrap>();
    }

    static void ApplyFramePolicy()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFps;
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyFramePolicy();
        ResetDiagnosticsWindow();
    }

    void Update()
    {
        if (Application.targetFrameRate != TargetFps || QualitySettings.vSyncCount != 0)
            ApplyFramePolicy();

        if (!DiagnosticsEnabled)
            return;

        float delta = Time.unscaledDeltaTime;
        _elapsed += delta;
        _frameCount++;
        if (delta > _worstDelta)
            _worstDelta = delta;

        if (_elapsed >= DiagnosticsInterval)
        {
            float averageFps = _frameCount / Mathf.Max(0.0001f, _elapsed);
            Debug.Log($"[Perf] scene={SceneManager.GetActiveScene().name} avgFps={averageFps:F1} worstFrameMs={_worstDelta * 1000f:F1} targetFps={TargetFps}");
            ResetDiagnosticsWindow();
        }
    }

    static bool DiagnosticsEnabled =>
        PlayerPrefs.GetInt(DiagnosticsPrefsKey, 0) == 1;

    void ResetDiagnosticsWindow()
    {
        _frameCount = 0;
        _elapsed = 0f;
        _worstDelta = 0f;
    }
}
