using PhoneUI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 修复一键配置后 Qualitychanger 按钮 onClick 目标丢失；运行时重新绑定到设置入口。
/// </summary>
public static class GameplaySettingsButtonWiring
{
    static readonly System.Collections.Generic.HashSet<int> WiredButtonIds =
        new System.Collections.Generic.HashSet<int>();

    public static void WireScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;
        if (GameplayUiSceneExclusions.ShouldSkipScene(scene))
            return;

        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var buttons = roots[i].GetComponentsInChildren<Button>(true);
            for (int b = 0; b < buttons.Length; b++)
                TryWireButton(buttons[b]);
        }
    }

    public static void TryWireButton(Button button)
    {
        if (button == null)
            return;

        string name = button.name ?? "";
        if (!name.StartsWith("Qualitychanger", System.StringComparison.Ordinal))
            return;

        int id = button.GetInstanceID();
        if (WiredButtonIds.Contains(id))
            return;

        button.onClick.RemoveListener(OnSettingsHudClicked);
        button.onClick.AddListener(OnSettingsHudClicked);
        WiredButtonIds.Add(id);
    }

    static void OnSettingsHudClicked()
    {
        var session = PhoneSessionController.FindBestInScene();
        if (session != null)
        {
            session.OpenPhoneToSettings();
            return;
        }

        var mgr = Object.FindObjectOfType<PhoneUIManager>(true);
        if (mgr != null)
        {
            mgr.OpenPhoneToSettings();
            return;
        }

        var qc = Object.FindObjectOfType<QualityController>(true);
        if (qc != null)
            qc.ToggleQualityPanel();
    }
}
