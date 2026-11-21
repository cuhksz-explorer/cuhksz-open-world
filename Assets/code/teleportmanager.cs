using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ???????????T?????????????????????????????
/// ??????????????????????????
/// </summary>
public class TeleportDataManager : MonoBehaviour
{
    static TeleportDataManager _instance;

    public static TeleportDataManager Instance
    {
        get
        {
            if (_instance != null)
                return _instance;
            _instance = FindObjectOfType<TeleportDataManager>();
            return _instance;
        }
    }

    public string TargetSceneName { get; private set; }
    public string SourceSceneName { get; private set; }
    public Vector3 TargetPosition { get; private set; }
    public Quaternion TargetRotation { get; private set; }
    public bool SnapToGround { get; private set; }
    public bool HasPendingTeleport { get; private set; }

    string _serverSpawnRestoreBlockScene;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetTeleportTarget(string sceneName, Vector3 position, Quaternion rotation = default, bool snapToGround = false)
    {
        TargetSceneName = sceneName;
        SourceSceneName = SceneManager.GetActiveScene().name;
        TargetPosition = position;
        TargetRotation = rotation == default ? Quaternion.identity : rotation;
        SnapToGround = snapToGround;
        HasPendingTeleport = true;
        _serverSpawnRestoreBlockScene = SceneTeleportHelper.NormalizeSceneName(sceneName);
    }

    public bool TryPeekForScene(string loadedSceneName, out Vector3 position, out Quaternion rotation, out bool snapToGround)
    {
        position = TargetPosition;
        rotation = TargetRotation;
        snapToGround = SnapToGround;

        if (!HasPendingTeleport || string.IsNullOrWhiteSpace(TargetSceneName))
            return false;

        return SceneTeleportHelper.SceneNameMatches(loadedSceneName, TargetSceneName);
    }

    public void ConsumePendingTeleport()
    {
        HasPendingTeleport = false;
    }

    public bool ShouldSuppressServerSpawnRestore(string loadedSceneName)
    {
        return !string.IsNullOrWhiteSpace(_serverSpawnRestoreBlockScene) &&
               SceneTeleportHelper.SceneNameMatches(loadedSceneName, _serverSpawnRestoreBlockScene);
    }

    public void ClearServerSpawnRestoreBlock()
    {
        _serverSpawnRestoreBlockScene = null;
    }
}
