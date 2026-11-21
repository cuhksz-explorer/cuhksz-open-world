using System;
using UnityEngine.SceneManagement;

/// <summary>
/// 哪些场景允许第三人称；其余场景仅使用第一人称（CC1P / FirstPersonCamera）。
/// </summary>
public static class CameraViewScenePolicy
{
    static readonly string[] ThirdPersonAllowedSceneNames =
    {
        "uppergarden",
        "middlegarden",
        "undergarden"
    };

    public static bool IsThirdPersonAllowedInScene(string unitySceneName)
    {
        string name = NormalizeSceneName(unitySceneName);
        if (string.IsNullOrEmpty(name))
            return false;
        for (int i = 0; i < ThirdPersonAllowedSceneNames.Length; i++)
        {
            if (string.Equals(name, ThirdPersonAllowedSceneNames[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static bool IsThirdPersonAllowedInActiveScene()
    {
        return IsThirdPersonAllowedInScene(SceneManager.GetActiveScene().name);
    }

    public static string NormalizeSceneName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return "";
        string normalized = sceneName.Trim().Replace('\\', '/');
        if (normalized.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            normalized = normalized.Substring(0, normalized.Length - ".unity".Length);
        if (normalized.EndsWith(".scene", StringComparison.OrdinalIgnoreCase))
            normalized = normalized.Substring(0, normalized.Length - ".scene".Length);
        int slash = normalized.LastIndexOf('/');
        if (slash >= 0 && slash < normalized.Length - 1)
            normalized = normalized.Substring(slash + 1);
        return normalized;
    }
}
