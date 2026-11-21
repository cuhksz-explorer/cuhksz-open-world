using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace DormMall
{
    /// <summary>
    /// 图标加载：优先 ProductDatabase 内置 Sprite/Resources；其次支持远程 URL（http/https 或 /static/...）。
    /// </summary>
    public static class DormMallIconLoader
    {
        sealed class Runner : MonoBehaviour { }
        struct PendingTarget
        {
            public Image image;
            public Sprite fallback;
        }

        static Runner _runner;
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, List<PendingTarget>> Pending = new Dictionary<string, List<PendingTarget>>();
        static readonly Dictionary<string, Sprite> LocalSpriteFromTextureCache = new Dictionary<string, Sprite>();

        static Runner EnsureRunner()
        {
            if (_runner != null)
                return _runner;
            var go = new GameObject("[DormMallIconLoader]");
            Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<Runner>();
            return _runner;
        }

        public static void SetIcon(Image target, ProductDatabase db, ProductEntry entry, Sprite placeholder = null)
        {
            if (target == null || entry == null)
                return;

            var local = db != null ? db.GetIcon(entry) : null;
            if (local == null)
                local = TryLoadLocalSpriteFromTexture(entry.iconPath);
            if (local != null)
            {
                target.sprite = local;
                target.enabled = true;
                return;
            }

            string raw = (entry.iconPath ?? "").Trim();
            string url = ResolveRemoteUrl(raw);
            Sprite fallback = placeholder != null ? placeholder : GetGeneratedFallback(entry.productName);
            if (string.IsNullOrEmpty(url))
            {
                target.sprite = fallback;
                target.enabled = target.sprite != null;
                return;
            }

            if (Cache.TryGetValue(url, out var s) && s != null)
            {
                target.sprite = s;
                target.enabled = true;
                return;
            }

            target.sprite = fallback;
            target.enabled = target.sprite != null;

            if (!Pending.TryGetValue(url, out var list))
            {
                list = new List<PendingTarget>();
                Pending[url] = list;
                EnsureRunner().StartCoroutine(CoLoad(url));
            }
            list.Add(new PendingTarget { image = target, fallback = fallback });
        }

        static Sprite TryLoadLocalSpriteFromTexture(string iconPath)
        {
            string p = (iconPath ?? "").Trim();
            if (string.IsNullOrEmpty(p))
                return null;
            // 仅处理 Resources 本地路径；URL / static 路径不在此分支处理。
            if (p.StartsWith("http://") || p.StartsWith("https://") || p.StartsWith("/static/") || p.StartsWith("static/"))
                return null;

            if (LocalSpriteFromTextureCache.TryGetValue(p, out var cached) && cached != null)
                return cached;

            var tex = Resources.Load<Texture2D>(p);
            if (tex == null)
                return null;

            var sp = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f);
            LocalSpriteFromTextureCache[p] = sp;
            return sp;
        }

        static IEnumerator CoLoad(string url)
        {
            using (var req = UnityWebRequestTexture.GetTexture(url))
            {
                yield return req.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
                bool ok = req.result == UnityWebRequest.Result.Success;
#else
                bool ok = !(req.isNetworkError || req.isHttpError);
#endif
                if (!ok)
                {
                    ApplyFallbackIfAny(url);
                    Pending.Remove(url);
                    yield break;
                }

                var tex = DownloadHandlerTexture.GetContent(req);
                if (tex == null)
                {
                    ApplyFallbackIfAny(url);
                    Pending.Remove(url);
                    yield break;
                }

                var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                Cache[url] = sp;
                if (Pending.TryGetValue(url, out var list))
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        var img = list[i].image;
                        if (img == null) continue;
                        img.sprite = sp;
                        img.enabled = true;
                    }
                }
                Pending.Remove(url);
            }
        }

        static void ApplyFallbackIfAny(string url)
        {
            if (!Pending.TryGetValue(url, out var list))
                return;
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                if (p.image == null) continue;
                if (p.image.sprite == null && p.fallback != null)
                {
                    p.image.sprite = p.fallback;
                    p.image.enabled = true;
                }
            }
        }

        static Sprite GetGeneratedFallback(string productName)
        {
            string key = "fallback::" + (productName ?? "");
            if (Cache.TryGetValue(key, out var s) && s != null)
                return s;

            int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color c = ColorFromName(productName);
            Color dark = new Color(c.r * 0.62f, c.g * 0.62f, c.b * 0.62f, 1f);
            var arr = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float t = y / (float)(size - 1);
                Color row = Color.Lerp(dark, c, t);
                int baseIdx = y * size;
                for (int x = 0; x < size; x++)
                    arr[baseIdx + x] = row;
            }
            tex.SetPixels(arr);
            tex.Apply();
            var sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            Cache[key] = sp;
            return sp;
        }

        static Color ColorFromName(string s)
        {
            unchecked
            {
                int h = 23;
                var txt = s ?? "";
                for (int i = 0; i < txt.Length; i++)
                    h = h * 31 + txt[i];
                float r = 0.35f + ((h & 0xFF) / 255f) * 0.55f;
                float g = 0.35f + (((h >> 8) & 0xFF) / 255f) * 0.55f;
                float b = 0.35f + (((h >> 16) & 0xFF) / 255f) * 0.55f;
                return new Color(r, g, b, 1f);
            }
        }

        static string ResolveRemoteUrl(string iconPath)
        {
            if (string.IsNullOrEmpty(iconPath))
                return null;

            if (iconPath.StartsWith("http://") || iconPath.StartsWith("https://"))
                return iconPath;

            string p = iconPath;
            if (p.StartsWith("static/"))
                p = "/" + p;
            if (!p.StartsWith("/static/"))
                return null;

            string baseUrl = GameServerSync.Instance != null && !string.IsNullOrEmpty(GameServerSync.Instance.BaseUrlTrimmed)
                ? GameServerSync.Instance.BaseUrlTrimmed
                : GameServerEndpoint.DefaultBaseUrl;
            return baseUrl.TrimEnd('/') + p;
        }
    }
}
