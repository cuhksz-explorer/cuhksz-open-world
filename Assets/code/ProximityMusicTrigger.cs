using System.Collections;
using UnityEngine;

/// <summary>
/// 玩家进入指定世界坐标半径内时，淡入播放背景音乐；离开后淡出停止。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class ProximityMusicTrigger : MonoBehaviour
{
    [Header("触发区域")]
    [SerializeField] Vector3 triggerWorldPosition = new Vector3(-1360.78003f, 13.6599998f, 1100.43005f);

    [SerializeField] float triggerRadius = 20f;

    [Tooltip("仅比较水平距离（忽略 Y），适合平地校园场景")]
    [SerializeField] bool useHorizontalDistance = true;

    [Header("音频")]
    [SerializeField] AudioClip musicClip;

    [Tooltip("未指定 Clip 时从 Resources/Music/我和我的祖国 加载")]
    [SerializeField] bool loadClipFromResources = true;

    [Tooltip("Resources 下路径（不含扩展名）")]
    [SerializeField] string resourcesClipPath = "Music/我和我的祖国";

    [Range(0f, 1f)]
    [SerializeField] float volume = 0.22f;

    [SerializeField] bool loop = true;

    [Header("淡入淡出")]
    [SerializeField] float fadeInDuration = 2.5f;

    [SerializeField] float fadeOutDuration = 2f;

    [SerializeField] float checkInterval = 0.15f;

    [Header("玩家")]
    [SerializeField] PlayerMovement playerMovement;

    AudioSource _source;
    Coroutine _fadeRoutine;
    Coroutine _pollRoutine;
    bool _insideZone;
    bool _wasPlaying;

    public Vector3 TriggerWorldPosition
    {
        get => triggerWorldPosition;
        set => triggerWorldPosition = value;
    }

    public float TriggerRadius
    {
        get => triggerRadius;
        set => triggerRadius = Mathf.Max(0.1f, value);
    }

    void Awake()
    {
        _source = GetComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = loop;
        _source.spatialBlend = 0f;
        _source.volume = 0f;
        ResolveMusicClip();
        if (musicClip != null)
            _source.clip = musicClip;
    }

    void ResolveMusicClip()
    {
        if (musicClip != null)
            return;
        if (!loadClipFromResources || string.IsNullOrWhiteSpace(resourcesClipPath))
            return;
        musicClip = Resources.Load<AudioClip>(resourcesClipPath.Trim());
    }

    void OnEnable()
    {
        if (_pollRoutine == null)
            _pollRoutine = StartCoroutine(CoPollPlayerDistance());
    }

    void OnDisable()
    {
        if (_pollRoutine != null)
        {
            StopCoroutine(_pollRoutine);
            _pollRoutine = null;
        }
        StopFade();
        if (_source != null)
        {
            _source.Stop();
            _source.volume = 0f;
        }
        _insideZone = false;
        _wasPlaying = false;
    }

    IEnumerator CoPollPlayerDistance()
    {
        var wait = new WaitForSeconds(Mathf.Max(0.05f, checkInterval));
        while (enabled)
        {
            if (musicClip == null || _source == null)
            {
                yield return wait;
                continue;
            }

            if (_source.clip != musicClip)
                _source.clip = musicClip;

            Transform player = ResolvePlayerTransform();
            bool inside = player != null && IsWithinRadius(player.position);

            if (inside && !_insideZone)
                BeginFadeIn();
            else if (!inside && _insideZone)
                BeginFadeOut();

            _insideZone = inside;
            yield return wait;
        }
    }

    Transform ResolvePlayerTransform()
    {
        if (playerMovement == null)
            playerMovement = FindObjectOfType<PlayerMovement>(true);
        return playerMovement != null ? playerMovement.transform : null;
    }

    bool IsWithinRadius(Vector3 playerPosition)
    {
        if (useHorizontalDistance)
        {
            float dx = playerPosition.x - triggerWorldPosition.x;
            float dz = playerPosition.z - triggerWorldPosition.z;
            return dx * dx + dz * dz <= triggerRadius * triggerRadius;
        }

        return Vector3.Distance(playerPosition, triggerWorldPosition) <= triggerRadius;
    }

    void BeginFadeIn()
    {
        if (_source.clip == null)
            return;

        StopFade();
        if (!_source.isPlaying)
        {
            _source.volume = 0f;
            _source.Play();
        }
        _fadeRoutine = StartCoroutine(CoFadeTo(TargetVolume, fadeInDuration, stopAfter: false));
        _wasPlaying = true;
    }

    void BeginFadeOut()
    {
        if (!_wasPlaying && !_source.isPlaying)
            return;

        StopFade();
        _fadeRoutine = StartCoroutine(CoFadeTo(0f, fadeOutDuration, stopAfter: true));
        _wasPlaying = false;
    }

    float TargetVolume => volume;

    void StopFade()
    {
        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }
    }

    IEnumerator CoFadeTo(float target, float duration, bool stopAfter)
    {
        float from = _source != null ? _source.volume : 0f;
        float d = Mathf.Max(0.01f, duration);
        float t = 0f;

        while (t < d && _source != null)
        {
            t += Time.unscaledDeltaTime;
            _source.volume = Mathf.Lerp(from, target, t / d);
            yield return null;
        }

        if (_source != null)
        {
            _source.volume = target;
            if (stopAfter)
            {
                _source.Stop();
                _source.volume = 0f;
            }
        }

        _fadeRoutine = null;
    }

    public void SetMusicClip(AudioClip clip)
    {
        musicClip = clip;
        if (_source != null)
            _source.clip = clip;
    }

    public void Configure(Vector3 worldPosition, float radius, float targetVolume, AudioClip clip = null)
    {
        triggerWorldPosition = worldPosition;
        triggerRadius = Mathf.Max(0.1f, radius);
        volume = Mathf.Clamp01(targetVolume);
        if (clip != null)
            SetMusicClip(clip);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.9f, 0.35f, 0.2f, 0.35f);
        Gizmos.DrawWireSphere(triggerWorldPosition, triggerRadius);
        Gizmos.color = new Color(0.9f, 0.35f, 0.2f, 0.85f);
        Gizmos.DrawSphere(triggerWorldPosition, 0.6f);
    }

    void OnValidate()
    {
        triggerRadius = Mathf.Max(0.1f, triggerRadius);
        if (_source == null)
            _source = GetComponent<AudioSource>();
        if (_source == null)
            return;
        _source.loop = loop;
        _source.spatialBlend = 0f;
        if (musicClip != null)
            _source.clip = musicClip;
    }
}
