using System;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class NpcDialogueOption
{
    [Tooltip("选项按钮文本")]
    public string label = "继续";

    [Tooltip("跳转到的节点索引；-1 表示结束对话")]
    public int nextNodeIndex = -1;

    [Tooltip("点击该选项后触发，可绑定任务推进/开门/给道具等事件")]
    public UnityEvent onSelected;
}

[Serializable]
public class NpcDialogueNode
{
    [TextArea(2, 8)]
    [Tooltip("底部对话内容区显示的文本")]
    public string content;

    [Tooltip("进入此节点时触发，可用于播音效、改状态等")]
    public UnityEvent onNodeEntered;

    [Tooltip("该节点可选项列表")]
    public NpcDialogueOption[] options = Array.Empty<NpcDialogueOption>();
}

/// <summary>
/// 挂在 NPC 上：配置触发距离与对话树数据。
/// </summary>
public class NpcDialogueInteractor : MonoBehaviour
{
    [Header("基础")]
    public string npcDisplayName = "NPC";
    public Transform triggerPoint;
    public float interactDistance = 6f;
    public KeyCode interactKey = KeyCode.F;
    public string interactionTypeName = "对话";

    [Header("对话内容")]
    public NpcDialogueNode[] nodes = Array.Empty<NpcDialogueNode>();
    public int startNodeIndex = 0;
    public bool appendExitOption = true;
    public string exitOptionText = "离开";

    [Header("事件")]
    [Tooltip("开始对话时触发")]
    public UnityEvent onDialogueStarted;
    [Tooltip("对话真正关闭时触发（任意原因）")]
    public UnityEvent onDialogueClosed;
    [Tooltip("点击“离开”选项关闭时触发")]
    public UnityEvent onExitOptionChosen;
    [Tooltip("对话自然走完（非离开按钮）时触发")]
    public UnityEvent onDialogueCompleted;

    public Vector3 TriggerPosition => triggerPoint != null ? triggerPoint.position : transform.position;

    void OnEnable()
    {
        NpcDialogueUIManager.Register(this);
    }

    void OnDisable()
    {
        NpcDialogueUIManager.Unregister(this);
    }

    public bool CanStart()
    {
        return nodes != null && nodes.Length > 0 && startNodeIndex >= 0 && startNodeIndex < nodes.Length;
    }

    public NpcDialogueNode GetNode(int index)
    {
        if (nodes == null || index < 0 || index >= nodes.Length)
            return null;
        return nodes[index];
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(TriggerPosition, interactDistance);
    }
}

