using UnityEngine;

/// <summary>
/// 一键配置时：若联机服务物体不在已 <see cref="GameServerSync"/> DontDestroy 的根下，用本组件在运行时保留跨场景。
/// 挂在 <c>GameServer_Network</c> 子物体上时可关掉（由父物体统一 DDOL）。
/// </summary>
[DisallowMultipleComponent]
public class MultiplayerServiceRoot : MonoBehaviour
{
    [Tooltip("为 true 时 Awake 对本物体调用 DontDestroyOnLoad（勿与父级重复 DDOL）")]
    [SerializeField] bool dontDestroyOnLoad = true;

    void Awake()
    {
        if (!dontDestroyOnLoad)
            return;
        DontDestroyOnLoad(gameObject);
    }
}
