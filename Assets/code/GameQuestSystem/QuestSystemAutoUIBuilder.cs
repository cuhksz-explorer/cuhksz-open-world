using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using System;

namespace GameQuestSystem
{
    public class QuestSystemAutoUIBuilder : MonoBehaviour
    {
        [SerializeField] private Font questUIFont;
        [SerializeField] private string preferredFontName = "Alibaba Pu Hui Ti-3-75-Semi Bold";

        private Font _resolvedFont;

        [ContextMenu("One Click Build Quest UI")]
        public void OneClickBuildQuestUI()
        {
            _resolvedFont = ResolveQuestFont();
            var existingCanvas = FindObjectOfType<Canvas>();
            Canvas canvas = existingCanvas;
            if (canvas == null)
            {
                var canvasGo = new GameObject("QuestCanvas");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 210;
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            var ui = GetComponent<QuestSystemUI>();
            if (ui == null) ui = gameObject.AddComponent<QuestSystemUI>();

            var brief = BuildBriefPanel(canvas.transform);
            var full = BuildFullPanel(canvas.transform);

            BindUiReferences(ui, brief, full);
            Debug.Log("[QuestSystem] 已一键生成任务栏 UI。");
        }

        struct BriefRefs
        {
            public GameObject root;
            public Text distance;
            public Text title;
            public Text progress;
            public Text scene;
            public Text intro;
            public Button openBtn;
        }

        struct FullRefs
        {
            public GameObject root;
            public Text distance;
            public Text title;
            public Text progress;
            public Text scene;
            public Text detail;
            public Text rewards;
            public Button closeBtn;
        }

        BriefRefs BuildBriefPanel(Transform parent)
        {
            var existing = parent.Find("QuestBriefPanel");
            if (existing != null)
                DestroyImmediate(existing.gameObject);

            var root = new GameObject("QuestBriefPanel", typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            var rt = root.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(1.40625f, 12.3f);
            rt.sizeDelta = new Vector2(400f, 400f);
            rt.localScale = Vector3.one * 0.3125f;
            var img = root.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.5f);

            var distance = CreateUndergardenBriefBand(root.transform, "DistanceText", -35f, 70f, 35);
            distance.gameObject.SetActive(false);
            var title = CreateUndergardenBriefBand(root.transform, "TitleText", -52f, 88f, 35);
            var progress = CreateUndergardenBriefBand(root.transform, "ProgressText", -122f, 44f, 35);
            var scene = CreateUndergardenBriefBand(root.transform, "SceneText", -172f, 36f, 28);
            scene.color = new Color(0.82f, 0.80f, 0.74f, 1f);
            var intro = CreateUndergardenBriefBand(root.transform, "IntroText", -300f, 180f, 35);

            return new BriefRefs
            {
                root = root,
                distance = distance,
                title = title,
                progress = progress,
                scene = scene,
                intro = intro,
                openBtn = root.GetComponent<Button>()
            };
        }

        Text CreateUndergardenBriefBand(Transform parent, string name, float topOffsetY, float height, int fontSize)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, topOffsetY);
            rt.sizeDelta = new Vector2(0f, height);

            var text = go.GetComponent<Text>();
            text.font = _resolvedFont != null ? _resolvedFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = false;
            text.resizeTextMinSize = fontSize;
            text.resizeTextMaxSize = fontSize;
            text.supportRichText = false;
            return text;
        }

        FullRefs BuildFullPanel(Transform parent)
        {
            var existing = parent.Find("QuestFullPanel");
            if (existing != null)
                DestroyImmediate(existing.gameObject);

            var root = new GameObject("QuestFullPanel", typeof(Image));
            root.transform.SetParent(parent, false);
            var rt = root.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var bg = root.GetComponent<Image>();
            bg.color = new Color(0.015f, 0.014f, 0.012f, 0.9f);

            var body = new GameObject("Body", typeof(Image));
            body.transform.SetParent(root.transform, false);
            var brt = body.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.1f, 0.08f);
            brt.anchorMax = new Vector2(0.9f, 0.92f);
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            body.GetComponent<Image>().color = new Color(0.045f, 0.042f, 0.036f, 0.96f);

            CreateDecor(body.transform, "RightDetailCard",
                new Vector2(0.345f, 0f), new Vector2(0.985f, 1f),
                new Vector2(0f, 82f), new Vector2(0f, -48f),
                new Color(0.075f, 0.07f, 0.06f, 0.96f));
            CreateDecor(body.transform, "GoldColumnDivider",
                new Vector2(0.335f, 0f), new Vector2(0.335f, 1f),
                new Vector2(0f, 78f), new Vector2(2f, -48f),
                new Color(0.88f, 0.68f, 0.32f, 1f));
            CreateDecor(body.transform, "GoldTopRule",
                new Vector2(0.36f, 1f), new Vector2(0.94f, 1f),
                new Vector2(0f, -86f), new Vector2(0f, -83f),
                new Color(0.88f, 0.68f, 0.32f, 1f));

            var distance = CreateText(body.transform, "DistanceText", new Vector2(300, -50), new Vector2(-90, -84), 24, TextAnchor.UpperLeft);
            distance.color = new Color(0.95f, 0.82f, 0.48f, 1f);
            var title = CreateText(body.transform, "TitleText", new Vector2(300, -100), new Vector2(-90, -162), 42, TextAnchor.UpperLeft);
            var progress = CreateText(body.transform, "ProgressText", new Vector2(300, -176), new Vector2(-90, -218), 31, TextAnchor.UpperLeft);
            var scene = CreateText(body.transform, "SceneText", new Vector2(300, -222), new Vector2(-90, -254), 18, TextAnchor.UpperLeft);
            scene.color = new Color(0.72f, 0.70f, 0.64f, 1f);
            var detail = CreateText(body.transform, "DetailText", new Vector2(300, -262), new Vector2(-50, -572), 24, TextAnchor.UpperLeft);
            var rewards = CreateText(body.transform, "RewardsText", new Vector2(300, -582), new Vector2(-50, -672), 20, TextAnchor.UpperLeft);
            rewards.color = new Color(0.72f, 0.70f, 0.64f, 1f);

            var closeGo = new GameObject("CloseButton", typeof(Image), typeof(Button));
            closeGo.transform.SetParent(body.transform, false);
            var crt = closeGo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(1f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(1f, 1f);
            crt.anchoredPosition = new Vector2(-20f, -20f);
            crt.sizeDelta = new Vector2(50f, 50f);
            closeGo.GetComponent<Image>().color = new Color(0.88f, 0.68f, 0.32f, 1f);
            var closeText = CreateText(closeGo.transform, "Text", Vector2.zero, Vector2.zero, 30, TextAnchor.MiddleCenter);
            closeText.rectTransform.anchorMin = Vector2.zero;
            closeText.rectTransform.anchorMax = Vector2.one;
            closeText.rectTransform.offsetMin = Vector2.zero;
            closeText.rectTransform.offsetMax = Vector2.zero;
            closeText.text = "X";
            closeText.color = Color.black;

            root.SetActive(false);
            return new FullRefs
            {
                root = root,
                distance = distance,
                title = title,
                progress = progress,
                scene = scene,
                detail = detail,
                rewards = rewards,
                closeBtn = closeGo.GetComponent<Button>()
            };
        }

        Text CreateText(Transform parent, string name, Vector2 topLeft, Vector2 bottomRight, int fontSize, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = _resolvedFont != null ? _resolvedFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(topLeft.x, bottomRight.y);
            rt.offsetMax = new Vector2(bottomRight.x, topLeft.y);
            return text;
        }

        void CreateDecor(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        Text CreateTextStretchTop(Transform parent, string name, float posY, float height, int fontSize, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = _resolvedFont != null ? _resolvedFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = false;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(10f, 0f);
            rt.offsetMax = new Vector2(-10f, 0f);
            rt.anchoredPosition = new Vector2(0f, posY);
            rt.sizeDelta = new Vector2(0f, height);
            return text;
        }

        void BindUiReferences(QuestSystemUI ui, BriefRefs brief, FullRefs full)
        {
            SetField(ui, "questUIFont", _resolvedFont);
            SetField(ui, "preferredFontName", preferredFontName);
            SetField(ui, "briefPanel", brief.root);
            SetField(ui, "briefDistanceText", brief.distance);
            SetField(ui, "briefTitleText", brief.title);
            SetField(ui, "briefProgressText", brief.progress);
            SetField(ui, "briefSceneText", brief.scene);
            SetField(ui, "briefIntroText", brief.intro);
            SetField(ui, "briefOpenButton", brief.openBtn);
            SetField(ui, "fullPanel", full.root);
            SetField(ui, "fullDistanceText", full.distance);
            SetField(ui, "fullTitleText", full.title);
            SetField(ui, "fullProgressText", full.progress);
            SetField(ui, "fullSceneText", full.scene);
            SetField(ui, "fullDetailText", full.detail);
            SetField(ui, "fullRewardsText", full.rewards);
            SetField(ui, "fullCloseButton", full.closeBtn);
        }

        Font ResolveQuestFont()
        {
            if (questUIFont != null)
                return questUIFont;

            var all = Resources.FindObjectsOfTypeAll<Font>();
            if (all == null || all.Length == 0)
                return null;

            for (int i = 0; i < all.Length; i++)
            {
                var f = all[i];
                if (f == null) continue;
                if (string.Equals(f.name, preferredFontName, System.StringComparison.OrdinalIgnoreCase))
                    return f;
            }

            for (int i = 0; i < all.Length; i++)
            {
                var f = all[i];
                if (f == null || string.IsNullOrEmpty(f.name)) continue;
                if (f.name.IndexOf("Alibaba", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return f;
            }

            return null;
        }

        void SetField(object target, string fieldName, object value)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var field = target.GetType().GetField(fieldName, flags);
            if (field != null) field.SetValue(target, value);
        }
    }
}
