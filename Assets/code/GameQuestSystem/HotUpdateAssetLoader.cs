using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace GameQuestSystem
{
    /// <summary>从热更新缓存目录加载 AssetBundle 内资源。</summary>
    public static class HotUpdateAssetLoader
    {
        static readonly Dictionary<string, AssetBundle> LoadedBundles = new Dictionary<string, AssetBundle>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, string> AssetPathToBundleName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static void UnloadAll()
        {
            foreach (var pair in LoadedBundles)
            {
                if (pair.Value != null)
                    pair.Value.Unload(false);
            }

            LoadedBundles.Clear();
            AssetPathToBundleName.Clear();
        }

        public static void TryBuildAssetIndexFromManifest(ContentManifestFileJson manifest)
        {
            AssetPathToBundleName.Clear();
            if (manifest?.bundles == null)
                return;

            for (int i = 0; i < manifest.bundles.Length; i++)
            {
                var bundle = manifest.bundles[i];
                if (bundle == null || string.IsNullOrWhiteSpace(bundle.name) || bundle.assets == null)
                    continue;

                for (int j = 0; j < bundle.assets.Length; j++)
                {
                    string assetPath = NormalizeAssetPath(bundle.assets[j]);
                    if (string.IsNullOrEmpty(assetPath))
                        continue;
                    AssetPathToBundleName[assetPath] = bundle.name;
                }
            }
        }

        public static void TryBuildAssetIndexFromDisk()
        {
            AssetPathToBundleName.Clear();
            string manifestPath = Path.Combine(ResourceHotUpdateService.CacheRootDirectory, "resource_manifest.json");
            if (!File.Exists(manifestPath))
                return;

            try
            {
                var manifest = JsonUtility.FromJson<ContentManifestFileJson>(
                    File.ReadAllText(manifestPath, Encoding.UTF8));
                TryBuildAssetIndexFromManifest(manifest);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[HotUpdateAssetLoader] 读取本地 manifest 失败: " + e.Message);
            }
        }

        public static GameObject TryLoadPrefab(string prefabPath)
        {
            prefabPath = NormalizeAssetPath(prefabPath);
            if (string.IsNullOrEmpty(prefabPath))
                return null;

            if (!AssetPathToBundleName.TryGetValue(prefabPath, out string bundleName))
                return null;

            var bundle = GetOrLoadBundle(bundleName);
            if (bundle == null)
                return null;

            return bundle.LoadAsset<GameObject>(prefabPath);
        }

        public static T TryLoadAsset<T>(string assetPath) where T : UnityEngine.Object
        {
            assetPath = NormalizeAssetPath(assetPath);
            if (string.IsNullOrEmpty(assetPath))
                return null;

            if (!AssetPathToBundleName.TryGetValue(assetPath, out string bundleName))
                return null;

            var bundle = GetOrLoadBundle(bundleName);
            return bundle != null ? bundle.LoadAsset<T>(assetPath) : null;
        }

        static AssetBundle GetOrLoadBundle(string bundleName)
        {
            if (string.IsNullOrWhiteSpace(bundleName))
                return null;

            if (LoadedBundles.TryGetValue(bundleName, out var cached) && cached != null)
                return cached;

            string path = Path.Combine(ResourceHotUpdateService.BundleCacheDirectory, bundleName);
            if (!File.Exists(path))
                return null;

            var bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null)
            {
                Debug.LogWarning("[HotUpdateAssetLoader] 无法加载 bundle: " + path);
                return null;
            }

            LoadedBundles[bundleName] = bundle;
            return bundle;
        }

        static string NormalizeAssetPath(string path)
        {
            return (path ?? "").Trim().Replace('\\', '/');
        }
    }
}
