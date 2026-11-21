using UnityEngine;
using UnityEngine.SceneManagement;

namespace CombatSystem
{
    public static class CombatSystemBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            WeaponInventoryData.EnsureInitialized();
            BulletTracerPool.Ensure();
            CombatHudController.Ensure();
            GachaPanelController.Ensure();

            SceneManager.sceneLoaded += OnSceneLoaded;
            TryAttachWeaponController();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive)
                return;
            TryAttachWeaponController();
        }

        static void TryAttachWeaponController()
        {
            if (string.Equals(SceneManager.GetActiveScene().name, "Loadingscene", System.StringComparison.OrdinalIgnoreCase))
                return;

            var pm = Object.FindObjectOfType<PlayerMovement>();
            if (pm == null)
                return;
            if (pm.GetComponent<WeaponController>() == null)
                pm.gameObject.AddComponent<WeaponController>();
        }
    }
}
