using System.Collections.Generic;
using Mirror;
using Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

public class InteractableNPC : MonoBehaviour
{
    const string DefaultShopObjectName = "VehicleShopCanvas";
    const string DefaultSpawnPointName = "CarSpawnPoint";

    public GameObject vehicleShopUI;      // 全屏选车界面 Canvas
    public Transform spawnPoint;          // 车辆生成位置
    public VehicleData[] availableVehicles; // 可选车辆列表（Inspector中配置）

    GameObject currentSpawnedVehicle;

    void Awake()
    {
        ResolveSceneReferences();
        BindShop();
        CloseShop();
        UIInteractionState.SetNpcShopOpen(false);
    }

    void OnEnable()
    {
        ResolveSceneReferences();
        BindShop();
        CloseShop();
    }

    public void OpenShop()
    {
        ResolveSceneReferences();
        BindShop();

        var shop = GetShop();
        if (shop != null)
            shop.Show();
        else if (vehicleShopUI != null)
            vehicleShopUI.SetActive(true);
        else
            Debug.LogWarning("[InteractableNPC] 未找到 VehicleShopUI，无法打开选车界面。", this);
    }

    public void CloseShop()
    {
        var shop = GetShop();
        if (shop != null)
            shop.CloseShop();
        else if (vehicleShopUI != null)
            vehicleShopUI.SetActive(false);
        UIInteractionState.SetNpcShopOpen(false);
    }

    public void OnVehicleSelected(VehicleData vehicle)
    {
        int index = ResolveVehicleIndex(vehicle);
        if (index < 0)
        {
            Debug.LogError($"[InteractableNPC] 车辆不在 availableVehicles 中: {vehicle?.vehicleName}", this);
            return;
        }

        if (!ResolveSpawnPoint())
        {
            Debug.LogError("[InteractableNPC] 未设置 spawnPoint，无法生成车辆。", this);
            return;
        }

        CarSystemVehicleNetworkProxy.TryRegisterNetworkPrefab(vehicle.vehiclePrefab);

        if (NetworkClient.active && !NetworkServer.active)
        {
            if (!CoopNetworkPlayer.RequestCarSystemVehicleSpawn(this, index))
                Debug.LogWarning("[InteractableNPC] 联机客机请求生成车辆失败，已回退为仅关闭商店。", this);
            CloseShop();
            return;
        }

        SpawnSelectedVehicleByIndex(index);
        CloseShop();
    }

    public bool SpawnSelectedVehicleByIndex(int index)
    {
        ResolveSceneReferences();
        if (!ResolveSpawnPoint())
            return false;
        if (availableVehicles == null || index < 0 || index >= availableVehicles.Length)
            return false;

        var vehicle = availableVehicles[index];
        if (vehicle == null || vehicle.vehiclePrefab == null)
        {
            Debug.LogError($"[InteractableNPC] 车辆数据无效或未设置预制体: {vehicle?.vehicleName}", this);
            return false;
        }

        if (!PrepareReplaceCurrentVehicle())
            return false;

        GameObject newCar = Instantiate(vehicle.vehiclePrefab, spawnPoint.position, spawnPoint.rotation);
        if (!ConfigureSpawnedVehicle(newCar, vehicle))
        {
            SafeDestroyVehicle(newCar);
            return false;
        }

        currentSpawnedVehicle = newCar;

        if (NetworkServer.active)
        {
            var nid = newCar.GetComponent<NetworkIdentity>();
            if (nid == null)
            {
                Debug.LogError($"[InteractableNPC] 联机生成失败：{vehicle.vehiclePrefab.name} 缺少 NetworkIdentity。请先执行赛车系统一键迁移工具。", this);
                SafeDestroyVehicle(newCar);
                currentSpawnedVehicle = null;
                return false;
            }

            CarSystemVehicleNetworkProxy.TryRegisterNetworkPrefab(vehicle.vehiclePrefab);
            NetworkServer.Spawn(newCar);
        }

        Debug.Log($"[InteractableNPC] 已生成车辆: {vehicle.vehicleName}", this);
        return true;
    }

    public string GetStablePath()
    {
        var parts = new List<string>(8);
        Transform t = transform;
        while (t != null)
        {
            parts.Add(t.name);
            t = t.parent;
        }
        parts.Reverse();
        return string.Join("/", parts);
    }

    public static InteractableNPC FindByStablePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var all = FindObjectsOfType<InteractableNPC>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && string.Equals(all[i].GetStablePath(), path, System.StringComparison.Ordinal))
                return all[i];
        }
        return null;
    }

    void ResolveSceneReferences()
    {
        if (vehicleShopUI == null)
        {
            GameObject shop = FindInLoadedScenes(DefaultShopObjectName);
            if (shop != null)
                vehicleShopUI = shop;
        }

        ResolveSpawnPoint();
    }

    bool ResolveSpawnPoint()
    {
        if (spawnPoint != null)
            return true;

        GameObject point = FindInLoadedScenes(DefaultSpawnPointName);
        if (point != null)
            spawnPoint = point.transform;
        return spawnPoint != null;
    }

    void BindShop()
    {
        if (vehicleShopUI == null)
            return;

        var shop = GetShop();
        if (shop == null)
            return;

        shop.SetNPC(this);
        if (availableVehicles != null && availableVehicles.Length > 0)
            shop.vehicles = new List<VehicleData>(availableVehicles);
    }

    VehicleShopUI GetShop()
    {
        if (vehicleShopUI == null)
            return null;
        return vehicleShopUI.GetComponentInChildren<VehicleShopUI>(true);
    }

    int ResolveVehicleIndex(VehicleData vehicle)
    {
        if (vehicle == null || availableVehicles == null)
            return -1;
        for (int i = 0; i < availableVehicles.Length; i++)
        {
            if (availableVehicles[i] == vehicle)
                return i;
        }
        return -1;
    }

    bool PrepareReplaceCurrentVehicle()
    {
        if (currentSpawnedVehicle == null)
            return true;

        if (NetworkServer.active)
        {
            var nid = currentSpawnedVehicle.GetComponent<NetworkIdentity>();
            if (nid != null)
            {
                NetworkServer.Destroy(currentSpawnedVehicle);
                currentSpawnedVehicle = null;
                return true;
            }
        }

        Destroy(currentSpawnedVehicle);
        currentSpawnedVehicle = null;
        return true;
    }

    bool ConfigureSpawnedVehicle(GameObject newCar, VehicleData vehicle)
    {
        if (newCar == null)
            return false;

        InteractableVehicle iv = newCar.GetComponent<InteractableVehicle>();
        if (iv == null)
            iv = newCar.AddComponent<InteractableVehicle>();

        Carbilili controller = newCar.GetComponent<Carbilili>();
        Rigidbody rb = newCar.GetComponent<Rigidbody>();
        CarCamera cam = newCar.GetComponentInChildren<CarCamera>(true);
        CarSystemVehicleHUD hud = FindObjectOfType<CarSystemVehicleHUD>(true);
        string prefabName = vehicle.vehiclePrefab.name;

        if (controller == null)
        {
            Debug.LogError($"[InteractableNPC] 车辆预制体缺少 Carbilili，已取消生成: {prefabName}", this);
            return false;
        }

        if (rb == null)
        {
            Debug.LogError($"[InteractableNPC] 车辆预制体缺少 Rigidbody，已取消生成: {prefabName}", this);
            return false;
        }

        if (controller.wheelColliders == null || controller.wheelColliders.Length < 4)
        {
            Debug.LogError($"[InteractableNPC] 车辆预制体轮碰撞体配置不足(需要>=4)，已取消生成: {prefabName}", this);
            return false;
        }

        controller.SetLocalInputEnabled(false);
        controller.enabled = false;

        iv.carController = controller;
        iv.carRigidbody = rb;
        iv.carCamera = cam;
        iv.vehicleHud = hud;
        iv.vehicleDisplayName = string.IsNullOrWhiteSpace(vehicle.vehicleName) ? "载具" : vehicle.vehicleName;
        iv.isOccupied = false;
        return true;
    }

    void SafeDestroyVehicle(GameObject vehicle)
    {
        if (vehicle == null)
            return;
        if (Application.isPlaying)
            Destroy(vehicle);
        else
            DestroyImmediate(vehicle);
    }

    static GameObject FindInLoadedScenes(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        for (int si = 0; si < SceneManager.sceneCount; si++)
        {
            var scene = SceneManager.GetSceneAt(si);
            if (!scene.isLoaded)
                continue;

            var roots = scene.GetRootGameObjects();
            for (int ri = 0; ri < roots.Length; ri++)
            {
                Transform found = FindChildByName(roots[ri].transform, objectName);
                if (found != null)
                    return found.gameObject;
            }
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