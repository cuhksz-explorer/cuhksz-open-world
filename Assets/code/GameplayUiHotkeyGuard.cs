using GameControls;
using PhoneUI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DormMall;
using GameQuestSystem;

/// <summary>
/// UI 与游戏输入互斥：输入框焦点时屏蔽联机热键；联机房间/好友面板打开时屏蔽世界操作（移动、视角、任务热键等）。
/// Loading 场景内屏蔽 DontDestroyOnLoad 侧 UI 热键，登录面板仍可正常输入与点击。
/// </summary>
public static class GameplayUiHotkeyGuard
{
    static readonly string[] DontDestroyHotkeyBlockScenes = { "Loadingscene" };

    static int _exclusiveUiCacheFrame = -1;
    static bool _exclusiveUiCacheValue;
    static int _inputFieldFocusCacheFrame = -1;
    static bool _inputFieldFocusCacheValue;

    /// <summary>在 Loading/登录流程场景中，禁止 DontDestroyOnLoad 侧 UI 热键（K/C/O/B/T 等）。</summary>
    public static bool ShouldBlockDontDestroyOnLoadHotkeys()
    {
        return IsSceneInList(SceneManager.GetActiveScene().name, DontDestroyHotkeyBlockScenes);
    }

    /// <summary>进入 Loading 场景时关闭 DontDestroyOnLoad 上可能仍打开的面板，避免挡住登录 UI。</summary>
    public static void DismissDontDestroyOnLoadPanelsForLoginFlowScene()
    {
        var room = Object.FindObjectOfType<MultiplayerRoomCodePanelController>(true);
        if (room != null && room.IsPanelOpen)
            room.ClosePanel();

        var chat = Object.FindObjectOfType<MultiplayerFriendChatSidebarController>(true);
        if (chat != null && chat.IsOpen)
            chat.ForceCloseSidebar();

        var auction = Object.FindObjectOfType<FlagAuctionPanelController>(true);
        if (auction != null && auction.IsPanelOpen)
            auction.TogglePanel();

        if (InventoryUI.Instance != null)
            InventoryUI.Instance.Close();

        var malls = Object.FindObjectsOfType<DormMallUI>(true);
        for (int i = 0; i < malls.Length; i++)
        {
            var mall = malls[i];
            if (mall != null && mall.gameObject.activeSelf)
                mall.gameObject.SetActive(false);
        }

        var questUi = Object.FindObjectOfType<QuestSystemUI>(true);
        if (questUi != null)
            questUi.DismissForLoginFlow();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// 任意独占 UI（车辆/设置/联机/手机/背包/地图等）打开时暂停第一/第三人称鼠标环视。
    /// 坐下椅子等非 UI 状态不拦截（椅子有独立镜头）。
    /// </summary>
    public static bool BlocksCameraMouseLook()
    {
        if (ChairSitState.BlocksGameplay)
            return false;

        if (IsStoryDialogueOpen())
            return true;

        if (VehicleShopUiSession.IsActive)
            return true;

        if (PlacementModeController.Instance != null && PlacementModeController.Instance.IsPlacementActive)
            return true;

        if (PhoneUI.PhonePresentationController.IsBlockingGameplayCamera)
            return true;

        if (IsAnyQuestFullPanelOpen())
            return true;

        return ShouldBlockGameplayHotkeys();
    }

    static bool IsAnyQuestFullPanelOpen()
    {
        var all = Object.FindObjectsOfType<QuestSystemUI>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].IsFullPanelOpen)
                return true;
        }

        return false;
    }

    /// <summary>联机房间号面板（K）或好友侧栏（C）打开时，按键应只作用于面板内输入，不驱动游戏。</summary>
    public static bool BlocksGameplayBecauseMultiplayerUi()
    {
        return MultiplayerRoomCodePanelController.BlocksGameplayInput
               || MultiplayerFriendChatSidebarController.BlocksGameplayInput
               || DeathReviveUI.BlocksGameplayInput
               || FlagAuctionPanelController.BlocksGameplayInput
               || ElevatorFloorInputUI.BlocksGameplayInput
               || GameCreditsScrollOverlay.BlocksGameplayInput
               || LguHubPanelController.BlocksGameplayInput
               || MapSystem.BlocksGameplayInput
               || IsAnyExclusiveUiOpen();
    }

    /// <summary>
    /// 独占 UI（背包/地图/手机/车行等）打开时，世界交互提示 InteractPrompt_Shared 应隐藏。
    /// 单一查询入口，供 InteractPromptSharedLabelFrame 与交互器共用。
    /// </summary>
    public static bool ShouldHideWorldInteractPrompt()
    {
        if (UIInteractionState.BlocksWorldPrompt)
            return true;
        if (VehicleShopUiSession.IsActive)
            return true;
        if (PhoneSessionController.BlocksGameplayInput)
            return true;
        if (PlacementModeController.Instance != null && PlacementModeController.Instance.IsPlacementActive)
            return true;
        return IsAnyExclusiveUiOpen();
    }

    public static bool ShouldBlockGameplayHotkeys()
    {
        if (ChairSitState.BlocksGameplay)
            return true;
        if (GameInput.BlocksGameplayHotkeys())
            return true;
        if (PhoneSessionController.BlocksGameplayInput)
            return true;
        if (ShouldBlockDontDestroyOnLoadHotkeys())
            return true;
        if (BlocksGameplayBecauseMultiplayerUi())
            return true;
        if (IsSelectedInputFieldFocused())
            return true;
        return IsAnyInputFieldFocused();
    }

    static bool IsAnyExclusiveUiOpen()
    {
        if (_exclusiveUiCacheFrame == Time.frameCount)
            return _exclusiveUiCacheValue;

        bool open = false;
        if (DormMall.DormMallUI.IsAnyMainShopPanelOpenInHierarchy())
            open = true;
        else if (DormMall.InventoryUI.IsAnyInventoryPanelOpen())
            open = true;
        else
        {
            var mode2 = Object.FindObjectsOfType<InteractWithMouseAndMovementLock>(true);
            for (int i = 0; i < mode2.Length; i++)
            {
                if (mode2[i] != null && mode2[i].IsInteractionCanvasOpen)
                {
                    open = true;
                    break;
                }
            }
        }

        if (!open)
        {
            var dialogue = Object.FindObjectsOfType<CanvasInteractionUIManager>(true);
            for (int i = 0; i < dialogue.Length; i++)
            {
                if (dialogue[i] != null && dialogue[i].IsDialogueOpen)
                {
                    open = true;
                    break;
                }
            }
        }

        if (!open && IsStoryDialogueOpen())
            open = true;

        if (!open)
        {
            var npcDialogue = Object.FindObjectsOfType<NpcDialogueUIManager>(true);
            for (int i = 0; i < npcDialogue.Length; i++)
            {
                if (npcDialogue[i] != null && npcDialogue[i].IsDialogueOpen)
                {
                    open = true;
                    break;
                }
            }
        }

        if (!open)
        {
            var proximity = Object.FindObjectsOfType<Multiplayer.ProximityChatPanelController>(true);
            for (int i = 0; i < proximity.Length; i++)
            {
                if (proximity[i] != null && proximity[i].IsStandalonePanelOpen)
                {
                    open = true;
                    break;
                }
            }
        }

        if (!open && MapSystem.BlocksGameplayInput)
            open = true;

        _exclusiveUiCacheFrame = Time.frameCount;
        _exclusiveUiCacheValue = open;
        return open;
    }

    static bool IsSelectedInputFieldFocused()
    {
        if (EventSystem.current == null)
            return false;

        GameObject sel = EventSystem.current.currentSelectedGameObject;
        if (sel == null)
            return false;

        var tmp = sel.GetComponent<TMP_InputField>();
        if (tmp != null && tmp.isFocused)
            return true;

        var legacy = sel.GetComponent<InputField>();
        return legacy != null && legacy.isFocused;
    }

    public static bool IsStoryDialogueOpen()
    {
        var storyUi = Object.FindObjectsOfType<StoryDialogueUI>(true);
        for (int i = 0; i < storyUi.Length; i++)
        {
            if (storyUi[i] != null && storyUi[i].IsOpen)
                return true;
        }

        return false;
    }

    public static bool IsAnyInputFieldFocused()
    {
        if (_inputFieldFocusCacheFrame == Time.frameCount)
            return _inputFieldFocusCacheValue;

        var allTmp = Object.FindObjectsOfType<TMP_InputField>(true);
        for (int i = 0; i < allTmp.Length; i++)
        {
            var f = allTmp[i];
            if (f != null && f.isFocused)
                return CacheInputFieldFocus(true);
        }

        var allLegacy = Object.FindObjectsOfType<InputField>(true);
        for (int i = 0; i < allLegacy.Length; i++)
        {
            var f = allLegacy[i];
            if (f != null && f.isFocused)
                return CacheInputFieldFocus(true);
        }

        return CacheInputFieldFocus(false);
    }

    static bool CacheInputFieldFocus(bool focused)
    {
        _inputFieldFocusCacheFrame = Time.frameCount;
        _inputFieldFocusCacheValue = focused;
        return focused;
    }

    static bool IsSceneInList(string sceneName, string[] list)
    {
        if (string.IsNullOrEmpty(sceneName) || list == null)
            return false;
        for (int i = 0; i < list.Length; i++)
        {
            var n = list[i];
            if (!string.IsNullOrEmpty(n) &&
                string.Equals(n.Trim(), sceneName, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public enum ExclusivePanelKind
    {
        None,
        Map,
        Inventory,
        Quest,
        Multiplayer,
        Shop,
        Phone
    }

    /// <summary>打开独占面板时关闭其它快捷键面板（背包/地图/任务/联机等）。</summary>
    public static void CloseOtherExclusivePanels(ExclusivePanelKind keepOpen, bool preservePhoneSession = false)
    {
        if (!preservePhoneSession && keepOpen != ExclusivePanelKind.Phone)
        {
            var phone = PhoneSessionController.Instance
                        ?? Object.FindObjectOfType<PhoneSessionController>(true);
            if (phone != null && PhoneSessionController.IsPhoneOpen)
                phone.ClosePhone();
        }

        if (keepOpen != ExclusivePanelKind.Map)
        {
            var map = Object.FindObjectOfType<MapSystem>(true);
            if (map != null && map.IsMapWindowOpen)
            {
                map.HideMap();
                var pm = Object.FindObjectOfType<PlayerMovement>();
                if (pm != null)
                {
                    pm.isMovementPaused = false;
                    Time.timeScale = 1f;
                }
            }
        }

        if (keepOpen != ExclusivePanelKind.Inventory && InventoryUI.Instance != null)
            InventoryUI.Instance.Close();

        if (keepOpen != ExclusivePanelKind.Quest)
        {
            var questUi = Object.FindObjectOfType<QuestSystemUI>(true);
            if (questUi != null && questUi.IsFullPanelOpen)
                questUi.CloseFullPanelFromPhone();
        }

        if (keepOpen != ExclusivePanelKind.Multiplayer)
        {
            var room = MultiplayerRoomCodePanelController.Instance
                       ?? Object.FindObjectOfType<MultiplayerRoomCodePanelController>(true);
            if (room != null && room.IsPanelOpen)
                room.ClosePanel();
        }

        if (keepOpen != ExclusivePanelKind.Shop)
        {
            var malls = Object.FindObjectsOfType<DormMallUI>(true);
            for (int i = 0; i < malls.Length; i++)
            {
                var mall = malls[i];
                if (mall != null && mall.gameObject.activeSelf)
                    mall.gameObject.SetActive(false);
            }
        }

        _exclusiveUiCacheFrame = -1;
    }
}
