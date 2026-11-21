using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 挂在每棵需要优化的树（或父物体）上：按与主相机的距离关闭阴影、可选切换低模、再远则完全关闭渲染。
/// 由 <see cref="VegetationCullSystem"/> 统一在每帧一次循环中处理，避免大量 LateUpdate。
/// </summary>
[DisallowMultipleComponent]
public class VegetationDistanceCuller : MonoBehaviour
{
    [Header("距离（米，世界空间）")]
    [Tooltip("超过此距离关闭投影（仍绘制高精度网格，减轻阴影 Pass）")]
    public float shadowOffDistance = 32f;

    [Tooltip("超过此距离只显示「低模代理」，关闭高精度渲染（需指定 Low Poly Proxy）")]
    public float proxySwapDistance = 48f;

    [Tooltip("超过此距离关闭高精度与代理，不再绘制")]
    public float meshHideDistance = 75f;

    [Header("低模代理（可选）")]
    [Tooltip("一般为当前物体下的子物体：几个三角面的圆柱/贴图片即可；须为子层级以便自动从高精度里排除")]
    public GameObject lowPolyProxy;

    [Header("滞回")]
    [Tooltip("重新显示/恢复阴影时使用的比例，略小于 1 可避免在边界反复开关")]
    [Range(0.82f, 0.98f)]
    public float hysteresis = 0.9f;

    [Header("目标")]
    [Tooltip("为空则使用 Camera.main")]
    public Camera targetCamera;

    MeshRenderer[] _highDetailRenderers;
    MeshRenderer[] _proxyRenderers;
    ShadowCastingMode[] _originalShadowModes;
    bool _cached;
    bool _meshHidden;
    bool _proxyMode;
    bool _shadowStripped;

    void Awake()
    {
        CacheRenderers();
    }

    void OnEnable()
    {
        CacheRenderers();
        VegetationCullSystem.Register(this);
    }

    void OnDisable()
    {
        VegetationCullSystem.Unregister(this);
        RestoreAll();
    }

    void CacheRenderers()
    {
        if (_cached) return;

        if (lowPolyProxy != null)
        {
            var all = GetComponentsInChildren<MeshRenderer>(true);
            var high = new List<MeshRenderer>(all.Length);
            Transform proxyRoot = lowPolyProxy.transform;
            for (int i = 0; i < all.Length; i++)
            {
                var r = all[i];
                if (r == null) continue;
                if (r.transform == proxyRoot || r.transform.IsChildOf(proxyRoot))
                    continue;
                high.Add(r);
            }

            _highDetailRenderers = high.ToArray();
            _proxyRenderers = lowPolyProxy.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < _proxyRenderers.Length; i++)
            {
                if (_proxyRenderers[i] != null)
                    _proxyRenderers[i].enabled = false;
            }
        }
        else
        {
            _highDetailRenderers = GetComponentsInChildren<MeshRenderer>(true);
            _proxyRenderers = System.Array.Empty<MeshRenderer>();
        }

        _originalShadowModes = new ShadowCastingMode[_highDetailRenderers.Length];
        for (int i = 0; i < _highDetailRenderers.Length; i++)
            _originalShadowModes[i] = _highDetailRenderers[i].shadowCastingMode;

        _cached = true;
    }

    internal void Evaluate(Camera cam)
    {
        if (!isActiveAndEnabled || !_cached)
            return;

        bool hasProxy = lowPolyProxy != null && _proxyRenderers != null && _proxyRenderers.Length > 0;
        if ((_highDetailRenderers == null || _highDetailRenderers.Length == 0) && !hasProxy)
            return;

        if (cam == null) cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null) return;

        Vector3 p = transform.position;
        float d2 = (cam.transform.position - p).sqrMagnitude;

        float hideSq = meshHideDistance * meshHideDistance;
        float hideInSq = meshHideDistance * hysteresis * (meshHideDistance * hysteresis);
        float shadowSq = shadowOffDistance * shadowOffDistance;
        float shadowInSq = shadowOffDistance * hysteresis * (shadowOffDistance * hysteresis);
        float proxySq = proxySwapDistance * proxySwapDistance;
        float proxyInSq = proxySwapDistance * hysteresis * (proxySwapDistance * hysteresis);

        if (_meshHidden)
        {
            if (d2 > hideInSq)
                return;
            _meshHidden = false;
            if (hasProxy && d2 >= proxySq)
            {
                EnterProxyMode();
                SetHighVisible(false);
                SetProxyVisible(true);
                return;
            }

            LeaveProxyMode();
        }

        if (d2 >= hideSq)
        {
            SetHighVisible(false);
            SetProxyVisible(false);
            _meshHidden = true;
            _proxyMode = false;
            _shadowStripped = false;
            return;
        }

        if (hasProxy)
        {
            if (_proxyMode)
            {
                if (d2 < proxyInSq)
                    LeaveProxyMode();
            }
            else if (d2 >= proxySq)
                EnterProxyMode();

            if (_proxyMode)
            {
                SetHighVisible(false);
                SetProxyVisible(true);
                return;
            }

            SetHighVisible(true);
            SetProxyVisible(false);
        }
        else
        {
            SetHighVisible(true);
        }

        if (_shadowStripped)
        {
            if (d2 <= shadowInSq)
            {
                RestoreShadows();
                _shadowStripped = false;
            }
        }
        else if (d2 >= shadowSq)
        {
            StripShadows();
            _shadowStripped = true;
        }
    }

    void EnterProxyMode()
    {
        _proxyMode = true;
    }

    void LeaveProxyMode()
    {
        _proxyMode = false;
    }

    void SetHighVisible(bool visible)
    {
        if (_highDetailRenderers == null) return;
        for (int i = 0; i < _highDetailRenderers.Length; i++)
        {
            if (_highDetailRenderers[i] != null)
                _highDetailRenderers[i].enabled = visible;
        }
    }

    void SetProxyVisible(bool visible)
    {
        if (_proxyRenderers == null) return;
        for (int i = 0; i < _proxyRenderers.Length; i++)
        {
            if (_proxyRenderers[i] != null)
                _proxyRenderers[i].enabled = visible;
        }
    }

    void StripShadows()
    {
        for (int i = 0; i < _highDetailRenderers.Length; i++)
        {
            if (_highDetailRenderers[i] == null) continue;
            _highDetailRenderers[i].shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    void RestoreShadows()
    {
        for (int i = 0; i < _highDetailRenderers.Length; i++)
        {
            if (_highDetailRenderers[i] == null) continue;
            _highDetailRenderers[i].shadowCastingMode = _originalShadowModes[i];
        }
    }

    void RestoreAll()
    {
        if (!_cached) return;
        SetHighVisible(true);
        SetProxyVisible(false);
        RestoreShadows();
        _meshHidden = false;
        _proxyMode = false;
        _shadowStripped = false;
    }
}

/// <summary>
/// 首次有 <see cref="VegetationDistanceCuller"/> 启用时自动创建，负责每帧一次遍历所有注册对象。
/// 也可手动拖到任意物体上，避免运行时动态创建。
/// </summary>
[DefaultExecutionOrder(-200)]
public class VegetationCullSystem : MonoBehaviour
{
    static VegetationCullSystem _instance;
    static readonly List<VegetationDistanceCuller> Registered = new List<VegetationDistanceCuller>(256);

    public static void Register(VegetationDistanceCuller c)
    {
        if (c == null) return;
        if (!Registered.Contains(c))
            Registered.Add(c);
        EnsureInstance();
    }

    public static void Unregister(VegetationDistanceCuller c)
    {
        if (c == null) return;
        Registered.Remove(c);
    }

    static void EnsureInstance()
    {
        if (_instance != null) return;
        var existing = FindObjectOfType<VegetationCullSystem>(true);
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        var go = new GameObject(nameof(VegetationCullSystem));
        _instance = go.AddComponent<VegetationCullSystem>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }
        _instance = this;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void Update()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            for (int i = 0; i < Registered.Count; i++)
            {
                var c = Registered[i];
                if (c == null) continue;
                cam = c.targetCamera;
                if (cam != null) break;
            }
        }

        if (cam == null) return;

        for (int i = Registered.Count - 1; i >= 0; i--)
        {
            var c = Registered[i];
            if (c == null)
            {
                Registered.RemoveAt(i);
                continue;
            }
            c.Evaluate(cam);
        }
    }
}

/// <summary>
/// 挂在主相机（或指定相机）上：对某一整层使用「最大绘制距离」，由引擎在裁剪阶段丢弃远景物体，面数下降明显且几乎不占 CPU。
/// 使用前请在 Project Settings → Tags and Layers 新建层（如 Vegetation），把所有树设到该层，并确保相机 Culling Mask 包含该层。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class VegetationCameraLayerCull : MonoBehaviour
{
    [Tooltip("对该层应用最大可见距离（米）。0 表示该层仍用相机 far clip")]
    public string layerName = "Vegetation";

    public float maxDrawDistance = 55f;

    [Tooltip("true：按到相机的球面距离裁剪（通常更合理）")]
    public bool sphericalCull = true;

    [Tooltip("是否在 OnEnable 时再次写入（切场景/动态开关相机时可勾选）")]
    public bool applyOnEnable = true;

    void OnEnable()
    {
        if (applyOnEnable)
            ApplyLayerCull();
    }

    [ContextMenu("Apply Layer Cull Now")]
    public void ApplyLayerCull()
    {
        var cam = GetComponent<Camera>();
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0 || layer > 31)
        {
            Debug.LogWarning($"[VegetationCameraLayerCull] 未找到层「{layerName}」，请在 Layers 里添加并赋值。");
            return;
        }

        if ((cam.cullingMask & (1 << layer)) == 0)
            Debug.LogWarning($"[VegetationCameraLayerCull] 相机未勾选层「{layerName}」，裁剪无效。");

        float[] distances = new float[32];
        if (cam.layerCullDistances != null && cam.layerCullDistances.Length == 32)
            System.Array.Copy(cam.layerCullDistances, distances, 32);

        distances[layer] = Mathf.Max(0.01f, maxDrawDistance);
        cam.layerCullDistances = distances;
        cam.layerCullSpherical = sphericalCull;
    }
}
