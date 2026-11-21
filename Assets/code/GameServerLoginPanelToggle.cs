using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 登录面板开关：挂在始终处于激活状态的物体上（如 GameServer_Network），并指定 Login 面板根物体。
/// 默认进入游戏时隐藏面板；按 P 打开并显示鼠标，再按 P 关闭并恢复锁定光标。
/// </summary>
public class GameServerLoginPanelToggle : MonoBehaviour
{
    [SerializeField] GameObject loginPanel;
    [SerializeField] KeyCode toggleKey = KeyCode.P;

    [Tooltip("关闭面板时锁定并隐藏鼠标（适合第一人称）；若你希望关闭后不抢光标，可取消勾选")]
    [SerializeField] bool lockCursorWhenClosed = true;

    [Header("场景特例")]
    [Tooltip("在这些场景中禁用 P 开关，不在 Start 里强制隐藏登录面板；交给 MenuLoginPortalFlow 控制。")]
    [SerializeField] string[] disableToggleScenes = { "Loadingscene" };

    bool _isOpen;
    bool _disableToggleInScene;

    void Start()
    {
        _disableToggleInScene = IsSceneInList(SceneManager.GetActiveScene().name, disableToggleScenes);

        if (!_disableToggleInScene) 
        {
            if (loginPanel != null)
                loginPanel.SetActive(false);
            _isOpen = false;
        }
        else
        {
            _isOpen = loginPanel != null && loginPanel.activeSelf;
            ApplyCursorForMenuOpen();
        }

        if (lockCursorWhenClosed && !_isOpen && !_disableToggleInScene)
            ApplyCursorForMenuClosed();
    }

    void Update()
    {
        if (_disableToggleInScene)
            return;

        if (loginPanel == null || !Input.GetKeyDown(toggleKey))
            return;

        // 已登录且面板打开时，P 用于切换账号/登出，不由本脚本关闭面板
        if (loginPanel.activeSelf)
        {
            var sync = GameServerSync.Instance;
            if (sync != null && sync.IsLoggedIn)
                return;
        }

        _isOpen = !_isOpen;
        loginPanel.SetActive(_isOpen);

        if (_isOpen)
            ApplyCursorForMenuOpen();
        else if (lockCursorWhenClosed)
            ApplyCursorForMenuClosed();
    }

    void LateUpdate()
    {
        // 第一人称等脚本可能在 Update 里锁鼠标，这里每帧再拉回，保证登录界面可操作
        if (_isOpen && loginPanel != null && loginPanel.activeSelf)
            ApplyCursorForMenuOpen();
    }

    void OnDestroy()
    {
        if (lockCursorWhenClosed && _isOpen)
            ApplyCursorForMenuClosed();
    }

    static void ApplyCursorForMenuOpen()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    static void ApplyCursorForMenuClosed()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    static bool IsSceneInList(string sceneName, string[] list)
    {
        if (string.IsNullOrEmpty(sceneName) || list == null) return false;
        for (int i = 0; i < list.Length; i++)
        {
            var n = list[i];
            if (!string.IsNullOrEmpty(n) &&
                string.Equals(n.Trim(), sceneName, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
