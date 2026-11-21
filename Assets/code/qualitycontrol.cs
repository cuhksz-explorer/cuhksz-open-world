using UnityEngine;
using UnityEngine.UI;

public class QualityController : MonoBehaviour
{
    // 画质调整面板（拖拽赋值）
    public GameObject qualityPanel;
    // 5�?画质按钮（可选，用于高亮当前选中项）
    public Button[] qualityButtons;
    // 画质名称（与Unity Quality Settings�?的名称�?�应�?
    private string[] qualityNames = { "Low", "Medium", "High", "Very High", "Ultra" };

    void Start()
    {
        // 初始隐藏画质面板
        qualityPanel.SetActive(false);
        // 加载上次保存的画质设置（若无则默认第2档“中”）
        bool cinematic = PlayerPrefs.GetInt("SavedQualityIsCinematic_v1", 0) == 1;
        if (cinematic)
            SetCinematicQuality();
        else
        {
            int savedQuality = PlayerPrefs.GetInt("SavedQuality", 1);
            SetQuality(savedQuality);
        }
    }

    // 切换画质面板显示/隐藏（绑定到主按�?�?
    public void ToggleQualityPanel()
    {
        qualityPanel.SetActive(!qualityPanel.activeSelf);
        if (!qualityPanel.activeSelf)
        {
            GameplayMenuCameraHelper.RestoreAfterMenuClosed();
            GameplayCameraInputRestorer.RestoreNow();
        }
    }

    // 设置画质（绑定到5�?画质按钮，参数分�?�?0-4�?
    public void SetQuality(int qualityLevel)
    {
        // 限制参数范围：0-4对应5档画质
        qualityLevel = Mathf.Clamp(qualityLevel, 0, 4);
        // 应用画质设置（Unity内置方法，立即生效）
        QualitySettings.SetQualityLevel(qualityLevel, true);
        // 保存设置（下次启动生效）
        PlayerPrefs.SetInt("SavedQuality", qualityLevel);
        PlayerPrefs.SetInt("SavedQualityIsCinematic_v1", 0);
        PlayerPrefs.Save();

        // 高亮当前选中的按钮（可选功能）
        HighlightSelectedButton(qualityLevel);
    }

    public void SetCinematicQuality()
    {
        int cinematicIndex = qualityNames.Length - 1;
        QualitySettings.SetQualityLevel(cinematicIndex, true);
        PlayerPrefs.SetInt("SavedQuality", cinematicIndex);
        PlayerPrefs.SetInt("SavedQualityIsCinematic_v1", 1);
        PlayerPrefs.Save();
        HighlightSelectedButton(cinematicIndex);
    }

    // 高亮当前选中的画质按�?（可选）
    private void HighlightSelectedButton(int index)
    {
        if (qualityButtons == null || qualityButtons.Length != 5) return;

        for (int i = 0; i < qualityButtons.Length; i++)
        {
            // 选中按钮用红色，�?选中用白色（�?�?定义颜色�?
            ColorBlock colors = qualityButtons[i].colors;
            colors.normalColor = (i == index) ? Color.red : Color.white;
            qualityButtons[i].colors = colors;
        }
    }
}