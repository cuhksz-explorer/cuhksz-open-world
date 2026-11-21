using UnityEngine;
using UnityEngine.SceneManagement;

namespace DormMall
{
    /// <summary>
    /// 进入寝室前记录「返回点」：离开 dorm 时回到进入前的场景与坐标。
    /// </summary>
    public sealed class DormReturnState : MonoBehaviour
    {
        const string PrefsSceneKey = "dorm_return_scene_v1";
        const string PrefsPosKey = "dorm_return_pos_v1";
        const string PrefsRotKey = "dorm_return_rot_v1";
        const string PrefsHasKey = "dorm_return_has_v1";

        static DormReturnState _instance;

        string _returnSceneName = "";
        Vector3 _returnPosition;
        Vector3 _returnRotationEuler;
        bool _hasReturn;

        public static bool HasReturn => Instance != null && Instance._hasReturn;

        public static string ReturnSceneName =>
            Instance != null ? Instance._returnSceneName : "";

        static DormReturnState Instance
        {
            get
            {
                EnsureExists();
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            EnsureExists();
        }

        public static void EnsureExists()
        {
            if (_instance != null)
                return;
            var existing = FindObjectOfType<DormReturnState>(true);
            if (existing != null)
            {
                _instance = existing;
                return;
            }

            var go = new GameObject("[DormReturnState]");
            _instance = go.AddComponent<DormReturnState>();
            DontDestroyOnLoad(go);
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
            LoadFromPrefs();
        }

        /// <summary>进入寝室前调用：记录当前（或传送门）场景与坐标。</summary>
        public static void RecordBeforeEnteringDorm(
            string fromSceneName,
            Vector3 returnPosition,
            Quaternion returnRotation)
        {
            if (string.IsNullOrWhiteSpace(fromSceneName))
                fromSceneName = SceneManager.GetActiveScene().name;

            string scene = SceneTeleportHelper.NormalizeSceneName(fromSceneName);
            if (DormSceneConfig.IsDormScene(scene))
            {
                Debug.LogWarning("[DormReturnState] 已在寝室场景内，忽略记录返回点。");
                return;
            }

            var inst = Instance;
            inst._returnSceneName = scene;
            inst._returnPosition = returnPosition;
            inst._returnRotationEuler = returnRotation.eulerAngles;
            inst._hasReturn = true;
            inst.SaveToPrefs();

            Debug.Log(
                $"[DormReturnState] 已记录返回点：场景={scene} 位置={returnPosition} 旋转={inst._returnRotationEuler}");
        }

        /// <summary>用当前场景与主角位置记录（进入寝室按钮常用）。</summary>
        public static void RecordFromCurrentPlayer(Transform returnAnchorOverride = null)
        {
            Vector3 pos;
            Quaternion rot;
            if (returnAnchorOverride != null)
            {
                pos = returnAnchorOverride.position;
                rot = returnAnchorOverride.rotation;
            }
            else if (SceneTeleportHelper.TryFindLocalPlayer(out var player))
            {
                pos = player.transform.position;
                rot = player.transform.rotation;
            }
            else
            {
                pos = Vector3.zero;
                rot = Quaternion.identity;
            }

            RecordBeforeEnteringDorm(SceneManager.GetActiveScene().name, pos, rot);
        }

        public static bool TryGetReturn(out string sceneName, out Vector3 position, out Vector3 rotationEuler)
        {
            sceneName = "";
            position = default;
            rotationEuler = default;

            var inst = Instance;
            if (inst == null || !inst._hasReturn || string.IsNullOrEmpty(inst._returnSceneName))
                return false;

            sceneName = inst._returnSceneName;
            position = inst._returnPosition;
            rotationEuler = inst._returnRotationEuler;
            return true;
        }

        public static void Clear()
        {
            if (_instance == null)
                return;
            _instance._hasReturn = false;
            _instance._returnSceneName = "";
            _instance._returnPosition = default;
            _instance._returnRotationEuler = default;
            _instance.SaveToPrefs();
        }

        void SaveToPrefs()
        {
            PlayerPrefs.SetInt(PrefsHasKey, _hasReturn ? 1 : 0);
            PlayerPrefs.SetString(PrefsSceneKey, _returnSceneName ?? "");
            PlayerPrefs.SetFloat(PrefsPosKey + ".x", _returnPosition.x);
            PlayerPrefs.SetFloat(PrefsPosKey + ".y", _returnPosition.y);
            PlayerPrefs.SetFloat(PrefsPosKey + ".z", _returnPosition.z);
            PlayerPrefs.SetFloat(PrefsRotKey + ".x", _returnRotationEuler.x);
            PlayerPrefs.SetFloat(PrefsRotKey + ".y", _returnRotationEuler.y);
            PlayerPrefs.SetFloat(PrefsRotKey + ".z", _returnRotationEuler.z);
            PlayerPrefs.Save();
        }

        void LoadFromPrefs()
        {
            _hasReturn = PlayerPrefs.GetInt(PrefsHasKey, 0) == 1;
            _returnSceneName = PlayerPrefs.GetString(PrefsSceneKey, "");
            _returnPosition = new Vector3(
                PlayerPrefs.GetFloat(PrefsPosKey + ".x", 0f),
                PlayerPrefs.GetFloat(PrefsPosKey + ".y", 0f),
                PlayerPrefs.GetFloat(PrefsPosKey + ".z", 0f));
            _returnRotationEuler = new Vector3(
                PlayerPrefs.GetFloat(PrefsRotKey + ".x", 0f),
                PlayerPrefs.GetFloat(PrefsRotKey + ".y", 0f),
                PlayerPrefs.GetFloat(PrefsRotKey + ".z", 0f));
        }
    }
}
