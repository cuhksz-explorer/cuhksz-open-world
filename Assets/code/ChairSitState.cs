/// <summary>
/// 坐下交互的全局状态，供 PlayerMovement / KeyInputController 等查询。
/// </summary>
public static class ChairSitState
{
    public static bool IsSitting { get; internal set; }

    public static bool BlocksGameplay => IsSitting;
}
