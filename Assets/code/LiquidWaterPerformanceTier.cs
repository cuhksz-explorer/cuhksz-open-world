/// <summary>液态水平静/踩踏水面的性能档位。</summary>
public enum LiquidWaterPerformanceTier
{
    /// <summary>关波纹模拟与装饰投影，最省 CPU/GPU。</summary>
    HighPerformance = 0,
    /// <summary>保留踩踏波纹，降低模拟分辨率与更新频率。</summary>
    Balanced = 1,
    /// <summary>当前默认平静+踩踏效果。</summary>
    Quality = 2,
}
