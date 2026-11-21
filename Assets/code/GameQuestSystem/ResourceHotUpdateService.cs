using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace GameQuestSystem
{
    /// <summary>
    /// 进游戏前资源完整性校验与补全：拉 manifest → 校验关键资源 → POST patch → 下载缺失文件。
    /// </summary>
    public static class ResourceHotUpdateService
    {
        public const string PatchRelativePath = "/api/content/patch";
        public const string BundleRelativePathPrefix = "/api/content/bundle/";

        const string CacheRootFolderName = "hotupdate";
        const string BundleFolderName = "bundles";
        const string ContentFolderName = "content";
        const string CachedManifestFileName = "resource_manifest.json";

        public static event Action OnResourcesReady;

        public static bool IsReady { get; private set; }
        public static bool IsBusy { get; private set; }
        public static string LastError { get; private set; }
        public static string ActiveContentVersion { get; private set; }
        public static ContentManifestFileJson CachedManifest { get; private set; }

        public static event Action<float, string> OnProgress;

        public static string CacheRootDirectory =>
            Path.Combine(Application.persistentDataPath, CacheRootFolderName);

        public static string BundleCacheDirectory =>
            Path.Combine(CacheRootDirectory, BundleFolderName);

        public static string ContentCacheDirectory =>
            Path.Combine(CacheRootDirectory, ContentFolderName);

        public static void ResetState()
        {
            IsReady = false;
            IsBusy = false;
            LastError = null;
        }

        public static IEnumerator EnsureResourcesReadyCoroutine(
            string baseUrlTrimmed,
            StoryDialogueDatabase bundledStoryDatabase,
            bool skipWhenOffline = false)
        {
            IsReady = false;
            IsBusy = true;
            LastError = null;

            if (skipWhenOffline)
            {
                ReportProgress(1f, "离线模式：跳过资源热更新");
                HotUpdateAssetLoader.TryBuildAssetIndexFromDisk();
                IsReady = true;
                IsBusy = false;
                OnResourcesReady?.Invoke();
                yield break;
            }

            baseUrlTrimmed = (baseUrlTrimmed ?? "").Trim().TrimEnd('/');
            if (string.IsNullOrEmpty(baseUrlTrimmed))
            {
                LastError = "baseUrl 为空";
                FallbackToLocalOrBundled(bundledStoryDatabase, "未配置服务器，使用内置资源");
                yield break;
            }

            ReportProgress(0.05f, "正在获取资源清单…");
            ContentManifestFileJson manifest = null;
            yield return CoFetchManifest(baseUrlTrimmed, result => manifest = result);
            if (manifest == null)
            {
                FallbackToLocalOrBundled(bundledStoryDatabase, "无法获取资源清单，使用本地/内置资源");
                yield break;
            }

            CachedManifest = manifest;
            ActiveContentVersion = manifest.contentVersion ?? "";
            TrySaveCachedManifest(manifest);

            var missing = CollectMissingOrOutdated(manifest);
            if (missing.Count > 0)
            {
                ReportProgress(0.15f, $"发现 {missing.Count} 项资源需补全…");
                ContentPatchResponseJson patch = null;
                yield return CoRequestPatch(baseUrlTrimmed, missing, result => patch = result);
                if (patch?.files == null || patch.files.Length == 0)
                {
                    LastError = "服务端未返回可补全文件";
                    IsBusy = false;
                    yield break;
                }

                yield return CoDownloadPatchFiles(baseUrlTrimmed, patch.files, bundledStoryDatabase);
                if (!string.IsNullOrEmpty(LastError))
                {
                    IsBusy = false;
                    yield break;
                }
            }
            else
            {
                ReportProgress(0.4f, "本地资源已完整");
                TryApplyCachedStoryDatabase(bundledStoryDatabase);
            }

            HotUpdateAssetLoader.TryBuildAssetIndexFromManifest(manifest);
            ReportProgress(1f, "资源就绪");
            IsReady = true;
            IsBusy = false;
            OnResourcesReady?.Invoke();
        }

        static void FallbackToLocalOrBundled(StoryDialogueDatabase bundledStoryDatabase, string progressMessage)
        {
            ReportProgress(1f, progressMessage);
            HotUpdateAssetLoader.TryBuildAssetIndexFromDisk();
            TryApplyCachedStoryDatabase(bundledStoryDatabase);
            IsReady = true;
            IsBusy = false;
            OnResourcesReady?.Invoke();
        }

        static IEnumerator CoFetchManifest(string baseUrlTrimmed, Action<ContentManifestFileJson> onDone)
        {
            string url = baseUrlTrimmed + ContentHotUpdateService.ManifestRelativePath;
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
                    onDone?.Invoke(JsonUtility.FromJson<ContentManifestFileJson>(req.downloadHandler.text));
                }
                catch (Exception e)
                {
                    LastError = "解析 manifest 失败: " + e.Message;
                    onDone?.Invoke(null);
                }
            }
        }

        static IEnumerator CoRequestPatch(string baseUrlTrimmed, List<string> missing, Action<ContentPatchResponseJson> onDone)
        {
            string url = baseUrlTrimmed + PatchRelativePath;
            var body = new ContentPatchRequestJson
            {
                missing = missing.ToArray()
            };
            byte[] payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(body));
            using (var req = new UnityWebRequest(url, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(payload);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
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
                    onDone?.Invoke(JsonUtility.FromJson<ContentPatchResponseJson>(req.downloadHandler.text));
                }
                catch (Exception e)
                {
                    LastError = "解析 patch 响应失败: " + e.Message;
                    onDone?.Invoke(null);
                }
            }
        }

        static IEnumerator CoDownloadPatchFiles(
            string baseUrlTrimmed,
            ContentPatchFileJson[] files,
            StoryDialogueDatabase bundledStoryDatabase)
        {
            Directory.CreateDirectory(BundleCacheDirectory);
            Directory.CreateDirectory(ContentCacheDirectory);

            int total = files.Length;
            for (int i = 0; i < total; i++)
            {
                var file = files[i];
                if (file == null || string.IsNullOrWhiteSpace(file.name))
                    continue;

                float begin = 0.2f + 0.7f * (i / (float)Mathf.Max(1, total));
                float end = 0.2f + 0.7f * ((i + 1) / (float)Mathf.Max(1, total));
                ReportProgress(begin, $"正在下载 {file.name}…");

                string destPath = ResolveLocalPath(file);
                string downloadUrl = ResolveDownloadUrl(baseUrlTrimmed, file.url);
                yield return CoDownloadFile(downloadUrl, destPath, progress =>
                {
                    ReportProgress(Mathf.Lerp(begin, end, progress), $"正在下载 {file.name}…");
                });

                if (!string.IsNullOrEmpty(LastError))
                    yield break;

                if (!string.IsNullOrWhiteSpace(file.sha256))
                {
                    string actual = ComputeSha256HexFile(destPath);
                    if (!string.Equals(actual, file.sha256.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        LastError = file.name + " 校验失败 (sha256)";
                        yield break;
                    }
                }

                if (IsStoryDatabaseFile(file))
                    TryApplyStoryDatabaseFile(destPath, bundledStoryDatabase);
            }
        }

        static IEnumerator CoDownloadFile(string url, string destPath, Action<float> onProgress)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destPath) ?? CacheRootDirectory);
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
                    if (File.Exists(destPath))
                    {
                        try { File.Delete(destPath); } catch { /* ignore */ }
                    }
                }
            }
        }

        static List<string> CollectMissingOrOutdated(ContentManifestFileJson manifest)
        {
            var missing = new List<string>();
            if (manifest == null)
                return missing;

            if (manifest.storyDatabase != null &&
                !string.IsNullOrWhiteSpace(manifest.storyDatabase.version))
            {
                string storyPath = Path.Combine(ContentCacheDirectory, "story_database.json");
                if (!IsFileValid(storyPath, manifest.storyDatabase.sha256, critical: true))
                    missing.Add("story_database");
            }

            if (manifest.bundles == null)
                return missing;

            for (int i = 0; i < manifest.bundles.Length; i++)
            {
                var bundle = manifest.bundles[i];
                if (bundle == null || string.IsNullOrWhiteSpace(bundle.name))
                    continue;

                string localPath = ResolveLocalPathFromManifest(bundle);
                if (!File.Exists(localPath))
                {
                    missing.Add(bundle.name);
                    continue;
                }

                if (bundle.critical && !string.IsNullOrWhiteSpace(bundle.sha256))
                {
                    string actual = ComputeSha256HexFile(localPath);
                    if (!string.Equals(actual, bundle.sha256.Trim(), StringComparison.OrdinalIgnoreCase))
                        missing.Add(bundle.name);
                }
            }

            return missing;
        }

        static bool IsFileValid(string path, string expectedSha256, bool critical)
        {
            if (!File.Exists(path))
                return false;
            if (!critical || string.IsNullOrWhiteSpace(expectedSha256))
                return true;

            string actual = ComputeSha256HexFile(path);
            return string.Equals(actual, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        static string ResolveLocalPathFromManifest(ContentManifestBundleJson bundle)
        {
            string fileName = string.IsNullOrWhiteSpace(bundle.fileName) ? bundle.name : bundle.fileName;
            string kind = (bundle.kind ?? "bundle").Trim().ToLowerInvariant();
            if (kind == "json" || kind == "catalog")
                return Path.Combine(ContentCacheDirectory, fileName);
            return Path.Combine(BundleCacheDirectory, fileName);
        }

        static string ResolveLocalPath(ContentPatchFileJson file)
        {
            string fileName = string.IsNullOrWhiteSpace(file.fileName) ? file.name : file.fileName;
            string kind = (file.kind ?? "bundle").Trim().ToLowerInvariant();
            if (kind == "json" || kind == "catalog")
                return Path.Combine(ContentCacheDirectory, fileName);
            return Path.Combine(BundleCacheDirectory, fileName);
        }

        static string ResolveDownloadUrl(string baseUrlTrimmed, string url)
        {
            string value = (url ?? "").Trim();
            if (string.IsNullOrEmpty(value))
                return baseUrlTrimmed;
            if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return value;
            if (!value.StartsWith("/", StringComparison.Ordinal))
                value = "/" + value;
            return baseUrlTrimmed + value;
        }

        static bool IsStoryDatabaseFile(ContentPatchFileJson file)
        {
            if (file == null)
                return false;
            if (string.Equals(file.name, "story_database", StringComparison.OrdinalIgnoreCase))
                return true;
            return string.Equals(file.kind, "json", StringComparison.OrdinalIgnoreCase) &&
                   (file.fileName ?? "").IndexOf("story_database", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static void TryApplyCachedStoryDatabase(StoryDialogueDatabase bundledStoryDatabase)
        {
            string storyPath = Path.Combine(ContentCacheDirectory, "story_database.json");
            if (!File.Exists(storyPath))
                return;

            try
            {
                string json = File.ReadAllText(storyPath, Encoding.UTF8);
                var file = JsonUtility.FromJson<StoryDialogueDatabaseFileJson>(json);
                if (file == null || file.documents == null || file.documents.Length == 0)
                    return;

                var database = StoryDialogueDatabaseJsonUtility.FromFileJson(file, bundledStoryDatabase);
                ContentHotUpdateService.ApplyRuntimeDatabase(database, file.contentVersion);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ResourceHotUpdate] 应用本地剧情缓存失败: " + e.Message);
            }
        }

        static void TryApplyStoryDatabaseFile(string path, StoryDialogueDatabase bundledStoryDatabase)
        {
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                var file = JsonUtility.FromJson<StoryDialogueDatabaseFileJson>(json);
                if (file == null || file.documents == null || file.documents.Length == 0)
                    return;

                var database = StoryDialogueDatabaseJsonUtility.FromFileJson(file, bundledStoryDatabase);
                ContentHotUpdateService.ApplyRuntimeDatabase(database, file.contentVersion);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ResourceHotUpdate] 应用剧情热更新失败: " + e.Message);
            }
        }

        static void TrySaveCachedManifest(ContentManifestFileJson manifest)
        {
            try
            {
                Directory.CreateDirectory(CacheRootDirectory);
                File.WriteAllText(
                    Path.Combine(CacheRootDirectory, CachedManifestFileName),
                    JsonUtility.ToJson(manifest, true),
                    Encoding.UTF8);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ResourceHotUpdate] 写入 manifest 缓存失败: " + e.Message);
            }
        }

        static void ReportProgress(float value, string message)
        {
            OnProgress?.Invoke(Mathf.Clamp01(value), message ?? "");
        }

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
    }
}
