using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace DormMall
{
    /// <summary>
    /// 为每位登录用户分配独立寝室槽位（仅用于布置存档键 dorm::roomN）。
    /// 所有人进入同一张 dorm 地图的同一出生点，各自看到并保存自己的家具布置。
    /// </summary>
    public class UserDormRoomService : MonoBehaviour
    {
        public const int MaxDormRooms = 64;

        [Serializable]
        class DormRoomWire
        {
            public bool ok;
            public int dorm_room_index;
            public int max_rooms;
        }

        [Serializable]
        class MeWire
        {
            public int user_id;
        }

        static UserDormRoomService _instance;
        static int _roomIndex = -1;
        static int _cachedUserId;

        public static int RoomIndex => _roomIndex;
        public static int CachedUserId => _cachedUserId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            EnsureInstance();
        }

        static void EnsureInstance()
        {
            if (_instance != null)
                return;
            var go = new GameObject("[UserDormRoomService]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<UserDormRoomService>();
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            RefreshRoomIndexLocal();
        }

        void OnEnable()
        {
            GameServerSync.OnAuthSuccess += OnAuthSuccess;
            GameServerSync.OnLoggedOut += OnLoggedOut;
        }

        void OnDisable()
        {
            GameServerSync.OnAuthSuccess -= OnAuthSuccess;
            GameServerSync.OnLoggedOut -= OnLoggedOut;
        }

        void OnAuthSuccess(string _, string __)
        {
            RefreshRoomIndexLocal();
            StartCoroutine(CoFetchRoomIndexFromServer());
        }

        void OnLoggedOut()
        {
            _roomIndex = -1;
            _cachedUserId = 0;
        }

        /// <summary>寝室布置持久化用的场景键（Unity 场景名 + 用户寝室槽）。</summary>
        public static string GetLayoutSceneKey(string unitySceneName)
        {
            string scn = string.IsNullOrEmpty(unitySceneName) ? "UnknownScene" : unitySceneName.Trim();
            int room = RoomIndex;
            if (room < 0)
                return scn;
            return $"{scn}::room{room}";
        }

        public static string GetActiveLayoutSceneKey()
        {
            return GetLayoutSceneKey(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        public static Vector3 GetRoomSpawnPosition(Vector3 defaultWorldPosition)
        {
            // 共享实例：出生点由场景中唯一的 DormRoomAnchor（sharedInstanceSpawn）决定，不按槽位平移。
            return defaultWorldPosition;
        }

        public static void RefreshRoomIndexLocal()
        {
            EnsureInstance();
            _cachedUserId = 0;
            _roomIndex = -1;

            string uname = "";
            if (GameServerSync.Instance != null)
                uname = (GameServerSync.Instance.SavedUsername ?? "").Trim();
            if (string.IsNullOrEmpty(uname))
            {
                _roomIndex = -1;
                return;
            }

            string prefsKey = "dorm_room_index_v1::" + uname.ToLowerInvariant();
            if (PlayerPrefs.HasKey(prefsKey))
            {
                _roomIndex = Mathf.Clamp(PlayerPrefs.GetInt(prefsKey, 0), 0, MaxDormRooms - 1);
            }
            else
            {
                _roomIndex = StableHashToRoom(uname);
                PlayerPrefs.SetInt(prefsKey, _roomIndex);
                PlayerPrefs.Save();
            }
        }

        static int StableHashToRoom(string seed)
        {
            if (string.IsNullOrEmpty(seed))
                return 0;
            unchecked
            {
                int hash = 23;
                for (int i = 0; i < seed.Length; i++)
                    hash = hash * 31 + seed[i];
                if (hash < 0) hash = -hash;
                return hash % MaxDormRooms;
            }
        }

        IEnumerator CoFetchRoomIndexFromServer()
        {
            if (GameServerSync.Instance == null || !GameServerSync.Instance.IsLoggedIn)
                yield break;
            string token = GameServerSync.Instance.AuthTokenForExtensions;
            string baseUrl = GameServerSync.Instance.BaseUrlTrimmed;
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(baseUrl))
                yield break;

            using (var meReq = UnityWebRequest.Get($"{baseUrl}/api/me?token={UnityWebRequest.EscapeURL(token)}"))
            {
                yield return meReq.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
                bool meOk = meReq.result == UnityWebRequest.Result.Success;
#else
                bool meOk = !(meReq.isNetworkError || meReq.isHttpError);
#endif
                if (meOk)
                {
                    try
                    {
                        var me = JsonUtility.FromJson<MeWire>(meReq.downloadHandler.text);
                        if (me != null && me.user_id > 0)
                            _cachedUserId = me.user_id;
                    }
                    catch { }
                }
            }

            using (var req = UnityWebRequest.Get($"{baseUrl}/api/dorm_room?token={UnityWebRequest.EscapeURL(token)}"))
            {
                yield return req.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
                bool ok = req.result == UnityWebRequest.Result.Success;
#else
                bool ok = !(req.isNetworkError || req.isHttpError);
#endif
                if (!ok)
                    yield break;
                try
                {
                    var wire = JsonUtility.FromJson<DormRoomWire>(req.downloadHandler.text);
                    if (wire != null && wire.ok)
                    {
                        int max = wire.max_rooms > 0 ? wire.max_rooms : MaxDormRooms;
                        _roomIndex = Mathf.Clamp(wire.dorm_room_index, 0, max - 1);
                        string uname = (GameServerSync.Instance.SavedUsername ?? "").Trim().ToLowerInvariant();
                        if (!string.IsNullOrEmpty(uname))
                        {
                            PlayerPrefs.SetInt("dorm_room_index_v1::" + uname, _roomIndex);
                            PlayerPrefs.Save();
                        }
                        DormPlacementPersistence.NotifyUserRoomChanged();
                    }
                }
                catch { }
            }
        }
    }
}
