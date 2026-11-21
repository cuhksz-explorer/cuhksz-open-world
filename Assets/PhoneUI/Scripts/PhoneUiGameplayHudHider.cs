using System.Collections.Generic;
using UnityEngine;

namespace PhoneUI
{
    /// <summary>隐藏常驻 HUD（小地图、任务条、血条等）；手机打开与沉浸模式共用，互动 UI 不受影响。</summary>
    public static class PhoneUiGameplayHudHider
    {
        static readonly List<GameObject> Hidden = new();
        static readonly HashSet<GameObject> HiddenSet = new();
        static bool _phoneSuppressed;
        static bool _cached;

        public static bool IsPersistentHudHidden =>
            _phoneSuppressed || ImmersiveModeSettings.IsEnabled;

        public static void SetSuppressed(bool suppress)
        {
            _phoneSuppressed = suppress;
            RefreshPersistentHudVisibility();
        }

        public static void RefreshPersistentHudVisibility()
        {
            if (IsPersistentHudHidden)
                HideGameplayHudOnce();
            else
                RestoreGameplayHud();
        }

        public static void EnsureSuppressed()
        {
            if (!_phoneSuppressed)
                return;
            HideGameplayHudOnce();
        }

        /// <summary>设置子界面打开时再次隐藏血条等（防止 HUD 在 overlay 期间被其它逻辑重新显示）。</summary>
        public static void EnsureHiddenForSettingsOverlay()
        {
            SetSuppressed(true);
            var session = PhoneSessionController.Instance;
            if (session != null)
            {
                session.EnsureHudPanelReference();
                if (session.hudPanel != null)
                    ForceHideObject(session.hudPanel);
            }

            ForceHideByExactName("openpanel");
            ForceHideByExactName("HP");
            ForceHideByExactName("HPamount");
            ForceHideByExactName("STamount");
            ApplyBarsVisible(false);
        }

        static void HideGameplayHudOnce()
        {
            _cached = true;

            var session = PhoneSessionController.Instance;
            if (session == null)
            {
                var sessions = Object.FindObjectsOfType<PhoneSessionController>(true);
                for (int i = 0; i < sessions.Length; i++)
                {
                    if (sessions[i] != null && PhoneSessionController.IsConfigured(sessions[i]))
                    {
                        session = sessions[i];
                        break;
                    }
                }
            }

            if (session != null)
            {
                session.EnsureHudPanelReference();
                if (session.hudPanel != null)
                    HideObject(session.hudPanel);
            }

            HideByExactNameOnce("openpanel");
            HideByExactNameOnce("minimapdisplay");
            HideByExactNameOnce("HP");
            HideByExactNameOnce("HPamount");
            HideByExactNameOnce("STamount");

            var minimaps = Object.FindObjectsOfType<MinimapWithRedDot>(true);
            for (int i = 0; i < minimaps.Length; i++)
            {
                var mm = minimaps[i];
                if (mm == null)
                    continue;
                if (mm.minimapDisplay != null)
                    HideObject(mm.minimapDisplay.gameObject);
            }

            ApplyBarsVisible(false);
        }

        static void HideByExactNameOnce(string objectName)
        {
            if (string.Equals(objectName, "openpanel", System.StringComparison.Ordinal))
                InteractPromptSharedUiLocator.DetachSharedPromptFromHudIfNeeded();
            ForceHideByExactName(objectName);
        }

        static void ForceHideByExactName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
                return;

            if (string.Equals(objectName, "openpanel", System.StringComparison.Ordinal))
                InteractPromptSharedUiLocator.DetachSharedPromptFromHudIfNeeded();

            var all = Object.FindObjectsOfType<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null || !t.gameObject.activeSelf)
                    continue;
                if (!string.Equals(t.name, objectName, System.StringComparison.Ordinal))
                    continue;
                if (IsUnderPhoneUi(t))
                    continue;
                ForceHideObject(t.gameObject);
            }
        }

        static void ForceHideObject(GameObject go) => HideObject(go);

        static void ApplyBarsVisible(bool visible)
        {
            var healthBars = Object.FindObjectsOfType<HealthBarUI>(true);
            for (int i = 0; i < healthBars.Length; i++)
            {
                if (healthBars[i] != null)
                    healthBars[i].SetBarVisible(visible);
            }

            var staminaBars = Object.FindObjectsOfType<StaminaBarUI>(true);
            for (int i = 0; i < staminaBars.Length; i++)
            {
                if (staminaBars[i] != null)
                    staminaBars[i].SetBarVisible(visible);
            }
        }

        static bool IsUnderPhoneUi(Transform t)
        {
            if (t == null)
                return false;
            return t.GetComponentInParent<PhoneSessionController>(true) != null
                   || t.GetComponentInParent<PhoneUIManager>(true) != null;
        }

        static void HideObject(GameObject go)
        {
            if (go == null || !go.activeSelf)
                return;
            if (HiddenSet.Contains(go))
                return;
            HiddenSet.Add(go);
            Hidden.Add(go);
            go.SetActive(false);
        }

        static void RestoreGameplayHud()
        {
            if (IsPersistentHudHidden)
                return;

            for (int i = 0; i < Hidden.Count; i++)
            {
                if (Hidden[i] != null)
                    Hidden[i].SetActive(true);
            }
            Hidden.Clear();
            HiddenSet.Clear();
            _cached = false;
            ApplyBarsVisible(true);
        }
    }
}
