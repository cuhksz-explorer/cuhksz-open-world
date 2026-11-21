using UnityEngine;

/// <summary>
/// 场景加载：平静外观 + 可踩踏波纹（在 WaterRipples 启用前配置参数）。
/// </summary>
[DefaultExecutionOrder(-100)]
public class LiquidWaterRippleApplier : MonoBehaviour
{
    void Awake()
    {
        var profile = GetComponent<LiquidWaterPerformanceProfile>();
        var tier = profile != null
            ? profile.Tier
            : LiquidWaterPerformanceTier.Balanced;
        LiquidWaterPerformanceSettings.Apply(gameObject, tier);
        if (tier != LiquidWaterPerformanceTier.HighPerformance)
            LiquidWaterInteractiveRipplesUtility.EnsurePlayerEmitter();
    }
}
