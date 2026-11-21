using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 进入世界场景后，将已选主角模型应用到 Tag=Player 的对象上。
/// 挂在 DontDestroyOnLoad 的 GameServer_Network 上即可跨场景生效。
/// </summary>
public class PlayerCharacterApplier : MonoBehaviour
{
    static readonly string[] SkipSceneNames = { "Loadingscene", "MainMenu", "Menu" };

    [SerializeField] PlayerCharacterCatalog catalog;
    [SerializeField] bool applyOnSceneLoaded = true;

    const string VisualRootName = "PlayerCharacterVisual";
    const int ApplyRetryFrames = 12;

    void Awake()
    {
        if (catalog == null)
            catalog = PlayerCharacterCatalog.LoadDefault();
    }

    void OnEnable()
    {
        if (applyOnSceneLoaded)
            SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        if (!applyOnSceneLoaded)
            return;

        var scene = SceneManager.GetActiveScene();
        if (ShouldSkipScene(scene.name))
            return;

        StartCoroutine(CoApplyUntilReady());
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (ShouldSkipScene(scene.name))
            return;

        StartCoroutine(CoApplyUntilReady());
    }

    IEnumerator CoApplyUntilReady()
    {
        for (int i = 0; i < ApplyRetryFrames; i++)
        {
            if (TryApplyToPlayerInScene())
                yield break;
            yield return null;
        }

        Debug.LogWarning("[PlayerCharacterApplier] 多次尝试后仍未成功换模，已恢复场景烘焙模型显示。");
        RestoreLegacyRenderers(GameObject.FindGameObjectWithTag("Player")?.transform);
    }

    string ResolveCharacterId()
    {
        string characterId = GameServerSync.Instance != null
            ? GameServerSync.Instance.SavedCharacterId
            : PlayerPrefs.GetString("GameServer_CharacterId_v1", "");
        if (!string.IsNullOrEmpty(characterId))
            return characterId;

        if (catalog == null)
            catalog = PlayerCharacterCatalog.LoadDefault();
        var fallback = catalog != null ? catalog.GetDefault() : null;
        return fallback != null ? fallback.id : "hero_female";
    }

    static bool ShouldSkipScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return true;
        for (int i = 0; i < SkipSceneNames.Length; i++)
        {
            if (string.Equals(sceneName, SkipSceneNames[i], System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public void ApplyToPlayerInScene()
    {
        TryApplyToPlayerInScene();
    }

    bool TryApplyToPlayerInScene()
    {
        if (catalog == null)
            catalog = PlayerCharacterCatalog.LoadDefault();
        if (catalog == null)
            return false;

        catalog.EnsureDefaultOptionsIfEmpty();

        string characterId = ResolveCharacterId();
        if (string.IsNullOrEmpty(characterId))
            return false;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return false;

        return ApplyVisual(player.transform, characterId);
    }

    public bool ApplyVisual(Transform player, string characterId)
    {
        if (player == null || catalog == null)
            return false;

        var option = catalog.GetById(characterId) ?? catalog.GetDefault();
        if (option == null)
        {
            Debug.LogWarning("[PlayerCharacterApplier] 未找到角色配置: " + (characterId ?? "?"));
            RestoreLegacyRenderers(player);
            return false;
        }

        var prefab = catalog.LoadPrefab(option);
        if (prefab == null)
        {
            Debug.LogWarning("[PlayerCharacterApplier] 未找到角色 prefab: " + (option.id ?? "?"));
            RestoreLegacyRenderers(player);
            return false;
        }

        if (!HasEnabledRenderer(prefab))
        {
            Debug.LogWarning("[PlayerCharacterApplier] prefab 没有可用 Renderer: " + prefab.name);
            RestoreLegacyRenderers(player);
            return false;
        }

        ClearExistingVisual(player);

        var visualRoot = new GameObject(VisualRootName);
        visualRoot.transform.SetParent(player, false);
        visualRoot.transform.localPosition = Vector3.zero;
        visualRoot.transform.localRotation = Quaternion.identity;

        var instance = Instantiate(prefab, visualRoot.transform);
        instance.name = prefab.name;
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.Euler(0f, option.previewYawDegrees, 0f);
        instance.transform.localScale = Vector3.one * Mathf.Max(0.01f, option.worldScale);
        PlayerCharacterCatalog.PreparePrefabInstance(instance, option);
        EnsureRenderersEnabled(instance);

        if (!HasEnabledRenderer(instance))
        {
            Debug.LogWarning("[PlayerCharacterApplier] 实例化后仍无可见 Renderer: " + prefab.name);
            Destroy(visualRoot);
            RestoreLegacyRenderers(player);
            return false;
        }

        DisableLegacyRenderers(player);

        var marker = visualRoot.AddComponent<PlayerCharacterVisualMarker>();
        marker.characterId = option.id;

        if (player.GetComponent<PlayerCharacterSceneRoot>() == null)
            player.gameObject.AddComponent<PlayerCharacterSceneRoot>();

        var controller = PlayerCharacterAnimationUtility.LoadMovementController(catalog);
        PlayerCharacterAnimationUtility.BindMovementAnimator(player, instance, controller);

        var movement = player.GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.RefreshFirstPersonCameraBinding();
            movement.StartCoroutine(CoRefreshCameraAfterVisual(movement));
        }

        Debug.Log("[PlayerCharacterApplier] 已应用角色: " + option.displayName + " (" + option.id + ")");
        return true;
    }

    static System.Collections.IEnumerator CoRefreshCameraAfterVisual(PlayerMovement movement)
    {
        if (movement == null)
            yield break;
        yield return null;
        movement.RefreshFirstPersonCameraBinding();
        yield return null;
        movement.RefreshFirstPersonCameraBinding();
    }

    static bool HasEnabledRenderer(GameObject root)
    {
        if (root == null)
            return false;
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].enabled)
                return true;
        }
        return renderers.Length > 0;
    }

    static void EnsureRenderersEnabled(GameObject root)
    {
        if (root == null)
            return;
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = true;
        }
    }

    static void ClearExistingVisual(Transform player)
    {
        for (int i = player.childCount - 1; i >= 0; i--)
        {
            var child = player.GetChild(i);
            if (child == null)
                continue;
            if (child.GetComponent<PlayerCharacterVisualMarker>() != null
                || string.Equals(child.name, VisualRootName, System.StringComparison.Ordinal))
                Destroy(child.gameObject);
        }
    }

    static void DisableLegacyRenderers(Transform player)
    {
        if (player == null)
            return;

        var renderers = player.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null || IsProtectedTransform(r.transform, player))
                continue;
            if (IsUnderPlayerCharacterVisual(r.transform, player))
                continue;
            r.enabled = false;
        }
    }

    static void RestoreLegacyRenderers(Transform player)
    {
        if (player == null)
            return;

        var renderers = player.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null || IsProtectedTransform(r.transform, player))
                continue;
            if (IsUnderPlayerCharacterVisual(r.transform, player))
                continue;
            r.enabled = true;
        }
    }

    static bool IsUnderPlayerCharacterVisual(Transform t, Transform playerRoot)
    {
        while (t != null && t != playerRoot)
        {
            if (string.Equals(t.name, VisualRootName, System.StringComparison.Ordinal))
                return true;
            if (t.GetComponent<PlayerCharacterVisualMarker>() != null)
                return true;
            t = t.parent;
        }
        return false;
    }

    static bool IsProtectedTransform(Transform t, Transform playerRoot)
    {
        while (t != null && t != playerRoot)
        {
            string n = t.name ?? "";
            if (n.IndexOf("Camera", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (t.GetComponent<Camera>() != null)
                return true;
            t = t.parent;
        }
        return false;
    }
}

public class PlayerCharacterVisualMarker : MonoBehaviour
{
    public string characterId;
}
