using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// 传送/加载界面：从多个 VideoClip 中随机选一条（供 TeleportButton、MapSystem、公交车等共用）。
/// </summary>
public static class LoadingVideoRandomUtil
{
    public static VideoClip PickRandomClip(VideoClip[] variants, VideoClip fallback)
    {
        if (variants != null && variants.Length > 0)
        {
            int start = Random.Range(0, variants.Length);
            for (int k = 0; k < variants.Length; k++)
            {
                var c = variants[(start + k) % variants.Length];
                if (c != null) return c;
            }
        }
        return fallback;
    }

    public static bool HasAnyClip(VideoClip[] variants, VideoClip fallback)
    {
        if (fallback != null) return true;
        if (variants == null) return false;
        for (int i = 0; i < variants.Length; i++)
        {
            if (variants[i] != null) return true;
        }
        return false;
    }
}
