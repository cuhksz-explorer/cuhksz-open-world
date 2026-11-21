using UnityEngine;

namespace GameQuestSystem
{
    /// <summary>
    /// 剧情 NPC 根节点上的可选外观配置（由编辑器菜单写入）。
    /// </summary>
    public class StoryNpcVisualProfile : MonoBehaviour
    {
        [Tooltip("相对根节点的模型缩放")]
        public float modelScale = 1f;

        [Tooltip("模型脚底 Y 偏移")]
        public float groundOffsetY;

        [Tooltip("用于对话立绘/截图的空节点（可选）")]
        public Transform portraitAnchor;

        [Tooltip("镜像/幻影类 NPC：半透明虚化")]
        public bool applyGhostEffect;

        [Range(0.05f, 1f)]
        public float ghostAlpha = 0.45f;

        public Color ghostTint = new Color(0.75f, 0.88f, 1f, 1f);

        public const string VisualChildName = "Visual";
    }
}
