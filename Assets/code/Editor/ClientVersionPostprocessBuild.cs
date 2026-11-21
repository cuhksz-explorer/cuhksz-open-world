using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 构建后自动写入客户端版本文件，供 GameServer 上传时自动识别。
/// 写入位置：*_Data/StreamingAssets/client_version.txt
/// </summary>
public sealed class ClientVersionPostprocessBuild : IPostprocessBuildWithReport
{
    public int callbackOrder => 1000;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report == null || string.IsNullOrWhiteSpace(report.summary.outputPath))
            return;

        string version = ClientBuildVersion.Normalize(PlayerSettings.bundleVersion);
        if (string.IsNullOrWhiteSpace(version))
            return;

        string dataDir = ResolveDataDirectory(report.summary.outputPath);
        if (string.IsNullOrEmpty(dataDir) || !Directory.Exists(dataDir))
        {
            Debug.LogWarning("[ClientVersionPostprocessBuild] 未找到 _Data 目录，跳过写入 client_version.txt");
            return;
        }

        string streamingAssetsDir = Path.Combine(dataDir, "StreamingAssets");
        Directory.CreateDirectory(streamingAssetsDir);

        string versionFile = Path.Combine(streamingAssetsDir, "client_version.txt");
        File.WriteAllText(versionFile, version, System.Text.Encoding.UTF8);
        Debug.Log($"[ClientVersionPostprocessBuild] client version file: {versionFile} = {version}");
    }

    static string ResolveDataDirectory(string outputPath)
    {
        if (outputPath.EndsWith(".exe", System.StringComparison.OrdinalIgnoreCase))
        {
            string buildDir = Path.GetDirectoryName(outputPath);
            string exeStem = Path.GetFileNameWithoutExtension(outputPath);
            return Path.Combine(buildDir ?? "", exeStem + "_Data");
        }

        string productName = PlayerSettings.productName;
        if (string.IsNullOrWhiteSpace(productName))
            productName = Application.productName;

        return Path.Combine(outputPath, productName + "_Data");
    }
}
