using UnityEngine;

/// <summary>
/// 坐下/站起动画片段集合，放在 Resources/ChairSit 下供运行时加载。
/// </summary>
[CreateAssetMenu(fileName = "ChairSitClipSet", menuName = "LGU/Chair Sit Clip Set")]
public class ChairSitClipSet : ScriptableObject
{
    public AnimationClip sitDownClip;
    public AnimationClip sitIdleClip;
    public AnimationClip standUpClip;

    public bool IsComplete =>
        sitDownClip != null && sitIdleClip != null && standUpClip != null;
}
