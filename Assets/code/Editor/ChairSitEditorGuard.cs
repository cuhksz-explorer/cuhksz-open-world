#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// 椅子交互编辑器工具的统一守卫：在 Play 模式下点菜单会让 EditorSceneManager 抛
/// "This cannot be used during play mode."，这里集中拦截。
/// </summary>
internal static class ChairSitEditorGuard
{
    /// <summary>
    /// 如果当前处于 Play 或正在切换 Play，弹窗提示并返回 false。
    /// </summary>
    public static bool EnsureNotPlaying()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
        {
            bool stopNow = EditorUtility.DisplayDialog(
                "椅子交互",
                "现在处于 Play 模式，无法修改场景或保存资源。\n请先停止 Play 再点该菜单。",
                "立即停止 Play",
                "取消");

            if (stopNow)
                EditorApplication.isPlaying = false;

            return false;
        }
        return true;
    }
}
#endif
