#if UNITY_EDITOR
using UnityEngine.SceneManagement;

/// <summary>
/// 兼容旧调用方；批量逻辑已统一到 <see cref="UndergardenUiSceneSetup"/>。
/// </summary>
public static class GameplayUiStandardizeAllScenesSetup
{
    public static void RunBatch()
    {
        UndergardenUiSceneSetup.ConfigureAllScenesInternal();
    }

    public static SceneConfigureResult ConfigureScene(Scene targetScene, Scene templateScene)
    {
        var report = UndergardenUiSceneSetup.ConfigureScene(targetScene, templateScene);
        return new SceneConfigureResult
        {
            UiSystemsCopied = (report.CopiedPhoneHud ? 1 : 0) + (report.CopiedQuestSystemUi ? 1 : 0),
            CanvasesConfigured = report.CanvasScaleFixed,
            PromptConfigured = report.PromptConfigured,
        };
    }

    public struct SceneConfigureResult
    {
        public int UiSystemsCopied;
        public int CanvasesConfigured;
        public bool PromptConfigured;
    }
}
#endif
