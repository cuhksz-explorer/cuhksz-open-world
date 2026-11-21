using System;
using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Multiplayer
{
    [DefaultExecutionOrder(-240)]
    [DisallowMultipleComponent]
    public class SceneShardCoordinator : MonoBehaviour
    {
        [Serializable]
        public class ShardEndpoint
        {
            public string shardId = "A";
            public string address = "127.0.0.1";
            public ushort port = 7777;
        }

        public static SceneShardStatusResponse LastStatus { get; private set; }
        public static event Action<SceneShardStatusResponse> OnStatusUpdated;

        [SerializeField] bool dontDestroyOnLoad = true;
        [SerializeField] bool autoConnectOnSceneLoaded = true;
        [SerializeField] float heartbeatSeconds = 15f;
        [SerializeField] ShardEndpoint[] shardEndpoints =
        {
            new ShardEndpoint { shardId = "A", address = "127.0.0.1", port = 7777 },
            new ShardEndpoint { shardId = "B", address = "127.0.0.1", port = 7778 },
            new ShardEndpoint { shardId = "C", address = "127.0.0.1", port = 7779 },
        };
        [Tooltip("若 endpoint 地址仍为 127.0.0.1，则自动替换为 GameServer baseUrl 的主机 IP，避免多机各连各自本地。")]
        [SerializeField] bool autoUseGameServerHostForShardAddress = true;
        [Tooltip("已在房间联机时不主动重连分片端点，避免与房间连接状态打架。")]
        [SerializeField] bool avoidShardReconnectWhileInCoopSession = true;

        MultiplayerApiClient _api;
        Coroutine _hbLoop;
        string _sceneName = "";
        string _shardId = "";

        void Awake()
        {
            if (dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);
            _api = GetComponent<MultiplayerApiClient>() ?? gameObject.AddComponent<MultiplayerApiClient>();
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_hbLoop != null)
                StopCoroutine(_hbLoop);
            _hbLoop = null;
        }

        void Start()
        {
            if (autoConnectOnSceneLoaded)
                StartCoroutine(CoEnterCurrentSceneAndConnect());
        }

        void OnApplicationQuit()
        {
            if (_api != null && _api.IsReady && !string.IsNullOrWhiteSpace(_sceneName))
                StartCoroutine(CoLeaveScene(_sceneName));
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!autoConnectOnSceneLoaded) return;
            StartCoroutine(CoEnterCurrentSceneAndConnect());
        }

        IEnumerator CoEnterCurrentSceneAndConnect()
        {
            if (_api == null || !_api.IsReady)
                yield break;

            string oldScene = _sceneName;
            _sceneName = SceneManager.GetActiveScene().name;
            if (string.IsNullOrWhiteSpace(_sceneName))
                yield break;
            if (!string.IsNullOrWhiteSpace(oldScene) && !string.Equals(oldScene, _sceneName, StringComparison.Ordinal))
                yield return CoLeaveScene(oldScene);

            SceneShardStatusResponse resp = null;
            string err = null;
            yield return _api.CoSceneShardEnter(_sceneName, r => resp = r, e => err = e);
            if (!string.IsNullOrEmpty(err) || resp == null || string.IsNullOrEmpty(resp.shard_id))
            {
                Debug.LogWarning("[Shard] 进入分服失败: " + err);
                yield break;
            }

            _shardId = resp.shard_id;
            LastStatus = resp;
            OnStatusUpdated?.Invoke(resp);

            if (!TryResolveEndpoint(_shardId, out var ep))
            {
                Debug.LogWarning("[Shard] 未配置分服地址: " + _shardId);
                yield break;
            }

            if (avoidShardReconnectWhileInCoopSession && CoopSessionState.InSession)
            {
                if (_hbLoop != null)
                    StopCoroutine(_hbLoop);
                _hbLoop = StartCoroutine(CoHeartbeatLoop());
                yield break;
            }

            EnsureNetworkManager();
            if (LanCoopNetworkManager.Instance != null && !NetworkServer.active)
            {
                bool alreadyConnectedTarget =
                    NetworkClient.isConnected &&
                    string.Equals(LanCoopNetworkManager.Instance.networkAddress, ep.address, StringComparison.OrdinalIgnoreCase) &&
                    LanCoopNetworkManager.Instance.networkPort == ep.port;
                if (!alreadyConnectedTarget)
                {
                    if (NetworkClient.active)
                        LanCoopNetworkManager.Instance.StopClient();
                    LanCoopNetworkManager.Instance.StartClientWithAddress(ep.address, ep.port);
                    float timeoutAt = Time.realtimeSinceStartup + 10f;
                    while (!NetworkClient.isConnected && Time.realtimeSinceStartup < timeoutAt)
                        yield return null;
                    if (!NetworkClient.isConnected)
                    {
                        Debug.LogWarning("[Shard] Mirror 未连上（将继续重试）: " + _sceneName + "/" + _shardId + " -> " + ep.address + ":" + ep.port);
                    }
                }
            }

            if (_hbLoop != null)
                StopCoroutine(_hbLoop);
            _hbLoop = StartCoroutine(CoHeartbeatLoop());
        }

        IEnumerator CoHeartbeatLoop()
        {
            var wait = new WaitForSeconds(Mathf.Max(5f, heartbeatSeconds));
            while (_api != null && _api.IsReady && !string.IsNullOrEmpty(_sceneName) && !string.IsNullOrEmpty(_shardId))
            {
                yield return wait;
                if (avoidShardReconnectWhileInCoopSession && CoopSessionState.InSession)
                    goto HEARTBEAT_ONLY;

                if (!NetworkClient.isConnected)
                {
                    if (TryResolveEndpoint(_shardId, out var epRetry))
                    {
                        EnsureNetworkManager();
                        if (LanCoopNetworkManager.Instance != null && !NetworkServer.active && !NetworkClient.active)
                            LanCoopNetworkManager.Instance.StartClientWithAddress(epRetry.address, epRetry.port);
                    }
                }
HEARTBEAT_ONLY:
                SceneShardStatusResponse resp = null;
                yield return _api.CoSceneShardHeartbeat(_sceneName, _shardId, r => resp = r, _ => { });
                if (resp != null)
                {
                    LastStatus = resp;
                    OnStatusUpdated?.Invoke(resp);
                }
            }
            _hbLoop = null;
        }

        IEnumerator CoLeaveScene(string sceneName)
        {
            bool done = false;
            yield return _api.CoSceneShardLeave(sceneName, () => done = true, _ => done = true);
            if (done && string.Equals(_sceneName, sceneName, StringComparison.Ordinal))
            {
                _shardId = "";
                LastStatus = null;
                OnStatusUpdated?.Invoke(null);
            }
        }

        bool TryResolveEndpoint(string shardId, out ShardEndpoint endpoint)
        {
            endpoint = null;
            if (shardEndpoints == null) return false;
            string autoHost = ResolveGameServerHostAddress();
            for (int i = 0; i < shardEndpoints.Length; i++)
            {
                var x = shardEndpoints[i];
                if (x == null || string.IsNullOrWhiteSpace(x.shardId)) continue;
                if (string.Equals(x.shardId.Trim(), shardId, StringComparison.OrdinalIgnoreCase))
                {
                    endpoint = new ShardEndpoint
                    {
                        shardId = x.shardId,
                        address = NormalizeEndpointAddress(x.address, autoHost),
                        port = x.port
                    };
                    return true;
                }
            }
            return false;
        }

        string ResolveGameServerHostAddress()
        {
            if (!autoUseGameServerHostForShardAddress || GameServerSync.Instance == null)
                return "";
            string baseUrl = GameServerSync.Instance.BaseUrlTrimmed;
            if (string.IsNullOrWhiteSpace(baseUrl))
                return "";
            try
            {
                var uri = new Uri(baseUrl);
                return uri.Host ?? "";
            }
            catch
            {
                return "";
            }
        }

        static string NormalizeEndpointAddress(string configured, string autoHost)
        {
            string c = (configured ?? "").Trim();
            if (string.IsNullOrEmpty(c))
                return string.IsNullOrEmpty(autoHost) ? "127.0.0.1" : autoHost;
            if ((string.Equals(c, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(c, "localhost", StringComparison.OrdinalIgnoreCase)) &&
                !string.IsNullOrEmpty(autoHost))
                return autoHost;
            return c;
        }

        static void EnsureNetworkManager()
        {
            if (LanCoopNetworkManager.Instance != null) return;
            if (NetworkManager.singleton != null && LanCoopNetworkManager.Instance == null) return;
            var go = new GameObject("LanCoopNetworkManager");
            DontDestroyOnLoad(go);
            go.AddComponent<LanCoopNetworkManager>();
        }
    }
}
