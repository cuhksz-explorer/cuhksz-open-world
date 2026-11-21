namespace GameQuestSystem
{
    /// <summary>
    /// 主线章节推进门控（预留接口）。
    /// 可在自定义系统中实现：当指定支线未完成时，阻止对应主线章节继续推进。
    /// 默认逻辑见 <see cref="MainChapterGate"/> 与 <see cref="QuestChainDefinition.requiredSideQuestIds"/>。
    /// </summary>
    public interface IMainChapterProgressGate
    {
        /// <summary>是否允许该主线任务推进（接取/追踪/完成子任务）。</summary>
        bool CanAdvanceMainQuest(QuestChainDefinition mainQuest, QuestSystemManager manager);
    }
}
