using UnityEngine;

[ExecuteInEditMode]
public class MinimapUIAnchor : MonoBehaviour
{
    [Tooltip("小地图宽度")]
    public int width = 180;

    [Tooltip("小地图高度")]
    public int height = 180;

    [Tooltip("距离左边缘的偏移（像素）")]
    public int leftOffset = 15;

    [Tooltip("距离上边缘的偏移（像素）")]
    public int topOffset = 15;

    void Update()
    {
        RectTransform rect = GetComponent<RectTransform>();
        if (rect == null) return;

        // 固定在左上角
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1); // 锚点在左上角
        rect.anchoredPosition = new Vector2(leftOffset, -topOffset); // 偏移（Y取负是因为UI从上往下是负方向）
        rect.sizeDelta = new Vector2(width, height);
    }
}