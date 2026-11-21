using TMPro;
using UnityEngine;

/// <summary>
/// 旧版「多实例共用 TMP」用组件；当前交互脚本已改回自行驱动面板显隐，本组件不应再改写 CanvasGroup，否则会与 interaction mode2 冲突。
/// 若物体上仍挂着本脚本，将在 Awake 自动禁用，仅作占位避免丢失引用报错。
/// </summary>
[DefaultExecutionOrder(1000)]
public class InteractPromptSharedResolver : MonoBehaviour
{
    [Tooltip("已废弃，不再使用")]
    public TextMeshProUGUI interactNameText;

    [Tooltip("已废弃，不再使用")]
    public TextMeshProUGUI interactTypeText;

    void Awake()
    {
        enabled = false;
    }
}

/// <summary>保留类型名，避免旧代码或程序集引用报错；不再参与逻辑。</summary>
public static class InteractSharedPromptFrame
{
    public static InteractWithMouseAndMovementLock LastInputOwner { get; private set; }

    public static bool IsBound => false;

    public static bool IsInputOwnerThisFrame(InteractWithMouseAndMovementLock self) => true;
}
