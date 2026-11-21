namespace CombatSystem
{
    /// <summary>
    /// 战斗 UI Canvas 层级，对齐项目 UI 修复计划（交互提示 900、背包 100、任务 210…）。
    /// 战斗 HUD 保持在 HUD 带（78–95），模态面板低于任务条、高于背包。
    /// </summary>
    public static class CombatUiLayers
    {
        public const int CombatHud = 85;
        public const int GachaPanel = 115;
    }
}
