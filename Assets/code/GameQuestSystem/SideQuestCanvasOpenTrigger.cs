using UnityEngine;

namespace GameQuestSystem
{
    /// <summary>
    /// 挂在 Canvas 或面板根物体上：首次启用时上报 CanvasOpen，用于接取支线。
    /// </summary>
    public class SideQuestCanvasOpenTrigger : MonoBehaviour
    {
        [Tooltip("留空则使用本物体名")]
        public string canvasId;

        bool _reported;

        void OnEnable()
        {
            if (_reported)
                return;
            _reported = true;
            string id = string.IsNullOrWhiteSpace(canvasId) ? gameObject.name : canvasId.Trim();
            QuestSideQuestAcceptListener.ReportCanvasOpen(id);
        }
    }
}
