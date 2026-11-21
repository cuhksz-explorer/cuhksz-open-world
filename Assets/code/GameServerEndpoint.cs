using UnityEngine;

/// <summary>
/// 终端 GameServer 的 HTTP 根地址（账号、存档、联机信令、商城目录等）。
/// <para><b>更换服务器地址的三种方式（任选其一）：</b></para>
/// <list type="number">
/// <item>改 <see cref="DefaultBaseUrl"/>（代码内默认值，新建物体时 Inspector 会跟它走）。</item>
/// <item>在场景中 <see cref="GameServerSync"/> 组件的「Base Url」字段填写。</item>
/// <item>运行时写入 PlayerPrefs 键 <see cref="PlayerPrefsOverrideKey"/>（免改场景、免重建；可用 <see cref="SetPlayerPrefsOverride"/>）。</item>
/// </list>
/// </summary>
public static class GameServerEndpoint
{
    /// <summary>代码内默认地址。格式：<c>http://主机:端口</c>，无尾部斜杠。</summary>
    public const string DefaultBaseUrl = "http://10.31.162.55:8765";

    /// <summary>旧版本默认地址；若场景里仍是这些值，运行时会自动升级为 <see cref="DefaultBaseUrl"/>。</summary>
    static readonly string[] LegacyDefaultBaseUrls =
    {
        "http://192.168.1.113:8765",
        "http://10.24.20.10:8765",
        "http://10.31.2.130:8765",
        "http://10.24.20.18:8765",
        "http://10.31.4.124:8765",
        "http://10.31.6.200:8765",
        "http://10.31.3.16:8765",
        "http://127.0.0.1:8765",
    };

    /// <summary>非空时优先于 Inspector 中的 baseUrl（便于临时切内网 IP）。</summary>
    public const string PlayerPrefsOverrideKey = "GameServer_BaseUrl_Override";

    /// <summary>解析最终使用的 Base URL：PlayerPrefs 覆盖 &gt; Inspector（含旧默认升级） &gt; <see cref="DefaultBaseUrl"/>。</summary>
    public static string ResolveBaseUrl(string inspectorSerializedValue)
    {
        var fromPrefs = PlayerPrefs.GetString(PlayerPrefsOverrideKey, "");
        if (!string.IsNullOrWhiteSpace(fromPrefs))
            return Normalize(fromPrefs);

        var v = inspectorSerializedValue ?? "";
        var vt = v.Trim();
        if (IsLegacyDefaultBaseUrl(vt))
            v = DefaultBaseUrl;
        if (!string.IsNullOrWhiteSpace(v))
            return Normalize(v);

        return DefaultBaseUrl;
    }

    static bool IsLegacyDefaultBaseUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return false;
        for (int i = 0; i < LegacyDefaultBaseUrls.Length; i++)
        {
            if (string.Equals(url, LegacyDefaultBaseUrls[i], System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    static string Normalize(string url) => (url ?? "").Trim().TrimEnd('/');

    /// <summary>写入覆盖并保存；传入空或仅空白则清除覆盖，恢复为 Inspector/默认值逻辑。</summary>
    public static void SetPlayerPrefsOverride(string baseUrlOrEmptyToClear)
    {
        if (string.IsNullOrWhiteSpace(baseUrlOrEmptyToClear))
        {
            ClearPlayerPrefsOverride();
            return;
        }

        PlayerPrefs.SetString(PlayerPrefsOverrideKey, Normalize(baseUrlOrEmptyToClear));
        PlayerPrefs.Save();
    }

    public static void ClearPlayerPrefsOverride()
    {
        PlayerPrefs.DeleteKey(PlayerPrefsOverrideKey);
        PlayerPrefs.Save();
    }

    public static bool HasPlayerPrefsOverride() =>
        !string.IsNullOrWhiteSpace(PlayerPrefs.GetString(PlayerPrefsOverrideKey, ""));
}
