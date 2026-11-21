using UnityEngine;

/// <summary>
/// 挂在 xxx_LiquidWater 根节点；运行时由 LiquidWaterRippleApplier 按档位应用性能设置。
/// </summary>
public class LiquidWaterPerformanceProfile : MonoBehaviour
{
    public LiquidWaterPerformanceTier Tier = LiquidWaterPerformanceTier.Balanced;
}
