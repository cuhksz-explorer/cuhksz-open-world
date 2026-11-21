#if UNITY_EDITOR
using System.Collections.Generic;
using GameQuestSystem;
using UnityEditor;
using UnityEngine;

public static class StoryNpcVisualUtility
{
    public const string SketchfabImportRoot = "Assets/Imports/Sketchfab";
    public const string SupercyanMalePrefab =
        "Assets/Supercyan Character Pack Free Sample/Prefabs/Base/High Quality/FreeSample_male_1.prefab";
    public const string CharacterQPrefab = "Assets/NPCs/characterQ.prefab";

    static readonly HashSet<string> SkipNpcNames = new HashSet<string>
    {
        "剧情触发点", "Null", "均分幻影", "她第一次深刻地意识到",
    };

    static readonly HashSet<string> FemaleNameHints = new HashSet<string>
    {
        "沈青禾", "林见鹿", "赵梓璇", "苏然", "苏沐", "夏林", "宿管阿姨", "顾澄",
        "(第二天）沈青禾", "（若干天后）顾澄",
    };

    public static bool ShouldSkipStoryNpc(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return true;
        return SkipNpcNames.Contains(displayName);
    }

    public static bool LooksLikeFemaleNpc(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return false;
        if (FemaleNameHints.Contains(displayName))
            return true;
        foreach (var hint in FemaleNameHints)
        {
            if (displayName.Contains(hint))
                return true;
        }

        return displayName.Contains("阿姨")
               || displayName.Contains("禾")
               || displayName.Contains("鹿")
               || displayName.Contains("璇")
               || displayName.Contains("沐");
    }

    public static string ResolveDefaultCharacterPrefab(string displayName)
    {
        if (displayName == "周砚" || displayName == "赵承勋")
            return CharacterQPrefab;
        return SupercyanMalePrefab;
    }

    public static float ResolveDefaultScale(string displayName)
    {
        return LooksLikeFemaleNpc(displayName) ? 0.92f : 1f;
    }

    public static void ApplyCharacterToStoryNpcPrefab(
        string storyPrefabPath,
        GameObject characterPrefab,
        float scale = 1f,
        float groundOffsetY = 0f,
        float targetHeightMeters = 1.75f,
        float yawOffsetDegrees = 180f,
        float pitchOffsetDegrees = 0f,
        string displayName = null)
    {
        if (string.IsNullOrWhiteSpace(storyPrefabPath) || characterPrefab == null)
            return;

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = System.IO.Path.GetFileNameWithoutExtension(storyPrefabPath);

        var root = PrefabUtility.LoadPrefabContents(storyPrefabPath);
        try
        {
            ApplyCharacterToStoryNpcRoot(
                root,
                characterPrefab,
                scale,
                groundOffsetY,
                targetHeightMeters,
                yawOffsetDegrees,
                pitchOffsetDegrees,
                displayName);
            PrefabUtility.SaveAsPrefabAsset(root, storyPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    public static void ApplyCharacterToStoryNpcRoot(
        GameObject root,
        GameObject characterPrefab,
        float scale = 1f,
        float groundOffsetY = 0f,
        float targetHeightMeters = 1.75f,
        float yawOffsetDegrees = 180f,
        float pitchOffsetDegrees = 0f,
        string displayName = null)
    {
        if (root == null || characterPrefab == null)
            return;

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = root.name;

        StripPlaceholderCapsuleMesh(root);
        EnsureStoryNpcComponents(root);

        var existing = root.transform.Find(StoryNpcVisualProfile.VisualChildName);
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        GameObject visual;
        if (EditorUtility.IsPersistent(characterPrefab) && PrefabUtility.IsPartOfPrefabAsset(characterPrefab))
            visual = (GameObject)PrefabUtility.InstantiatePrefab(characterPrefab, root.transform);
        else
            visual = Object.Instantiate(characterPrefab, root.transform);

        visual.name = StoryNpcVisualProfile.VisualChildName;
        visual.transform.localPosition = new Vector3(0f, groundOffsetY, 0f);
        visual.transform.localRotation = Quaternion.Euler(pitchOffsetDegrees, yawOffsetDegrees, 0f);
        visual.transform.localScale = Vector3.one * scale;

        NormalizeVisualHeight(visual, targetHeightMeters);
        AlignVisualFeetToGround(visual);
        DisableVisualColliders(visual);
        FitCapsuleCollider(root);
        EnsurePortraitAnchor(root);
        WriteVisualProfile(root, scale, groundOffsetY, displayName);
    }

    public static GameObject LoadModelAsset(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return null;

        var main = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (main != null)
            return main;

        var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is GameObject go && !string.IsNullOrEmpty(go.name))
                return go;
        }

        return null;
    }

    static void NormalizeVisualHeight(GameObject visual, float targetHeightMeters)
    {
        if (visual == null || targetHeightMeters <= 0.01f)
            return;

        var renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        float h = bounds.size.y;
        if (h < 0.01f)
            return;

        float factor = targetHeightMeters / h;
        visual.transform.localScale *= factor;
    }

    /// <summary>把模型最低点对齐到 NPC 根节点地面（y=0）。</summary>
    static void AlignVisualFeetToGround(GameObject visual)
    {
        var renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;

        float minY = float.PositiveInfinity;
        for (int i = 0; i < renderers.Length; i++)
            minY = Mathf.Min(minY, renderers[i].bounds.min.y);

        if (float.IsPositiveInfinity(minY))
            return;

        Vector3 pos = visual.transform.position;
        pos.y += -minY;
        visual.transform.position = pos;
    }

    static void EnsureStoryNpcComponents(GameObject root)
    {
        if (root.GetComponent<StoryQuestNpc>() == null)
            root.AddComponent<StoryQuestNpc>();

        var col = root.GetComponent<CapsuleCollider>();
        if (col == null)
            col = root.AddComponent<CapsuleCollider>();
        col.enabled = true;
        col.isTrigger = false;
    }

    static void StripPlaceholderCapsuleMesh(GameObject root)
    {
        var mf = root.GetComponent<MeshFilter>();
        if (mf != null)
            Object.DestroyImmediate(mf, true);
        var mr = root.GetComponent<MeshRenderer>();
        if (mr != null)
            Object.DestroyImmediate(mr, true);
    }

    static void DisableVisualColliders(GameObject visualRoot)
    {
        foreach (var col in visualRoot.GetComponentsInChildren<Collider>(true))
            col.enabled = false;
    }

    static void FitCapsuleCollider(GameObject root)
    {
        var col = root.GetComponent<CapsuleCollider>();
        if (col == null)
            return;

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            col.height = 2f;
            col.radius = 0.35f;
            col.center = new Vector3(0f, 1f, 0f);
            return;
        }

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 localCenter = root.transform.InverseTransformPoint(bounds.center);
        float height = Mathf.Max(bounds.size.y, 1.6f);
        col.height = height;
        col.radius = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z) * 0.25f, 0.25f, 0.55f);
        col.center = localCenter;
        col.direction = 1;
    }

    static void EnsurePortraitAnchor(GameObject root)
    {
        var anchor = root.transform.Find("PortraitAnchor");
        if (anchor == null)
        {
            var go = new GameObject("PortraitAnchor");
            anchor = go.transform;
            anchor.SetParent(root.transform, false);
        }

        float y = 1.55f;
        var col = root.GetComponent<CapsuleCollider>();
        if (col != null)
            y = col.center.y + col.height * 0.42f;
        anchor.localPosition = new Vector3(0f, y, 0.25f);
        anchor.localRotation = Quaternion.identity;
        anchor.localScale = Vector3.one;
    }

    static void WriteVisualProfile(GameObject root, float scale, float groundOffsetY, string displayName)
    {
        var profile = root.GetComponent<StoryNpcVisualProfile>();
        if (profile == null)
            profile = root.AddComponent<StoryNpcVisualProfile>();
        profile.modelScale = scale;
        profile.groundOffsetY = groundOffsetY;
        profile.portraitAnchor = root.transform.Find("PortraitAnchor");
        profile.applyGhostEffect = StoryNpcUserModelMapping.ShouldUseGhostEffect(displayName);
        profile.ghostAlpha = 0.45f;
        profile.ghostTint = new Color(0.75f, 0.88f, 1f, 1f);

        if (profile.applyGhostEffect && root.GetComponent<StoryNpcGhostVisual>() == null)
            root.AddComponent<StoryNpcGhostVisual>();
    }

    public static List<string> FindImportedModelAssetPaths()
    {
        var result = new List<string>();
        if (!AssetDatabase.IsValidFolder(SketchfabImportRoot))
            return result;

        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { SketchfabImportRoot });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(path))
                continue;
            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".fbx" || ext == ".glb" || ext == ".gltf" || ext == ".obj")
                result.Add(path);
        }

        result.Sort();
        return result;
    }
}
#endif
