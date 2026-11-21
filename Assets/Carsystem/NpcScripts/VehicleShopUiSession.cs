using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 选车界面打开时：锁定移动、显示鼠标、隐藏血条/小地图与交互提示 UI。
/// </summary>
public static class VehicleShopUiSession
{
    const float StatsPanelHeight = 260f;

    static readonly List<GameObject> HiddenObjects = new List<GameObject>();
    static readonly HashSet<GameObject> HiddenSet = new HashSet<GameObject>();

    static PlayerMovement _playerMovement;
    static FirstPersonCamera _fpsCamera;
    static bool _movementEnabledBefore;
    static bool _cursorLockedBefore;
    static bool _active;

    public static bool IsActive => _active;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void BootstrapEscapeRunner()
    {
        VehicleShopUiSessionRunner.EnsureInstance();
    }

    public static void Enter()
    {
        if (_active)
            return;
        _active = true;

        _playerMovement = Object.FindObjectOfType<PlayerMovement>(true);
        if (_playerMovement != null)
        {
            _movementEnabledBefore = _playerMovement.enabled;
            _playerMovement.enabled = false;
        }

        _fpsCamera = Object.FindObjectOfType<FirstPersonCamera>(true);
        if (_fpsCamera != null)
        {
            _cursorLockedBefore = _fpsCamera.IsCursorLocked;
            _fpsCamera.SetCursorLocked(false);
            _fpsCamera.suppressMouseLook = true;
        }

        GameplayCursorLock.UnlockForModalUi();

        HideGameplayHud();
        HideInteractionUi();
    }

    public static void Exit()
    {
        if (!_active)
            return;
        _active = false;

        if (_playerMovement != null)
            _playerMovement.enabled = _movementEnabledBefore;

        if (_fpsCamera != null)
        {
            _fpsCamera.suppressMouseLook = false;
            _fpsCamera.SetCursorLocked(_cursorLockedBefore);
            if (_cursorLockedBefore)
                GameplayCursorLock.LockForGameplayView();
            else
                GameplayCursorLock.SetLocked(false);
        }
        else
        {
            GameplayCursorLock.TryApplyGameplayDefaultLock();
        }

        _playerMovement = null;
        _fpsCamera = null;

        RestoreHiddenObjects();
    }

    static void HideInteractionUi()
    {
        if (InteractPromptSharedUiLocator.TryGetCanonical(out var sharedPanel, out var nameTmp, out var typeTmp))
        {
            InteractPromptSharedUiLocator.SetSharedPanelVisible(nameTmp, typeTmp, false);
            HideObject(sharedPanel.gameObject);
        }
        else
        {
            HideByExactName(InteractPromptSharedUiLocator.PanelObjectName);
        }

        var dealers = Object.FindObjectsOfType<VehicleDealerNPC>(true);
        for (int i = 0; i < dealers.Length; i++)
        {
            if (dealers[i] != null && dealers[i].interactPromptCanvas != null)
                HideObject(dealers[i].interactPromptCanvas);
        }

        var mode2Controllers = Object.FindObjectsOfType<InteractWithMouseAndMovementLock>(true);
        for (int i = 0; i < mode2Controllers.Length; i++)
        {
            var controller = mode2Controllers[i];
            if (controller == null || controller.interactPromptPanel == null)
                continue;
            if (InteractPromptSharedUiLocator.IsSharedPromptPanel(controller.interactPromptPanel))
                continue;
            HideObject(controller.interactPromptPanel.gameObject);
        }

        HideByExactName("Npcinteract");
    }

    static void HideGameplayHud()
    {
        HideByExactName("openpanel");
        HideByExactName("HP");
        HideByExactName("HPamount");
        HideByExactName("minimapdisplay");

        var minimaps = Object.FindObjectsOfType<MinimapWithRedDot>(true);
        for (int i = 0; i < minimaps.Length; i++)
        {
            var mm = minimaps[i];
            if (mm != null && mm.minimapDisplay != null)
                HideObject(mm.minimapDisplay.gameObject);
        }
    }

    static void HideByExactName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return;

        var all = Object.FindObjectsOfType<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null || !t.gameObject.activeSelf)
                continue;
            if (!string.Equals(t.name, objectName, System.StringComparison.Ordinal))
                continue;
            if (IsUnderVehicleShop(t))
                continue;
            HideObject(t.gameObject);
        }
    }

    static bool IsUnderVehicleShop(Transform t)
    {
        return t != null && t.GetComponentInParent<VehicleShopUI>(true) != null;
    }

    static void HideObject(GameObject go)
    {
        if (go == null || !go.activeSelf || HiddenSet.Contains(go))
            return;
        HiddenSet.Add(go);
        HiddenObjects.Add(go);
        go.SetActive(false);
    }

    static void RestoreHiddenObjects()
    {
        for (int i = 0; i < HiddenObjects.Count; i++)
        {
            if (HiddenObjects[i] != null)
                HiddenObjects[i].SetActive(true);
        }
        HiddenObjects.Clear();
        HiddenSet.Clear();
    }

    public static float GetStatsPanelHeight() => StatsPanelHeight;

    /// <summary>Esc 兜底：会话已锁移动但选车 Canvas 未正常显示时仍可退出。</summary>
    public static void TryCloseFromEscape()
    {
        if (!_active)
            return;

        var shops = Object.FindObjectsOfType<VehicleShopUI>(true);
        for (int i = 0; i < shops.Length; i++)
        {
            var shop = shops[i];
            if (shop != null && shop.IsShopOpen)
            {
                shop.CloseShop();
                return;
            }
        }

        Exit();
    }
}

/// <summary>全局监听 Esc，避免 VehicleShopUI 不可见时会话卡死。</summary>
[DefaultExecutionOrder(10001)]
sealed class VehicleShopUiSessionRunner : MonoBehaviour
{
    static VehicleShopUiSessionRunner _instance;

    public static void EnsureInstance()
    {
        if (_instance != null)
            return;

        var go = new GameObject(nameof(VehicleShopUiSessionRunner));
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<VehicleShopUiSessionRunner>();
    }

    void Update()
    {
        if (!VehicleShopUiSession.IsActive)
            return;
        if (Input.GetKeyDown(KeyCode.Escape))
            VehicleShopUiSession.TryCloseFromEscape();
    }
}
