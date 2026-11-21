#if UNITY_EDITOR
using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 一键把当前场景或 Assets/scene 下全部场景切到 Carsystem 主线：
/// - 创建/复用 VehicleDealerNPC、VehicleShopCanvas、Carcanvas、CarSpawnPoint
/// - 自动绑定 InteractableNPC / VehicleDealerNPC / VehicleShopUI
/// - 批量关闭旧系统 VehicleController 载具
/// - 批量给 Carsystem 车辆预制体补齐联机组件
/// </summary>
public static class CarSystemReplacementOneClickSetup
{
    const string MenuRoot = "Tools/赛车/系统二替换系统一/";
    const string MenuCurrentScene = MenuRoot + "一键配置当前场景";
    const string MenuAllScenes = MenuRoot + "批量配置 Assets/scene 全部场景";

    const string VehicleDealerPrefabPath = "Assets/Carsystem/Canvass/VehicleDealerNPC.prefab";
    const string VehicleShopPrefabPath = "Assets/Carsystem/Canvass/VehicleShopCanvas.prefab";
    const string CarHudPrefabPath = "Assets/Carsystem/Canvass/Carcanvas.prefab";
    const string SpawnPointPrefabPath = "Assets/Carsystem/Canvass/CarSpawnPoint.prefab";
    const string SceneRootFolder = "Assets/scene";
    const string CarsPrefabFolder = "Assets/Carsystem/CarPrefabs";

    [MenuItem(MenuCurrentScene, false, 1)]
    public static void ConfigureCurrentScene()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("赛车系统替换", "请在编辑模式下执行。", "确定");
            return;
        }

        ConfigureCarsystemPrefabs();
        ConfigureScene(SceneManager.GetActiveScene(), saveScene: true);
        EditorUtility.DisplayDialog("赛车系统替换", "当前场景已切换到 Carsystem 主线配置。", "确定");
    }

    [MenuItem(MenuAllScenes, false, 2)]
    public static void ConfigureAllScenes()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("赛车系统替换", "请在编辑模式下执行。", "确定");
            return;
        }

        ConfigureCarsystemPrefabs();

        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { SceneRootFolder });
        string originalScenePath = SceneManager.GetActiveScene().path;

        for (int i = 0; i < sceneGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            ConfigureScene(scene, saveScene: true);
        }

        if (!string.IsNullOrWhiteSpace(originalScenePath))
            EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);

        EditorUtility.DisplayDialog("赛车系统替换", "Assets/scene 下所有场景已完成 Carsystem 替换配置。", "确定");
    }

    static void ConfigureScene(Scene scene, bool saveScene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        var vehicleDataAssets = LoadVehicleDataAssets();
        Vector3 anchorPos = FindLegacyVehicleAnchor(scene, out var legacyVehicles);

        GameObject shopRoot = EnsurePrefabInScene(scene, VehicleShopPrefabPath, "VehicleShopCanvas");
        GameObject hudRoot = EnsurePrefabInScene(scene, CarHudPrefabPath, "Carcanvas");
        GameObject npcRoot = EnsurePrefabInScene(scene, VehicleDealerPrefabPath, "VehicleDealerNPC");
        Transform spawnPoint = EnsureSpawnPoint(scene, anchorPos);

        if (npcRoot != null)
        {
            if (npcRoot.transform.position == Vector3.zero && anchorPos != Vector3.zero)
                npcRoot.transform.position = anchorPos + new Vector3(-4f, 0f, 0f);
            else if (npcRoot.transform.position == Vector3.zero)
                npcRoot.transform.position = new Vector3(-4f, 0f, 0f);
        }

        if (spawnPoint != null && spawnPoint.position == Vector3.zero && anchorPos != Vector3.zero)
            spawnPoint.position = anchorPos;

        ConfigureNpcBindings(npcRoot, shopRoot, spawnPoint, vehicleDataAssets);
        ConfigureShopBindings(shopRoot, npcRoot != null ? npcRoot.GetComponent<InteractableNPC>() : null, vehicleDataAssets);
        ConfigureHudBindings(hudRoot);
        DisableLegacyVehicles(legacyVehicles);

        EnsureSharedInteractPromptForScene(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        if (saveScene)
            EditorSceneManager.SaveScene(scene);
    }

    static void ConfigureCarsystemPrefabs()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { CarsPrefabFolder });
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            var root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;

            var car = root.GetComponent<InteractableVehicle>();
            if (car == null)
            {
                car = root.AddComponent<InteractableVehicle>();
                changed = true;
            }

            if (car.interactDistance != InteractableVehicle.DefaultInteractDistance)
            {
                car.interactDistance = InteractableVehicle.DefaultInteractDistance;
                changed = true;
            }
            if (car.distanceBuffer != InteractableVehicle.DefaultDistanceBuffer)
            {
                car.distanceBuffer = InteractableVehicle.DefaultDistanceBuffer;
                changed = true;
            }

            var controller = root.GetComponent<Carbilili>();
            if (controller != null)
            {
                if (car.carController != controller)
                {
                    car.carController = controller;
                    changed = true;
                }
            }

            var rb = root.GetComponent<Rigidbody>();
            if (rb != null && car.carRigidbody != rb)
            {
                car.carRigidbody = rb;
                changed = true;
            }

            var cam = root.GetComponentInChildren<CarCamera>(true);
            if (cam != null && car.carCamera != cam)
            {
                car.carCamera = cam;
                changed = true;
            }

            if (root.GetComponent<NetworkIdentity>() == null)
            {
                root.AddComponent<NetworkIdentity>();
                changed = true;
            }

            var nt = root.GetComponent<NetworkTransformReliable>();
            if (nt == null)
            {
                nt = root.AddComponent<NetworkTransformReliable>();
                changed = true;
            }
            if (nt != null && nt.syncDirection != SyncDirection.ClientToServer)
            {
                nt.syncDirection = SyncDirection.ClientToServer;
                changed = true;
            }

            if (root.GetComponent<CarSystemVehicleNetworkProxy>() == null)
            {
                root.AddComponent<CarSystemVehicleNetworkProxy>();
                changed = true;
            }

            if (root.GetComponent<CarSystemVehicleFlipRescue>() == null)
            {
                root.AddComponent<CarSystemVehicleFlipRescue>();
                changed = true;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("[赛车系统替换] 已升级 Carsystem 车辆预制体: " + path);
            }

            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static List<VehicleData> LoadVehicleDataAssets()
    {
        var list = new List<VehicleData>();
        string[] guids = AssetDatabase.FindAssets("t:VehicleData", new[] { "Assets/Carsystem" });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var asset = AssetDatabase.LoadAssetAtPath<VehicleData>(path);
            if (asset != null)
                list.Add(asset);
        }
        return list;
    }

    static Vector3 FindLegacyVehicleAnchor(Scene scene, out List<VehicleController> legacyVehicles)
    {
        legacyVehicles = new List<VehicleController>();
        var all = Object.FindObjectsOfType<VehicleController>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].gameObject.scene == scene)
                legacyVehicles.Add(all[i]);
        }

        if (legacyVehicles.Count > 0)
            return legacyVehicles[0].transform.position;
        return Vector3.zero;
    }

    static void DisableLegacyVehicles(List<VehicleController> legacyVehicles)
    {
        for (int i = 0; i < legacyVehicles.Count; i++)
        {
            var vc = legacyVehicles[i];
            if (vc == null)
                continue;

            if (vc.gameObject.activeSelf)
            {
                vc.gameObject.SetActive(false);
                EditorUtility.SetDirty(vc.gameObject);
                Debug.Log("[赛车系统替换] 已停用旧系统车辆: " + vc.gameObject.name);
            }
        }
    }

    static GameObject EnsurePrefabInScene(Scene scene, string prefabPath, string objectName)
    {
        GameObject existing = FindInScene(scene, objectName);
        if (existing != null)
            return existing;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("[赛车系统替换] 未找到预制体: " + prefabPath);
            return null;
        }

        var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (instance != null)
            instance.name = objectName;
        return instance;
    }

    static Transform EnsureSpawnPoint(Scene scene, Vector3 anchorPos)
    {
        GameObject existing = FindInScene(scene, "CarSpawnPoint");
        if (existing != null)
            return existing.transform;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpawnPointPrefabPath);
        GameObject instance = null;
        if (prefab != null)
            instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;

        if (instance == null)
        {
            instance = new GameObject("CarSpawnPoint");
            SceneManager.MoveGameObjectToScene(instance, scene);
        }

        if (anchorPos != Vector3.zero)
            instance.transform.position = anchorPos;
        return instance.transform;
    }

    static void ConfigureNpcBindings(GameObject npcRoot, GameObject shopRoot, Transform spawnPoint, List<VehicleData> vehicles)
    {
        if (npcRoot == null)
            return;

        var npc = npcRoot.GetComponent<InteractableNPC>();
        if (npc == null)
            npc = npcRoot.AddComponent<InteractableNPC>();

        npc.vehicleShopUI = shopRoot;
        npc.spawnPoint = spawnPoint;
        npc.availableVehicles = vehicles.ToArray();
        EditorUtility.SetDirty(npc);

        var dealer = npcRoot.GetComponent<VehicleDealerNPC>();
        if (dealer == null)
            dealer = npcRoot.AddComponent<VehicleDealerNPC>();

        dealer.interactableNpc = npc;
        dealer.vehicleShopCanvas = shopRoot;
        dealer.disableSelfControl = true;
        dealer.unifiedInteractKey = KeyCode.F;
        EditorUtility.SetDirty(dealer);

        var mode2 = npcRoot.GetComponent<InteractWithMouseAndMovementLock>();
        if (mode2 != null)
        {
            mode2.enabled = false;
            mode2.proximityTargets = System.Array.Empty<InteractProximityTarget>();
            EditorUtility.SetDirty(mode2);
        }
    }

    static void ConfigureShopBindings(GameObject shopRoot, InteractableNPC npc, List<VehicleData> vehicles)
    {
        if (shopRoot == null)
            return;

        var shop = shopRoot.GetComponentInChildren<VehicleShopUI>(true);
        if (shop == null)
            return;

        shop.SetNPC(npc);
        shop.vehicles = new List<VehicleData>(vehicles);
        EditorUtility.SetDirty(shop);
    }

    static void ConfigureHudBindings(GameObject hudRoot)
    {
        if (hudRoot == null)
            return;

        var hud = hudRoot.GetComponent<CarSystemVehicleHUD>();
        if (hud == null)
            hud = hudRoot.AddComponent<CarSystemVehicleHUD>();

        if (InteractionPromptOneClickSetup.TryBindExistingSharedPanelInScene(hudRoot.scene))
        {
            // TryBind 已绑定全部消费者（含 CarSystemVehicleHUD）
        }
        else if (TryFindSharedTmpInScene(hudRoot.scene, out var panel, out var nameTmp, out var typeTmp))
        {
            hud.sharedPromptPanel = panel;
            hud.sharedPromptNameText = nameTmp;
            hud.sharedPromptTypeText = typeTmp;
            hud.preferSharedPrompt = true;
            hud.autoFindSharedPrompt = true;
        }

        EditorUtility.SetDirty(hud);
    }

    /// <summary>已有共用面板则只绑定；没有且存在交互器时才创建（避免赛车配置再建一套）。</summary>
    static void EnsureSharedInteractPromptForScene(Scene scene)
    {
        if (FindInScene(scene, InteractPromptSharedUiLocator.PanelObjectName) != null)
        {
            InteractionPromptOneClickSetup.TryBindExistingSharedPanelInScene(scene);
            return;
        }

        if (Object.FindObjectOfType<InteractWithMouseAndMovementLock>(true) == null
            || Object.FindObjectOfType<Canvas>(true) == null)
            return;

        EditorApplication.ExecuteMenuItem("Tools/UI/Interaction Prompt/一键配置交互提示（主 UI · 多物体共用）");
    }

    static bool TryFindSharedTmpInScene(
        Scene scene,
        out RectTransform panel,
        out TextMeshProUGUI nameTmp,
        out TextMeshProUGUI typeTmp)
    {
        panel = null;
        nameTmp = null;
        typeTmp = null;
        GameObject go = FindInScene(scene, InteractPromptSharedUiLocator.PanelObjectName);
        if (go == null)
            return false;
        panel = go.GetComponent<RectTransform>();
        foreach (var tmp in go.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (string.Equals(tmp.name, "InteractNameText", System.StringComparison.Ordinal))
                nameTmp = tmp;
            if (string.Equals(tmp.name, "InteractTypeText", System.StringComparison.Ordinal))
                typeTmp = tmp;
        }
        return panel != null && nameTmp != null && typeTmp != null;
    }

    static GameObject FindInScene(Scene scene, string objectName)
    {
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(objectName))
            return null;

        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindChildByName(roots[i].transform, objectName);
            if (found != null)
                return found.gameObject;
        }
        return null;
    }

    static Transform FindChildByName(Transform root, string objectName)
    {
        if (root == null)
            return null;
        if (string.Equals(root.name, objectName, System.StringComparison.Ordinal))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByName(root.GetChild(i), objectName);
            if (found != null)
                return found;
        }
        return null;
    }
}
#endif
