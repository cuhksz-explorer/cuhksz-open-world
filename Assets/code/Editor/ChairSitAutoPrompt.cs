#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 编辑器启动 / 场景加载时检测：
/// 1) 玩家是否已挂 PlayerSitSequence；
/// 2) 当前场景里是否有 InteractableChair。
/// 如果缺，弹一次窗，问用户是否一键修复。
/// 用 SessionState 保证一次会话只问一次，不烦人。
/// </summary>
[InitializeOnLoad]
internal static class ChairSitAutoPrompt
{
    const string AskedFlag = "ChairSitAutoPrompt.AskedThisSession";
    const string DisableFlag = "ChairSitAutoPrompt.DisablePermanently";

    static ChairSitAutoPrompt()
    {
        EditorApplication.delayCall += TryPromptOnce;
        EditorSceneManager_sceneOpened_Hook();
    }

    static void EditorSceneManager_sceneOpened_Hook()
    {
        UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += (scene, mode) =>
        {
            EditorApplication.delayCall += TryPromptOnce;
        };
    }

    static void TryPromptOnce()
    {
        if (Application.isPlaying)
            return;
        if (EditorPrefs.GetBool(DisableFlag, false))
            return;
        if (SessionState.GetBool(AskedFlag, false))
            return;

        var pm = Object.FindObjectOfType<PlayerMovement>(true);
        if (pm == null)
            return;

        bool hasSitSeq = pm.GetComponent<PlayerSitSequence>() != null;
        bool hasAnyChair = Object.FindObjectOfType<InteractableChair>(true) != null;

        if (hasSitSeq && hasAnyChair)
            return;

        SessionState.SetBool(AskedFlag, true);

        string missing =
            (!hasSitSeq ? "· 玩家未挂 PlayerSitSequence（坐下动画组件）\n" : "") +
            (!hasAnyChair ? "· 当前场景没有任何 InteractableChair（椅子交互）\n" : "");

        int choice = EditorUtility.DisplayDialogComplex(
            "椅子坐下 · 自动配置",
            $"检测到当前场景缺少坐下交互配置：\n\n{missing}\n点「一键修复」会自动：\n· 导入 4 个 Mixamo 坐下 FBX\n· 给玩家挂上 PlayerSitSequence\n· 给场景里所有 bench/chair/seat 等加坐下交互\n· 保存场景",
            "一键修复",
            "稍后再说",
            "不再提醒");

        switch (choice)
        {
            case 0:
                ChairSitOneClickWizard.RunFullSetupWithBenchesPublic();
                break;
            case 2:
                EditorPrefs.SetBool(DisableFlag, true);
                break;
        }
    }

    [MenuItem("Tools/Chair Sit/恢复启动自动提示", false, 200)]
    [MenuItem("Tools/椅子交互/恢复启动自动提示", false, 200)]
    static void ResetDisableFlag()
    {
        EditorPrefs.DeleteKey(DisableFlag);
        SessionState.EraseBool(AskedFlag);
        EditorUtility.DisplayDialog("椅子交互", "已恢复启动自动提示。", "确定");
    }
}
#endif
