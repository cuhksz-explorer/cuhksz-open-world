#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 角色 prefab / 场景 Player 共用的骨架与 WASD 动作配置。
/// </summary>
public static class PlayerCharacterEditorShared
{
    public const string ResourcesRoot = "Assets/Resources";
    public const string PrefabRoot = "Assets/Resources/PlayerCharacters";
    public const string CatalogPath = "Assets/Resources/PlayerCharacterCatalog.asset";
    public const string FemaleFbx = "Assets/character/character/自机角色女 裸模9.fbx";
    public const string MaleFbxA = "Assets/character/player_select/npc2.fbx";
    public const string MaleFbxB = "Assets/character/player_select/npc._42812_autosave.fbx";
    public const string MovementControllerPath = "Assets/code/wasd(1).controller";
    public const string MovementControllerResourcesPath = "Assets/Resources/PlayerCharacters/PlayerMovement.controller";

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        var name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    public static void EnsureMovementControllerInResources()
    {
        EnsureFolder(PrefabRoot);
        if (!File.Exists(MovementControllerPath))
        {
            Debug.LogWarning("[PlayerCharacterEditorShared] 未找到 Animator Controller: " + MovementControllerPath);
            return;
        }

        if (File.Exists(MovementControllerResourcesPath))
            AssetDatabase.DeleteAsset(MovementControllerResourcesPath);
        AssetDatabase.CopyAsset(MovementControllerPath, MovementControllerResourcesPath);
    }

    public static RuntimeAnimatorController LoadMovementControllerAsset()
    {
        return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(MovementControllerPath);
    }

    public static Avatar FindAvatarAtPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return null;
        var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        Avatar best = null;
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Avatar avatar)
            {
                if (avatar.isValid && avatar.isHuman)
                    return avatar;
                if (best == null)
                    best = avatar;
            }
        }
        return best;
    }

    /// <summary>
    /// 优先使用模型自身 Humanoid Avatar；否则与本科新生共用同一 Avatar（WASD 重定向）。
    /// </summary>
    public static Avatar ResolveMovementAvatar(string modelFbxPath)
    {
        var modelAvatar = FindAvatarAtPath(modelFbxPath);
        if (modelAvatar != null && modelAvatar.isValid && modelAvatar.isHuman)
            return modelAvatar;

        var femaleAvatar = FindAvatarAtPath(FemaleFbx);
        if (femaleAvatar != null && femaleAvatar.isValid)
            return femaleAvatar;

        var femalePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "/hero_female.prefab");
        if (femalePrefab != null)
        {
            var animator = femalePrefab.GetComponent<Animator>();
            if (animator != null && animator.avatar != null && animator.avatar.isValid)
                return animator.avatar;
        }

        return null;
    }

    public static void EnsureHumanoidModelImport(string fbxPath)
    {
        if (string.IsNullOrEmpty(fbxPath) || !File.Exists(fbxPath))
            return;

        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null)
            return;

        bool changed = false;
        if (importer.animationType != ModelImporterAnimationType.Human)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            changed = true;
        }

        if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
        {
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
            Debug.Log("[PlayerCharacterEditorShared] 已将模型设为 Humanoid: " + fbxPath);
        }
    }

    public static void EnsureAllMaleFbxHumanoidImport()
    {
        EnsureHumanoidModelImport(MaleFbxA);
        EnsureHumanoidModelImport(MaleFbxB);
        EnsureHumanoidModelImport(PrefabRoot + "/hero_male_a.fbx");
        EnsureHumanoidModelImport(PrefabRoot + "/hero_male_b.fbx");
    }

    public static void EnsureMovementAnimatorOnInstance(GameObject root, string avatarSourcePath)
    {
        if (root == null)
            return;

        var animator = root.GetComponent<Animator>();
        if (animator == null)
            animator = root.AddComponent<Animator>();

        var controller = LoadMovementControllerAsset();
        if (controller != null)
            animator.runtimeAnimatorController = controller;

        var avatar = ResolveMovementAvatar(avatarSourcePath);
        if (avatar != null)
            animator.avatar = avatar;
        else
            Debug.LogWarning("[PlayerCharacterEditorShared] 未找到 Humanoid Avatar: " + avatarSourcePath);

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        animator.enabled = true;
    }

    public static GameObject RebuildCharacterPrefab(string sourceFbxPath, string prefabName, float rootScale, bool forceRebuild = false)
    {
        EnsureHumanoidModelImport(sourceFbxPath);
        EnsureFolder(PrefabRoot);

        string prefabPath = PrefabRoot + "/" + prefabName + ".prefab";
        string strayFbxPath = PrefabRoot + "/" + prefabName + ".fbx";
        if (File.Exists(strayFbxPath))
            AssetDatabase.DeleteAsset(strayFbxPath);

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(sourceFbxPath);
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (model == null)
        {
            Debug.LogWarning("[PlayerCharacterEditorShared] 未找到源模型: " + sourceFbxPath);
            return existing;
        }

        bool rebuild = forceRebuild || existing == null || !PrefabHasValidMovementAnimator(existing);
        if (!rebuild)
        {
            ConfigurePrefabMovement(sourceFbxPath, prefabName);
            return existing;
        }

        if (existing != null)
            AssetDatabase.DeleteAsset(prefabPath);

        var temp = Object.Instantiate(model);
        temp.name = prefabName;
        temp.transform.localScale = Vector3.one * rootScale;
        EnsureMovementAnimatorOnInstance(temp, sourceFbxPath);
        var prefab = PrefabUtility.SaveAsPrefabAsset(temp, prefabPath);
        Object.DestroyImmediate(temp);
        AssetDatabase.SaveAssets();
        Debug.Log("[PlayerCharacterEditorShared] 已重建角色 prefab: " + prefabPath);
        return prefab;
    }

    public static void ConfigurePrefabMovement(string fbxPath, string prefabName)
    {
        string prefabPath = PrefabRoot + "/" + prefabName + ".prefab";
        if (!File.Exists(prefabPath))
        {
            Debug.LogWarning("[PlayerCharacterEditorShared] 缺少 prefab: " + prefabPath);
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            EnsureMovementAnimatorOnInstance(root, fbxPath);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    public static void ConfigureAllCharacterPrefabsMovement()
    {
        EnsureMovementControllerInResources();
        EnsureFolder(PrefabRoot);
        EnsureAllMaleFbxHumanoidImport();

        RebuildCharacterPrefab(FemaleFbx, "hero_female", 1.1f);
        RebuildCharacterPrefab(MaleFbxA, "hero_male_a", 1f, forceRebuild: true);
        RebuildCharacterPrefab(MaleFbxB, "hero_male_b", 1f, forceRebuild: true);
    }

    public static void WireCatalogMovementController()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<PlayerCharacterCatalog>(CatalogPath);
        if (catalog == null)
            return;

        var controller = LoadMovementControllerAsset();
        var so = new SerializedObject(catalog);
        so.FindProperty("movementAnimatorController").objectReferenceValue = controller;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    /// <summary>
    /// 为场景 Player 根节点补齐 KeyInputController + Animator（WASD 骨架/动作）。
    /// </summary>
    public static void EnsureScenePlayerMovementComponents(GameObject player)
    {
        if (player == null)
            return;

        if (player.GetComponent<KeyInputController>() == null)
            Undo.AddComponent<KeyInputController>(player);

        var animator = player.GetComponent<Animator>();
        if (animator == null)
            animator = Undo.AddComponent<Animator>(player);

        var controller = LoadMovementControllerAsset();
        if (controller != null)
        {
            Undo.RecordObject(animator, "Assign Movement Controller");
            animator.runtimeAnimatorController = controller;
        }

        if (animator.avatar == null)
        {
            var avatar = FindAvatarAtPath(FemaleFbx);
            if (avatar == null)
            {
                var femalePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "/hero_female.prefab");
                if (femalePrefab != null)
                {
                    var femaleAnimator = femalePrefab.GetComponent<Animator>();
                    if (femaleAnimator != null)
                        avatar = femaleAnimator.avatar;
                }
            }
            if (avatar != null)
            {
                Undo.RecordObject(animator, "Assign Player Avatar");
                animator.avatar = avatar;
            }
        }

        Undo.RecordObject(animator, "Configure Player Animator");
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        animator.enabled = true;
    }

    public static bool PrefabHasValidMovementAnimator(GameObject prefab)
    {
        if (prefab == null)
            return false;
        var animator = prefab.GetComponent<Animator>();
        return animator != null
               && animator.runtimeAnimatorController != null
               && animator.avatar != null;
    }
}
#endif
