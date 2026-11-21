using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 进入 undergarden 时自动创建《我和我的祖国》近距离音乐触发器（无需手动摆场景物体）。
/// </summary>
public static class UndergardenPatrioticMusicBootstrap
{
    const string SceneName = "undergarden";
    const string TriggerName = "PatrioticMusicTrigger_我和我的祖国";
    static readonly Vector3 Position = new Vector3(-1360.78003f, 13.6599998f, 1100.43005f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void OnAfterSceneLoad()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !string.Equals(scene.name, SceneName, System.StringComparison.OrdinalIgnoreCase))
            return;

        if (Object.FindObjectOfType<ProximityMusicTrigger>(true) != null)
            return;

        var existing = GameObject.Find(TriggerName);
        if (existing != null && existing.GetComponent<ProximityMusicTrigger>() != null)
            return;

        var go = existing != null ? existing : new GameObject(TriggerName);
        go.transform.position = Position;

        var trigger = go.GetComponent<ProximityMusicTrigger>();
        if (trigger == null)
            trigger = go.AddComponent<ProximityMusicTrigger>();

        var source = go.GetComponent<AudioSource>();
        if (source == null)
            source = go.AddComponent<AudioSource>();

        var clip = Resources.Load<AudioClip>("Music/我和我的祖国")
            ?? Resources.Load<AudioClip>("Music/BGM_undergarden_patriotic");
        trigger.Configure(Position, 20f, 0.22f, clip);
        if (clip == null)
            Debug.LogWarning(
                "[PatrioticMusic] 未找到 Resources/Music/BGM_undergarden_patriotic 或 我和我的祖国。");
    }
}
