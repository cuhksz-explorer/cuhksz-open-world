using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 行走/环视时统一的光标锁定：Esc 解锁显示鼠标，左键点击非 UI 区域恢复锁定与视角控制。
/// </summary>
public static class GameplayCursorLock
{
    public static bool IsLocked { get; private set; } = true;
    static bool _userPrefersUnlocked;
    static int _escapeToggleFrame = -1;

    public static void SetLocked(bool locked)
    {
        IsLocked = locked;
        _userPrefersUnlocked = !locked;
        ApplyCursorState(locked);
    }

    /// <summary>独占 UI 打开时解锁光标，不记入玩家 Esc 解锁偏好。</summary>
    public static void UnlockForModalUi()
    {
        IsLocked = false;
        ApplyCursorState(false);
    }

    /// <summary>进入行走视角或系统恢复时强制锁定（会清除玩家 Esc 解锁状态）。</summary>
    public static void LockForGameplayView()
    {
        _userPrefersUnlocked = false;
        IsLocked = true;
        ApplyCursorState(true);
    }

    /// <summary>Reconcile / Restore 等：仅在玩家未主动 Esc 解锁时才重新锁定。</summary>
    public static void TryApplyGameplayDefaultLock()
    {
        if (_userPrefersUnlocked)
            return;
        LockForGameplayView();
    }

    /// <summary>Esc 切换锁定；返回 true 表示本帧已处理。</summary>
    public static bool TryToggleEscape()
    {
        if (!Input.GetKeyDown(KeyCode.Escape))
            return false;

        if (_escapeToggleFrame == Time.frameCount)
            return true;

        if (DormMall.PlacementModeController.Instance != null &&
            DormMall.PlacementModeController.Instance.IsPlacementActive)
            return false;

        if (InteractionEscGate.ShouldSuppressFirstPersonEscToggleThisFrame())
            return false;

        if (GameplayUiHotkeyGuard.BlocksCameraMouseLook())
            return false;

        _escapeToggleFrame = Time.frameCount;
        SetLocked(!IsLocked);
        return true;
    }

    /// <summary>光标已解锁时，左键点击非 UI 区域恢复锁定（应在 LateUpdate 调用，便于 UI 先处理点击）。</summary>
    public static void TryRelockOnPrimaryClick()
    {
        if (IsLocked)
            return;

        if (!Input.GetMouseButtonDown(0))
            return;

        if (GameplayUiHotkeyGuard.BlocksCameraMouseLook())
            return;

        if (IsPointerOverGameUi())
            return;

        SetLocked(true);
    }

    /// <summary>鼠标是否点在允许保持解锁的 HUD 上：设置按钮、简易任务栏、小地图。</summary>
    public static bool IsPointerOverGameUi()
    {
        if (EventSystem.current == null)
            return false;

        if (IsPointerInTopLeftSettingsButtonArea(Input.mousePosition))
            return true;

        var eventData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition,
            button = PointerEventData.InputButton.Left,
        };

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        for (int i = 0; i < results.Count; i++)
        {
            var go = results[i].gameObject;
            if (go == null)
                continue;

            if (IsPointerOverBigMapUi(go))
                return true;

            if (IsAllowedGameplayHudUi(go))
                return true;
        }

        return false;
    }

    static bool IsPointerOverBigMapUi(GameObject go)
    {
        if (go == null || !MapSystem.BlocksGameplayInput)
            return false;

        var map = Object.FindObjectOfType<MapSystem>(true);
        if (map == null || map.mapWindow == null)
            return false;

        return go.transform.IsChildOf(map.mapWindow.transform);
    }

    static bool IsPointerInTopLeftSettingsButtonArea(Vector2 pointer)
    {
        float size = Mathf.Max(72f, Mathf.Min(Screen.width, Screen.height) * 0.09f);
        return pointer.x >= 0f
               && pointer.x <= size
               && pointer.y >= Screen.height - size
               && pointer.y <= Screen.height;
    }

    static bool IsAllowedGameplayHudUi(GameObject go)
    {
        var button = go.GetComponentInParent<Button>();
        if (button != null && IsSettingsButton(button))
            return true;

        if (go.GetComponentInParent<MinimapWithRedDot>() != null)
            return true;

        Transform t = go.transform;
        while (t != null)
        {
            string n = t.name;
            if (LooksLikeBriefQuestHudName(n) || LooksLikeMinimapName(n))
                return true;
            t = t.parent;
        }

        return false;
    }

    static bool IsSettingsButton(Button button)
    {
        if (button == null)
            return false;

        var click = button.onClick;
        int count = click != null ? click.GetPersistentEventCount() : 0;
        for (int i = 0; i < count; i++)
        {
            string method = click.GetPersistentMethodName(i);
            if (method == "OpenPhoneToSettings"
                || method == "OpenPhoneLauncherFromHud"
                || method == "ToggleQualityPanel")
                return true;
        }

        Transform t = button.transform;
        while (t != null)
        {
            string n = t.name;
            if (!string.IsNullOrEmpty(n))
            {
                string lower = n.ToLowerInvariant();
                if (lower.Contains("setting") || lower.Contains("quality") || n.Contains("设置") || n.Contains("画质"))
                    return true;
            }
            t = t.parent;
        }

        return false;
    }

    static bool LooksLikeBriefQuestHudName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        string lower = name.ToLowerInvariant();
        return lower.Contains("questbrief")
               || lower.Contains("briefquest")
               || lower.Contains("briefpanel")
               || name.Contains("简易任务")
               || name.Contains("简要任务")
               || name.Contains("任务栏");
    }

    static bool LooksLikeMinimapName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        string lower = name.ToLowerInvariant();
        return lower.Contains("minimap")
               || lower.Contains("mini map")
               || name.Contains("小地图");
    }

    static void ApplyCursorState(bool locked)
    {
        CursorLockMode targetLock = locked ? CursorLockMode.Locked : CursorLockMode.None;
        bool targetVisible = !locked;
        if (Cursor.lockState == targetLock && Cursor.visible == targetVisible)
            return;

        Cursor.lockState = targetLock;
        Cursor.visible = targetVisible;
    }
}
