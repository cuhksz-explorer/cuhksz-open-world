using TMPro;
using UnityEngine;

namespace DormMall
{
    /// <summary>
    /// 挂在引导点附近的 Trigger 碰撞体上：玩家进入时显示一句提示（可选绑定屏幕 TMP）。
    /// 坐标可与 <see cref="GameQuestSystem.LingCollegeBDormQuestPreset.DormTutorialWorldPosition"/> 对齐。
    /// </summary>
    public class DormTutorialProximityHint : MonoBehaviour
    {
        [TextArea(1, 3)]
        public string hintText = "按 T 打开寝室商城，购买物品后可按 B 打开背包。";

        [Tooltip("可选：屏幕上的 TMP（如共用提示条下的临时子物体），留空则仅依赖任务栏文案")]
        public TextMeshProUGUI optionalScreenLabel;

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerMovement>() == null)
                return;
            if (optionalScreenLabel != null)
            {
                optionalScreenLabel.gameObject.SetActive(true);
                optionalScreenLabel.text = Loc.Choose(
                    hintText,
                    "Press T to open the dorm shop. After buying, press B for inventory.");
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<PlayerMovement>() == null)
                return;
            if (optionalScreenLabel != null)
                optionalScreenLabel.gameObject.SetActive(false);
        }
    }
}
