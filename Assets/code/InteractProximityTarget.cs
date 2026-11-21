using System;
using UnityEngine;

/// <summary>
/// 单个近距离交互点数据（仅序列化数据，不要挂到物体上作组件）。
/// 在 <see cref="InteractWithMouseAndMovementLock"/> 的 Proximity Targets 列表里编辑名称与类型即可。
/// </summary>
[Serializable]
public class InteractProximityTarget
{
    [Tooltip("该交互点在世界中的位置（通常放碰撞体/标记 Transform）")]
    public Transform trigger;

    [Tooltip("提示第一行：交互点显示名称")]
    public string displayName = "交互点";

    [Tooltip("提示第二行：交互类型说明（如「对话」），可与按键前缀组合显示")]
    public string interactionType = "交互";

    [Tooltip("对话/头像相机/NPC 情绪；为空则使用 trigger")]
    public Transform dialogueNpcRoot;
}
