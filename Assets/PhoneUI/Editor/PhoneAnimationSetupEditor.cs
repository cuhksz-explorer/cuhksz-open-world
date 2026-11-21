#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PhoneUI.Editor
{
    /// <summary>
    /// 一键创建手机动画占位 Clip、上半身 Mask、wasd(1).controller 的 Phone Layer。
    /// Mixamo 资源就绪后：在 Phone_Open/Hold/Close 状态上替换 Motion 即可。
    /// </summary>
    public static class PhoneAnimationSetupEditor
    {
        const string ClipsDir = "Assets/PhoneUI/Animation/Clips";
        const string MaskPath = "Assets/PhoneUI/Animation/Phone_UpperBody.mask";
        const string ControllerPath = "Assets/code/wasd(1).controller";
        const string PropPrefabPath = "Assets/PhoneUI/Prefabs/PhoneProp.prefab";

        const string OpenClipName = "Phone_Open";
        const string HoldClipName = "Phone_Hold_Loop";
        const string CloseClipName = "Phone_Close";

        [MenuItem("PhoneUI/Setup Phone Animator (Clips + Layer + Prop Prefab)")]
        public static void SetupPhoneAnimator()
        {
            EnsureDirectory(ClipsDir);
            var openClip = EnsureClip($"{ClipsDir}/{OpenClipName}.anim", OpenClipName, 1.2f, false);
            var holdClip = EnsureClip($"{ClipsDir}/{HoldClipName}.anim", HoldClipName, 2f, true);
            var closeClip = EnsureClip($"{ClipsDir}/{CloseClipName}.anim", CloseClipName, 1f, false);

            var mask = EnsureUpperBodyMask();
            SetupAnimatorLayer(openClip, holdClip, closeClip, mask);
            EnsurePhonePropPrefab();
            CopyPhonePropToResources();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PhoneUI] 手机动画层已配置。请在 Mixamo 导入后替换 Phone Layer 各状态的 Motion。");
        }

        [MenuItem("PhoneUI/Configure Active Scene Phone Presentation")]
        public static void ConfigureActiveScenePresentation()
        {
            var session = Object.FindObjectOfType<PhoneUI.PhoneSessionController>(true);
            if (session == null)
            {
                Debug.LogWarning("[PhoneUI] 场景中未找到 PhoneSessionController。");
                return;
            }

            var so = new SerializedObject(session);
            so.FindProperty("launcherOnly").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            var presentation = session.presentation;
            if (presentation == null)
                presentation = session.GetComponent<PhoneUI.PhonePresentationController>();
            if (presentation == null)
                presentation = Object.FindObjectOfType<PhoneUI.PhonePresentationController>(true);

            if (presentation != null)
            {
                var presSo = new SerializedObject(presentation);
                presSo.FindProperty("m_Enabled").boolValue = true;
                presSo.ApplyModifiedPropertiesWithoutUndo();

                var rig = session.GetComponent<PhoneUI.PhonePlayerRigSetup>();
                if (rig == null)
                    rig = session.gameObject.AddComponent<PhoneUI.PhonePlayerRigSetup>();
                rig.presentation = presentation;
                rig.playerMovement = session.playerMovement;
                rig.EnsureRig();

                EditorUtility.SetDirty(presentation);
            }

            EditorUtility.SetDirty(session);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            Debug.Log("[PhoneUI] 已设置 launcherOnly=false 并启用 Presentation / Rig。");
        }

        static void EnsureDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
                var leaf = Path.GetFileName(path);
                if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                    EnsureDirectory(parent);
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }

        static AnimationClip EnsureClip(string path, string clipName, float length, bool loop)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null)
                return existing;

            var clip = new AnimationClip { name = clipName };
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            var curve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(length, 0f));
            clip.SetCurve("", typeof(Transform), "localPosition.x", curve);

            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        static AvatarMask EnsureUpperBodyMask()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            if (existing != null)
                return existing;

            var mask = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);

            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);

            AssetDatabase.CreateAsset(mask, MaskPath);
            return mask;
        }

        static void SetupAnimatorLayer(AnimationClip open, AnimationClip hold, AnimationClip close, AvatarMask mask)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError("[PhoneUI] 未找到: " + ControllerPath);
                return;
            }

            EnsureParameter(controller, "OpenPhone", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "ClosePhone", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "UsingPhone", AnimatorControllerParameterType.Bool);

            if (HasLayer(controller, "Phone"))
            {
                Debug.Log("[PhoneUI] Phone Layer 已存在，跳过重建。");
                return;
            }

            var layer = new AnimatorControllerLayer
            {
                name = "Phone",
                defaultWeight = 1f,
                avatarMask = mask,
                blendingMode = AnimatorLayerBlendingMode.Override
            };

            var sm = new AnimatorStateMachine { name = "Phone" };
            sm.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(sm, controller);

            var idle = sm.AddState("PhoneIdle", new Vector3(250f, 0f, 0f));
            sm.defaultState = idle;

            var openState = sm.AddState("Phone_Open", new Vector3(450f, 0f, 0f));
            openState.motion = open;

            var holdState = sm.AddState("Phone_Hold", new Vector3(650f, 0f, 0f));
            holdState.motion = hold;

            var closeState = sm.AddState("Phone_Close", new Vector3(850f, 0f, 0f));
            closeState.motion = close;

            var toOpen = idle.AddTransition(openState);
            toOpen.AddCondition(AnimatorConditionMode.If, 0, "OpenPhone");
            toOpen.duration = 0.1f;
            toOpen.hasExitTime = false;

            var toHold = openState.AddTransition(holdState);
            toHold.duration = 0.15f;
            toHold.hasExitTime = true;
            toHold.exitTime = 0.92f;

            var toClose = holdState.AddTransition(closeState);
            toClose.AddCondition(AnimatorConditionMode.If, 0, "ClosePhone");
            toClose.duration = 0.1f;
            toClose.hasExitTime = false;

            var toIdle = closeState.AddTransition(idle);
            toIdle.duration = 0.15f;
            toIdle.hasExitTime = true;
            toIdle.exitTime = 0.9f;

            layer.stateMachine = sm;
            controller.AddLayer(layer);
            EditorUtility.SetDirty(controller);
        }

        static bool HasLayer(AnimatorController controller, string layerName)
        {
            for (int i = 0; i < controller.layers.Length; i++)
            {
                if (controller.layers[i].name == layerName)
                    return true;
            }
            return false;
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

        static void EnsurePhonePropPrefab()
        {
            EnsureDirectory("Assets/PhoneUI/Prefabs");
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PropPrefabPath);
            if (existing != null)
                return;

            GameObject root = new GameObject("PhoneProp");
            var mesh = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PhoneUI/CellPhone/phone.fbx");
            if (mesh != null)
            {
                var inst = Object.Instantiate(mesh, root.transform);
                inst.name = "phone_mesh";
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                inst.transform.localScale = Vector3.one * 0.01f;
            }
            else
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "phone_mesh";
                cube.transform.SetParent(root.transform, false);
                cube.transform.localScale = new Vector3(0.06f, 0.12f, 0.02f);
                Object.DestroyImmediate(cube.GetComponent<Collider>());
            }

            root.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, PropPrefabPath);
            Object.DestroyImmediate(root);
        }

        static void CopyPhonePropToResources()
        {
            const string resDir = "Assets/PhoneUI/Resources/PhoneUI";
            EnsureDirectory(resDir);
            var src = PropPrefabPath;
            var dst = $"{resDir}/PhoneProp.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(dst) == null)
                AssetDatabase.CopyAsset(src, dst);
        }
    }
}
#endif
