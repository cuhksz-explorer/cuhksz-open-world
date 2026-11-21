using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DormMall
{
    /// <summary>
    /// 进入 dorm 后传送到共享寝室出生点（同一张地图；布置数据仍按用户槽位隔离）。
    /// </summary>
    public class DormPlayerRoomSpawn : MonoBehaviour
    {
        static DormPlayerRoomSpawn _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (_instance != null)
                return;
            var go = new GameObject("[DormPlayerRoomSpawn]");
            _instance = go.AddComponent<DormPlayerRoomSpawn>();
            Object.DontDestroyOnLoad(go);
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
            if (!DormSceneConfig.IsDormScene(scene.name))
                return;
            StartCoroutine(CoApplySpawnNextFrames());
        }

        static IEnumerator CoApplySpawnNextFrames()
        {
            yield return null;
            yield return null;
            TryTeleportLocalPlayerToRoomAnchor();
        }

        public static bool TryTeleportLocalPlayerToRoomAnchor()
        {
            if (!DormSceneConfig.IsActiveDormScene())
                return false;
            if (!TryResolveSpawn(out Vector3 pos, out Quaternion rot))
                return false;

            var player = FindLocalPlayer();
            if (player == null)
                return false;

            var movement = player.GetComponent<PlayerMovement>();
            if (movement != null)
                movement.Teleport(pos);
            else
                player.transform.SetPositionAndRotation(pos, rot);

            DormPlacementZoneService.InvalidateCache();
            return true;
        }

        static bool TryResolveSpawn(out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;

            int room = UserDormRoomService.RoomIndex;
            var anchors = Object.FindObjectsOfType<DormRoomAnchor>(true);

            DormRoomAnchor shared = null;
            DormRoomAnchor roomMatch = null;
            for (int i = 0; i < anchors.Length; i++)
            {
                var a = anchors[i];
                if (a == null)
                    continue;
                if (a.sharedInstanceSpawn)
                {
                    if (shared == null)
                        shared = a;
                    continue;
                }
                if (room >= 0 && a.roomIndex == room)
                    roomMatch = a;
            }

            var pick = shared != null ? shared : roomMatch;
            if (pick != null)
            {
                position = pick.SpawnPosition;
                rotation = pick.SpawnRotation;
                return true;
            }

            var spawn = GameObject.Find("PlayerSpawn");
            if (spawn != null)
            {
                position = spawn.transform.position;
                rotation = spawn.transform.rotation;
                return true;
            }

            return false;
        }

        static GameObject FindLocalPlayer()
        {
            try
            {
                var tagged = GameObject.FindWithTag("Player");
                if (tagged != null)
                    return tagged;
            }
            catch { }

            var movement = Object.FindObjectOfType<PlayerMovement>();
            return movement != null ? movement.gameObject : null;
        }
    }
}
