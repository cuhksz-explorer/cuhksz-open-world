using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 跨场景传送落点应用：读取 TeleportDataManager 并在场景加载后把玩家放到正确位置。
/// </summary>
public class CrossSceneTeleportApplicator : MonoBehaviour
{
    static CrossSceneTeleportApplicator _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (_instance != null)
            return;

        var go = new GameObject("[CrossSceneTeleportApplicator]");
        _instance = go.AddComponent<CrossSceneTeleportApplicator>();
        DontDestroyOnLoad(go);
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
        StartCoroutine(CoApplyPending(scene.name));
    }

    public static bool TryApplyPendingNow(string loadedSceneName = null)
    {
        if (_instance == null)
            Boot();

        loadedSceneName ??= SceneManager.GetActiveScene().name;
        return _instance.ApplyPending(loadedSceneName);
    }

    IEnumerator CoApplyPending(string loadedSceneName)
    {
        yield return null;
        ApplyPending(loadedSceneName);
        yield return null;
        ApplyPending(loadedSceneName);
    }

    bool ApplyPending(string loadedSceneName)
    {
        var manager = TeleportDataManager.Instance;
        if (manager == null || !manager.HasPendingTeleport)
            return false;

        if (!manager.TryPeekForScene(loadedSceneName, out Vector3 position, out Quaternion rotation, out bool snapToGround))
            return false;

        bool usedRegistry = false;
        if (SceneTeleportEntryRegistry.Instance != null)
        {
            Vector3 rotationEuler = rotation.eulerAngles;
            if (SceneTeleportEntryRegistry.Instance.TryResolve(
                    manager.SourceSceneName,
                    loadedSceneName,
                    ref position,
                    ref rotationEuler,
                    out bool registrySnapToGround))
            {
                rotation = Quaternion.Euler(rotationEuler);
                snapToGround = registrySnapToGround;
                usedRegistry = true;
            }
        }

        if (snapToGround)
            position = SceneTeleportGroundSnap.SnapPosition(position);

        SceneTeleportHelper.ApplyPlayerTransform(position, rotation.eulerAngles);
        if (SceneTeleportHelper.TryFindLocalPlayer(out var player))
        {
            var movement = player.GetComponent<PlayerMovement>();
            if (movement != null)
                MapTeleportCameraSnap.AfterPlayerMoved(movement);
        }

        manager.ConsumePendingTeleport();
        string source = usedRegistry ? "SceneTeleportEntryRegistry" : "TeleportButton";
        Debug.Log(
            $"[CrossSceneTeleport] 已应用落点 -> 场景 {loadedSceneName}，坐标 {position}，" +
            $"来源={source}，贴地={snapToGround}");
        return true;
    }
}
