using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace GameQuestSystem
{
    /// <summary>
    /// 从 GameServer 拉取剧情内容热更新（JSON），缓存到 persistentDataPath/content。
    /// 预制体引用仍来自客户端内置资源，热更新主要覆盖对话、任务链、坐标等数据。
    /// </summary>
    public static class ContentHotUpdateService
    {
        public const string ManifestRelativePath = "/api/content/manifest";
        public const string StoryDatabaseRelativePath = "/api/content/story_database";

        const string CacheFolderName = "content";
        const string CachedManifestFileName = "manifest.json";
        const string CachedStoryDatabaseFileName = "story_database.json";
        const string PlayerPrefsLastVersionKey = "ContentHotUpdate_LastVersion";

        public static StoryDialogueDatabase CachedRuntimeDatabase { get; private set; }
        public static string ActiveContentVersion { get; private set; }
        public static bool LastFetchOk { get; private set; }
        public static string LastError { get; private set; }

        public static event Action<StoryDialogueDatabase> OnContentApplied;

        public static string CacheDirectory =>
            Path.Combine(Application.persistentDataPath, CacheFolderName);

        public static void ClearRuntimeCache()
        {
            if (CachedRuntimeDatabase != null)
            {
                UnityEngine.Object.Destroy(CachedRuntimeDatabase);
                CachedRuntimeDatabase = null;
            }

            ActiveContentVersion = null;
            LastFetchOk = false;
            LastError = null;
        }

        public static StoryDialogueDatabase TryLoadCachedDatabase(StoryDialogueDatabase bundledDatabase)
        {
            try
            {
                string dbPath = Path.Combine(CacheDirectory, CachedStoryDatabaseFileName);
                if (!File.Exists(dbPath))
                    return null;

                string json = File.ReadAllText(dbPath, Encoding.UTF8);
                var file = JsonUtility.FromJson<StoryDialogueDatabaseFileJson>(json);
                if (file == null || file.documents == null || file.documents.Length == 0)
                    return null;

                var database = StoryDialogueDatabaseJsonUtility.FromFileJson(file, bundledDatabase);
                ReplaceRuntimeDatabase(database, file.contentVersion);
                LastFetchOk = true;
                LastError = null;
                return database;
            }
            catch (Exception e)
            {
                LastFetchOk = false;
                LastError = e.Message;
                return null;
            }
        }

        public static IEnumerator EnsureLatestContentCoroutine(
            string baseUrlTrimmed,
            StoryDialogueDatabase bundledDatabase,
            bool forceRefresh = false)
        {
            baseUrlTrimmed = (baseUrlTrimmed ?? "").Trim().TrimEnd('/');
            if (string.IsNullOrEmpty(baseUrlTrimmed))
            {
                LastFetchOk = false;
                LastError = "baseUrl 为空";
                yield break;
            }

            if (!forceRefresh && CachedRuntimeDatabase != null && LastFetchOk)
                yield break;

            string manifestUrl = baseUrlTrimmed + ManifestRelativePath;
            ContentManifestFileJson remoteManifest = null;
            using (var manifestReq = UnityWebRequest.Get(manifestUrl))
            {
                yield return manifestReq.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
                if (manifestReq.result != UnityWebRequest.Result.Success)
#else
                if (manifestReq.isNetworkError || manifestReq.isHttpError)
#endif
                {
                    LastFetchOk = CachedRuntimeDatabase != null;
                    LastError = manifestReq.error;
                    yield break;
                }

                try
                {
                    remoteManifest = JsonUtility.FromJson<ContentManifestFileJson>(manifestReq.downloadHandler.text);
                }
                catch (Exception e)
                {
                    LastFetchOk = CachedRuntimeDatabase != null;
                    LastError = "解析 manifest 失败: " + e.Message;
                    yield break;
                }
            }

            if (remoteManifest?.storyDatabase == null ||
                string.IsNullOrWhiteSpace(remoteManifest.storyDatabase.version))
            {
                LastFetchOk = CachedRuntimeDatabase != null;
                LastError = "manifest 缺少 storyDatabase.version";
                yield break;
            }

            string remoteVersion = remoteManifest.storyDatabase.version.Trim();
            string localVersion = PlayerPrefs.GetString(PlayerPrefsLastVersionKey, "");
            string cachedManifestVersion = ReadCachedManifestVersion();

            if (!forceRefresh &&
                string.Equals(remoteVersion, localVersion, StringComparison.Ordinal) &&
                string.Equals(remoteVersion, cachedManifestVersion, StringComparison.Ordinal) &&
                File.Exists(Path.Combine(CacheDirectory, CachedStoryDatabaseFileName)))
            {
                var cached = TryLoadCachedDatabase(bundledDatabase);
                if (cached != null)
                    yield break;
            }

            string dbUrl = ResolveStoryDatabaseUrl(baseUrlTrimmed, remoteManifest.storyDatabase.url);
            string downloadedJson = null;
            using (var dbReq = UnityWebRequest.Get(dbUrl))
            {
                yield return dbReq.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
                if (dbReq.result != UnityWebRequest.Result.Success)
#else
                if (dbReq.isNetworkError || dbReq.isHttpError)
#endif
                {
                    LastFetchOk = CachedRuntimeDatabase != null;
                    LastError = dbReq.error;
                    yield break;
                }

                downloadedJson = dbReq.downloadHandler.text;
            }

            if (string.IsNullOrWhiteSpace(downloadedJson))
            {
                LastFetchOk = CachedRuntimeDatabase != null;
                LastError = "story_database 响应为空";
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(remoteManifest.storyDatabase.sha256))
            {
                string actualHash = ComputeSha256Hex(downloadedJson);
                if (!string.Equals(actualHash, remoteManifest.storyDatabase.sha256.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    LastFetchOk = CachedRuntimeDatabase != null;
                    LastError = "story_database 校验失败 (sha256)";
                    yield break;
                }
            }

            StoryDialogueDatabaseFileJson file;
            try
            {
                file = JsonUtility.FromJson<StoryDialogueDatabaseFileJson>(downloadedJson);
            }
            catch (Exception e)
            {
                LastFetchOk = CachedRuntimeDatabase != null;
                LastError = "解析 story_database 失败: " + e.Message;
                yield break;
            }

            if (file == null || file.documents == null || file.documents.Length == 0)
            {
                LastFetchOk = CachedRuntimeDatabase != null;
                LastError = "story_database 无有效 documents";
                yield break;
            }

            if (string.IsNullOrWhiteSpace(file.contentVersion))
                file.contentVersion = remoteVersion;

            try
            {
                Directory.CreateDirectory(CacheDirectory);
                File.WriteAllText(
                    Path.Combine(CacheDirectory, CachedStoryDatabaseFileName),
                    downloadedJson,
                    Encoding.UTF8);
                File.WriteAllText(
                    Path.Combine(CacheDirectory, CachedManifestFileName),
                    JsonUtility.ToJson(remoteManifest, true),
                    Encoding.UTF8);
                PlayerPrefs.SetString(PlayerPrefsLastVersionKey, remoteVersion);
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ContentHotUpdate] 写入本地缓存失败: " + e.Message);
            }

            var database = StoryDialogueDatabaseJsonUtility.FromFileJson(file, bundledDatabase);
            ApplyRuntimeDatabase(database, file.contentVersion);
            LastFetchOk = true;
            LastError = null;
            OnContentApplied?.Invoke(database);
        }

        public static void ApplyRuntimeDatabase(StoryDialogueDatabase database, string contentVersion)
        {
            ReplaceRuntimeDatabase(database, contentVersion);
        }

        static void ReplaceRuntimeDatabase(StoryDialogueDatabase database, string contentVersion)
        {
            if (CachedRuntimeDatabase != null && CachedRuntimeDatabase != database)
                UnityEngine.Object.Destroy(CachedRuntimeDatabase);

            CachedRuntimeDatabase = database;
            ActiveContentVersion = contentVersion ?? "";
        }

        static string ReadCachedManifestVersion()
        {
            try
            {
                string path = Path.Combine(CacheDirectory, CachedManifestFileName);
                if (!File.Exists(path))
                    return "";

                var manifest = JsonUtility.FromJson<ContentManifestFileJson>(File.ReadAllText(path, Encoding.UTF8));
                return manifest?.storyDatabase?.version?.Trim() ?? "";
            }
            catch
            {
                return "";
            }
        }

        static string ResolveStoryDatabaseUrl(string baseUrlTrimmed, string manifestUrl)
        {
            string url = (manifestUrl ?? "").Trim();
            if (string.IsNullOrEmpty(url))
                return baseUrlTrimmed + StoryDatabaseRelativePath;
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return url;
            if (!url.StartsWith("/", StringComparison.Ordinal))
                url = "/" + url;
            return baseUrlTrimmed + url;
        }

        public static string ComputeSha256Hex(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? "");
            byte[] hash = SHA256.Create().ComputeHash(bytes);
            var sb = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }
    }
}
