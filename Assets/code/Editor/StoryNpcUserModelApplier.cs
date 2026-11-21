#if UNITY_EDITOR
/// <summary>
/// BatchMode 兼容入口。菜单已合并到 <see cref="StoryNpcSketchfabImportMenu"/>。
/// </summary>
public static class StoryNpcUserModelApplier
{
    public static void ApplyAll() => StoryNpcSketchfabImportMenu.ApplyAllUserProvidedNpcModels();

    public static void ApplyAllMenu() => StoryNpcSketchfabImportMenu.ApplyUserProvidedNpcModels();
}
#endif
