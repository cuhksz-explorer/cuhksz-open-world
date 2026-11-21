using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerCharacterCatalog", menuName = "Game/Player Character Catalog")]
public class PlayerCharacterCatalog : ScriptableObject
{
    public static readonly string[] DefaultCharacterIds = { "hero_female", "hero_male_a", "hero_male_b" };

    [Serializable]
    public class Option
    {
        public string id;
        public string displayName;
        [Tooltip("头像框内显示的短标识，如 女 / A / B")]
        public string avatarShortLabel;
        [TextArea(2, 5)]
        public string bio;
        public GameObject prefab;
        public string resourcesPrefabPath;
        public Sprite portrait;
        public float worldScale = 1f;
        public float previewScale = 0.55f;
        public float previewYawDegrees = 0f;
        public float previewPitchDegrees = 0f;
        public Vector3 previewOffset;
    }

    [SerializeField] RuntimeAnimatorController movementAnimatorController;

    public RuntimeAnimatorController MovementAnimatorController =>
        movementAnimatorController != null
            ? movementAnimatorController
            : Resources.Load<RuntimeAnimatorController>(PlayerCharacterAnimationUtility.MovementControllerResourcesPath);

    [SerializeField] List<Option> options = new List<Option>();

    public IReadOnlyList<Option> Options => options;

    public Option GetById(string id)
    {
        if (string.IsNullOrEmpty(id) || options == null)
            return null;
        for (int i = 0; i < options.Count; i++)
        {
            var o = options[i];
            if (o != null && string.Equals(o.id, id, StringComparison.Ordinal))
                return o;
        }
        return null;
    }

    public Option GetDefault()
    {
        var female = GetById("hero_female");
        if (female != null)
            return female;
        if (options == null || options.Count == 0)
            return null;
        return options[0];
    }

    public GameObject LoadPrefab(Option option)
    {
        if (option == null)
            return null;
        if (option.prefab != null)
            return option.prefab;
        if (!string.IsNullOrEmpty(option.resourcesPrefabPath))
        {
            option.prefab = Resources.Load<GameObject>(option.resourcesPrefabPath);
            return option.prefab;
        }
        return null;
    }

    public static void PreparePrefabInstance(GameObject instance, Option option)
    {
        if (instance == null || option == null)
            return;

        foreach (var skinned in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            skinned.updateWhenOffscreen = true;

        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null)
                renderer.enabled = true;
        }

        if (string.Equals(option.id, "hero_male_a", StringComparison.Ordinal)
            || string.Equals(option.id, "hero_male_b", StringComparison.Ordinal))
            FixMaleMetarig(instance);
    }

    static void FixMaleMetarig(GameObject root)
    {
        Transform metarig = null;
        for (int i = 0; i < root.transform.childCount; i++)
        {
            var child = root.transform.GetChild(i);
            if (child.name.IndexOf("metarig", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                metarig = child;
                break;
            }
        }

        if (metarig != null)
        {
            if (Mathf.Abs(metarig.localPosition.x) > 1f || Mathf.Abs(metarig.localPosition.z) > 1f)
                metarig.localPosition = new Vector3(0f, metarig.localPosition.y, 0f);
            if (metarig.localPosition.y < 0.1f || metarig.localPosition.y > 5f)
                metarig.localPosition = new Vector3(0f, 0.9936742f, 0f);
        }

        foreach (var light in root.GetComponentsInChildren<Light>(true))
            light.enabled = false;
    }

    public static PlayerCharacterCatalog LoadDefault()
    {
        var catalog = Resources.Load<PlayerCharacterCatalog>("PlayerCharacterCatalog");
        if (catalog == null)
            catalog = CreateInstance<PlayerCharacterCatalog>();
        catalog.EnsureDefaultOptionsIfEmpty();
        return catalog;
    }

    public void EnsureDefaultOptionsIfEmpty()
    {
        if (options == null)
            options = new List<Option>();

        EnsureOptionExists("hero_female", "本科新生", "PlayerCharacters/hero_female", 1.1f, 0.52f);
        EnsureOptionExists("hero_male_a", "研一新生", "PlayerCharacters/hero_male_a", 1.1f, 0.48f);
        EnsureOptionExists("hero_male_b", "博一新生", "PlayerCharacters/hero_male_b", 1.1f, 0.48f);

        for (int i = 0; i < options.Count; i++)
            ApplyMetadataDefaults(options[i]);

        SortOptionsToDefaultOrder();
    }

    void SortOptionsToDefaultOrder()
    {
        if (options == null)
            return;

        var sorted = new List<Option>(DefaultCharacterIds.Length);
        for (int i = 0; i < DefaultCharacterIds.Length; i++)
        {
            var option = GetById(DefaultCharacterIds[i]);
            if (option != null)
                sorted.Add(option);
        }

        for (int i = 0; i < options.Count; i++)
        {
            var option = options[i];
            if (option == null || sorted.Contains(option))
                continue;
            sorted.Add(option);
        }

        options = sorted;
    }

    void EnsureOptionExists(string id, string displayName, string resourcesPath, float worldScale, float previewScale)
    {
        var existing = GetById(id);
        if (existing != null)
        {
            if (string.IsNullOrEmpty(existing.displayName))
                existing.displayName = displayName;
            if (string.IsNullOrEmpty(existing.resourcesPrefabPath))
                existing.resourcesPrefabPath = resourcesPath;
            if (existing.prefab == null)
                existing.prefab = Resources.Load<GameObject>(resourcesPath);
            ApplyMetadataDefaults(existing);
            return;
        }

        TryAddDefaultOption(id, displayName, resourcesPath, worldScale, previewScale);
    }

    void TryAddDefaultOption(string id, string displayName, string resourcesPath, float worldScale, float previewScale)
    {
        var prefab = Resources.Load<GameObject>(resourcesPath);
        if (prefab == null)
        {
            Debug.LogWarning("[PlayerCharacterCatalog] 缺少角色 prefab: Resources/" + resourcesPath);
            return;
        }

        var option = new Option
        {
            id = id,
            displayName = displayName,
            prefab = prefab,
            resourcesPrefabPath = resourcesPath,
            worldScale = worldScale,
            previewScale = previewScale,
        };
        ApplyMetadataDefaults(option);
        options.Add(option);
    }

    static void ApplyMetadataDefaults(Option option)
    {
        if (option == null)
            return;

        switch (option.id)
        {
            case "hero_female":
                option.displayName = "本科新生";
                option.avatarShortLabel = "本";
                option.worldScale = 1.1f;
                option.bio = "香港中文大学（深圳）本科新生，刚踏入大学校园。性格温和细心，善于观察与倾听，面对新环境既谨慎又充满好奇。身材纤瘦、齐肩黑发，穿着简洁清爽，整体给人干净柔和的第一印象。";
                option.previewScale = 0.52f;
                option.previewYawDegrees = 0f;
                option.previewPitchDegrees = 0f;
                option.previewOffset = Vector3.zero;
                break;
            case "hero_male_a":
                option.displayName = "研一新生";
                option.avatarShortLabel = "研";
                option.worldScale = 1.1f;
                option.bio = "香港中文大学（深圳）硕士研究生新生，已具备一定专业基础。性格开朗果断，沟通直接，遇到任务喜欢迅速推进。身高偏高、身形挺拔，短发利落，常穿休闲得体的日常装扮，显得干练有朝气。";
                option.previewScale = 0.48f;
                option.previewYawDegrees = 0f;
                option.previewPitchDegrees = 0f;
                option.previewOffset = Vector3.zero;
                break;
            case "hero_male_b":
                option.displayName = "博一新生";
                option.avatarShortLabel = "博";
                option.worldScale = 1.1f;
                option.bio = "香港中文大学（深圳）博士研究生新生，学术方向明确。性格沉稳内敛，思考深入，表达简练而有条理。中等身材、轮廓分明，穿搭偏简约正式，举止成熟克制，给人可靠稳重的印象。";
                option.previewScale = 0.48f;
                option.previewYawDegrees = 0f;
                option.previewPitchDegrees = 0f;
                option.previewOffset = Vector3.zero;
                break;
        }
    }

    public static string GetLocalizedDisplayName(Option option)
    {
        if (option == null)
            return "";
        switch (option.id)
        {
            case "hero_female":
                return Loc.Choose("本科新生", "Undergraduate freshman");
            case "hero_male_a":
                return Loc.Choose("研一新生", "Graduate year 1");
            case "hero_male_b":
                return Loc.Choose("博一新生", "PhD year 1");
            default:
                return Loc.Term(option.displayName);
        }
    }

    public static string GetLocalizedBio(Option option)
    {
        if (option == null || string.IsNullOrEmpty(option.bio))
            return Loc.Choose("暂无角色介绍。", "No character bio yet.");
        switch (option.id)
        {
            case "hero_female":
                return Loc.Choose(option.bio,
                    "A CUHK-Shenzhen undergraduate freshman, gentle and observant, curious about campus life.");
            case "hero_male_a":
                return Loc.Choose(option.bio,
                    "A CUHK-Shenzhen master's freshman, outgoing and decisive, prefers to move tasks forward quickly.");
            case "hero_male_b":
                return Loc.Choose(option.bio,
                    "A CUHK-Shenzhen PhD freshman, calm and thoughtful, concise and reliable in manner.");
            default:
                return option.bio;
        }
    }
}
