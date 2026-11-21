using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 客户端版本校验与热更新：对比内置版本与云端版本；不一致时仅下载缺失/过期文件到暂存区，重启后应用。
/// </summary>
public static class ClientVersionUpdateService
{
    public const string VersionCheckRelativePath = "/api/client/version";

    const string UpdateRootFolder = "client_update";
    const string StagingFolderName = "staging";
    const string RemovedListFileName = "removed_files.txt";
    const string PendingStateFileName = "pending_update.json";

    public static bool IsReady { get; private set; }
    public static bool IsBusy { get; private set; }
    public static bool UpdateAppliedPendingRestart { get; private set; }
    public static string LocalVersion => ClientBuildVersion.LocalVersion;
    public static string LatestVersion { get; private set; }
    public static string LastError { get; private set; }
    public static string LastUpdateMode { get; private set; }
    public static string RestartHint { get; private set; }

    public static event Action<float, string> OnProgress;

    public static string UpdateRootDirectory =>
        Path.Combine(Application.persistentDataPath, UpdateRootFolder);

    public static string StagingDirectory =>
        Path.Combine(UpdateRootDirectory, StagingFolderName);

    public static string PendingStatePath =>
        Path.Combine(UpdateRootDirectory, PendingStateFileName);

    public static void ResetState()
    {
        IsReady = false;
        IsBusy = false;
        UpdateAppliedPendingRestart = false;
        LastError = null;
        LatestVersion = null;
        LastUpdateMode = null;
        RestartHint = null;
    }

    public static IEnumerator EnsureLatestClientCoroutine(string baseUrlTrimmed, bool skipWhenOffline = false)
    {
        ResetState();
        IsBusy = true;

#if UNITY_EDITOR
        ReportProgress(1f, "编辑器模式：跳过客户端热更新");
        IsReady = true;
        IsBusy = false;
        yield break;
#endif

        if (skipWhenOffline)
        {
            ReportProgress(1f, "离线模式：跳过版本校验");
            IsReady = true;
            IsBusy = false;
            yield break;
        }

        baseUrlTrimmed = (baseUrlTrimmed ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrlTrimmed))
        {
            ReportProgress(1f, "未配置服务器，使用当前客户端");
            IsReady = true;
            IsBusy = false;
            yield break;
        }

        if (TryLoadPendingState(out var pending) && Directory.Exists(pending.stagingDir))
        {
            LatestVersion = pending.targetVersion;
            UpdateAppliedPendingRestart = true;
            RestartHint = $"已下载 v{pending.targetVersion} 更新包，请重启游戏以完成安装。";
            ReportProgress(1f, RestartHint);
            IsReady = false;
            IsBusy = false;
            yield break;
        }

        ReportProgress(0.05f, $"正在校验版本（本地 v{LocalVersion}）…");
        ClientVersionCheckResponse check = null;
        yield return CoCheckVersion(baseUrlTrimmed, result => check = result);
        if (check == null)
        {
            ReportProgress(1f, "无法连接版本服务器，使用当前客户端");
            IsReady = true;
            IsBusy = false;
            yield break;
        }

        LatestVersion = ClientBuildVersion.Normalize(check.latestVersion ?? LocalVersion);
        if (!check.updateRequired || ClientBuildVersion.Equals(LocalVersion, LatestVersion))
        {
            ReportProgress(1f, $"客户端已是最新版本 (v{LocalVersion})");
            IsReady = true;
            IsBusy = false;
            yield break;
        }

        Directory.CreateDirectory(UpdateRootDirectory);
        ReportProgress(0.1f, $"发现新版本 v{LatestVersion}，正在比对缺失文件…");

        ClientPatchResponse patch = null;
        if (!string.IsNullOrWhiteSpace(check.patchUrl))
            yield return CoFetchPatch(baseUrlTrimmed, check.patchUrl, result => patch = result);

        if (patch == null || patch.files == null || patch.files.Length == 0)
        {
            LastError = "服务端未返回可下载的补丁文件，请从下载页获取完整客户端";
            IsBusy = false;
            yield break;
        }

        LastUpdateMode = string.IsNullOrWhiteSpace(patch.mode) ? "incremental" : patch.mode;
        yield return CoDownloadMissingFiles(baseUrlTrimmed, patch);

        if (!string.IsNullOrEmpty(LastError))
        {
            IsBusy = false;
            yield break;
        }

        if (!HasStagedFiles())
        {
            ReportProgress(1f, $"本地资源已与 v{LatestVersion} 一致，可直接进入游戏");
            IsReady = true;
            IsBusy = false;
            yield break;
        }

        if (!TrySavePendingState(LatestVersion, StagingDirectory, patch.removed))
        {
            LastError = "无法写入更新状态";
            IsBusy = false;
            yield break;
        }

        UpdateAppliedPendingRestart = true;
        RestartHint = $"更新已下载（v{LocalVersion} → v{LatestVersion}），请重启游戏以应用补丁。";
        ReportProgress(1f, RestartHint);
        IsReady = false;
        IsBusy = false;
    }

    public static void LaunchPendingUpdateAndQuit()
    {
#if UNITY_EDITOR
        UnityEngine.Debug.Log("[ClientUpdate] 编辑器模式不执行重启安装");
        return;
#else
        if (!TryLoadPendingState(out var pending))
        {
            UnityEngine.Debug.LogWarning("[ClientUpdate] 无待应用更新");
            Application.Quit();
            return;
        }

        if (!TryLaunchWindowsUpdater(pending.stagingDir, pending.removedFiles))
            return;

        TryDeleteFile(PendingStatePath);
        Application.Quit();
#endif
    }

    static IEnumerator CoDownloadMissingFiles(string baseUrlTrimmed, ClientPatchResponse patch)
    {
        string gameRoot = GetGameRootDirectory();
        if (string.IsNullOrEmpty(gameRoot))
        {
            LastError = "无法定位游戏安装目录";
            yield break;
        }

        var files = patch.files ?? Array.Empty<ClientPatchFileEntry>();
        var needDownload = new List<ClientPatchFileEntry>();
        for (int i = 0; i < files.Length; i++)
        {
            var entry = files[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.path))
                continue;

            if (IsLocalFileUpToDate(gameRoot, entry))
                continue;

            needDownload.Add(entry);
        }

        if (needDownload.Count == 0)
        {
            ReportProgress(0.95f, "本地文件已完整，准备完成更新…");
            yield break;
        }

        TryDeleteDirectory(StagingDirectory);
        Directory.CreateDirectory(StagingDirectory);

        int total = needDownload.Count;
        ReportProgress(0.12f, $"需补全 {total} 个文件…");

        for (int i = 0; i < total; i++)
        {
            var entry = needDownload[i];
            float begin = 0.12f + 0.83f * (i / (float)Mathf.Max(1, total));
            float end = 0.12f + 0.83f * ((i + 1) / (float)Mathf.Max(1, total));
            string destPath = Path.Combine(StagingDirectory, entry.path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destPath) ?? StagingDirectory);
            string url = ResolveDownloadUrl(baseUrlTrimmed, entry.url);
            yield return CoDownloadFile(url, destPath, p =>
            {
                ReportProgress(Mathf.Lerp(begin, end, p), $"下载中 ({i + 1}/{total}) {entry.path}");
            });

            if (!string.IsNullOrEmpty(LastError))
                yield break;

            if (!string.IsNullOrWhiteSpace(entry.sha256))
            {
                string actual = ComputeSha256HexFile(destPath);
                if (!string.Equals(actual, entry.sha256.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    LastError = "文件校验失败: " + entry.path;
                    yield break;
                }
            }
        }

        ReportProgress(0.96f, "下载完成，等待重启后安装…");
    }

    static bool IsLocalFileUpToDate(string gameRoot, ClientPatchFileEntry entry)
    {
        string localPath = Path.Combine(gameRoot, entry.path.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(localPath))
            return false;
        if (string.IsNullOrWhiteSpace(entry.sha256))
            return false;
        string actual = ComputeSha256HexFile(localPath);
        return string.Equals(actual, entry.sha256.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    static IEnumerator CoCheckVersion(string baseUrlTrimmed, Action<ClientVersionCheckResponse> onDone)
    {
        string url = $"{baseUrlTrimmed}{VersionCheckRelativePath}?current={UnityWebRequest.EscapeURL(LocalVersion)}";
        using (var req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
            if (req.result != UnityWebRequest.Result.Success)
#else
            if (req.isNetworkError || req.isHttpError)
#endif
            {
                LastError = req.error;
                onDone?.Invoke(null);
                yield break;
            }

            try
            {
                onDone?.Invoke(JsonUtility.FromJson<ClientVersionCheckResponse>(req.downloadHandler.text));
            }
            catch (Exception e)
            {
                LastError = "解析版本信息失败: " + e.Message;
                onDone?.Invoke(null);
            }
        }
    }

    static IEnumerator CoFetchPatch(string baseUrlTrimmed, string patchUrl, Action<ClientPatchResponse> onDone)
    {
        string url = ResolveDownloadUrl(baseUrlTrimmed, patchUrl);
        using (var req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
            if (req.result != UnityWebRequest.Result.Success)
#else
            if (req.isNetworkError || req.isHttpError)
#endif
            {
                LastError = req.error;
                onDone?.Invoke(null);
                yield break;
            }

            try
            {
                onDone?.Invoke(JsonUtility.FromJson<ClientPatchResponse>(req.downloadHandler.text));
            }
            catch (Exception e)
            {
                LastError = "解析补丁清单失败: " + e.Message;
                onDone?.Invoke(null);
            }
        }
    }

    static IEnumerator CoDownloadFile(string url, string destPath, Action<float> onProgress)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destPath) ?? UpdateRootDirectory);
        using (var req = UnityWebRequest.Get(url))
        {
            req.downloadHandler = new DownloadHandlerFile(destPath);
            var op = req.SendWebRequest();
            while (!op.isDone)
            {
                onProgress?.Invoke(req.downloadProgress);
                yield return null;
            }

#if UNITY_2020_2_OR_NEWER
            if (req.result != UnityWebRequest.Result.Success)
#else
            if (req.isNetworkError || req.isHttpError)
#endif
            {
                LastError = req.error;
                TryDeleteFile(destPath);
            }
        }
    }

    static bool TryLaunchWindowsUpdater(string stagingDir, string[] removedFiles)
    {
#if !UNITY_STANDALONE_WIN
        LastError = "当前平台暂不支持自动安装，请从下载页手动获取客户端";
        return false;
#else
        try
        {
            string gameRoot = GetGameRootDirectory();
            if (string.IsNullOrEmpty(gameRoot) || !Directory.Exists(stagingDir))
            {
                LastError = "更新暂存目录无效";
                return false;
            }

            string exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath))
                exePath = Path.Combine(gameRoot, Application.productName + ".exe");

            string removedPath = Path.Combine(UpdateRootDirectory, RemovedListFileName);
            WriteRemovedList(removedPath, removedFiles);

            string scriptPath = Path.Combine(UpdateRootDirectory, "apply_update.ps1");
            string psContent =
                "$ErrorActionPreference = 'Stop'\n" +
                "Start-Sleep -Seconds 2\n" +
                "$staging = '" + EscapePsSingleQuoted(stagingDir) + "'\n" +
                "$target = '" + EscapePsSingleQuoted(gameRoot) + "'\n" +
                "$exe = '" + EscapePsSingleQuoted(exePath) + "'\n" +
                "$removedList = '" + EscapePsSingleQuoted(removedPath) + "'\n" +
                "Get-ChildItem -LiteralPath $staging -Recurse -File | ForEach-Object {\n" +
                "  $rel = $_.FullName.Substring($staging.Length).TrimStart('\\','/')\n" +
                "  $dest = Join-Path $target $rel\n" +
                "  $dir = Split-Path -Parent $dest\n" +
                "  if ($dir -and !(Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }\n" +
                "  Copy-Item -LiteralPath $_.FullName -Destination $dest -Force\n" +
                "}\n" +
                "if (Test-Path -LiteralPath $removedList) {\n" +
                "  Get-Content -LiteralPath $removedList | ForEach-Object {\n" +
                "    $rel = $_.Trim()\n" +
                "    if (!$rel) { return }\n" +
                "    $p = Join-Path $target $rel\n" +
                "    if (Test-Path -LiteralPath $p) { Remove-Item -LiteralPath $p -Recurse -Force -ErrorAction SilentlyContinue }\n" +
                "  }\n" +
                "}\n" +
                "Start-Process -FilePath $exe\n" +
                "Remove-Item -LiteralPath $PSCommandPath -Force\n";

            File.WriteAllText(scriptPath, psContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + scriptPath + "\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = gameRoot
            };
            Process.Start(psi);
            return true;
        }
        catch (Exception e)
        {
            LastError = "启动更新程序失败: " + e.Message;
            return false;
        }
#endif
    }

    static bool HasStagedFiles()
    {
        if (!Directory.Exists(StagingDirectory))
            return false;
        return Directory.GetFiles(StagingDirectory, "*", SearchOption.AllDirectories).Length > 0;
    }

    static bool TrySavePendingState(string targetVersion, string stagingDir, string[] removedFiles)
    {
        try
        {
            Directory.CreateDirectory(UpdateRootDirectory);
            var state = new PendingUpdateState
            {
                targetVersion = targetVersion,
                stagingDir = stagingDir,
                removedFiles = removedFiles ?? Array.Empty<string>(),
            };
            File.WriteAllText(PendingStatePath, JsonUtility.ToJson(state), Encoding.UTF8);
            return true;
        }
        catch (Exception e)
        {
            LastError = e.Message;
            return false;
        }
    }

    static bool TryLoadPendingState(out PendingUpdateState state)
    {
        state = null;
        try
        {
            if (!File.Exists(PendingStatePath))
                return false;
            state = JsonUtility.FromJson<PendingUpdateState>(File.ReadAllText(PendingStatePath, Encoding.UTF8));
            return state != null && !string.IsNullOrWhiteSpace(state.stagingDir);
        }
        catch
        {
            return false;
        }
    }

    static void WriteRemovedList(string path, string[] removedFiles)
    {
        if (removedFiles == null || removedFiles.Length == 0)
        {
            TryDeleteFile(path);
            return;
        }

        var sb = new StringBuilder();
        for (int i = 0; i < removedFiles.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(removedFiles[i]))
                continue;
            sb.AppendLine(removedFiles[i].Replace('\\', '/'));
        }
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    static string GetGameRootDirectory() =>
        Directory.GetParent(Application.dataPath)?.FullName;

    static string ResolveDownloadUrl(string baseUrlTrimmed, string downloadUrl)
    {
        string url = (downloadUrl ?? "").Trim();
        if (string.IsNullOrEmpty(url))
            return baseUrlTrimmed + "/api/client/download/latest";
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return url;
        if (!url.StartsWith("/", StringComparison.Ordinal))
            url = "/" + url;
        return baseUrlTrimmed + url;
    }

    static string EscapePsSingleQuoted(string value) =>
        (value ?? "").Replace("'", "''");

    static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { /* ignore */ }
    }

    static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch { /* ignore */ }
    }

    static void ReportProgress(float value, string message) =>
        OnProgress?.Invoke(Mathf.Clamp01(value), message ?? "");

    public static string ComputeSha256HexFile(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(stream);
        var sb = new StringBuilder(hash.Length * 2);
        for (int i = 0; i < hash.Length; i++)
            sb.Append(hash[i].ToString("x2"));
        return sb.ToString();
    }

    [Serializable]
    class PendingUpdateState
    {
        public string targetVersion;
        public string stagingDir;
        public string[] removedFiles;
    }

    [Serializable]
    public class ClientVersionCheckResponse
    {
        public bool ok;
        public bool updateRequired;
        public string localVersion;
        public string latestVersion;
        public string downloadUrl;
        public string sha256;
        public long size;
        public string releaseNotes;
        public bool forceUpdate;
        public string message;
        public string updateMode;
        public string patchUrl;
        public long patchSize;
        public int changedCount;
    }

    [Serializable]
    public class ClientPatchResponse
    {
        public bool ok;
        public string mode;
        public string fromVersion;
        public string toVersion;
        public int changedCount;
        public int removedCount;
        public long patchSize;
        public long fullSize;
        public ClientPatchFileEntry[] files;
        public string[] removed;
        public string fullDownloadUrl;
        public string sha256;
        public long size;
        public bool syncMissingOnly;
    }

    [Serializable]
    public class ClientPatchFileEntry
    {
        public string path;
        public string sha256;
        public long size;
        public string url;
    }
}
