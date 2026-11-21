using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;

namespace DormMall
{
    /// <summary>
    /// 持久化寝室已放置物：按「用户 + 场景」保存，进入场景时自动恢复实例。
    /// </summary>
    public class DormPlacementPersistence : MonoBehaviour
    {
        [Serializable]
        class DormLayoutServerGetWire
        {
            public bool ok;
            public bool has_saved;
            public string scene_name;
            public string layout_json;
            public float updated_at;
        }

        [Serializable]
        class DormLayoutServerPostWire
        {
            public bool ok;
            public bool skipped;
            public string scene_name;
            public float updated_at;
        }

        [Serializable]
        class DormLayoutDiffOpWire
        {
            public string op; // upsert | delete
            public string id;
            public int itemId;
            public string productName;
            public string prefabPath;
            public Vec3Data position;
            public QuatData rotation;
            public Vec3Data visualScale;
        }

        [Serializable]
        class DormLayoutDiffPostBody
        {
            public string token;
            public string scene_name;
            public double client_updated_at;
            public double base_updated_at;
            public DormLayoutDiffOpWire[] operations;
        }

        [Serializable]
        struct Vec3Data
        {
            public float x;
            public float y;
            public float z;

            public static Vec3Data From(Vector3 v) => new Vec3Data { x = v.x, y = v.y, z = v.z };
            public Vector3 ToVector3() => new Vector3(x, y, z);
        }

        [Serializable]
        struct QuatData
        {
            public float x;
            public float y;
            public float z;
            public float w;

            public static QuatData From(Quaternion q) => new QuatData { x = q.x, y = q.y, z = q.z, w = q.w };
            public Quaternion ToQuaternion() => new Quaternion(x, y, z, w);
        }

        [Serializable]
        class DormPlacedItemRecord
        {
            public string id;
            public int itemId;
            public string productName;
            public string prefabPath;
            public Vec3Data position;
            public QuatData rotation;
            public Vec3Data visualScale;
        }

        [Serializable]
        class DormPlacedSceneRecord
        {
            public List<DormPlacedItemRecord> items = new List<DormPlacedItemRecord>();
        }

        static DormPlacementPersistence _instance;
        const string PrefsKeyPrefix = "dorm_placed_items_v1";
        const string PrefsUpdatedAtKeyPrefix = "dorm_placed_items_updated_at_v1";
        const string PrefsSyncedUpdatedAtKeyPrefix = "dorm_placed_items_synced_updated_at_v1";
        const float DirtySceneRetryIntervalSeconds = 3f;
        string _activeUserBucket = "local_guest";
        int _activeRoomIndex = -2;
        bool _remoteLayoutFetchInFlight;
        bool _remoteLayoutPostInFlight;
        bool _suppressRemoteSaveOnce;
        string _pendingPostSceneName;
        DormPlacedSceneRecord _pendingPostData;
        readonly HashSet<string> _dirtyScenes = new HashSet<string>();
        float _nextDirtyRetryAt;
        readonly Dictionary<string, DormPlacedSceneRecord> _lastSyncedSnapshotByScene = new Dictionary<string, DormPlacedSceneRecord>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            EnsureInstance();
        }

        static void EnsureInstance()
        {
            if (_instance != null)
                return;
            var go = new GameObject("[DormPlacementPersistence]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<DormPlacementPersistence>();
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            _activeUserBucket = ResolveUserBucket();
            // 首次进入游戏时，当前场景不会触发 sceneLoaded 回调；仅 dorm 场景恢复寝室布置。
            if (DormSceneConfig.IsDormScene(SceneManager.GetActiveScene().name))
                RestoreForScene(ResolveLayoutSceneKey(SceneManager.GetActiveScene().name));
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            GameServerSync.OnAuthSuccess += OnAuthSuccess;
            GameServerSync.OnLoggedOut += OnLoggedOut;
            _nextDirtyRetryAt = 0f;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            GameServerSync.OnAuthSuccess -= OnAuthSuccess;
            GameServerSync.OnLoggedOut -= OnLoggedOut;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode _)
        {
            string layoutKey = ResolveLayoutSceneKey(scene.name);
            if (!DormSceneConfig.IsDormScene(scene.name))
            {
                ClearSpawnedRuntimeObjectsForScene(layoutKey);
                return;
            }
            RestoreForScene(layoutKey);
            TryFetchDormLayoutFromServer(layoutKey);
        }

        void OnAuthSuccess(string _, string __)
        {
            RebuildCurrentSceneForCurrentUser();
            TryFetchDormLayoutFromServer(ResolveLayoutSceneKey(SceneManager.GetActiveScene().name));
        }

        public static void NotifyUserRoomChanged()
        {
            EnsureInstance();
            if (_instance != null)
                _instance.RebuildCurrentSceneForCurrentUser();
        }

        /// <summary>删除 lingcollegeB 场景下的本地布置存档（不迁移到 dorm，直接丢弃）。</summary>
        public static int PurgeLegacyLingCollegeBLayoutData(params string[] userBuckets)
        {
            int removed = 0;
            var users = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (userBuckets != null)
            {
                for (int i = 0; i < userBuckets.Length; i++)
                {
                    string u = NormalizeUserBucket(userBuckets[i]);
                    if (!string.IsNullOrEmpty(u))
                        users.Add(u);
                }
            }
            if (users.Count == 0)
                users.Add("local_guest");

            for (int r = 0; r < UserDormRoomService.MaxDormRooms; r++)
            {
                string layoutKey = "lingcollegeB::room" + r;
                foreach (string user in users)
                    removed += DeleteLayoutPrefsForUserScene(user, layoutKey);
            }
            foreach (string user in users)
                removed += DeleteLayoutPrefsForUserScene(user, "lingcollegeB");

            PlayerPrefs.Save();
            return removed;
        }

        static int DeleteLayoutPrefsForUserScene(string userBucket, string layoutSceneKey)
        {
            int n = 0;
            string user = NormalizeUserBucket(userBucket);
            string scn = string.IsNullOrEmpty(layoutSceneKey) ? "UnknownScene" : layoutSceneKey.Trim();
            string[] keys =
            {
                $"{PrefsKeyPrefix}::{user}::{scn}",
                $"{PrefsUpdatedAtKeyPrefix}::{user}::{scn}",
                $"{PrefsSyncedUpdatedAtKeyPrefix}::{user}::{scn}"
            };
            for (int i = 0; i < keys.Length; i++)
            {
                if (!PlayerPrefs.HasKey(keys[i]))
                    continue;
                PlayerPrefs.DeleteKey(keys[i]);
                n++;
            }
            return n;
        }

        static string NormalizeUserBucket(string user)
        {
            if (string.IsNullOrWhiteSpace(user))
                return "local_guest";
            return user.Trim().ToLowerInvariant();
        }

        static string ResolveLayoutSceneKey(string unitySceneName)
        {
            string raw = unitySceneName ?? "";
            if (raw.Contains("::room", StringComparison.Ordinal))
                return raw;
            return UserDormRoomService.GetLayoutSceneKey(raw);
        }

        void OnLoggedOut()
        {
            RebuildCurrentSceneForCurrentUser();
            _lastSyncedSnapshotByScene.Clear();
        }

        void Update()
        {
            if (_remoteLayoutPostInFlight)
                return;
            if (_dirtyScenes.Count == 0)
                return;
            if (Time.unscaledTime < _nextDirtyRetryAt)
                return;
            string sceneName = null;
            foreach (var x in _dirtyScenes)
            {
                sceneName = x;
                break;
            }
            if (string.IsNullOrEmpty(sceneName))
                return;
            _nextDirtyRetryAt = Time.unscaledTime + DirtySceneRetryIntervalSeconds;
            TryPostDormLayoutToServer(sceneName, LoadSceneRecord(sceneName));
        }

        public static string SavePlacedItem(
            string sceneName,
            PurchasedItemData item,
            string prefabPath,
            Transform placedRoot,
            Vector3 visualLocalScale)
        {
            EnsureInstance();
            if (_instance == null || placedRoot == null || string.IsNullOrEmpty(prefabPath))
                return "";
            return _instance.SavePlacedItemInternal(
                ResolveLayoutSceneKey(sceneName),
                item,
                prefabPath,
                placedRoot,
                visualLocalScale);
        }

        public static void NotifyPlacedObjectMoved(Transform placedRoot, Vector3 visualLocalScale)
        {
            EnsureInstance();
            if (_instance == null || placedRoot == null)
                return;
            _instance.NotifyPlacedObjectMovedInternal(placedRoot, visualLocalScale);
        }

        public static void NotifyPlacedObjectRemoved(GameObject placedRoot)
        {
            EnsureInstance();
            if (_instance == null || placedRoot == null)
                return;
            _instance.NotifyPlacedObjectRemovedInternal(placedRoot);
        }

        public static string ExportSceneLayoutSnapshotJson(string unitySceneName)
        {
            EnsureInstance();
            string layoutKey = ResolveLayoutSceneKey(unitySceneName);
            if (_instance == null || string.IsNullOrEmpty(layoutKey))
                return "";
            var data = _instance.LoadSceneRecord(layoutKey);
            return JsonUtility.ToJson(data ?? new DormPlacedSceneRecord());
        }

        public static bool ImportSceneLayoutSnapshotJson(string unitySceneName, string snapshotJson, bool triggerCloudSync = true)
        {
            EnsureInstance();
            string layoutKey = ResolveLayoutSceneKey(unitySceneName);
            if (_instance == null || string.IsNullOrEmpty(layoutKey) || string.IsNullOrEmpty(snapshotJson))
                return false;
            DormPlacedSceneRecord data = null;
            try
            {
                data = JsonUtility.FromJson<DormPlacedSceneRecord>(snapshotJson);
            }
            catch
            {
                data = null;
            }
            if (data == null)
                return false;
            _instance.SaveSceneRecord(layoutKey, data, GetUnixTimeSeconds(), markDirty: triggerCloudSync);
            _instance.RestoreForScene(layoutKey);
            if (triggerCloudSync)
                _instance.TryPostDormLayoutToServer(layoutKey, data);
            return true;
        }

        string SavePlacedItemInternal(
            string sceneName,
            PurchasedItemData item,
            string prefabPath,
            Transform placedRoot,
            Vector3 visualLocalScale)
        {
            if (!DormSceneConfig.IsDormLayoutKey(sceneName))
                return "";

            var data = LoadSceneRecord(sceneName);
            string id = Guid.NewGuid().ToString("N");
            data.items.Add(new DormPlacedItemRecord
            {
                id = id,
                itemId = item != null ? item.id : 0,
                productName = item != null ? (item.productName ?? "") : "",
                prefabPath = prefabPath,
                position = Vec3Data.From(placedRoot.position),
                rotation = QuatData.From(placedRoot.rotation),
                visualScale = Vec3Data.From(visualLocalScale)
            });
            SaveSceneRecord(sceneName, data, GetUnixTimeSeconds());
            TryPostDormLayoutToServer(sceneName, data);
            return id;
        }

        void NotifyPlacedObjectMovedInternal(Transform placedRoot, Vector3 visualLocalScale)
        {
            var marker = placedRoot.GetComponent<DormPlacedRuntimeMarker>();
            if (marker == null)
                marker = placedRoot.GetComponentInParent<DormPlacedRuntimeMarker>();
            if (marker == null || string.IsNullOrEmpty(marker.sceneName) || string.IsNullOrEmpty(marker.recordId))
                return;
            if (!DormSceneConfig.IsDormLayoutKey(marker.sceneName))
                return;
            var data = LoadSceneRecord(marker.sceneName);
            if (data.items == null)
                data.items = new List<DormPlacedItemRecord>();
            for (int i = 0; i < data.items.Count; i++)
            {
                var row = data.items[i];
                if (row == null || !string.Equals(row.id ?? "", marker.recordId ?? "", StringComparison.Ordinal))
                    continue;
                row.position = Vec3Data.From(placedRoot.position);
                row.rotation = QuatData.From(placedRoot.rotation);
                row.visualScale = Vec3Data.From(visualLocalScale);
                SaveSceneRecord(marker.sceneName, data, GetUnixTimeSeconds());
                TryPostDormLayoutToServer(marker.sceneName, data);
                return;
            }
        }

        void NotifyPlacedObjectRemovedInternal(GameObject placedRoot)
        {
            var marker = placedRoot.GetComponent<DormPlacedRuntimeMarker>();
            if (marker == null)
                marker = placedRoot.GetComponentInParent<DormPlacedRuntimeMarker>();
            if (marker == null || string.IsNullOrEmpty(marker.sceneName) || string.IsNullOrEmpty(marker.recordId))
                return;
            if (!DormSceneConfig.IsDormLayoutKey(marker.sceneName))
                return;
            var data = LoadSceneRecord(marker.sceneName);
            if (data.items == null || data.items.Count == 0)
                return;
            int removed = data.items.RemoveAll(x => x != null && string.Equals(x.id ?? "", marker.recordId ?? "", StringComparison.Ordinal));
            if (removed <= 0)
                return;
            SaveSceneRecord(marker.sceneName, data, GetUnixTimeSeconds());
            TryPostDormLayoutToServer(marker.sceneName, data);
        }

        void RestoreForScene(string sceneName)
        {
            if (!DormSceneConfig.IsDormLayoutKey(sceneName))
            {
                ClearSpawnedRuntimeObjectsForScene(sceneName);
                return;
            }

            string userBucket = ResolveUserBucket();
            _activeUserBucket = userBucket;
            ClearSpawnedRuntimeObjectsForScene(sceneName);

            var data = LoadSceneRecord(sceneName);
            if (data.items == null || data.items.Count == 0)
                return;

            for (int i = 0; i < data.items.Count; i++)
            {
                var row = data.items[i];
                if (row == null || string.IsNullOrEmpty(row.prefabPath))
                    continue;

                var prefab = Resources.Load<GameObject>(row.prefabPath);
                if (prefab == null)
                {
                    Debug.LogWarning($"[DormPlacementPersistence] 恢复失败，找不到预制体: {row.prefabPath}");
                    continue;
                }

                var root = new GameObject("Placed_" + prefab.name + "_Saved");
                var visual = Instantiate(prefab);
                visual.name = prefab.name + "_Visual";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localScale = row.visualScale.ToVector3();
                AlignChildPivotToBoundsCenter(root.transform, visual.transform);

                root.transform.SetPositionAndRotation(row.position.ToVector3(), row.rotation.ToQuaternion());
                PrepareCollidersForPlacedObject(root);

                var marker = root.AddComponent<DormPlacedRuntimeMarker>();
                marker.recordId = row.id ?? "";
                marker.prefabPath = row.prefabPath ?? "";
                marker.sceneName = sceneName ?? "";
                marker.ownerUserBucket = userBucket;
            }
        }

        void TryFetchDormLayoutFromServer(string sceneName)
        {
            if (_remoteLayoutFetchInFlight)
                return;
            if (string.IsNullOrEmpty(sceneName))
                return;
            if (GameServerSync.Instance == null || !GameServerSync.Instance.IsLoggedIn)
                return;
            if (string.IsNullOrEmpty(GameServerSync.Instance.AuthTokenForExtensions))
                return;
            if (string.IsNullOrEmpty(GameServerSync.Instance.BaseUrlTrimmed))
                return;
            _remoteLayoutFetchInFlight = true;
            StartCoroutine(CoFetchDormLayoutFromServer(sceneName));
        }

        System.Collections.IEnumerator CoFetchDormLayoutFromServer(string sceneName)
        {
            string baseUrl = GameServerSync.Instance != null ? GameServerSync.Instance.BaseUrlTrimmed : "";
            string token = GameServerSync.Instance != null ? GameServerSync.Instance.AuthTokenForExtensions : "";
            string url =
                $"{baseUrl}/api/dorm_layout?token={UnityWebRequest.EscapeURL(token)}&scene_name={UnityWebRequest.EscapeURL(sceneName)}";
            using (var req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
                bool ok = req.result == UnityWebRequest.Result.Success;
#else
                bool ok = !(req.isNetworkError || req.isHttpError);
#endif
                if (!ok)
                {
                    _remoteLayoutFetchInFlight = false;
                    yield break;
                }

                DormLayoutServerGetWire wire = null;
                try
                {
                    wire = JsonUtility.FromJson<DormLayoutServerGetWire>(req.downloadHandler.text);
                }
                catch
                {
                    wire = null;
                }
                if (wire != null && wire.ok && wire.has_saved && !string.IsNullOrEmpty(wire.layout_json))
                {
                    try
                    {
                        double localUpdatedAt = GetLocalSceneUpdatedAt(sceneName);
                        double remoteUpdatedAt = wire.updated_at;
                        var data = JsonUtility.FromJson<DormPlacedSceneRecord>(wire.layout_json);
                        if (data != null)
                        {
                            if (localUpdatedAt > remoteUpdatedAt + 0.001d)
                            {
                                // 本地更新更新：不回滚到旧云档，并反向推送一次本地布局。
                                TryPostDormLayoutToServer(sceneName, LoadSceneRecord(sceneName));
                            }
                            else
                            {
                            _suppressRemoteSaveOnce = true;
                                SaveSceneRecord(sceneName, data, remoteUpdatedAt > 0 ? remoteUpdatedAt : GetUnixTimeSeconds(), false);
                            RestoreForScene(sceneName);
                                SaveSyncedSnapshot(sceneName, data);
                                SetSyncedSceneUpdatedAt(sceneName, remoteUpdatedAt > 0 ? remoteUpdatedAt : GetUnixTimeSeconds());
                            }
                        }
                    }
                    catch
                    {
                        // ignore malformed remote payload
                    }
                }
            }
            _remoteLayoutFetchInFlight = false;
        }

        void TryPostDormLayoutToServer(string sceneName, DormPlacedSceneRecord data)
        {
            if (_suppressRemoteSaveOnce)
            {
                _suppressRemoteSaveOnce = false;
                return;
            }
            MarkSceneDirty(sceneName);
            if (_remoteLayoutPostInFlight)
            {
                _pendingPostSceneName = sceneName;
                _pendingPostData = CloneSceneRecord(data);
                return;
            }
            if (string.IsNullOrEmpty(sceneName) || data == null)
                return;
            if (GameServerSync.Instance == null || !GameServerSync.Instance.IsLoggedIn)
                return;
            if (string.IsNullOrEmpty(GameServerSync.Instance.AuthTokenForExtensions))
                return;
            if (string.IsNullOrEmpty(GameServerSync.Instance.BaseUrlTrimmed))
                return;
            _remoteLayoutPostInFlight = true;
            var localSnapshot = CloneSceneRecord(data) ?? new DormPlacedSceneRecord();
            var baseSnapshot = GetLastSyncedSnapshot(sceneName);
            double baseUpdatedAt = GetSyncedSceneUpdatedAt(sceneName);
            var diffOps = BuildDiffOperations(baseSnapshot, localSnapshot);
            if (diffOps.Count > 0)
                StartCoroutine(CoPostDormLayoutDiffToServer(sceneName, localSnapshot, diffOps, baseUpdatedAt));
            else
                StartCoroutine(CoPostDormLayoutToServer(sceneName, localSnapshot));
        }

        System.Collections.IEnumerator CoPostDormLayoutDiffToServer(
            string sceneName,
            DormPlacedSceneRecord localSnapshot,
            List<DormLayoutDiffOpWire> diffOps,
            double baseUpdatedAt)
        {
            string baseUrl = GameServerSync.Instance != null ? GameServerSync.Instance.BaseUrlTrimmed : "";
            string token = GameServerSync.Instance != null ? GameServerSync.Instance.AuthTokenForExtensions : "";
            double clientUpdatedAt = GetLocalSceneUpdatedAt(sceneName);
            var bodyObj = new DormLayoutDiffPostBody
            {
                token = token,
                scene_name = sceneName,
                client_updated_at = clientUpdatedAt,
                base_updated_at = baseUpdatedAt,
                operations = diffOps.ToArray()
            };
            string body = JsonUtility.ToJson(bodyObj);
            bool postOk = false;
            bool skipped = false;
            using (var req = new UnityWebRequest($"{baseUrl}/api/dorm_layout_diff", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                yield return req.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
                bool ok = req.result == UnityWebRequest.Result.Success;
#else
                bool ok = !(req.isNetworkError || req.isHttpError);
#endif
                if (ok)
                {
                    try
                    {
                        var wire = JsonUtility.FromJson<DormLayoutServerPostWire>(req.downloadHandler.text);
                        if (wire != null && wire.ok)
                        {
                            postOk = true;
                            skipped = wire.skipped;
                            if (!wire.skipped)
                            {
                                SaveSyncedSnapshot(sceneName, localSnapshot);
                                SetSyncedSceneUpdatedAt(sceneName, wire.updated_at > 0 ? wire.updated_at : clientUpdatedAt);
                            }
                        }
                    }
                    catch { }
                }
            }
            if (postOk && skipped)
            {
                TryFetchDormLayoutFromServer(sceneName);
                StartCoroutine(CoPostFinalize(sceneName, false));
            }
            else if (!postOk)
            {
                StartCoroutine(CoPostFinalize(sceneName, false));
            }
            else
            {
                StartCoroutine(CoPostFinalize(sceneName, true));
            }
        }

        System.Collections.IEnumerator CoPostDormLayoutToServer(string sceneName, DormPlacedSceneRecord data)
        {
            string baseUrl = GameServerSync.Instance != null ? GameServerSync.Instance.BaseUrlTrimmed : "";
            string token = GameServerSync.Instance != null ? GameServerSync.Instance.AuthTokenForExtensions : "";
            string layoutJson = JsonUtility.ToJson(data ?? new DormPlacedSceneRecord());
            double clientUpdatedAt = GetLocalSceneUpdatedAt(sceneName);
            string body =
                "{\"token\":\"" + JsonEscape(token) + "\"," +
                "\"scene_name\":\"" + JsonEscape(sceneName) + "\"," +
                "\"layout_json\":" + JsonStringLiteral(layoutJson) + "," +
                "\"client_updated_at\":" + clientUpdatedAt.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "}";
            bool postOk = false;
            using (var req = new UnityWebRequest($"{baseUrl}/api/dorm_layout", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                yield return req.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
                bool ok = req.result == UnityWebRequest.Result.Success;
#else
                bool ok = !(req.isNetworkError || req.isHttpError);
#endif
                if (ok)
                {
                    try
                    {
                        var wire = JsonUtility.FromJson<DormLayoutServerPostWire>(req.downloadHandler.text);
                        if (wire != null && wire.ok)
                        {
                            postOk = true;
                            if (wire.skipped)
                                TryFetchDormLayoutFromServer(sceneName);
                            else
                            {
                                SaveSyncedSnapshot(sceneName, data);
                                SetSyncedSceneUpdatedAt(sceneName, wire.updated_at > 0 ? wire.updated_at : clientUpdatedAt);
                            }
                        }
                    }
                    catch { }
                }
            }
            StartCoroutine(CoPostFinalize(sceneName, postOk));
        }

        System.Collections.IEnumerator CoPostFinalize(string sceneName, bool postOk)
        {
            if (postOk)
                _dirtyScenes.Remove(sceneName);
            _remoteLayoutPostInFlight = false;
            if (!string.IsNullOrEmpty(_pendingPostSceneName) && _pendingPostData != null)
            {
                string nextScene = _pendingPostSceneName;
                var nextData = _pendingPostData;
                _pendingPostSceneName = null;
                _pendingPostData = null;
                TryPostDormLayoutToServer(nextScene, nextData);
            }
            yield break;
        }

        void RebuildCurrentSceneForCurrentUser()
        {
            string nowUser = ResolveUserBucket();
            int nowRoom = UserDormRoomService.RoomIndex;
            string layoutKey = ResolveLayoutSceneKey(SceneManager.GetActiveScene().name);
            if (string.Equals(nowUser, _activeUserBucket, StringComparison.Ordinal) && nowRoom == _activeRoomIndex)
                return;
            _activeUserBucket = nowUser;
            _activeRoomIndex = nowRoom;
            RestoreForScene(layoutKey);
        }

        static void ClearSpawnedRuntimeObjectsForScene(string layoutSceneKey)
        {
            string userBucket = ResolveUserBucketStatic();
            string legacyUnityScene = layoutSceneKey ?? "";
            int roomSep = legacyUnityScene.IndexOf("::room", StringComparison.Ordinal);
            if (roomSep > 0)
                legacyUnityScene = legacyUnityScene.Substring(0, roomSep);

            var all = FindObjectsOfType<DormPlacedRuntimeMarker>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var m = all[i];
                if (m == null || m.gameObject == null) continue;
                bool sameLayout = string.Equals(m.sceneName ?? "", layoutSceneKey ?? "", StringComparison.Ordinal);
                bool legacySameUser = string.Equals(m.sceneName ?? "", legacyUnityScene, StringComparison.Ordinal)
                    && string.Equals(m.ownerUserBucket ?? "", userBucket, StringComparison.Ordinal);
                if (!sameLayout && !legacySameUser) continue;
                Destroy(m.gameObject);
            }
        }

        static string ResolveUserBucketStatic()
        {
            string name = "";
            if (GameServerSync.Instance != null)
                name = (GameServerSync.Instance.SavedUsername ?? "").Trim();
            if (string.IsNullOrEmpty(name))
                name = "local_guest";
            return name.ToLowerInvariant();
        }

        DormPlacedSceneRecord LoadSceneRecord(string sceneName)
        {
            string key = BuildScenePrefsKey(sceneName);
            string json = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(json))
                return new DormPlacedSceneRecord();

            try
            {
                var data = JsonUtility.FromJson<DormPlacedSceneRecord>(json);
                return data ?? new DormPlacedSceneRecord();
            }
            catch
            {
                return new DormPlacedSceneRecord();
            }
        }

        void SaveSceneRecord(string sceneName, DormPlacedSceneRecord data)
        {
            SaveSceneRecord(sceneName, data, GetUnixTimeSeconds());
        }

        void SaveSceneRecord(string sceneName, DormPlacedSceneRecord data, double updatedAtUnixSeconds)
        {
            SaveSceneRecord(sceneName, data, updatedAtUnixSeconds, true);
        }

        void SaveSceneRecord(string sceneName, DormPlacedSceneRecord data, double updatedAtUnixSeconds, bool markDirty)
        {
            string key = BuildScenePrefsKey(sceneName);
            string json = JsonUtility.ToJson(data ?? new DormPlacedSceneRecord());
            PlayerPrefs.SetString(key, json);
            string tsKey = BuildScenePrefsUpdatedAtKey(sceneName);
            PlayerPrefs.SetString(tsKey, updatedAtUnixSeconds.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
            if (markDirty)
                MarkSceneDirty(sceneName);
        }

        string BuildScenePrefsKey(string sceneName)
        {
            string scn = string.IsNullOrEmpty(sceneName) ? "UnknownScene" : sceneName.Trim();
            string user = ResolveUserBucket();
            return $"{PrefsKeyPrefix}::{user}::{scn}";
        }

        string BuildScenePrefsUpdatedAtKey(string sceneName)
        {
            string scn = string.IsNullOrEmpty(sceneName) ? "UnknownScene" : sceneName.Trim();
            string user = ResolveUserBucket();
            return $"{PrefsUpdatedAtKeyPrefix}::{user}::{scn}";
        }

        string BuildScenePrefsSyncedUpdatedAtKey(string sceneName)
        {
            string scn = string.IsNullOrEmpty(sceneName) ? "UnknownScene" : sceneName.Trim();
            string user = ResolveUserBucket();
            return $"{PrefsSyncedUpdatedAtKeyPrefix}::{user}::{scn}";
        }

        double GetLocalSceneUpdatedAt(string sceneName)
        {
            string key = BuildScenePrefsUpdatedAtKey(sceneName);
            string raw = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(raw))
                return 0d;
            if (double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v))
                return v;
            return 0d;
        }

        void MarkSceneDirty(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return;
            _dirtyScenes.Add(sceneName);
            _nextDirtyRetryAt = Mathf.Min(_nextDirtyRetryAt, Time.unscaledTime + 0.15f);
        }

        void SaveSyncedSnapshot(string sceneName, DormPlacedSceneRecord snapshot)
        {
            if (string.IsNullOrEmpty(sceneName))
                return;
            _lastSyncedSnapshotByScene[sceneName] = CloneSceneRecord(snapshot) ?? new DormPlacedSceneRecord();
        }

        DormPlacedSceneRecord GetLastSyncedSnapshot(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return new DormPlacedSceneRecord();
            if (_lastSyncedSnapshotByScene.TryGetValue(sceneName, out var x) && x != null)
                return CloneSceneRecord(x) ?? new DormPlacedSceneRecord();
            return new DormPlacedSceneRecord();
        }

        double GetSyncedSceneUpdatedAt(string sceneName)
        {
            string key = BuildScenePrefsSyncedUpdatedAtKey(sceneName);
            string raw = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(raw))
                return 0d;
            if (double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v))
                return v;
            return 0d;
        }

        void SetSyncedSceneUpdatedAt(string sceneName, double updatedAt)
        {
            string key = BuildScenePrefsSyncedUpdatedAtKey(sceneName);
            PlayerPrefs.SetString(key, updatedAt.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
        }

        static List<DormLayoutDiffOpWire> BuildDiffOperations(DormPlacedSceneRecord oldData, DormPlacedSceneRecord newData)
        {
            var ops = new List<DormLayoutDiffOpWire>();
            var oldMap = new Dictionary<string, DormPlacedItemRecord>();
            if (oldData != null && oldData.items != null)
            {
                for (int i = 0; i < oldData.items.Count; i++)
                {
                    var it = oldData.items[i];
                    if (it == null || string.IsNullOrEmpty(it.id))
                        continue;
                    oldMap[it.id] = it;
                }
            }

            var newMap = new Dictionary<string, DormPlacedItemRecord>();
            if (newData != null && newData.items != null)
            {
                for (int i = 0; i < newData.items.Count; i++)
                {
                    var it = newData.items[i];
                    if (it == null || string.IsNullOrEmpty(it.id))
                        continue;
                    newMap[it.id] = it;
                }
            }

            foreach (var kv in newMap)
            {
                var id = kv.Key;
                var now = kv.Value;
                if (!oldMap.TryGetValue(id, out var old) || !RecordEquals(old, now))
                {
                    ops.Add(new DormLayoutDiffOpWire
                    {
                        op = "upsert",
                        id = now.id ?? "",
                        itemId = now.itemId,
                        productName = now.productName ?? "",
                        prefabPath = now.prefabPath ?? "",
                        position = now.position,
                        rotation = now.rotation,
                        visualScale = now.visualScale
                    });
                }
            }

            foreach (var kv in oldMap)
            {
                if (!newMap.ContainsKey(kv.Key))
                {
                    ops.Add(new DormLayoutDiffOpWire
                    {
                        op = "delete",
                        id = kv.Key
                    });
                }
            }
            return ops;
        }

        static bool RecordEquals(DormPlacedItemRecord a, DormPlacedItemRecord b)
        {
            if (a == null || b == null)
                return false;
            return
                string.Equals(a.id ?? "", b.id ?? "", StringComparison.Ordinal) &&
                a.itemId == b.itemId &&
                string.Equals(a.productName ?? "", b.productName ?? "", StringComparison.Ordinal) &&
                string.Equals(a.prefabPath ?? "", b.prefabPath ?? "", StringComparison.Ordinal) &&
                NearlyEqual(a.position.x, b.position.x) &&
                NearlyEqual(a.position.y, b.position.y) &&
                NearlyEqual(a.position.z, b.position.z) &&
                NearlyEqual(a.rotation.x, b.rotation.x) &&
                NearlyEqual(a.rotation.y, b.rotation.y) &&
                NearlyEqual(a.rotation.z, b.rotation.z) &&
                NearlyEqual(a.rotation.w, b.rotation.w) &&
                NearlyEqual(a.visualScale.x, b.visualScale.x) &&
                NearlyEqual(a.visualScale.y, b.visualScale.y) &&
                NearlyEqual(a.visualScale.z, b.visualScale.z);
        }

        static bool NearlyEqual(float x, float y)
        {
            return Mathf.Abs(x - y) <= 0.0001f;
        }

        string ResolveUserBucket()
        {
            string name = "";
            if (GameServerSync.Instance != null)
                name = (GameServerSync.Instance.SavedUsername ?? "").Trim();
            if (string.IsNullOrEmpty(name))
                name = "local_guest";
            return name.ToLowerInvariant();
        }

        static string JsonEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        static string JsonStringLiteral(string s)
        {
            return "\"" + JsonEscape(s).Replace("\n", "\\n").Replace("\r", "\\r") + "\"";
        }

        static DormPlacedSceneRecord CloneSceneRecord(DormPlacedSceneRecord source)
        {
            if (source == null)
                return null;
            try
            {
                string json = JsonUtility.ToJson(source);
                return JsonUtility.FromJson<DormPlacedSceneRecord>(json) ?? new DormPlacedSceneRecord();
            }
            catch
            {
                return new DormPlacedSceneRecord();
            }
        }

        static double GetUnixTimeSeconds()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        }

        static void PrepareCollidersForPlacedObject(GameObject obj)
        {
            var meshFilters = obj.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < meshFilters.Length; i++)
            {
                var mf = meshFilters[i];
                if (mf == null || mf.sharedMesh == null) continue;
                var mc = mf.gameObject.GetComponent<MeshCollider>();
                if (mc == null) mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = true;
                mc.enabled = true;
            }

            var colliders = obj.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = true;

            if (colliders.Length > 0) return;
            if (!TryGetWorldBounds(obj, out Bounds bounds)) return;

            var fallback = obj.AddComponent<BoxCollider>();
            fallback.center = obj.transform.InverseTransformPoint(bounds.center);

            Vector3 lossy = obj.transform.lossyScale;
            fallback.size = new Vector3(
                lossy.x != 0f ? bounds.size.x / Mathf.Abs(lossy.x) : bounds.size.x,
                lossy.y != 0f ? bounds.size.y / Mathf.Abs(lossy.y) : bounds.size.y,
                lossy.z != 0f ? bounds.size.z / Mathf.Abs(lossy.z) : bounds.size.z);
        }

        static void AlignChildPivotToBoundsCenter(Transform pivotRoot, Transform visualTransform)
        {
            if (pivotRoot == null || visualTransform == null) return;
            visualTransform.localPosition = Vector3.zero;
            if (!TryGetWorldBounds(visualTransform.gameObject, out Bounds b)) return;
            Vector3 localInPivot = pivotRoot.InverseTransformPoint(b.center);
            visualTransform.localPosition = -localInPivot;
        }

        static bool TryGetWorldBounds(GameObject obj, out Bounds bounds)
        {
            var colliders = obj.GetComponentsInChildren<Collider>(true);
            if (colliders.Length > 0)
            {
                bounds = colliders[0].bounds;
                for (int i = 1; i < colliders.Length; i++)
                    bounds.Encapsulate(colliders[i].bounds);
                return true;
            }

            var renderers = obj.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);
                return true;
            }

            bounds = default;
            return false;
        }
    }

    /// <summary>仅用于标记这是“持久化回放”的放置物实例，便于后续扩展删除/同步。</summary>
    public class DormPlacedRuntimeMarker : MonoBehaviour
    {
        public string recordId;
        public string prefabPath;
        public string sceneName;
        public string ownerUserBucket;
    }
}
