using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// 进入任意玩法场景时，确保存在 SunSimulation + TimeBasedShadowController，并让建筑接收实时阴影。
/// </summary>
public class SceneTimeShadowBootstrap : MonoBehaviour
{
    static SceneTimeShadowBootstrap _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (_instance != null)
            return;

        var go = new GameObject(nameof(SceneTimeShadowBootstrap));
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        _instance = go.AddComponent<SceneTimeShadowBootstrap>();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (ShouldSkipScene(scene.name))
            return;

        EnsureTimeShadowSystem(scene);
        BuildingRealtimeShadowSupport.ApplyToScene(scene);
    }

    static bool ShouldSkipScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return true;
        if (sceneName == "DontDestroyOnLoad")
            return true;
        return sceneName.IndexOf("Loading", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static void EnsureTimeShadowSystem(Scene scene)
    {
        var sunSim = SunSimulation.FindBestInScene(scene) ?? CreateSunSimulation(scene);
        if (sunSim == null)
            return;

        sunSim.EnsureRuntimeDefaults();

        var shadowCtrl = FindInScene<TimeBasedShadowController>(scene);
        if (shadowCtrl == null)
            shadowCtrl = CreateShadowController(scene, sunSim);
        else
            shadowCtrl.sunSimulationObject = sunSim.gameObject;

        EnsureDirectionalLights(scene, shadowCtrl);
    }

    static SunSimulation CreateSunSimulation(Scene scene)
    {
        var root = new GameObject("TimeLightingRoot");
        SceneManager.MoveGameObjectToScene(root, scene);

        var center = new GameObject("SunCenter");
        center.transform.SetParent(root.transform, false);
        center.transform.position = Vector3.zero;

        var sunGo = new GameObject("sun");
        sunGo.transform.SetParent(root.transform, false);
        var sunLight = sunGo.AddComponent<Light>();
        sunLight.type = LightType.Point;
        sunLight.range = 10000f;
        sunLight.shadows = LightShadows.None;

        var sim = sunGo.AddComponent<SunSimulation>();
        sim.centerObject = center.transform;
        sim.dayLengthInSeconds = 60f;
        sim.currentTimeOfDay = 8f;
        sim.sunDistance = 500f;
        sim.moonDistance = 500f;
        sim.celestialVisualScale = 50f;
        sim.EnsureRuntimeDefaults();
        CelestialBodyFactory.UpgradeSunSimulation(sim);

        return sim;
    }

    static TimeBasedShadowController CreateShadowController(Scene scene, SunSimulation sunSim)
    {
        var dirLight = FindDirectionalLight(scene);
        if (dirLight == null)
        {
            var dlGo = new GameObject("Directional Light");
            SceneManager.MoveGameObjectToScene(dlGo, scene);
            dirLight = dlGo.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = new Color(1f, 0.95686275f, 0.8392157f);
            dirLight.intensity = 1f;
            dirLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        Light moonLight = FindMoonLight(scene);
        if (moonLight == null)
        {
            var moonGo = new GameObject("moonlight");
            SceneManager.MoveGameObjectToScene(moonGo, scene);
            moonLight = moonGo.AddComponent<Light>();
            moonLight.type = LightType.Directional;
            moonLight.enabled = false;
            moonLight.intensity = 0.2f;
            moonLight.color = new Color(0.82f, 0.9f, 1f);
            moonLight.shadows = LightShadows.Soft;
        }

        var shadowGo = new GameObject("shadow");
        SceneManager.MoveGameObjectToScene(shadowGo, scene);
        var ctrl = shadowGo.AddComponent<TimeBasedShadowController>();
        ctrl.sunSimulationObject = sunSim.gameObject;
        ctrl.sunLight = dirLight;
        ctrl.moonLight = moonLight;
        return ctrl;
    }

    static void EnsureDirectionalLights(Scene scene, TimeBasedShadowController ctrl)
    {
        if (ctrl == null)
            return;

        if (ctrl.sunLight == null || ctrl.sunLight.type != LightType.Directional)
            ctrl.sunLight = FindDirectionalLight(scene);

        if (ctrl.sunLight != null)
        {
            if (ctrl.sunLight.shadows == LightShadows.None)
                ctrl.sunLight.shadows = LightShadows.Soft;
            LightBakeCompat.SetMixedBakeTypeIfSupported(ctrl.sunLight);
            RenderSettings.sun = ctrl.sunLight;
        }

        if (ctrl.moonLight != null && ctrl.moonLight.shadows == LightShadows.None)
            ctrl.moonLight.shadows = LightShadows.Soft;
    }

    static Light FindDirectionalLight(Scene scene)
    {
        Light best = null;
        foreach (var light in FindAllInScene<Light>(scene))
        {
            if (light == null || light.type != LightType.Directional)
                continue;
            if (light.name.IndexOf("moon", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            if (best == null || light.intensity > best.intensity)
                best = light;
        }
        return best;
    }

    static Light FindMoonLight(Scene scene)
    {
        foreach (var light in FindAllInScene<Light>(scene))
        {
            if (light != null && light.type == LightType.Directional &&
                light.name.IndexOf("moon", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return light;
        }
        return null;
    }

    static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (var item in Object.FindObjectsOfType<T>(true))
        {
            if (item != null && item.gameObject.scene == scene)
                return item;
        }
        return null;
    }

    static System.Collections.Generic.List<T> FindAllInScene<T>(Scene scene) where T : Component
    {
        var list = new System.Collections.Generic.List<T>();
        foreach (var item in Object.FindObjectsOfType<T>(true))
        {
            if (item != null && item.gameObject.scene == scene)
                list.Add(item);
        }
        return list;
    }
}
