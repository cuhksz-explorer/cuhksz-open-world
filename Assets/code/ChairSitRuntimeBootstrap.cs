using UnityEngine;

/// <summary>
/// 进入 Play 模式时在 Console 输出椅子动画绑定状态，便于确认 Resources 是否加载成功。
/// </summary>
public static class ChairSitRuntimeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void LogBindingOnPlay()
    {
        var set = Resources.Load<ChairSitClipSet>("ChairSit/ChairSitClipSet");
        if (set == null)
        {
            Debug.LogWarning(
                "[椅子交互] 未找到 Resources/ChairSit/ChairSitClipSet.asset。\n" +
                "请在编辑器运行 Tools → 椅子交互 → 重建 ClipSet 并验证绑定。");
            return;
        }

        if (ChairSitClipCatalog.TryResolveClipsFromResources(
                out AnimationClip down,
                out AnimationClip idle,
                out AnimationClip up))
        {
            Debug.Log(
                "[椅子交互] 动画已绑定（Resources 按名称解析）：\n" +
                $"  站→坐: {down.name} ({down.length:F2}s)\n" +
                $"  坐着: {idle.name} ({idle.length:F2}s)\n" +
                $"  站起: {up.name} ({up.length:F2}s)");
            return;
        }

        if (!ChairSitClipCatalog.IsClipSetBindingValid(set))
        {
            Debug.LogWarning(
                "[椅子交互] 椅子动画绑定无效（ClipSet GUID 可能失效，或 Resources 缺 FBX）：\n" +
                $"  站→坐: {(set.sitDownClip != null ? set.sitDownClip.name : "缺失")}\n" +
                $"  坐着: {(set.sitIdleClip != null ? set.sitIdleClip.name : "缺失")}\n" +
                $"  站起: {(set.standUpClip != null ? set.standUpClip.name : "缺失")}\n" +
                "期望：Sitting (2) / Sitting Idle / Sit To Stand (1)");
            return;
        }

        Debug.Log(
            "[椅子交互] 动画已绑定（ClipSet）：\n" +
            $"  站→坐: {set.sitDownClip.name} ({set.sitDownClip.length:F2}s)\n" +
            $"  坐着: {set.sitIdleClip.name} ({set.sitIdleClip.length:F2}s)\n" +
            $"  站起: {set.standUpClip.name} ({set.standUpClip.length:F2}s)");
    }
}
