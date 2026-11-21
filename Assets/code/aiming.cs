using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

// 直接挂载到你现有的「跟随视角UI面板」的GameObject上
public class ExistingUIPanel_CrosshairClick : MonoBehaviour
{
    [Header("准星设置（固定屏幕中心）")]
    public Color crosshairColor = new Color(1, 0, 0, 0.8f); // 红+80%透明
    public int crosshairSize = 18; // 十字长度
    public int crosshairThickness = 2; // 十字粗细

    [Header("检测配置（按需微调）")]
    [Tooltip("现有UGUI按钮所在的Layer，默认选UI层即可")]
    public LayerMask uiLayer = 1 << LayerMask.NameToLayer("UI");
    [Tooltip("3D交互按钮所在的Layer（没有3D按钮可留空）")]
    public LayerMask interact3DLayer;
    public float raycastDistance = 100f; // 3D射线检测距离

    private Vector2 screenCenter; // 屏幕中心（准星固定位置）
    private Camera mainCamera; // 主相机（用于3D射线检测）

    private void Awake()
    {
        // 1. 缓存屏幕中心（固定不变，不受UI跟随视角影响）
        screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);

        // 2. 自动获取主相机（如果现有UI面板有相机引用，可替换为你的相机变量）
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("未找到主相机！请手动指定mainCamera变量");
        }
    }

    private void Update()
    {
        // 监听鼠标左键按下（0=左键，全局生效）
        if (Input.GetMouseButtonDown(0))
        {
            // 准星瞄准按钮则触发点击，不影响原有左键功能（如射击）
            TriggerAimedButton();
        }
    }

    // 核心逻辑：检测准星（屏幕中心）是否瞄准按钮，触发点击
    private void TriggerAimedButton()
    {
        // 优先检测UGUI按钮（避免3D物体遮挡UI，符合游戏交互逻辑）
        if (TryTriggerUGUIButton())
            return;

        // 可选：检测3D交互按钮（没有3D按钮可注释此句）
        if (mainCamera != null)
            TryTrigger3DButton();
    }

    // 检测并触发现有UGUI按钮（适配你面板上的所有UI按钮）
    private bool TryTriggerUGUIButton()
    {
        // 从屏幕中心（准星）发射UI射线
        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = screenCenter // 关键：射线起点=屏幕中心（准星位置）
        };

        List<RaycastResult> hitResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, hitResults);

        // 遍历命中的UI，找到可交互的按钮
        foreach (var result in hitResults)
        {
            // 只检测指定的UI层（避免误触其他层）
            if (((1 << result.gameObject.layer) & uiLayer) == 0)
                continue;

            // 触发UGUI Button（支持你现有面板上的所有Button）
            UnityEngine.UI.Button targetBtn = result.gameObject.GetComponent<UnityEngine.UI.Button>();
            if (targetBtn != null && targetBtn.interactable)
            {
                targetBtn.onClick.Invoke(); // 触发按钮原有绑定的事件
                Debug.Log($"左键触发UI按钮：{targetBtn.gameObject.name}");
                return true;
            }

            // 若现有UI用的是TextMeshPro按钮，取消下面注释：
            // TMPro.TMP_Button tmpBtn = result.gameObject.GetComponent<TMPro.TMP_Button>();
            // if (tmpBtn != null && tmpBtn.interactable) { tmpBtn.onClick.Invoke(); return true; }
        }

        return false;
    }

    // 检测并触发3D交互按钮（可选，没有则注释）
    private void TryTrigger3DButton()
    {
        // 从屏幕中心发射3D射线（与准星对齐）
        Ray ray = mainCamera.ScreenPointToRay(screenCenter);
        if (Physics.Raycast(ray, out RaycastHit hitInfo, raycastDistance, interact3DLayer))
        {
            // 调用3D按钮的交互逻辑（需让3D按钮实现此接口）
            IInteractable3D interactable = hitInfo.collider.GetComponent<IInteractable3D>();
            if (interactable != null)
            {
                interactable.OnInteract();
                Debug.Log($"左键触发3D按钮：{hitInfo.collider.gameObject.name}");
            }
        }
    }

    // 在屏幕中心绘制准星（不影响现有UI面板的任何元素）
    private void OnGUI()
    {
        // 设置准星样式
        GUI.color = crosshairColor;
        GUI.skin.box.normal.background = CreateSinglePixelTexture(crosshairColor);

        // 绘制水平十字线（左右延伸）
        GUI.Box(new Rect(
            screenCenter.x - crosshairSize / 2,
            screenCenter.y - crosshairThickness / 2,
            crosshairSize,
            crosshairThickness
        ), "");

        // 绘制垂直十字线（上下延伸）
        GUI.Box(new Rect(
            screenCenter.x - crosshairThickness / 2,
            screenCenter.y - crosshairSize / 2,
            crosshairThickness,
            crosshairSize
        ), "");
    }

    // 辅助方法：创建纯色像素纹理（用于准星绘制）
    private Texture2D CreateSinglePixelTexture(Color color)
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }
}

// -------------------------- 可选：3D按钮交互接口 --------------------------
// 若你有3D交互按钮，给3D按钮挂载实现此接口的脚本即可
public interface IInteractable3D
{
    void OnInteract();
}

// 3D按钮示例脚本（挂在3D按钮GameObject上，需添加Collider）
public class Example3DButton : MonoBehaviour, IInteractable3D
{
    public void OnInteract()
    {
        // 这里写3D按钮的实际功能（如打开门、拾取物品）
        Debug.Log("3D按钮被准星瞄准并触发！");
    }
}