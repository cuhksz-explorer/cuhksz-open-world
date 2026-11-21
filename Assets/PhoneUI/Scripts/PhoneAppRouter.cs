using DormMall;
using GameQuestSystem;
using UnityEngine;

namespace PhoneUI
{
    /// <summary>
    /// 九宫格 App 与校园系统桥接：打开子界面时隐藏手机壳，按 ESC 回到九宫格（不退出手机会话）。
    /// </summary>
    public static class PhoneAppRouter
    {
        public static bool TryOpenApp(PhoneAppId appId, PhoneSessionController session)
        {
            if (session == null)
                return false;

            switch (appId)
            {
                case PhoneAppId.Map:
                    return OpenMap(session);
                case PhoneAppId.Quest:
                    return OpenQuest(session);
                case PhoneAppId.Shop:
                    return OpenShop(session);
                case PhoneAppId.Inventory:
                    return OpenInventory(session);
                case PhoneAppId.Multiplayer:
                    return OpenMultiplayer(session);
                case PhoneAppId.Account:
                    return OpenAccount(session);
                case PhoneAppId.Custom1:
                    return OpenKeyBindings(session);
                case PhoneAppId.Custom2:
                    return OpenLogout(session);
                default:
                    return false;
            }
        }

        static bool OpenMap(PhoneSessionController session)
        {
            var map = Object.FindObjectOfType<MapSystem>(true);
            if (map == null)
            {
                Debug.LogWarning("[PhoneUI] 未找到 MapSystem");
                return false;
            }

            GameplayUiHotkeyGuard.CloseOtherExclusivePanels(
                GameplayUiHotkeyGuard.ExclusivePanelKind.Map,
                preservePhoneSession: true);
            session.EnterOverlay(PhoneAppId.Map);
            map.ShowMap();
            return true;
        }

        static bool OpenQuest(PhoneSessionController session)
        {
            var questUi = Object.FindObjectOfType<QuestSystemUI>(true);
            if (questUi == null)
            {
                Debug.LogWarning("[PhoneUI] 未找到 QuestSystemUI");
                return false;
            }

            if (session.IsSettingsOverlayOpen)
            {
                questUi.OpenFullPanelFromSettings();
                return true;
            }

            GameplayUiHotkeyGuard.CloseOtherExclusivePanels(
                GameplayUiHotkeyGuard.ExclusivePanelKind.Quest,
                preservePhoneSession: true);
            session.EnterOverlay(PhoneAppId.Quest);
            questUi.OpenFullPanelFromPhoneLauncher();
            return true;
        }

        static bool OpenShop(PhoneSessionController session)
        {
            GameplayUiHotkeyGuard.CloseOtherExclusivePanels(
                GameplayUiHotkeyGuard.ExclusivePanelKind.Shop,
                preservePhoneSession: true);

            if (DormMallHotkeyTController.Instance != null)
            {
                if (DormMallUI.IsAnyMainShopPanelOpenInHierarchy())
                {
                    session.EnterOverlay(PhoneAppId.Shop);
                    return true;
                }

                DormMallHotkeyTController.Instance.TryOpenMallFromMerchant();
                if (!DormMallUI.IsAnyMainShopPanelOpenInHierarchy())
                    return false;

                session.EnterOverlay(PhoneAppId.Shop);
                return true;
            }

            var mall = Object.FindObjectOfType<DormMallUI>(true);
            if (mall == null)
            {
                Debug.LogWarning("[PhoneUI] 未找到 DormMallUI / DormMallHotkeyTController");
                return false;
            }

            session.EnterOverlay(PhoneAppId.Shop);
            mall.gameObject.SetActive(true);
            mall.RebuildUI();
            return true;
        }

        static bool OpenInventory(PhoneSessionController session)
        {
            if (InventoryUI.Instance == null)
            {
                Debug.LogWarning("[PhoneUI] 未找到 InventoryUI");
                return false;
            }

            session.EnterOverlay(PhoneAppId.Inventory);
            InventoryUI.Instance.OpenPanelExternal(preservePhoneSession: true);
            return true;
        }

        static bool OpenMultiplayer(PhoneSessionController session)
        {
            var room = MultiplayerRoomCodePanelController.Instance;
            if (room == null)
                room = Object.FindObjectOfType<MultiplayerRoomCodePanelController>(true);
            if (room == null)
            {
                Debug.LogWarning("[PhoneUI] 未找到 MultiplayerRoomCodePanelController");
                return false;
            }

            GameplayUiHotkeyGuard.CloseOtherExclusivePanels(
                GameplayUiHotkeyGuard.ExclusivePanelKind.Multiplayer,
                preservePhoneSession: true);
            session.EnterOverlay(PhoneAppId.Multiplayer);
            room.OpenPanel();
            return true;
        }

        static bool OpenAccount(PhoneSessionController session)
        {
            PhoneAccountPanelView.Show(session);
            return true;
        }

        static bool OpenKeyBindings(PhoneSessionController session)
        {
            session.OpenKeyBindingsPage();
            return PhoneKeyBindingPanelView.IsPageActive(session);
        }

        static bool OpenLogout(PhoneSessionController session)
        {
            return PerformLogout();
        }

        /// <summary>与九宫格「登出」一致：供设置页等外部 UI 调用。</summary>
        public static bool PerformLogout()
        {
            var logout = Object.FindObjectOfType<LogoutToLoadingSceneButton>(true);
            if (logout != null)
            {
                logout.OnLogoutClicked();
                return true;
            }

            if (GameServerSync.Instance != null)
                GameServerSync.Instance.Logout();
            Debug.LogWarning("[PhoneUI] 未找到 LogoutToLoadingSceneButton，已仅执行账号登出。");
            return GameServerSync.Instance != null;
        }
    }
}
