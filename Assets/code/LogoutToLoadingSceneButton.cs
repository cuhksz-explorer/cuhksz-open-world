using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 挂在“登出”按钮上：点击后执行账号登出，并切回加载场景。
/// </summary>
public class LogoutToLoadingSceneButton : MonoBehaviour
{
    [Header("场景")]
    [Tooltip("登出后要跳转的场景。支持场景名（Loadingscene）或路径（Assets/scene/Loadingscene 或 .unity 完整路径）")]
    [SerializeField] string loadingSceneName = "Assets/scene/Loadingscene";

    [Header("引用（可留空自动查找）")]
    [SerializeField] GameServerSync serverSync;
    [SerializeField] Button targetButton;

    [Header("行为")]
    [Tooltip("切回加载场景前，是否把时间缩放恢复为 1（避免暂停状态带过去）")]
    [SerializeField] bool resetTimeScaleBeforeLoad = true;

    void Awake()
    {
        if (targetButton == null)
            targetButton = GetComponent<Button>();

        if (targetButton != null)
            targetButton.onClick.AddListener(OnLogoutClicked);
    }

    void OnDestroy()
    {
        if (targetButton != null)
            targetButton.onClick.RemoveListener(OnLogoutClicked);
    }

    public void OnLogoutClicked()
    {
        EnsureServerSync();
        if (serverSync != null)
            serverSync.Logout();

        if (resetTimeScaleBeforeLoad)
            Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (string.IsNullOrEmpty(loadingSceneName))
        {
            Debug.LogError("[LogoutToLoadingSceneButton] loadingSceneName 为空，无法切场景。");
            return;
        }

        var target = loadingSceneName.Trim();
        if (target.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase))
            target = target.Substring(0, target.Length - ".unity".Length);

        SceneManager.LoadScene(target);
    }

    void EnsureServerSync()
    {
        if (serverSync != null && serverSync == GameServerSync.Instance)
            return;

        serverSync = GameServerSync.Instance;
        if (serverSync != null) return;

        serverSync = Object.FindObjectOfType<GameServerSync>(true);
    }
}
