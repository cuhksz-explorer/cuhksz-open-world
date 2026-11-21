using UnityEngine;

/// <summary>LGUHUB 服务地址（可通过 PlayerPrefs 覆盖，便于本地调试）。</summary>
public static class LguHubConfig
{
    public const string PrefsLguHubBaseUrl = "LguHub_BaseUrl_Override";
    public const string DefaultLguHubBaseUrl = "https://lguhub.cn";

    public static string ResolveLguHubBaseUrl()
    {
        var o = PlayerPrefs.GetString(PrefsLguHubBaseUrl, "").Trim();
        if (!string.IsNullOrEmpty(o))
            return o.TrimEnd('/');
        return DefaultLguHubBaseUrl;
    }

    public static void SetLguHubBaseUrlOverride(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            PlayerPrefs.DeleteKey(PrefsLguHubBaseUrl);
        else
            PlayerPrefs.SetString(PrefsLguHubBaseUrl, url.Trim().TrimEnd('/'));
        PlayerPrefs.Save();
    }
}
