using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PhoneUI
{
    /// <summary>九宫格启动器：统一下方标题，底部三格为账户 / 自定义按键 / 登出。</summary>
    public static class PhoneUiLauncherSlots
    {
        public static void Apply(PhoneSessionController session)
        {
            if (session == null)
                return;

            var slots = session.appGrid != null && session.appGrid.slots != null && session.appGrid.slots.Length > 0
                ? session.appGrid.slots
                : session.GetComponentsInChildren<PhoneAppSlot>(true);

            if (slots == null)
                return;

            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null) continue;

                ApplySlotTheme(slot);
                slot.ApplyBottomCaption(GetDisplayName(slot));
                if (slot.appId == PhoneAppId.Custom2)
                    ApplyLogoutIcon(slot);
            }

            ApplyGridBackground(session);
        }

        static string GetDisplayName(PhoneAppSlot slot)
        {
            switch (slot.appId)
            {
                case PhoneAppId.Settings:
                    return Loc.Choose("设置", "Settings");
                case PhoneAppId.Map:
                    return Loc.Choose("地图", "Map");
                case PhoneAppId.Quest:
                    return Loc.Choose("任务", "Quests");
                case PhoneAppId.Shop:
                    return Loc.Choose("商城", "Shop");
                case PhoneAppId.Inventory:
                    return Loc.Choose("背包", "Inventory");
                case PhoneAppId.Multiplayer:
                    return Loc.Choose("联机", "Multiplayer");
                case PhoneAppId.Account:
                    return Loc.Choose("账户", "Account");
                case PhoneAppId.Custom1:
                    return Loc.Choose("自定义按键", "Key Bindings");
                case PhoneAppId.Custom2:
                    return Loc.Choose("登出", "Log Out");
                default:
                    return slot.displayName;
            }
        }

        static void ApplyLogoutIcon(PhoneAppSlot slot)
        {
            var sprite = PhoneUiSimpleIcons.GetLogoutIcon();
            if (sprite == null)
                return;

            Image target = slot.iconImage;
            if (target == null && slot.visual != null)
                target = slot.visual.slotFace;
            if (target == null)
                return;

            target.sprite = sprite;
            target.color = Color.white;
            target.preserveAspect = true;
        }

        static void ApplySlotTheme(PhoneAppSlot slot)
        {
            if (slot.visual == null)
                slot.visual = slot.GetComponent<PhoneAppSlotVisual>();
            if (slot.visual == null)
                return;

            slot.visual.faceColorNormal = PhoneUiTheme.BgWhite;
            slot.visual.faceColorHighlight = PhoneUiTheme.BgGoldTint;
            slot.visual.faceColorPressed = PhoneUiTheme.BgSoft;
            slot.visual.sideColorNormal = PhoneUiTheme.BtnGoldBg;
            slot.visual.sideColorHighlight = PhoneUiTheme.TextGold;
        }

        static void ApplyGridBackground(PhoneSessionController session)
        {
            if (session.phoneShell == null)
                return;

            var inset = session.phoneShell.transform.Find("PhoneChromeBackdrop/PhoneScreenInset") as RectTransform;
            if (inset == null)
                inset = session.phoneShell.transform.Find("PhoneScreenInset") as RectTransform;
            if (inset != null)
            {
                var img = inset.GetComponent<Image>();
                if (img != null)
                    img.color = PhoneUiTheme.BgWhite;
            }

            var gridBg = session.phoneShell.transform.Find("AppGrid/AppGridBackground");
            if (gridBg != null)
            {
                var img = gridBg.GetComponent<Image>();
                if (img != null)
                    img.color = PhoneUiTheme.BgSoft;
            }
        }
    }
}
