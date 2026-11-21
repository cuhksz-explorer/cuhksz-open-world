using UnityEngine;

/// <summary>
/// Esc 键互斥：交互/UI 关闭本帧内禁止第一人称相机再次切换光标。
/// </summary>
public static class InteractionEscGate
{
    static int _suppressFrame = -1;

    public static void SuppressFpsEscForThisFrame()
    {
        _suppressFrame = Time.frameCount;
    }

    public static bool ShouldSuppressFirstPersonEscToggleThisFrame()
    {
        return _suppressFrame == Time.frameCount;
    }
}
