using PhoneUI;
using UnityEngine;

public class PhoneUIManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject hudPanel;      // 左上角小手机按钮所在 HUD
    public GameObject phonePanel;    // 全屏手机界面

    [Header("Player")]
    public PlayerMovement playerMovement;  // 把角色的 PlayerMovement 拖进来

    public bool IsPhoneOpen
    {
        get
        {
            var session = ResolveSession();
            if (session != null)
                return PhoneSessionController.IsPhoneOpen;
            return phonePanel != null && phonePanel.activeSelf;
        }
    }

    void Start()
    {
        InteractPromptSharedUiLocator.EnsureGameplayHudCanvasVisible();
        if (hudPanel != null) hudPanel.SetActive(true);
        if (phonePanel != null) phonePanel.SetActive(false);
    }

    PhoneSessionController ResolveSession()
    {
        var session = GetComponent<PhoneSessionController>();
        if (session == null)
            session = PhoneSessionController.Instance;
        if (session == null)
            session = PhoneSessionController.FindBestInScene();
        return session;
    }

    // 小手机按钮调用
    public void OpenPhone()
    {
        var session = ResolveSession();
        if (session != null)
        {
            session.OpenPhone();
            return;
        }

        if (hudPanel != null) hudPanel.SetActive(false);
        if (phonePanel != null) phonePanel.SetActive(true);

        if (playerMovement != null)
            playerMovement.enabled = false;
    }

    // 手机界面里的 Exit 按钮调用
    public void ClosePhone()
    {
        var session = ResolveSession();
        if (session != null)
        {
            session.ClosePhone();
            return;
        }

        if (hudPanel != null) hudPanel.SetActive(true);
        if (phonePanel != null) phonePanel.SetActive(false);

        if (playerMovement != null)
            playerMovement.enabled = true;

        GameplayMenuCameraHelper.RestoreAfterMenuClosed();
    }

    /// <summary>旧画质面板按钮兼容：打开手机并进入 Settings 页。</summary>
    public void OpenPhoneToSettings()
    {
        var session = ResolveSession();
        if (session != null)
        {
            session.OpenPhoneToSettings();
            return;
        }

        if (TryOpenLegacySettingsPanel())
            return;

        OpenPhone();
    }

    static bool TryOpenLegacySettingsPanel()
    {
        var qc = Object.FindObjectOfType<QualityController>(true);
        if (qc != null && qc.qualityPanel != null)
        {
            qc.ToggleQualityPanel();
            return true;
        }

        var curve = Object.FindObjectOfType<CurvePanelController>(true);
        if (curve != null && curve.curvePanel != null)
        {
            curve.ToggleCurvePanel();
            return true;
        }

        return false;
    }
}
