using System;
using System.Collections.Generic;
using GameControls;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PhoneUI
{
    /// <summary>1280×720 双行键位网格；点击键帽清空后监听新按键（含组合键）。</summary>
    public class PhoneKeyBindingUI
    {
        const float DesignWidth = 1280f;
        const float DesignHeight = 720f;
        const int ColumnsPerRow = 9;
        const float GridTop = 120f;
        const float GridBottom = 88f;
        const float SlotSpacing = 10f;

        static readonly GameControlAction[] Row1 =
        {
            GameControlAction.MoveForward,
            GameControlAction.MoveBack,
            GameControlAction.MoveLeft,
            GameControlAction.MoveRight,
            GameControlAction.Run,
            GameControlAction.Jump,
            GameControlAction.Crouch,
            GameControlAction.Interact,
            GameControlAction.TogglePhone
        };

        static readonly GameControlAction[] Row2 =
        {
            GameControlAction.OpenMap,
            GameControlAction.OpenInventory,
            GameControlAction.OpenQuest,
            GameControlAction.OpenMultiplayer,
            GameControlAction.ToggleCamera,
            GameControlAction.OpenFriends,
            GameControlAction.OpenFlagAuction,
            GameControlAction.OpenShop
        };

        sealed class SlotWidgets
        {
            public GameControlAction action;
            public Image background;
            public TMP_Text nameLabel;
            public TMP_Text keyLabel;
            public Button button;
        }

        RectTransform _panelRoot;
        TMP_Text _hintText;
        TMP_Text _backButtonText;
        TMP_Text _titleText;
        TMP_Text _resetButtonText;
        TMP_Text _confirmTitleText;
        TMP_Text _confirmMsgText;
        TMP_Text _confirmButtonText;
        TMP_Text _cancelButtonText;
        GameObject _confirmPanel;
        readonly List<SlotWidgets> _slots = new();
        GameControlAction? _waitingAction;
        float _conflictFlashUntil;
        Action _onBack;

        public bool IsBuilt => _panelRoot != null;

        public void Build(RectTransform parent, Action onBack)
        {
            if (_panelRoot != null)
                return;

            _onBack = onBack;
            _panelRoot = CreateObject("KeyBindingPanelRoot", parent).GetComponent<RectTransform>();
            CenterFixedSize(_panelRoot, DesignWidth, DesignHeight);

            var panelBg = _panelRoot.gameObject.AddComponent<Image>();
            panelBg.color = PhoneUiTheme.SettingsPanelOpaque;
            panelBg.raycastTarget = true;

            var frame = CreateObject("GoldFrame", _panelRoot);
            StretchWithInset(frame.GetComponent<RectTransform>(), 3f);
            var frameImg = frame.AddComponent<Image>();
            frameImg.color = PhoneUiTheme.SettingsSectionBorder;
            frameImg.raycastTarget = false;

            BuildBackButton(_panelRoot);
            BuildTitle(_panelRoot);
            BuildGrid(_panelRoot);
            BuildFooter(_panelRoot);
            BuildConfirmDialog(_panelRoot);
            RefreshAllSlots();
            _panelRoot.gameObject.SetActive(false);
        }

        void BuildBackButton(RectTransform panel)
        {
            var go = CreateObject("Btn_Back", panel);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24f, -16f);
            rt.sizeDelta = new Vector2(120f, 48f);
            var img = go.AddComponent<Image>();
            img.color = PhoneUiTheme.BtnPrimaryBg;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => _onBack?.Invoke());
            var txt = CreateObject("Text", go.transform).AddComponent<TextMeshProUGUI>();
            StretchFull(txt.rectTransform);
            _backButtonText = txt;
            _backButtonText.text = Loc.Choose("返回", "Back");
            _backButtonText.fontSize = 20;
            _backButtonText.fontStyle = FontStyles.Bold;
            _backButtonText.color = PhoneUiTheme.TextGold;
            _backButtonText.alignment = TextAlignmentOptions.Center;
            StyleTmp(_backButtonText);
        }

        void BuildTitle(RectTransform panel)
        {
            var title = CreateObject("Title", panel);
            var rt = title.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(140f, -56f);
            rt.offsetMax = new Vector2(-24f, -16f);
            var tmp = title.AddComponent<TextMeshProUGUI>();
            _titleText = tmp;
            _titleText.text = Loc.Choose("自定义按键", "Key Bindings");
            _titleText.fontSize = 30;
            _titleText.fontStyle = FontStyles.Bold;
            _titleText.color = PhoneUiTheme.TextBlack;
            _titleText.alignment = TextAlignmentOptions.Center;
            StyleTmp(_titleText);
        }

        void BuildGrid(RectTransform panel)
        {
            float usableW = DesignWidth - 48f;
            float slotW = (usableW - SlotSpacing * (ColumnsPerRow - 1)) / ColumnsPerRow;
            float rowH = (DesignHeight - GridTop - GridBottom - SlotSpacing) * 0.5f;

            BuildRow(panel, "Row1", Row1, GridTop, slotW, rowH, 0);
            BuildRow(panel, "Row2", Row2, GridTop + rowH + SlotSpacing, slotW, rowH, Row1.Length);
        }

        void BuildRow(RectTransform panel, string rowName, GameControlAction[] actions, float top, float slotW,
            float rowH, int slotOffset)
        {
            var row = CreateObject(rowName, panel);
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.anchorMin = rowRt.anchorMax = new Vector2(0.5f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, -top);
            rowRt.sizeDelta = new Vector2(DesignWidth - 48f, rowH);

            for (int i = 0; i < ColumnsPerRow; i++)
            {
                float x = i * (slotW + SlotSpacing);
                var slotGo = CreateObject($"Slot_{slotOffset + i}", row.transform);
                var slotRt = slotGo.GetComponent<RectTransform>();
                slotRt.anchorMin = slotRt.anchorMax = new Vector2(0f, 1f);
                slotRt.pivot = new Vector2(0f, 1f);
                slotRt.anchoredPosition = new Vector2(x, 0f);
                slotRt.sizeDelta = new Vector2(slotW, rowH);

                if (i >= actions.Length)
                {
                    slotGo.SetActive(false);
                    continue;
                }

                var widgets = BuildSlot(slotGo.transform, actions[i], slotRt);
                _slots.Add(widgets);
            }
        }

        SlotWidgets BuildSlot(Transform parent, GameControlAction action, RectTransform slotRt)
        {
            var bg = parent.gameObject.AddComponent<Image>();
            bg.color = PhoneUiTheme.BtnIdleBg;
            bg.raycastTarget = true;

            var btn = parent.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var cb = btn.colors;
            cb.highlightedColor = PhoneUiTheme.BgGoldTint;
            cb.pressedColor = PhoneUiTheme.BtnGoldBg;
            btn.colors = cb;

            var nameGo = CreateObject("Name", parent);
            var nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 1f);
            nameRt.anchorMax = new Vector2(1f, 1f);
            nameRt.pivot = new Vector2(0.5f, 1f);
            nameRt.offsetMin = new Vector2(6f, -36f);
            nameRt.offsetMax = new Vector2(-6f, -8f);
            var nameTmp = nameGo.AddComponent<TextMeshProUGUI>();
            nameTmp.text = GetActionLabel(action);
            nameTmp.fontSize = 16;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.color = PhoneUiTheme.TextBlack;
            nameTmp.alignment = TextAlignmentOptions.Center;
            StyleTmp(nameTmp);

            var keyGo = CreateObject("Key", parent);
            var keyRt = keyGo.GetComponent<RectTransform>();
            keyRt.anchorMin = new Vector2(0.08f, 0f);
            keyRt.anchorMax = new Vector2(0.92f, 0f);
            keyRt.pivot = new Vector2(0.5f, 0f);
            keyRt.offsetMin = new Vector2(0f, 12f);
            keyRt.offsetMax = new Vector2(0f, 52f);
            var keyBg = keyGo.AddComponent<Image>();
            keyBg.color = PhoneUiTheme.BtnPrimaryBg;
            keyBg.raycastTarget = false;

            var keyTmp = CreateObject("KeyText", keyGo.transform).AddComponent<TextMeshProUGUI>();
            StretchFull(keyTmp.rectTransform);
            keyTmp.fontSize = 18;
            keyTmp.fontStyle = FontStyles.Bold;
            keyTmp.color = PhoneUiTheme.TextGold;
            keyTmp.alignment = TextAlignmentOptions.Center;
            StyleTmp(keyTmp);

            var captured = action;
            btn.onClick.AddListener(() => BeginRebind(captured));

            return new SlotWidgets
            {
                action = action,
                background = bg,
                nameLabel = nameTmp,
                keyLabel = keyTmp,
                button = btn
            };
        }

        void BuildFooter(RectTransform panel)
        {
            var hintGo = CreateObject("Hint", panel);
            var hintRt = hintGo.GetComponent<RectTransform>();
            hintRt.anchorMin = new Vector2(0f, 0f);
            hintRt.anchorMax = new Vector2(1f, 0f);
            hintRt.pivot = new Vector2(0.5f, 0f);
            hintRt.offsetMin = new Vector2(24f, 52f);
            hintRt.offsetMax = new Vector2(-220f, 80f);
            _hintText = hintGo.AddComponent<TextMeshProUGUI>();
            _hintText.text = DefaultHintText();
            _hintText.fontSize = 15;
            _hintText.color = PhoneUiTheme.TextMuted;
            _hintText.alignment = TextAlignmentOptions.MidlineLeft;
            StyleTmp(_hintText);

            var resetGo = CreateObject("BtnReset", panel);
            var resetRt = resetGo.GetComponent<RectTransform>();
            resetRt.anchorMin = resetRt.anchorMax = new Vector2(1f, 0f);
            resetRt.pivot = new Vector2(1f, 0f);
            resetRt.anchoredPosition = new Vector2(-24f, 52f);
            resetRt.sizeDelta = new Vector2(160f, 44f);
            var resetImg = resetGo.AddComponent<Image>();
            resetImg.color = PhoneUiTheme.BtnPrimaryBg;
            var resetBtn = resetGo.AddComponent<Button>();
            resetBtn.targetGraphic = resetImg;
            resetBtn.onClick.AddListener(ShowConfirmReset);
            var resetTxt = CreateObject("Text", resetGo.transform).AddComponent<TextMeshProUGUI>();
            StretchFull(resetTxt.rectTransform);
            _resetButtonText = resetTxt;
            _resetButtonText.text = Loc.Choose("恢复默认", "Reset Defaults");
            _resetButtonText.fontSize = 18;
            _resetButtonText.color = PhoneUiTheme.TextGold;
            _resetButtonText.alignment = TextAlignmentOptions.Center;
            StyleTmp(_resetButtonText);
        }

        void BuildConfirmDialog(RectTransform panel)
        {
            _confirmPanel = CreateObject("ConfirmReset", panel);
            StretchFull(_confirmPanel.GetComponent<RectTransform>());
            var dim = _confirmPanel.AddComponent<Image>();
            dim.color = PhoneUiTheme.SettingsOverlayDim;
            dim.raycastTarget = true;

            var dlg = CreateObject("Dialog", _confirmPanel.transform);
            var dlgRt = dlg.GetComponent<RectTransform>();
            dlgRt.anchorMin = dlgRt.anchorMax = new Vector2(0.5f, 0.5f);
            dlgRt.sizeDelta = new Vector2(400f, 180f);
            dlg.AddComponent<Image>().color = PhoneUiTheme.SettingsSectionBg;

            var title = CreateObject("Title", dlg.transform).AddComponent<TextMeshProUGUI>();
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -20f);
            _confirmTitleText = title;
            _confirmTitleText.text = Loc.Choose("恢复默认键位？", "Reset to default key bindings?");
            _confirmTitleText.fontSize = 22;
            _confirmTitleText.fontStyle = FontStyles.Bold;
            _confirmTitleText.color = PhoneUiTheme.TextBlack;
            _confirmTitleText.alignment = TextAlignmentOptions.Center;
            StyleTmp(_confirmTitleText);

            var msg = CreateObject("Msg", dlg.transform).AddComponent<TextMeshProUGUI>();
            msg.rectTransform.anchorMin = msg.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            msg.rectTransform.sizeDelta = new Vector2(340f, 48f);
            _confirmMsgText = msg;
            _confirmMsgText.text = Loc.Choose("所有按键将恢复为默认设置。", "All keys will be restored to their default bindings.");
            _confirmMsgText.fontSize = 16;
            _confirmMsgText.color = PhoneUiTheme.TextMuted;
            _confirmMsgText.alignment = TextAlignmentOptions.Center;
            StyleTmp(_confirmMsgText);

            AddDialogButton(dlg.transform, Loc.Choose("确认", "Confirm"), new Vector2(-80f, 24f), true);
            AddDialogButton(dlg.transform, Loc.Choose("取消", "Cancel"), new Vector2(80f, 24f), false);
            _confirmPanel.SetActive(false);
        }

        void AddDialogButton(Transform parent, string label, Vector2 pos, bool confirm)
        {
            var go = CreateObject("Btn_" + label, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(120f, 40f);
            var img = go.AddComponent<Image>();
            img.color = confirm ? PhoneUiTheme.BtnGoldBg : PhoneUiTheme.BtnIdleBg;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            if (confirm)
                btn.onClick.AddListener(DoResetDefaults);
            else
                btn.onClick.AddListener(() => _confirmPanel?.SetActive(false));
            var txt = CreateObject("Text", go.transform).AddComponent<TextMeshProUGUI>();
            StretchFull(txt.rectTransform);
            txt.text = label;
            txt.fontSize = 17;
            txt.color = PhoneUiTheme.TextBlack;
            txt.alignment = TextAlignmentOptions.Center;
            if (confirm)
                _confirmButtonText = txt;
            else
                _cancelButtonText = txt;
            StyleTmp(txt);
        }

        void BeginRebind(GameControlAction action)
        {
            _waitingAction = action;
            GameInput.IsRebinding = true;
            if (_hintText != null)
                _hintText.text = Loc.F("正在绑定「{0}」… 按 Esc 取消", "Binding \"{0}\"… Press Esc to cancel", GetActionLabel(action));
            RefreshAllSlots();
        }

        public void Update()
        {
            if (_waitingAction == null)
                return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelRebind();
                return;
            }

            if (!GameInput.TryCaptureChordDown(out var chord))
                return;

            var profile = ControlBindingStore.Profile;
            if (!KeyBindingConflictChecker.TryAssign(profile, _waitingAction.Value, chord, out var conflict))
            {
                _conflictFlashUntil = Time.unscaledTime + 1.2f;
                if (_hintText != null)
                    _hintText.text = Loc.F("冲突：该按键已被「{0}」占用", "Conflict: that key is already used by \"{0}\"", GetActionLabel(conflict));
                RefreshAllSlots();
                return;
            }

            ControlBindingStore.Save(profile);
            _waitingAction = null;
            GameInput.IsRebinding = false;
            if (_hintText != null)
                _hintText.text = DefaultHintText();
            RefreshAllSlots();
        }

        void CancelRebind()
        {
            _waitingAction = null;
            GameInput.IsRebinding = false;
            if (_hintText != null)
                _hintText.text = DefaultHintText();
            RefreshAllSlots();
        }

        void RefreshAllSlots()
        {
            var profile = ControlBindingStore.Profile;
            bool conflictFlash = Time.unscaledTime < _conflictFlashUntil;
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.keyLabel == null)
                    continue;

                bool waiting = _waitingAction == slot.action;
                var chord = profile.GetChord(slot.action);
                slot.keyLabel.text = waiting ? "" : GameInput.FormatChord(chord);

                if (slot.background != null)
                {
                    if (waiting)
                        slot.background.color = PhoneUiTheme.BtnGoldBg;
                    else if (conflictFlash && _waitingAction == slot.action)
                        slot.background.color = new Color(0.85f, 0.35f, 0.30f, 1f);
                    else
                        slot.background.color = PhoneUiTheme.BtnIdleBg;
                }
            }
        }

        void ShowConfirmReset() => _confirmPanel?.SetActive(true);

        void DoResetDefaults()
        {
            ControlBindingStore.ResetToDefault();
            _confirmPanel?.SetActive(false);
            CancelRebind();
            RefreshAllSlots();
        }

        public void Show()
        {
            GameInput.SetKeyBindingsPanelOpen(true);
            _panelRoot?.gameObject.SetActive(true);
            CancelRebind();
            _confirmPanel?.SetActive(false);
            RefreshAllSlots();
        }

        public void Hide()
        {
            CancelRebind();
            _confirmPanel?.SetActive(false);
            GameInput.SetKeyBindingsPanelOpen(false);
            _panelRoot?.gameObject.SetActive(false);
        }

        public void RefreshLocalizedLabels()
        {
            if (_backButtonText != null)
                _backButtonText.text = Loc.Choose("返回", "Back");
            if (_titleText != null)
                _titleText.text = Loc.Choose("自定义按键", "Key Bindings");
            if (_resetButtonText != null)
                _resetButtonText.text = Loc.Choose("恢复默认", "Reset Defaults");
            if (_confirmTitleText != null)
                _confirmTitleText.text = Loc.Choose("恢复默认键位？", "Reset to default key bindings?");
            if (_confirmMsgText != null)
                _confirmMsgText.text = Loc.Choose("所有按键将恢复为默认设置。", "All keys will be restored to their default bindings.");
            if (_confirmButtonText != null)
                _confirmButtonText.text = Loc.Choose("确认", "Confirm");
            if (_cancelButtonText != null)
                _cancelButtonText.text = Loc.Choose("取消", "Cancel");

            if (_hintText != null && _waitingAction == null)
                _hintText.text = DefaultHintText();
            else if (_hintText != null && _waitingAction != null)
                _hintText.text = Loc.F("正在绑定「{0}」… 按 Esc 取消", "Binding \"{0}\"… Press Esc to cancel", GetActionLabel(_waitingAction.Value));

            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].nameLabel != null)
                    _slots[i].nameLabel.text = GetActionLabel(_slots[i].action);
            }
        }

        static string DefaultHintText() =>
            Loc.Choose(
                "点击键位 → 键位消失 → 按下新键（可 Ctrl / Shift / Alt 组合）",
                "Click a key slot → key clears → press a new key (Ctrl / Shift / Alt combos supported)");

        static string GetActionLabel(GameControlAction action) => action switch
        {
            GameControlAction.MoveForward => Loc.Choose("前进", "Forward"),
            GameControlAction.MoveBack => Loc.Choose("后退", "Back"),
            GameControlAction.MoveLeft => Loc.Choose("左移", "Move Left"),
            GameControlAction.MoveRight => Loc.Choose("右移", "Move Right"),
            GameControlAction.Run => Loc.Choose("奔跑", "Run"),
            GameControlAction.Jump => Loc.Choose("跳跃", "Jump"),
            GameControlAction.Crouch => Loc.Choose("蹲下", "Crouch"),
            GameControlAction.Interact => Loc.Choose("互动", "Interact"),
            GameControlAction.TogglePhone => Loc.Choose("手机", "Phone"),
            GameControlAction.OpenMap => Loc.Choose("地图", "Map"),
            GameControlAction.OpenInventory => Loc.Choose("背包", "Inventory"),
            GameControlAction.OpenQuest => Loc.Choose("任务", "Quest"),
            GameControlAction.OpenMultiplayer => Loc.Choose("联机", "Multiplayer"),
            GameControlAction.ToggleCamera => Loc.Choose("视角", "Camera"),
            GameControlAction.OpenFriends => Loc.Choose("好友", "Friends"),
            GameControlAction.OpenFlagAuction => Loc.Choose("竞拍", "Auction"),
            GameControlAction.OpenShop => Loc.Choose("商城", "Shop"),
            _ => action.ToString()
        };

        static GameObject CreateObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static void CenterFixedSize(RectTransform rt, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = Vector2.zero;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void StretchWithInset(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        static void StyleTmp(TMP_Text tmp)
        {
            if (tmp == null)
                return;
            PhoneUiAssetLoader.ApplyTmpFont(tmp);
            tmp.raycastTarget = false;
        }
    }
}
