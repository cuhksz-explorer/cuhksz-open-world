using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局 2D 音效播放（UI / 任务 / 系统）。从 Resources/Music 或 Inspector 引用加载。
/// </summary>
public class GameAudioManager : MonoBehaviour
{
    public static GameAudioManager Instance { get; private set; }

    [Header("音量")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float uiVolume = 0.85f;
    [Range(0f, 1f)] public float systemVolume = 0.9f;

    [Header("常用 UI 音效（可拖入，未指定则从 Resources 加载）")]
    public AudioClip clickDefault;
    public AudioClip panelOpen;
    public AudioClip panelClose;
    public AudioClip success;
    public AudioClip error;
    public AudioClip notification;

    [Header("系统音效")]
    public AudioClip teleportNormal;
    public AudioClip questComplete;
    public AudioClip coinGain;
    public AudioClip dialoguePage;
    public AudioClip deathStinger;
    public AudioClip revive;

    AudioSource _source;
    readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _source = GetComponent<AudioSource>();
        if (_source == null)
            _source = gameObject.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.spatialBlend = 0f;

        clickDefault = clickDefault ?? Load("SFX_UI_click_default");
        panelOpen = panelOpen ?? Load("SFX_UI_panel_open");
        panelClose = panelClose ?? Load("SFX_UI_panel_close");
        success = success ?? Load("SFX_UI_success");
        error = error ?? Load("SFX_UI_error");
        notification = notification ?? Load("SFX_UI_notification");
        teleportNormal = teleportNormal ?? Load("SFX_System_teleport_scifi");
        questComplete = questComplete ?? Load("SFX_System_quest_chain_complete");
        coinGain = coinGain ?? Load("SFX_System_coin_gain");
        dialoguePage = dialoguePage ?? Load("SFX_System_dialogue_page");
        deathStinger = deathStinger ?? Load("SFX_System_death_stinger");
        revive = revive ?? Load("SFX_System_revive");
    }

    AudioClip Load(string resourcesName)
    {
        if (_cache.TryGetValue(resourcesName, out var cached))
            return cached;
        var clip = Resources.Load<AudioClip>($"Music/{resourcesName}");
        if (clip != null)
            _cache[resourcesName] = clip;
        return clip;
    }

    public void PlayUi(AudioClip clip, float volumeScale = 1f)
    {
        Play2D(clip, uiVolume * volumeScale);
    }

    public void PlaySystem(AudioClip clip, float volumeScale = 1f)
    {
        Play2D(clip, systemVolume * volumeScale);
    }

    public void Play2D(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || _source == null)
            return;
        _source.PlayOneShot(clip, Mathf.Clamp01(masterVolume * volumeScale));
    }

    public void PlayClick() => PlayUi(clickDefault);
    public void PlayPanelOpen() => PlayUi(panelOpen);
    public void PlayPanelClose() => PlayUi(panelClose);
    public void PlaySuccess() => PlayUi(success);
    public void PlayError() => PlayUi(error);
    public void PlayNotification() => PlayUi(notification);
    public void PlayTeleport() => PlaySystem(teleportNormal);
    public void PlayQuestComplete() => PlaySystem(questComplete);
    public void PlayCoinGain() => PlaySystem(coinGain);
    public void PlayDialoguePage() => PlaySystem(dialoguePage);
    public void PlayDeath() => PlaySystem(deathStinger, 0.9f);
    public void PlayRevive() => PlaySystem(revive);
}
