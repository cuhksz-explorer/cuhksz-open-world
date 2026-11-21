using UnityEngine;

public class CloudScroller : MonoBehaviour
{
    public Material cloudMaterial; // 云材质
    public float scrollSpeedX = 0.05f; // 水平滚动速度
    public float scrollSpeedY = 0.02f; // 垂直滚动速度

    void Update()
    {
        // 计算偏移值（基于时间）
        float offsetX = Time.time * scrollSpeedX;
        float offsetY = Time.time * scrollSpeedY;

        // 应用偏移到材质
        cloudMaterial.SetTextureOffset("_MainTex", new Vector2(offsetX, offsetY));
    }
}