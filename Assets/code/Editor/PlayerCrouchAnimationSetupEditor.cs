#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 为 WASD Animator 配置蹲下（Crouch）参数与状态，并同步到 Resources/PlayerCharacters。
/// 动作来源：Mixamo「Right Cover Sneak」（项目内已有）；可从 Mixamo 下载 Crouch Idle / Crouch Walk 后替换 Motion。
/// </summary>
public static class PlayerCrouchAnimationSetupEditor
{
    const string AnimDir = "Assets/Animations/Locomotion";
    const string SneakSource = "Assets/Meshes/Materials/X Bot@Right Cover Sneak.fbx";
    const string CrouchWalkFbx = AnimDir + "/X Bot@Crouch Walk.fbx";
    const string CrouchIdleFbx = AnimDir + "/X Bot@Crouch Idle.fbx";
    const string CrouchIdleStateName = "crouch-idle";
    const string CrouchWalkStateName = "crouch-walk";
    const string CrouchParam = "Crouch";

    [MenuItem("Tools/Game Server/配置蹲下动作与 Animator", false, 123)]
    public static void SetupCrouchFromMenu()
    {
        if (!EditorUtility.DisplayDialog(
                "配置蹲下",
                "将：\n" +
                "1. 导入/配置蹲下动作 FBX（Humanoid + 循环）\n" +
                "2. 为 wasd(1).controller 添加 Crouch 状态与过渡\n" +
                "3. 同步到 Resources 并更新角色 prefab\n\n" +
                "是否继续？",
                "继续",
                "取消"))
            return;

        RunSetup();
        EditorUtility.DisplayDialog(
            "蹲下配置完成",
            "已配置蹲下 Animator。\n\n" +
            "请在游戏中按住 Ctrl 测试：\n" +
            "· 第一人称视点会随胶囊体降低\n" +
            "· 角色播放蹲下待机/行走动作\n\n" +
            "若需更真实的 Mixamo 蹲下：到 mixamo.com 搜索「Crouch Idle」「Crouch Walk Forward」" +
            "下载 FBX（Without Skin）后替换\n" + AnimDir + " 下同名文件，再执行本菜单。",
            "确定");
    }

    public static void RunSetup()
    {
        EnsureCrouchAnimationFiles();
        AssetDatabase.Refresh();

        ConfigureFbxImport(CrouchWalkFbx, loop: true);
        ConfigureFbxImport(CrouchIdleFbx, loop: true);

        AssetDatabase.ImportAsset(CrouchWalkFbx, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(CrouchIdleFbx, ImportAssetOptions.ForceUpdate);

        var walkClip = LoadFirstAnimationClip(CrouchWalkFbx);
        var idleClip = LoadFirstAnimationClip(CrouchIdleFbx);
        if (walkClip == null || idleClip == null)
        {
            Debug.LogError("[CrouchSetup] 未能从 FBX 读取 AnimationClip。");
            return;
        }

        SetupAnimatorController(walkClip, idleClip);

        PlayerCharacterEditorShared.EnsureMovementControllerInResources();
        PlayerCharacterEditorShared.ConfigureAllCharacterPrefabsMovement();
        PlayerCharacterEditorShared.WireCatalogMovementController();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CrouchSetup] 蹲下动作与 Animator 已配置。");
    }

    static void EnsureCrouchAnimationFiles()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Animations"))
            AssetDatabase.CreateFolder("Assets", "Animations");
        if (!AssetDatabase.IsValidFolder(AnimDir))
            AssetDatabase.CreateFolder("Assets/Animations", "Locomotion");

        if (!File.Exists(CrouchWalkFbx) || !File.Exists(CrouchIdleFbx))
        {
            if (!File.Exists(SneakSource))
            {
                Debug.LogError("[CrouchSetup] 缺少源动作: " + SneakSource);
                return;
            }

            File.Copy(SneakSource, CrouchWalkFbx, true);
            File.Copy(SneakSource, CrouchIdleFbx, true);
        }
    }

    static void ConfigureFbxImport(string assetPath, bool loop)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
            return;

        bool changed = false;
        if (importer.animationType != ModelImporterAnimationType.Human)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            changed = true;
        }

        if (importer.avatarSetup != ModelImporterAvatarSetup.NoAvatar)
        {
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            changed = true;
        }

        var clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = new[] { new ModelImporterClipAnimation { name = Path.GetFileNameWithoutExtension(assetPath) } };
        }

        for (int i = 0; i < clips.Length; i++)
        {
            clips[i].loopTime = loop;
            clips[i].loopPose = loop;
            clips[i].keepOriginalOrientation = true;
            clips[i].keepOriginalPositionY = true;
            clips[i].keepOriginalPositionXZ = true;
        }

        importer.clipAnimations = clips;

        if (changed)
            importer.SaveAndReimport();
        else
            importer.SaveAndReimport();
    }

    static AnimationClip LoadFirstAnimationClip(string fbxPath)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        AnimationClip best = null;
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is AnimationClip clip && !clip.name.StartsWith("__preview"))
            {
                if (best == null || clip.name.Contains("Crouch") || clip.name.Contains("Sneak"))
                    best = clip;
            }
        }
        return best;
    }

    static void SetupAnimatorController(AnimationClip walkClip, AnimationClip idleClip)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            PlayerCharacterEditorShared.MovementControllerPath);
        if (controller == null)
        {
            Debug.LogError("[CrouchSetup] 未找到 Animator Controller。");
            return;
        }

        EnsureParameter(controller, CrouchParam, AnimatorControllerParameterType.Bool);

        var layer = controller.layers[0];
        var sm = layer.stateMachine;
        if (sm == null)
            return;

        var crouchIdle = FindOrCreateState(sm, CrouchIdleStateName, new Vector3(400f, 720f, 0f));
        var crouchWalk = FindOrCreateState(sm, CrouchWalkStateName, new Vector3(650f, 720f, 0f));
        crouchIdle.motion = idleClip;
        crouchWalk.motion = walkClip;
        crouchIdle.speed = 0.35f;
        crouchWalk.speed = 1f;

        var idleState = FindState(sm, "idle");
        if (idleState == null)
            idleState = sm.defaultState;

        RemoveTransitionsTo(sm, crouchIdle);
        RemoveTransitionsTo(sm, crouchWalk);
        RemoveTransitionsFrom(crouchIdle, exceptTarget: null);
        RemoveTransitionsFrom(crouchWalk, exceptTarget: null);

        var anyToIdle = sm.AddAnyStateTransition(crouchIdle);
        anyToIdle.AddCondition(AnimatorConditionMode.If, 0, CrouchParam);
        anyToIdle.duration = 0.12f;
        anyToIdle.hasExitTime = false;
        anyToIdle.canTransitionToSelf = false;

        var idleExit = crouchIdle.AddTransition(idleState != null ? idleState : sm.defaultState);
        idleExit.AddCondition(AnimatorConditionMode.IfNot, 0, CrouchParam);
        idleExit.duration = 0.12f;
        idleExit.hasExitTime = false;

        var toWalk = crouchIdle.AddTransition(crouchWalk);
        toWalk.AddCondition(AnimatorConditionMode.If, 0, CrouchParam);
        toWalk.AddCondition(AnimatorConditionMode.If, 0, "W");
        toWalk.duration = 0.1f;
        toWalk.hasExitTime = false;

        var walkToIdle = crouchWalk.AddTransition(crouchIdle);
        walkToIdle.AddCondition(AnimatorConditionMode.If, 0, CrouchParam);
        walkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "W");
        walkToIdle.duration = 0.1f;
        walkToIdle.hasExitTime = false;

        var walkExit = crouchWalk.AddTransition(idleState != null ? idleState : sm.defaultState);
        walkExit.AddCondition(AnimatorConditionMode.IfNot, 0, CrouchParam);
        walkExit.duration = 0.12f;
        walkExit.hasExitTime = false;

        EditorUtility.SetDirty(controller);
    }

    static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        foreach (var p in controller.parameters)
        {
            if (p.name == name)
                return;
        }

        controller.AddParameter(name, type);
    }

    static AnimatorState FindOrCreateState(AnimatorStateMachine sm, string stateName, Vector3 pos)
    {
        var existing = FindState(sm, stateName);
        if (existing != null)
            return existing;
        return sm.AddState(stateName, pos);
    }

    static AnimatorState FindState(AnimatorStateMachine sm, string stateName)
    {
        foreach (var child in sm.states)
        {
            if (child.state != null && child.state.name == stateName)
                return child.state;
        }
        return null;
    }

    static void RemoveTransitionsTo(AnimatorStateMachine sm, AnimatorState target)
    {
        foreach (var child in sm.states)
        {
            if (child.state == null || child.state == target)
                continue;
            RemoveTransitionsOnState(child.state, target);
        }

        for (int i = sm.anyStateTransitions.Length - 1; i >= 0; i--)
        {
            if (sm.anyStateTransitions[i].destinationState == target
                && HasOnlyCondition(sm.anyStateTransitions[i], CrouchParam))
                sm.RemoveAnyStateTransition(sm.anyStateTransitions[i]);
        }
    }

    static void RemoveTransitionsFrom(AnimatorState source, AnimatorState exceptTarget)
    {
        if (source == null)
            return;

        for (int i = source.transitions.Length - 1; i >= 0; i--)
        {
            var t = source.transitions[i];
            if (exceptTarget != null && t.destinationState == exceptTarget)
                continue;
            source.RemoveTransition(t);
        }
    }

    static void RemoveTransitionsOnState(AnimatorState source, AnimatorState target)
    {
        for (int i = source.transitions.Length - 1; i >= 0; i--)
        {
            if (source.transitions[i].destinationState == target)
                source.RemoveTransition(source.transitions[i]);
        }
    }

    static bool HasOnlyCondition(AnimatorStateTransition transition, string paramName)
    {
        if (transition.conditions.Length != 1)
            return false;
        return transition.conditions[0].parameter == paramName;
    }
}
#endif
