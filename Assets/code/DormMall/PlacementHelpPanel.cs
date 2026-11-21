using TMPro;
using UnityEngine;

namespace DormMall
{
    /// <summary>
    /// 放置模式顶栏说明：由 <see cref="PlacementModeController"/> 在进入/退出放置模式时显示或隐藏，
    /// 文案根据控制器上的按键配置自动生成。
    /// </summary>
    public class PlacementHelpPanel : MonoBehaviour
    {
        [Tooltip("要显示/隐藏的条形容器（通常为顶栏根物体）")]
        [SerializeField] GameObject panelRoot;
        [Tooltip("说明正文（TMP）")]
        [SerializeField] TextMeshProUGUI bodyText;

        PlacementModeController _lastController;

        void Awake()
        {
            if (panelRoot == null)
                panelRoot = gameObject;
            HidePanel();
        }

        void OnEnable() => Loc.OnLanguageChanged += OnLanguageChanged;
        void OnDisable() => Loc.OnLanguageChanged -= OnLanguageChanged;

        void OnLanguageChanged()
        {
            if (_lastController != null && bodyText != null)
                bodyText.text = BuildInstructionText(_lastController);
        }

        /// <summary>根据当前按键配置刷新文案并显示。</summary>
        public void ShowFor(PlacementModeController controller)
        {
            _lastController = controller;
            if (panelRoot != null)
                panelRoot.SetActive(true);

            if (bodyText != null && controller != null)
            {
                bodyText.text = BuildInstructionText(controller);
                bodyText.alignment = TextAlignmentOptions.Center;
            }
        }

        public void HidePanel()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        public static string BuildInstructionText(PlacementModeController c)
        {
            if (c == null) return string.Empty;

            return Loc.Choose(
                "<b>放置模式说明</b>\n" +
                "<b>放置模式</b>\n" +
                "· 移动视角：照常移动鼠标（未开启 R/E/T/Y 微调时）。\n" +
                "· 对准表面：屏幕中心射线对准墙面/地面等，预览会贴合表面。\n" +
                $"· 确认放置：<b>{KeyLabel(c.confirmKey)}</b>　" +
                $"取消：<b>{KeyLabel(c.cancelKey)}</b>\n" +
                $"· <b>{KeyLabel(c.rotationTuneYawKey)}</b>：左右旋转（再按关闭）　" +
                $"<b>{KeyLabel(c.rotationTunePitchKey)}</b>：上下倾斜　" +
                $"<b>{KeyLabel(c.rotationTuneRollKey)}</b>：前后滚转（再按关闭）\n" +
                $"· <b>{KeyLabel(c.scaleTuneToggleKey)}</b>：开启缩放，滚轮或鼠标上下滑动控制物体大小（再按关闭）\n" +
                "· 使用 R/E/T/Y 微调期间，相机不转视角，仅调整物体。",
                "<b>Placement Mode</b>\n" +
                "<b>Placement</b>\n" +
                "· Move view: mouse as usual (when R/E/T/Y tuning is off).\n" +
                "· Aim surface: center-screen ray hits walls/floors; preview snaps to surface.\n" +
                $"· Place: <b>{KeyLabel(c.confirmKey)}</b>　" +
                $"Cancel: <b>{KeyLabel(c.cancelKey)}</b>\n" +
                $"· <b>{KeyLabel(c.rotationTuneYawKey)}</b>: yaw (toggle)　" +
                $"<b>{KeyLabel(c.rotationTunePitchKey)}</b>: pitch　" +
                $"<b>{KeyLabel(c.rotationTuneRollKey)}</b>: roll (toggle)\n" +
                $"· <b>{KeyLabel(c.scaleTuneToggleKey)}</b>: scale mode — scroll or mouse Y to resize (toggle)\n" +
                "· While tuning with R/E/T/Y, camera does not rotate — only the object moves.");
        }

        static string KeyLabel(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.Mouse0: return Loc.Choose("鼠标左键", "LMB");
                case KeyCode.Mouse1: return Loc.Choose("鼠标右键", "RMB");
                case KeyCode.Mouse2: return Loc.Choose("鼠标中键", "MMB");
                case KeyCode.Escape: return "Esc";
                case KeyCode.Return:
                case KeyCode.KeypadEnter: return "Enter";
                case KeyCode.Space: return Loc.Choose("空格", "Space");
                default: return k.ToString();
            }
        }
    }
}
