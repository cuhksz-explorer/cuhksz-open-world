using UnityEngine;
using UnityEngine.UI;

public class MinimapWithRedDot : MonoBehaviour
{
    [Tooltip("头顶摄像机（小地图画面源）")]
    public HoverCamera hoverCamera;

    [Tooltip("显示小地图的UI组件")]
    public RawImage minimapDisplay;

    [Tooltip("红点大小（像素）")]
    public int redDotSize = 8; // 可调整红点大小

    [Tooltip("红点颜色（默认红色）")]
    public Color redDotColor = Color.red; // 支持自定义颜色

    private Image _playerRedDot;
    private RenderTexture _minimapRT;

    void Start()
    {
        if (hoverCamera == null)
        {
            Debug.LogError("请赋值头顶摄像机！");
            return;
        }

        // 初始化小地图渲染纹理
        _minimapRT = new RenderTexture(256, 256, 16);
        hoverCamera.GetCamera().targetTexture = _minimapRT;
        minimapDisplay.texture = _minimapRT;

        // 在小地图中心创建红点（代表人物）
        CreateRedDot();
    }

    // 创建小地图中心的红点
    private void CreateRedDot()
    {
        GameObject dotObj = new GameObject("PlayerRedDot");
        dotObj.transform.SetParent(minimapDisplay.transform, false);

        // 添加图片组件
        _playerRedDot = dotObj.AddComponent<Image>();

        // 生成红色纹理（圆形红点，更美观）
        Texture2D dotTex = new Texture2D(redDotSize, redDotSize);
        Color[] pixels = new Color[redDotSize * redDotSize];

        // 绘制圆形红点（中心实心，边缘柔和）
        for (int y = 0; y < redDotSize; y++)
        {
            for (int x = 0; x < redDotSize; x++)
            {
                // 计算像素到中心的距离
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(redDotSize / 2f, redDotSize / 2f));
                // 距离小于半径则为红色，否则透明
                if (dist < redDotSize / 2f)
                {
                    // 边缘透明度渐变，更自然
                    float alpha = Mathf.Lerp(1f, 0.7f, dist / (redDotSize / 2f));
                    pixels[y * redDotSize + x] = new Color(redDotColor.r, redDotColor.g, redDotColor.b, alpha);
                }
                else
                {
                    pixels[y * redDotSize + x] = Color.clear;
                }
            }
        }

        dotTex.SetPixels(pixels);
        dotTex.Apply();

        // 设置红点精灵
        Sprite dotSprite = Sprite.Create(dotTex, new Rect(0, 0, redDotSize, redDotSize), new Vector2(0.5f, 0.5f));
        _playerRedDot.sprite = dotSprite;

        // 固定红点在小地图中心（人物位置）
        RectTransform rect = _playerRedDot.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f); // 锚定中心
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero; // 中心位置
        rect.sizeDelta = new Vector2(redDotSize, redDotSize); // 红点大小
    }

    void OnDestroy()
    {
        if (_minimapRT != null) Destroy(_minimapRT);
    }
}