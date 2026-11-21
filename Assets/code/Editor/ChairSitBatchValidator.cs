#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 供 batchmode 调用：重建 ClipSet 并输出 CHAIR_SIT_VALIDATION_OK / FAILED。
/// </summary>
public static class ChairSitBatchValidator
{
    public static void Run()
    {
        ChairSitOneClickWizard.BuildClipSetAndValidate(force: true);

        var set = AssetDatabase.LoadAssetAtPath<ChairSitClipSet>(
            "Assets/Resources/ChairSit/ChairSitClipSet.asset");

        if (set == null || !set.IsComplete)
        {
            Debug.LogError("CHAIR_SIT_VALIDATION_FAILED");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log(
            "CHAIR_SIT_VALIDATION_OK " +
            set.sitDownClip.name + " " +
            set.sitIdleClip.name + " " +
            set.standUpClip.name);
        EditorApplication.Exit(0);
    }
}
#endif
