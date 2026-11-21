using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 为子层级所有 Button 自动播放通用点击音（通过 GameAudioManager）。
/// 挂到 Canvas 或 UI 根节点即可。
/// </summary>
public class UiClickSoundAuto : MonoBehaviour
{
    [SerializeField] bool includeInactive = true;

    void OnEnable()
    {
        HookButtons();
    }

    public void HookButtons()
    {
        var buttons = GetComponentsInChildren<Button>(includeInactive);
        foreach (var btn in buttons)
        {
            if (btn == null)
                continue;
            btn.onClick.RemoveListener(PlayClick);
            btn.onClick.AddListener(PlayClick);
        }
    }

    void PlayClick()
    {
        if (GameAudioManager.Instance != null)
            GameAudioManager.Instance.PlayClick();
    }
}
