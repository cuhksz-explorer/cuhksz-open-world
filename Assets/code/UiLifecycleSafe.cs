using UnityEngine;

/// <summary>
/// 在场景卸载、退出 Play 等阶段对正在销毁或未加载场景中的物体调用 <see cref="GameObject.SetActive"/> 会报错；
/// 统一经此入口可安全跳过。
/// </summary>
public static class UiLifecycleSafe
{
    /// <summary>是否仍可安全访问该物体（读属性 / SetActive / GetComponent），避免场景卸载或销毁过程中误触。</summary>
    public static bool IsSafeToTouch(GameObject go)
    {
        if (go == null || !go) return false;
        var scene = go.scene;
        return scene.IsValid() && scene.isLoaded;
    }

    public static void SetActiveIfAlive(GameObject go, bool active)
    {
        if (!IsSafeToTouch(go)) return;

        if (go.activeSelf != active)
            go.SetActive(active);
    }
}
