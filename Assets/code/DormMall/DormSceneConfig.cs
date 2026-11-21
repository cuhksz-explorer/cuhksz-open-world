using UnityEngine;
using UnityEngine.SceneManagement;

namespace DormMall
{
    /// <summary>
    /// <see cref="TeleportButton"/> 寝室往返模式（在 Inspector 的「寝室」里选择）。
    /// </summary>
    public enum DormTeleportRole
    {
        /// <summary>普通传送；若目标场景为 dorm 也会自动记录返回点。</summary>
        None = 0,
        /// <summary>寝室入口：传送前记录返回点（目标场景应为 dorm）。</summary>
        EnterDorm = 1,
        /// <summary>寝室出口：回到进入前的场景与坐标，无需填写目标场景。</summary>
        ExitDorm = 2
    }

    /// <summary>
    /// 寝室专用场景：同一张 <see cref="UnitySceneName"/> 地图共享出生点与放置区；布置按 dorm::roomN 分账号保存。
    /// </summary>
    public static class DormSceneConfig
    {
        public const string UnitySceneName = "dorm";
        public const string UnityScenePath = "Assets/scene/dorm.scene";
        public const string LayoutKeyPrefix = "dorm";

        /// <summary>从未记录返回点时，离开寝室的默认场景（如上园）。</summary>
        public const string DefaultFallbackReturnScene = "uppergarden";

        /// <summary>默认返回落点（与上园寝室入口附近，可在出口按钮上覆盖）。</summary>
        public static readonly Vector3 DefaultFallbackReturnPosition =
            new Vector3(350.13f, 44.51f, -625.67f);

        public static readonly Vector3 DefaultFallbackReturnRotation = Vector3.zero;

        public static bool IsDormScene(string unitySceneName)
        {
            if (string.IsNullOrEmpty(unitySceneName))
                return false;
            return string.Equals(unitySceneName.Trim(), UnitySceneName, System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsActiveDormScene()
        {
            return IsDormScene(SceneManager.GetActiveScene().name);
        }

        public static bool IsDormLayoutKey(string layoutSceneKey)
        {
            if (string.IsNullOrEmpty(layoutSceneKey))
                return false;
            string key = layoutSceneKey.Trim();
            if (string.Equals(key, LayoutKeyPrefix, System.StringComparison.OrdinalIgnoreCase))
                return true;
            return key.StartsWith(LayoutKeyPrefix + "::room", System.StringComparison.OrdinalIgnoreCase);
        }

    }
}
