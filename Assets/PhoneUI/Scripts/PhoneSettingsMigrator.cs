using UnityEngine;

namespace PhoneUI
{
    /// <summary>
    /// 旧版 closepanel 画质 UI 迁入 SettingsPage（仅 legacy 场景使用；新 PhoneCanvas 请手动画质页并关闭本组件）。
    /// </summary>
    public class PhoneSettingsMigrator : MonoBehaviour
    {
        public PhoneSessionController session;
        public bool migrateOnStart;

        void Start()
        {
            if (!migrateOnStart || session == null)
                return;
            Migrate();
        }

        public void Migrate()
        {
            if (session == null || session.phonePanel == null || session.settingsPage == null)
                return;

            var qc = session.phonePanel.GetComponent<QualityController>();
            if (qc != null && qc.qualityPanel != null)
            {
                var qp = qc.qualityPanel.transform;
                qp.SetParent(session.settingsPage.transform, false);
                StretchRect(qp as RectTransform);
            }

            DisableLegacyClosePanelButtons(session.phonePanel.transform);
            PhoneSettingsPanelView.Ensure(session);
        }

        static void StretchRect(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(24f, 24f);
            rt.offsetMax = new Vector2(-24f, -24f);
        }

        static void DisableLegacyClosePanelButtons(Transform closepanelRoot)
        {
            var buttons = closepanelRoot.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                var b = buttons[i];
                if (b == null) continue;
                if (b.name == "Btn_Close") continue;
                if (b.GetComponentInParent<PhoneAppSlot>(true) != null) continue;
                if (b.GetComponentInParent<PhoneSessionController>(true) != null && b.name.Contains("Close"))
                    continue;
                int persistentCount = b.onClick.GetPersistentEventCount();
                for (int p = 0; p < persistentCount; p++)
                {
                    string method = b.onClick.GetPersistentMethodName(p);
                    if (method == "OpenPhone" || method == "ClosePhone")
                    {
                        b.gameObject.SetActive(false);
                        break;
                    }
                }
            }
        }
    }
}
