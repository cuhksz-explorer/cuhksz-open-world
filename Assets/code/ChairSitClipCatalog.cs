using UnityEngine;

/// <summary>
/// 椅子坐下/站起动画片段命名约定（与 Mixamo 导出文件名一致）。
/// </summary>
public static class ChairSitClipCatalog
{
    public static readonly string[] SitDownCandidates =
    {
        "Sitting (2)",
        "Sitting (1)",
        "Sitting"
    };

    public static readonly string[] SitIdleCandidates =
    {
        "Sitting Idle",
        "Sitting Talking",
        "Sitting"
    };

    public static readonly string[] StandUpCandidates =
    {
        "Sit To Stand (1)",
        "Sit To Stand",
        "Stand Up"
    };

    public static readonly string[] RequiredFbxFileNames =
    {
        "Sitting (2).fbx",
        "Sitting Idle.fbx",
        "Sit To Stand (1).fbx"
    };

    public static AnimationClip PickClip(AnimationClip[] clips, string[] candidates)
    {
        if (clips == null || clips.Length == 0 || candidates == null)
            return null;

        for (int c = 0; c < candidates.Length; c++)
        {
            string candidate = candidates[c];
            if (string.IsNullOrEmpty(candidate))
                continue;

            for (int i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                if (clip == null || clip.name.StartsWith("__preview"))
                    continue;
                if (string.Equals(clip.name, candidate, System.StringComparison.OrdinalIgnoreCase))
                    return clip;
            }
        }

        for (int c = 0; c < candidates.Length; c++)
        {
            string candidate = candidates[c];
            if (string.IsNullOrEmpty(candidate))
                continue;

            for (int i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                if (clip == null || clip.name.StartsWith("__preview"))
                    continue;
                if (clip.name.IndexOf(candidate, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return clip;
            }
        }

        return null;
    }

    public static bool MatchesAnyCandidate(string text, string[] candidates)
    {
        if (string.IsNullOrEmpty(text) || candidates == null)
            return false;

        for (int i = 0; i < candidates.Length; i++)
        {
            if (text.IndexOf(candidates[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    public static bool IsUsableClip(AnimationClip clip)
    {
        if (clip == null || clip.length < 0.05f)
            return false;
        if (clip.name.StartsWith("__preview", System.StringComparison.Ordinal))
            return false;
        if (string.Equals(clip.name, "mixamo.com", System.StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    public static bool IsSitDownClip(AnimationClip clip)
    {
        if (!IsUsableClip(clip))
            return false;
        return string.Equals(clip.name, "Sitting (2)", System.StringComparison.OrdinalIgnoreCase)
               || string.Equals(clip.name, "Sitting (1)", System.StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSitIdleClip(AnimationClip clip)
    {
        if (!IsUsableClip(clip))
            return false;
        return string.Equals(clip.name, "Sitting Idle", System.StringComparison.OrdinalIgnoreCase)
               || string.Equals(clip.name, "Sitting Talking", System.StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsStandUpClip(AnimationClip clip)
    {
        if (!IsUsableClip(clip))
            return false;
        for (int i = 0; i < StandUpCandidates.Length; i++)
        {
            if (string.Equals(clip.name, StandUpCandidates[i], System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static bool IsClipSetBindingValid(ChairSitClipSet set)
    {
        if (set == null)
            return false;
        return IsSitDownClip(set.sitDownClip)
               && IsSitIdleClip(set.sitIdleClip)
               && IsStandUpClip(set.standUpClip);
    }

    /// <summary>
    /// 按片段名从 Resources/ChairSit 解析（不依赖 ClipSet 内可能失效的 GUID）。
    /// </summary>
    public static bool TryResolveClipsFromResources(
        out AnimationClip sitDown,
        out AnimationClip sitIdle,
        out AnimationClip standUp,
        string resourcesFolder = "ChairSit")
    {
        sitDown = null;
        sitIdle = null;
        standUp = null;

        var clips = Resources.LoadAll<AnimationClip>(resourcesFolder);
        if (clips == null || clips.Length == 0)
            return false;

        sitDown = FindExactClip(clips, "Sitting (2)") ?? FindExactClip(clips, "Sitting (1)");
        sitIdle = FindExactClip(clips, "Sitting Idle") ?? FindExactClip(clips, "Sitting Talking");
        standUp = FindExactClip(clips, "Sit To Stand (1)")
                  ?? FindExactClip(clips, "Sit To Stand")
                  ?? FindExactClip(clips, "Stand Up");

        return IsSitDownClip(sitDown) && IsSitIdleClip(sitIdle) && IsStandUpClip(standUp);
    }

    public static AnimationClip FindExactClip(AnimationClip[] clips, string clipName)
    {
        if (clips == null || string.IsNullOrEmpty(clipName))
            return null;

        for (int i = 0; i < clips.Length; i++)
        {
            var clip = clips[i];
            if (clip == null)
                continue;
            if (string.Equals(clip.name, clipName, System.StringComparison.OrdinalIgnoreCase)
                && IsUsableClip(clip))
                return clip;
        }

        return null;
    }
}
