using PhoneUI;
using UnityEngine;

namespace GameQuestSystem
{
    public partial class QuestSystemUI
    {
        enum QuestFullPanelReturnTarget
        {
            Gameplay,
            PhoneLauncher,
            PhoneSettings,
            LegacySettingsPanel,
        }

        QuestFullPanelReturnTarget _fullPanelReturnTarget = QuestFullPanelReturnTarget.Gameplay;
        GameObject _legacyClosePanelRoot;
        bool _legacyClosePanelWasActive;
        GameObject _legacyQualityPanelRoot;
        bool _legacyQualityPanelWasActive;

        /// <summary>手机设置页内打开任务栏：ESC 后回到设置，不回到游戏。</summary>
        public void OpenFullPanelFromSettings()
        {
            _fullPanelReturnTarget = QuestFullPanelReturnTarget.PhoneSettings;
            OpenFullPanelCore();
        }

        /// <summary>手机九宫格「任务」：ESC 后回到九宫格。</summary>
        public void OpenFullPanelFromPhoneLauncher()
        {
            _fullPanelReturnTarget = QuestFullPanelReturnTarget.PhoneLauncher;
            GameplayUiHotkeyGuard.CloseOtherExclusivePanels(GameplayUiHotkeyGuard.ExclusivePanelKind.Quest);
            OpenFullPanelCore();
        }

        /// <summary>ESC 关闭完整任务栏并按打开来源返回上一级 UI。</summary>
        public bool TryHandleEscapeClose()
        {
            if (!_isFullOpen)
                return false;

            CloseFullPanelWithReturnNavigation();
            return true;
        }

        void OpenFullPanelWithContextDetect()
        {
            ResolveReturnTargetForOpen();
            OpenFullPanelCore();
        }

        void ResolveReturnTargetForOpen()
        {
            PhoneSessionController session = PhoneSessionController.Instance;
            if (session != null && PhoneSessionController.IsPhoneOpen)
            {
                if (session.IsSettingsOverlayOpen)
                {
                    _fullPanelReturnTarget = QuestFullPanelReturnTarget.PhoneSettings;
                    return;
                }

                _fullPanelReturnTarget = QuestFullPanelReturnTarget.PhoneLauncher;
                session.EnterOverlay(PhoneAppId.Quest);
                return;
            }

            if (IsLegacySettingsPanelOpen())
            {
                CaptureLegacySettingsRestoreState();
                _fullPanelReturnTarget = QuestFullPanelReturnTarget.LegacySettingsPanel;
                return;
            }

            _fullPanelReturnTarget = QuestFullPanelReturnTarget.Gameplay;
        }

        void CloseFullPanelWithReturnNavigation()
        {
            QuestFullPanelReturnTarget target = _fullPanelReturnTarget;
            CloseFullPanelCore();
            _fullPanelReturnTarget = QuestFullPanelReturnTarget.Gameplay;

            switch (target)
            {
                case QuestFullPanelReturnTarget.PhoneSettings:
                    RestorePhoneSettingsAfterQuest();
                    break;
                case QuestFullPanelReturnTarget.PhoneLauncher:
                    RestorePhoneLauncherAfterQuest();
                    break;
                case QuestFullPanelReturnTarget.LegacySettingsPanel:
                    RestoreLegacySettingsPanels();
                    break;
            }
        }

        static void RestorePhoneSettingsAfterQuest()
        {
            PhoneSessionController session = PhoneSessionController.Instance;
            if (session == null || !PhoneSessionController.IsPhoneOpen)
                return;

            session.OpenSettingsPage();
            session.EnsurePhoneUiCursor();
        }

        static void RestorePhoneLauncherAfterQuest()
        {
            PhoneSessionController session = PhoneSessionController.Instance;
            if (session == null || !PhoneSessionController.IsPhoneOpen)
                return;

            session.ShowLauncher();
            session.EnsurePhoneUiCursor();
        }

        bool IsLegacySettingsPanelOpen()
        {
            if (IsTopLeftSettingsCurvePanelOpen() || IsAnyQualitySettingsPanelOpen())
                return true;

            GameObject closePanel = ResolveLegacyClosePanelRoot();
            return closePanel != null && closePanel.activeInHierarchy;
        }

        static GameObject ResolveLegacyClosePanelRoot()
        {
            GameObject closePanel = GameObject.Find("closepanel");
            if (closePanel != null)
                return closePanel;

            PhoneSessionController session = PhoneSessionController.Instance;
            if (session != null && session.settingsPage != null)
            {
                Transform legacy = session.settingsPage.transform.Find("closepanel");
                if (legacy != null)
                    return legacy.gameObject;
            }

            return null;
        }

        void CaptureLegacySettingsRestoreState()
        {
            _legacyClosePanelRoot = ResolveLegacyClosePanelRoot();
            _legacyClosePanelWasActive = _legacyClosePanelRoot != null && _legacyClosePanelRoot.activeSelf;

            _legacyQualityPanelRoot = null;
            _legacyQualityPanelWasActive = false;
            if (!_settingsBlockingCacheValid)
                RefreshSettingsUiBlockingCache();

            if (_cachedQualityControllers == null)
                return;

            for (int i = 0; i < _cachedQualityControllers.Length; i++)
            {
                QualityController q = _cachedQualityControllers[i];
                if (q == null || q.qualityPanel == null)
                    continue;

                _legacyQualityPanelRoot = q.qualityPanel;
                _legacyQualityPanelWasActive = q.qualityPanel.activeSelf;
                return;
            }
        }

        void RestoreLegacySettingsPanels()
        {
            if (_legacyClosePanelRoot != null && _legacyClosePanelWasActive)
                _legacyClosePanelRoot.SetActive(true);

            if (_legacyQualityPanelRoot != null && _legacyQualityPanelWasActive)
                _legacyQualityPanelRoot.SetActive(true);

            if (_cachedSettingsCurveRoot != null && _cachedSettingsCurveRoot.activeSelf == false)
            {
                // curvePanel 由 ToggleCurvePanel 控制；仅在曾打开画质子面板时恢复 qualityPanel。
            }

            _legacyClosePanelRoot = null;
            _legacyQualityPanelRoot = null;
        }
    }
}
