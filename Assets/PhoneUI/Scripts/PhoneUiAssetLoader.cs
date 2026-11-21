using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace PhoneUI
{
    public static class PhoneUiAssetLoader
    {
        const string ResourcesRoot = "PhoneUI";
        const string SmartphoneResourcesPath = "PhoneUI/smartphone";
        const string SmartphoneAssetPath = "Assets/pictures/smartphone.png";
        const string PhoneFrameAssetPath = "Assets/PhoneUI/CellPhone/UI/Phoneee.png";
        const string DefaultTmpFontResourcesPath = "Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF";

        static readonly Dictionary<PhoneAppId, string> AppIconResourcePaths = new()
        {
            { PhoneAppId.Settings, "Icons/setting" },
            { PhoneAppId.Map, "Icons/map" },
            { PhoneAppId.Quest, "Icons/quest" },
            { PhoneAppId.Shop, "Icons/shop" },
            { PhoneAppId.Inventory, "Icons/inventory" },
            { PhoneAppId.Multiplayer, "Icons/online" },
            { PhoneAppId.Account, "Icons/account" },
            { PhoneAppId.Custom1, "Icons/17" },
            { PhoneAppId.Custom2, "Icons/18" },
        };

        static readonly Dictionary<PhoneAppId, string> AppIconAssetPaths = new()
        {
            { PhoneAppId.Settings, "Assets/PhoneUI/CellPhone/UI/setting.png" },
            { PhoneAppId.Map, "Assets/PhoneUI/CellPhone/UI/map.png" },
            { PhoneAppId.Quest, "Assets/PhoneUI/CellPhone/UI/quest.png" },
            { PhoneAppId.Shop, "Assets/PhoneUI/CellPhone/UI/shop.png" },
            { PhoneAppId.Inventory, "Assets/PhoneUI/CellPhone/UI/inventory.png" },
            { PhoneAppId.Multiplayer, "Assets/PhoneUI/CellPhone/UI/online.png" },
            { PhoneAppId.Account, "Assets/PhoneUI/CellPhone/UI/account.png" },
            { PhoneAppId.Custom1, "Assets/PhoneUI/CellPhone/UI/17.png" },
            { PhoneAppId.Custom2, "Assets/PhoneUI/CellPhone/UI/18.png" },
        };

        static Dictionary<PhoneAppId, Sprite> _iconCache;
        static Sprite _phoneFrameCache;
        static TMP_FontAsset _cachedDefaultTmpFont;

        public static Sprite GetPhoneFrameSprite()
        {
            if (_phoneFrameCache != null)
                return _phoneFrameCache;

            _phoneFrameCache = LoadSprite($"{ResourcesRoot}/PhoneFrame");
            if (_phoneFrameCache != null)
                return _phoneFrameCache;

            _phoneFrameCache = LoadSmartphoneFrame();
#if UNITY_EDITOR
            if (_phoneFrameCache == null)
                _phoneFrameCache = LoadSpriteAtPath(PhoneFrameAssetPath);
#endif
            return _phoneFrameCache;
        }

        public static Sprite LoadSmartphoneFrame()
        {
            var fromResources = LoadSprite(SmartphoneResourcesPath);
            if (fromResources != null)
                return fromResources;

#if UNITY_EDITOR
            return LoadSpriteAtPath(SmartphoneAssetPath);
#else
            return null;
#endif
        }

        public static Sprite GetAppIconSprite(PhoneAppId appId)
        {
            if (appId == PhoneAppId.None)
                return null;

            EnsureIconCache();
            return _iconCache.TryGetValue(appId, out var sprite) ? sprite : null;
        }

        static void EnsureIconCache()
        {
            if (_iconCache != null)
                return;

            _iconCache = new Dictionary<PhoneAppId, Sprite>();
            foreach (var pair in AppIconResourcePaths)
            {
                var sprite = LoadSprite($"{ResourcesRoot}/{pair.Value}");
#if UNITY_EDITOR
                if (sprite == null && AppIconAssetPaths.TryGetValue(pair.Key, out var assetPath))
                    sprite = LoadSpriteAtPath(assetPath);
#endif
                if (sprite != null)
                    _iconCache[pair.Key] = sprite;
            }
        }

        static Sprite LoadSprite(string resourcesPath)
        {
            var sprite = Resources.Load<Sprite>(resourcesPath);
            if (sprite != null)
                return sprite;

            var tex = Resources.Load<Texture2D>(resourcesPath);
            if (tex == null)
                return null;

            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

#if UNITY_EDITOR
        static Sprite LoadSpriteAtPath(string assetPath)
        {
            var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null)
                return sprite;

            var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null)
                return null;

            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
#endif

        public static TMP_FontAsset LoadDefaultTmpFont()
        {
            if (_cachedDefaultTmpFont != null)
                return _cachedDefaultTmpFont;

            var font = Resources.Load<TMP_FontAsset>(DefaultTmpFontResourcesPath);
#if UNITY_EDITOR
            if (font == null)
            {
                font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Assets/TextMesh Pro/Resources/Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF.asset");
            }
#endif
            if (font != null)
            {
                _cachedDefaultTmpFont = font;
                return font;
            }

            if (TMP_Settings.instance != null && TMP_Settings.defaultFontAsset != null)
            {
                _cachedDefaultTmpFont = TMP_Settings.defaultFontAsset;
                return _cachedDefaultTmpFont;
            }

            return null;
        }

        public static void ApplyTmpFont(TMP_Text text, TMP_FontAsset font = null)
        {
            if (text == null)
                return;
            font ??= LoadDefaultTmpFont();
            if (font != null)
                text.font = font;
        }
    }
}
