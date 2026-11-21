using UnityEngine;
using UnityEngine.UI;

namespace PhoneUI
{
    /// <summary>运行时补全预制体上丢失的 Phone 边框与九宫格图标（贴图 GUID/导入方式不一致时）。</summary>
    public static class PhoneUiVisualRefresh
    {
        public static void ApplyToSession(PhoneSessionController session)
        {
            if (session == null)
                return;

            if (session.phoneShell == null || session.phoneShell.GetComponent<PhoneUiChromeView>() == null)
                ApplyPhoneFrame(session);
            HideOversizedSlotFaceIcons(session);
            PhoneUiLauncherSlots.Apply(session);
            var slots = session.GetComponentsInChildren<PhoneAppSlot>(true);
            for (int i = 0; i < slots.Length; i++)
                slots[i].ApplySlotIcon();
        }

        static void ApplyPhoneFrame(PhoneSessionController session)
        {
            if (session.phoneShell == null)
                return;

            var frameSprite = PhoneUiAssetLoader.GetPhoneFrameSprite();
            if (frameSprite == null)
                return;

            var images = session.phoneShell.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                var img = images[i];
                if (img == null)
                    continue;

                var n = img.gameObject.name;
                if (n != "PhoneFrame" && n != "PhoneShell" && n != "Frame")
                    continue;

                img.sprite = frameSprite;
                img.color = Color.white;
                img.preserveAspect = true;
                img.type = Image.Type.Simple;
            }
        }

        /// <summary>避免单个 App 图标被拉满整个屏幕（旧预制体 SlotFace 尺寸异常时）。</summary>
        static void HideOversizedSlotFaceIcons(PhoneSessionController session)
        {
            var faces = session.phoneShell.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < faces.Length; i++)
            {
                var img = faces[i];
                if (img == null || img.gameObject.name != "SlotFace")
                    continue;

                var rt = img.rectTransform;
                if (rt == null)
                    continue;

                if (rt.rect.width > 200f || rt.rect.height > 200f)
                {
                    var slot = img.GetComponentInParent<PhoneAppSlot>();
                    if (slot != null)
                        slot.ApplySlotIcon();
                }
            }
        }
    }
}
