using UnityEngine;

namespace GameQuestSystem
{
    /// <summary>
    /// 剧情触发点运行时隐藏预制体模型，仅保留世界坐标供引导图标与 F 交互。
    /// </summary>
    public static class StoryQuestNpcVisualUtility
    {
        public const string StoryTriggerDisplayName = "剧情触发点";

        public static bool IsStoryTriggerDisplayName(string displayName)
        {
            return string.Equals(displayName, StoryTriggerDisplayName, System.StringComparison.Ordinal);
        }

        public static void ApplyTriggerOnlyAppearance(GameObject root, bool hideVisual)
        {
            if (root == null)
                return;

            if (!hideVisual)
            {
                SetPrefabVisualVisible(root, true);
                return;
            }

            if (!Application.isPlaying)
                return;

            SetPrefabVisualVisible(root, false);
        }

        public static void SetPrefabVisualVisible(GameObject root, bool visible)
        {
            if (root == null)
                return;

            Transform visual = root.transform.Find(StoryNpcVisualProfile.VisualChildName);
            if (visual != null)
            {
                visual.gameObject.SetActive(visible);
                return;
            }

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || IsPortraitAnchorHierarchy(renderer.transform, root.transform))
                    continue;

                renderer.enabled = visible;
            }
        }

        static bool IsPortraitAnchorHierarchy(Transform node, Transform root)
        {
            Transform anchor = root.Find("PortraitAnchor");
            if (anchor == null)
                return false;

            return node == anchor || node.IsChildOf(anchor);
        }
    }
}
