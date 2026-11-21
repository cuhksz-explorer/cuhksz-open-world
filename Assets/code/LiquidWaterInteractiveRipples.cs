using UnityEngine;

/// <summary>
/// 场景组件：在 LiquidWater 根物体上应用交互波纹设置（undergarden 等场景中的 Mesh*_LiquidWater）。
/// 静态逻辑见 <see cref="LiquidWaterInteractiveRipplesUtility"/>。
/// </summary>
[DisallowMultipleComponent]
public class LiquidWaterInteractiveRipples : MonoBehaviour
{
    void Awake()
    {
        LiquidWaterInteractiveRipplesUtility.Apply(gameObject);
        LiquidWaterInteractiveRipplesUtility.EnsurePlayerEmitter();
    }
}
