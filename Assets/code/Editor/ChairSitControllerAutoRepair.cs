#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Unity 打开工程后，若 ChairSit FBX 已存在但 Controller 动画引用断裂，则自动重建。
/// </summary>
[InitializeOnLoad]
internal static class ChairSitControllerAutoRepair
{
    const string SessionFlag = "ChairSitControllerAutoRepair.Attempted";

    static ChairSitControllerAutoRepair()
    {
        EditorApplication.delayCall += TryRepairOnce;
    }

    static void TryRepairOnce()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (SessionState.GetBool(SessionFlag, false))
            return;

        SessionState.SetBool(SessionFlag, true);

        const string folder = "Assets/Animations/ChairSit";
        const string controllerPath = folder + "/PlayerChairSit.controller";
        if (!File.Exists(Path.Combine(folder, "Sitting (2).fbx"))
            && !File.Exists(Path.Combine(folder, "Sitting (1).fbx"))
            && !File.Exists(Path.Combine(folder, "Sitting.fbx")))
            return;

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null || controller.layers == null || controller.layers.Length == 0)
        {
            ChairSitOneClickWizard.RebuildSitControllerFromFolder();
            return;
        }

        var states = controller.layers[0].stateMachine.states;
        if (states == null || states.Length == 0)
        {
            ChairSitOneClickWizard.RebuildSitControllerFromFolder();
            return;
        }

        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].state == null || states[i].state.motion == null)
            {
                if (ChairSitOneClickWizard.RebuildSitControllerFromFolder())
                    Debug.Log("[椅子交互] 已自动修复 PlayerChairSit.controller 的动画引用。");
                return;
            }
        }
    }
}
#endif
