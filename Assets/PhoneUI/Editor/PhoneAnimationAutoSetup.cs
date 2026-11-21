#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace PhoneUI.Editor
{
    /// <summary>
    /// 打开工程时若缺少手机动画资源，自动执行一次 Setup（可在菜单手动重新执行）。
    /// </summary> 
    [InitializeOnLoad]
    static class PhoneAnimationAutoSetup
    {
        const string OpenClipPath = "Assets/PhoneUI/Animation/Clips/Phone_Open.anim";

        static PhoneAnimationAutoSetup()
        {
            EditorApplication.delayCall += TryAutoSetup;
        }

        static void TryAutoSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(OpenClipPath) != null)
                return;

            PhoneAnimationSetupEditor.SetupPhoneAnimator();
        }
    }
}
#endif
