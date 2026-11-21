using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景加载后修复 AudioListener、空音频引用，并确保 GameAudioManager 存在。
/// </summary>
public static class GameAudioRuntimeBootstrap
{
    static readonly Dictionary<string, string> SceneBgmResources =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["uppergarden"] = "BGM_uppergarden",
            ["middlegarden"] = "BGM_uppergarden",
            ["undergarden"] = "BGM_uppergarden",
            ["minival"] = "BGM_uppergarden",
            ["lingcollegea"] = "BGM_uppergarden",
            ["lingcollegeb"] = "BGM_uppergarden",
            ["lingcollegec"] = "BGM_uppergarden",
            ["dlilgentiac"] = "BGM_uppergarden",
            ["lingdrom"] = "BGM_uppergarden",
            ["loadingscene"] = "BGM_uppergarden",
            ["bus"] = "BGM_uppergarden",
            ["dorm"] = "BGM_uppergarden",
            ["library"] = "BGM_library",
            ["libraryfloor1"] = "BGM_library",
            ["libraryfloor2"] = "BGM_library",
            ["libraryfloor4"] = "BGM_library",
        };

    static readonly Dictionary<string, string> SceneAmbientResources =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["undergarden"] = "BGM_uppergarden",
            ["lingdrom"] = "BGM_uppergarden",
        };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void OnAfterSceneLoad()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ScheduleRepair();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive)
            return;
        ScheduleRepair();
    }

    static void ScheduleRepair()
    {
        var host = DeferredRepairHost.Ensure();
        host.RequestRepair(2);
    }

    public static void RepairNow()
    {
        EnsureGameAudioManager();
        RepairGameAudioManagerSource();
        GameplayAudioListenerUtility.EnsureGameplayListener();
        RepairBackgroundMusic();
        RepairSceneAmbientAudio();
        RepairPlayerStepSounds();
        GameplayAudioListenerUtility.EnsureGameplayListener();
    }

    static void EnsureGameAudioManager()
    {
        if (GameAudioManager.Instance != null)
            return;

        var existing = UnityEngine.Object.FindObjectOfType<GameAudioManager>(true);
        if (existing != null)
            return;

        var go = new GameObject("GameAudioManager");
        go.AddComponent<AudioSource>();
        go.AddComponent<GameAudioManager>();
        UnityEngine.Object.DontDestroyOnLoad(go);
    }

    static void RepairGameAudioManagerSource()
    {
        foreach (var manager in UnityEngine.Object.FindObjectsOfType<GameAudioManager>(true))
        {
            if (manager == null)
                continue;

            var source = manager.GetComponent<AudioSource>();
            if (source == null)
                continue;

            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            if (source.clip != null)
            {
                source.Stop();
                source.clip = null;
            }
        }
    }

    static void RepairBackgroundMusic()
    {
        string scene = SceneManager.GetActiveScene().name;
        SceneBgmResources.TryGetValue(scene, out string resName);

        foreach (var bgm in UnityEngine.Object.FindObjectsOfType<BackgroundMusic>(true))
        {
            if (bgm == null)
                continue;

            if (bgm.bgmClip == null && !string.IsNullOrEmpty(resName))
                bgm.bgmClip = Resources.Load<AudioClip>($"Music/{resName}");

            var source = bgm.GetComponent<AudioSource>();
            if (source != null)
            {
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.ignoreListenerPause = true;
                if (bgm.bgmClip != null)
                    source.clip = bgm.bgmClip;
            }

            if (bgm.playOnStart && bgm.bgmClip != null && source != null)
                bgm.PlayBGM();
        }
    }

    static void RepairSceneAmbientAudio()
    {
        string scene = SceneManager.GetActiveScene().name;
        SceneAmbientResources.TryGetValue(scene, out string resName);

        foreach (var amb in UnityEngine.Object.FindObjectsOfType<SceneAmbientAudio>(true))
        {
            if (amb == null)
                continue;

            var source = amb.GetComponent<AudioSource>();
            if (source != null)
            {
                source.spatialBlend = 0f;
                source.ignoreListenerPause = true;
            }

            if (!string.IsNullOrEmpty(resName))
            {
                var clip = Resources.Load<AudioClip>($"Music/{resName}");
                if (clip != null)
                    amb.SetClipAndPlay(clip);
            }
            else
            {
                amb.PlayAmbientWithFade();
            }
        }
    }

    static void RepairPlayerStepSounds()
    {
        foreach (var step in UnityEngine.Object.FindObjectsOfType<PlayerStepSound>(true))
        {
            if (step == null)
                continue;

            if (step.walkClip == null)
                step.walkClip = Resources.Load<AudioClip>("Music/SFX_System_teleport_scifi");

            var sources = step.GetComponents<AudioSource>();
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] == null)
                    continue;

                sources[i].spatialBlend = 0f;
                sources[i].ignoreListenerPause = true;

                if (i > 0)
                {
                    sources[i].playOnAwake = false;
                    sources[i].Stop();
                    sources[i].clip = null;
                }
            }
        }
    }

    sealed class DeferredRepairHost : MonoBehaviour
    {
        public static DeferredRepairHost Ensure()
        {
            var existing = UnityEngine.Object.FindObjectOfType<DeferredRepairHost>();
            if (existing != null)
                return existing;

            var go = new GameObject("[GameAudioRuntimeBootstrap]");
            go.hideFlags = HideFlags.HideAndDontSave;
            return go.AddComponent<DeferredRepairHost>();
        }

        int _framesLeft;

        public void RequestRepair(int frames)
        {
            _framesLeft = Mathf.Max(_framesLeft, frames);
            enabled = true;
        }

        void Update()
        {
            if (_framesLeft > 0)
            {
                _framesLeft--;
                return;
            }

            try
            {
                RepairNow();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GameAudioRuntimeBootstrap] Repair failed: " + ex.Message);
            }

            enabled = false;
        }
    }
}
