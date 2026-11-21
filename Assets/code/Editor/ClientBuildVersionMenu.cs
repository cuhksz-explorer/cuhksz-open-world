#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class ClientBuildVersionMenu
{
    const string MenuRoot = "Tools/LGU Explorer/客户端版本/";

    [MenuItem(MenuRoot + "查看当前版本")]
    public static void ShowCurrentVersion()
    {
        EditorUtility.DisplayDialog(
            "客户端版本",
            $"Player Settings Version: {PlayerSettings.bundleVersion}\n" +
            $"规范化版本: {ClientBuildVersion.Normalize(PlayerSettings.bundleVersion)}",
            "确定");
    }

    [MenuItem(MenuRoot + "设为 0.0.1（初始版本）")]
    public static void SetInitialVersion()
    {
        SetVersion(ClientBuildVersion.DefaultVersion);
    }

    [MenuItem(MenuRoot + "版本号 +1（满 9 进位）")]
    public static void IncrementVersion()
    {
        string next = ClientBuildVersion.Increment(PlayerSettings.bundleVersion);
        SetVersion(next);
    }

    static void SetVersion(string version)
    {
        version = ClientBuildVersion.Normalize(version);
        PlayerSettings.bundleVersion = version;
        AssetDatabase.SaveAssets();
        Debug.Log("[ClientBuildVersion] Player Settings Version = " + version);
        EditorUtility.DisplayDialog("客户端版本已更新", "新版本: v" + version + "\n\n请重新 Build 客户端后再上传服务器。", "确定");
    }
}
#endif
