using UnityEngine;
using UnityEngine.UI;

namespace PhoneUI
{
    /// <summary>
    /// 运行时搭建 PhoneShell / AppGrid / AppSlot（若场景中未手工搭好）。挂到 Phone 物体上可选使用。
    /// </summary>
    public class PhoneUiBootstrap : MonoBehaviour
    {
        [Header("Build")]
        public bool buildOnAwake = false;
        public PhoneSessionController session;
        public Sprite phoneFrameSprite;
        public Color gridBackgroundColor = new Color(0.1f, 0.1f, 0.13f, 0.95f);

        static readonly PhoneAppId[] DefaultApps =
        {
            PhoneAppId.Settings, PhoneAppId.Map, PhoneAppId.Quest,
            PhoneAppId.Shop, PhoneAppId.Inventory, PhoneAppId.Multiplayer,
            PhoneAppId.Account, PhoneAppId.Custom1, PhoneAppId.Custom2
        };

        static readonly string[] DefaultNamesZh =
        {
            "设置", "地图", "任务", "商城", "背包", "联机", "账户", "自定义按键", "登出"
        };

        static readonly string[] DefaultNamesEn =
        {
            "Settings", "Map", "Quests", "Shop", "Inventory", "Co-op", "Account", "Key bindings", "Log out"
        };

        void Awake()
        {
            if (!buildOnAwake) return;
            if (session == null)
                session = GetComponent<PhoneSessionController>();
            if (session == null) return;
            if (session.phoneShell != null && session.appGrid != null)
                return;
            BuildUnderPhonePanel(session);
        }

        public static void BuildUnderPhonePanel(PhoneSessionController session)
        {
            if (session == null || session.phonePanel == null)
                return;

            if (session.phoneShell != null && session.appGrid != null)
                return;

            var panelRt = session.phonePanel.GetComponent<RectTransform>();
            if (panelRt == null)
                return;

            GameObject shell = CreateUiObject("PhoneShell", panelRt);
            StretchFull(shell.GetComponent<RectTransform>());
            session.phoneShell = shell;

            var dim = CreateUiObject("DimBackground", shell.GetComponent<RectTransform>());
            StretchFull(dim.GetComponent<RectTransform>());
            var dimImg = dim.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.55f);
            dimImg.raycastTarget = true;

            var frame = CreateUiObject("PhoneFrame", shell.GetComponent<RectTransform>());
            var frameRt = frame.GetComponent<RectTransform>();
            frameRt.anchorMin = new Vector2(1f, 0.5f);
            frameRt.anchorMax = new Vector2(1f, 0.5f);
            frameRt.pivot = new Vector2(1f, 0.5f);
            frameRt.sizeDelta = new Vector2(520f, 960f);
            frameRt.anchoredPosition = new Vector2(-28f, 0f);
            var frameImg = frame.AddComponent<Image>();
            frameImg.color = Color.white;
            var bootstrap = session.GetComponent<PhoneUiBootstrap>();
            if (bootstrap != null && bootstrap.phoneFrameSprite != null)
                frameImg.sprite = bootstrap.phoneFrameSprite;

            var gridRoot = CreateUiObject("AppGrid", frame.GetComponent<RectTransform>());
            var gridRt = gridRoot.GetComponent<RectTransform>();
            gridRt.anchorMin = new Vector2(0.08f, 0.12f);
            gridRt.anchorMax = new Vector2(0.92f, 0.72f);
            gridRt.offsetMin = Vector2.zero;
            gridRt.offsetMax = Vector2.zero;

            var bg = CreateUiObject("AppGridBackground", gridRt);
            StretchFull(bg.GetComponent<RectTransform>());
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = session.GetComponent<PhoneUiBootstrap>() != null
                ? session.GetComponent<PhoneUiBootstrap>().gridBackgroundColor
                : new Color(0.1f, 0.1f, 0.13f, 0.95f);
            bgImg.raycastTarget = false;

            var layoutGo = CreateUiObject("SlotsLayout", gridRt);
            StretchFull(layoutGo.GetComponent<RectTransform>());
            var grid = layoutGo.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.cellSize = new Vector2(118f, 118f);
            grid.spacing = new Vector2(14f, 18f);
            grid.padding = new RectOffset(12, 12, 16, 16);
            grid.childAlignment = TextAnchor.MiddleCenter;

            var slots = new PhoneAppSlot[9];
            for (int i = 0; i < 9; i++)
            {
                slots[i] = CreateAppSlot(layoutGo.transform, DefaultApps[i], Loc.Choose(DefaultNamesZh[i], DefaultNamesEn[i]), session, i);
            }

            var appGrid = gridRoot.AddComponent<PhoneAppGrid>();
            appGrid.slots = slots;
            session.appGrid = appGrid;

            var closeBtnGo = CreateUiObject("Btn_Close", shell.GetComponent<RectTransform>());
            var closeRt = closeBtnGo.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(0.5f, 0.04f);
            closeRt.anchorMax = new Vector2(0.5f, 0.04f);
            closeRt.sizeDelta = new Vector2(160f, 48f);
            var closeImg = closeBtnGo.AddComponent<Image>();
            closeImg.color = new Color(0.25f, 0.28f, 0.35f, 0.9f);
            var closeBtn = closeBtnGo.AddComponent<Button>();
            closeBtn.onClick.AddListener(session.ClosePhone);
            var closeLabel = CreateUiObject("Text", closeRt);
            StretchFull(closeLabel.GetComponent<RectTransform>());
            var tmp = closeLabel.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = Loc.Choose("关闭", "Close");
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.fontSize = 22;
            tmp.color = Color.white;
            PhoneUiAssetLoader.ApplyTmpFont(tmp);

            var settingsPage = CreateUiObject("SettingsPage", panelRt);
            StretchFull(settingsPage.GetComponent<RectTransform>());
            settingsPage.SetActive(false);
            session.settingsPage = settingsPage;

            var backGo = CreateUiObject("Btn_SettingsBack", settingsPage.GetComponent<RectTransform>());
            var backRt = backGo.GetComponent<RectTransform>();
            backRt.anchorMin = new Vector2(0f, 1f);
            backRt.anchorMax = new Vector2(0f, 1f);
            backRt.pivot = new Vector2(0f, 1f);
            backRt.anchoredPosition = new Vector2(16f, -16f);
            backRt.sizeDelta = new Vector2(100f, 40f);
            var backImg = backGo.AddComponent<Image>();
            backImg.color = new Color(0.2f, 0.22f, 0.28f, 0.85f);
            var backBtn = backGo.AddComponent<Button>();
            backBtn.onClick.AddListener(session.CloseSettingsPage);
            var backLabel = CreateUiObject("Text", backRt);
            StretchFull(backLabel.GetComponent<RectTransform>());
            var backTmp = backLabel.AddComponent<TMPro.TextMeshProUGUI>();
            backTmp.text = Loc.Choose("返回", "Back");
            backTmp.fontSize = 18;
            backTmp.alignment = TMPro.TextAlignmentOptions.Center;
            backTmp.color = Color.white;
            PhoneUiAssetLoader.ApplyTmpFont(backTmp);

            shell.SetActive(false);
            HideLegacyClosePanelChildren(panelRt, shell.transform, settingsPage.transform);
        }

        static void HideLegacyClosePanelChildren(RectTransform panelRt, Transform shell, Transform settingsPage)
        {
            for (int i = 0; i < panelRt.childCount; i++)
            {
                var ch = panelRt.GetChild(i);
                if (ch == shell || ch == settingsPage)
                    continue;
                ch.gameObject.SetActive(false);
            }
        }

        static PhoneAppSlot CreateAppSlot(Transform parent, PhoneAppId appId, string displayName, PhoneSessionController session, int index)
        {
            var root = CreateUiObject("AppSlot_" + index, parent);
            var rootRt = root.GetComponent<RectTransform>();
            rootRt.sizeDelta = new Vector2(118f, 118f);

            var slot = root.AddComponent<PhoneAppSlot>();
            slot.appId = appId;
            slot.displayName = displayName;
            slot.futureBindingId = "phone.app." + appId;
            slot.session = session;

            var shadow = CreateUiObject("SlotShadow", rootRt);
            var shadowRt = shadow.GetComponent<RectTransform>();
            shadowRt.anchorMin = new Vector2(0.08f, 0f);
            shadowRt.anchorMax = new Vector2(0.92f, 0.12f);
            shadowRt.offsetMin = Vector2.zero;
            shadowRt.offsetMax = Vector2.zero;
            var shadowImg = shadow.AddComponent<Image>();
            shadowImg.color = new Color(0f, 0f, 0f, 0.2f);
            shadowImg.raycastTarget = false;

            var body = CreateUiObject("SlotBody", rootRt);
            var bodyRt = body.GetComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0.05f, 0.08f);
            bodyRt.anchorMax = new Vector2(0.95f, 0.95f);
            bodyRt.offsetMin = Vector2.zero;
            bodyRt.offsetMax = Vector2.zero;

            var side = CreateUiObject("SlotSide", bodyRt);
            var sideRt = side.GetComponent<RectTransform>();
            sideRt.anchorMin = new Vector2(0f, 0f);
            sideRt.anchorMax = new Vector2(1f, 0.22f);
            sideRt.offsetMin = Vector2.zero;
            sideRt.offsetMax = Vector2.zero;
            var sideImg = side.AddComponent<Image>();
            sideImg.color = new Color(0.66f, 0.69f, 0.75f, 1f);

            var face = CreateUiObject("SlotFace", bodyRt);
            var faceRt = face.GetComponent<RectTransform>();
            faceRt.anchorMin = new Vector2(0f, 0.18f);
            faceRt.anchorMax = new Vector2(1f, 1f);
            faceRt.offsetMin = Vector2.zero;
            faceRt.offsetMax = Vector2.zero;
            var faceImg = face.AddComponent<Image>();
            faceImg.color = new Color(0.88f, 0.89f, 0.93f, 1f);
            var faceShadow = face.AddComponent<Shadow>();
            faceShadow.effectDistance = new Vector2(1f, -2f);

            var labelGo = CreateUiObject("Label", faceRt);
            StretchFull(labelGo.GetComponent<RectTransform>());
            var label = labelGo.AddComponent<TMPro.TextMeshProUGUI>();
            label.text = displayName;
            label.fontSize = 16;
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.color = new Color(0.15f, 0.17f, 0.22f, 1f);
            PhoneUiAssetLoader.ApplyTmpFont(label);
            slot.labelText = label;

            var hintGo = CreateUiObject("HotkeyHint", faceRt);
            var hintRt = hintGo.GetComponent<RectTransform>();
            hintRt.anchorMin = new Vector2(1f, 0f);
            hintRt.anchorMax = new Vector2(1f, 0f);
            hintRt.pivot = new Vector2(1f, 0f);
            hintRt.anchoredPosition = new Vector2(-4f, 4f);
            hintRt.sizeDelta = new Vector2(36f, 18f);
            var hint = hintGo.AddComponent<TMPro.TextMeshProUGUI>();
            hint.text = "--";
            hint.fontSize = 11;
            hint.alignment = TMPro.TextAlignmentOptions.BottomRight;
            hint.color = new Color(0.4f, 0.42f, 0.5f, 1f);
            PhoneUiAssetLoader.ApplyTmpFont(hint);
            slot.hotkeyHintText = hint;

            var hit = CreateUiObject("SlotHitArea", rootRt);
            StretchFull(hit.GetComponent<RectTransform>());
            var hitImg = hit.AddComponent<Image>();
            hitImg.color = new Color(1f, 1f, 1f, 0.004f);
            var btn = hit.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            slot.hitAreaButton = btn;

            var visual = hit.AddComponent<PhoneAppSlotVisual>();
            visual.slotBody = bodyRt;
            visual.slotFace = faceImg;
            visual.slotSide = sideImg;
            visual.slotShadow = shadowImg;
            visual.faceShadow = faceShadow;
            slot.visual = visual;
            slot.WireClick();

            return slot;
        }

        static GameObject CreateUiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
