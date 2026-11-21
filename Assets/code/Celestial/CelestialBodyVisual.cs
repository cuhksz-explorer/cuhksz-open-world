using UnityEngine;

/// <summary>
/// 让太阳/月亮始终面向相机，并使用真实感 CelestialBody 着色器材质。
/// </summary>
[DisallowMultipleComponent]
public class CelestialBodyVisual : MonoBehaviour
{
    public enum BodyKind
    {
        Sun = 0,
        Moon = 1
    }

    [SerializeField] BodyKind bodyKind = BodyKind.Sun;
    [SerializeField] float visualScale = 50f;
    [SerializeField] bool billboardToCamera = true;

    static Shader _cachedShader;
    static Texture2D _moonTexture;

    MeshRenderer _renderer;
    Material _runtimeMaterial;

    public BodyKind Kind
    {
        get => bodyKind;
        set
        {
            bodyKind = value;
            ApplyMaterialSettings();
        }
    }

    public float VisualScale
    {
        get => visualScale;
        set
        {
            visualScale = Mathf.Max(1f, value);
            transform.localScale = Vector3.one * visualScale;
        }
    }

    void LateUpdate()
    {
        if (!billboardToCamera)
            return;

        var cam = Camera.main ?? Camera.current;
        if (cam == null)
            return;

        transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);
    }

    public void Configure(BodyKind kind, float scale)
    {
        bodyKind = kind;
        visualScale = Mathf.Max(1f, scale);
        EnsureVisualMesh();
        ApplyMaterialSettings();
        transform.localScale = Vector3.one * visualScale;
    }

    public void EnsureVisualMesh()
    {
        var meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
            meshFilter = gameObject.AddComponent<MeshFilter>();

        if (meshFilter.sharedMesh == null)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            meshFilter.sharedMesh = sphere.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(sphere);
        }

        _renderer = GetComponent<MeshRenderer>();
        if (_renderer == null)
            _renderer = gameObject.AddComponent<MeshRenderer>();

        var collider = GetComponent<Collider>();
        if (collider != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                Object.DestroyImmediate(collider);
            else
#endif
                Destroy(collider);
        }

        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
        _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        _renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    public void ApplyMaterialSettings()
    {
        EnsureVisualMesh();

        if (_runtimeMaterial == null)
        {
            var shader = GetShader();
            if (shader == null)
                return;
            _runtimeMaterial = new Material(shader);
            _runtimeMaterial.hideFlags = HideFlags.DontSave;
        }

        if (bodyKind == BodyKind.Sun)
        {
            _runtimeMaterial.SetFloat("_IsMoon", 0f);
            _runtimeMaterial.SetColor("_CoreColor", new Color(1f, 0.97f, 0.88f));
            _runtimeMaterial.SetColor("_EdgeColor", new Color(1f, 0.62f, 0.18f));
            _runtimeMaterial.SetFloat("_EmissionStrength", 3.2f);
            _runtimeMaterial.SetFloat("_CoronaSize", 1.4f);
            _runtimeMaterial.SetFloat("_CoronaPower", 2.4f);
            _runtimeMaterial.SetFloat("_Granulation", 0.14f);
        }
        else
        {
            _runtimeMaterial.SetFloat("_IsMoon", 1f);
            var moonTex = GetMoonTexture();
            if (moonTex != null)
                _runtimeMaterial.SetTexture("_MainTex", moonTex);
            _runtimeMaterial.SetColor("_MoonTint", new Color(0.88f, 0.9f, 0.96f));
            _runtimeMaterial.SetColor("_MoonShadowTint", new Color(0.06f, 0.08f, 0.14f));
            _runtimeMaterial.SetFloat("_EmissionStrength", 2.2f);
            _runtimeMaterial.SetFloat("_MoonNormalStrength", 0.9f);
            _runtimeMaterial.SetFloat("_RimPower", 2.8f);
        }

        _renderer.sharedMaterial = _runtimeMaterial;
    }

    static Shader GetShader()
    {
        if (_cachedShader != null)
            return _cachedShader;
        _cachedShader = Shader.Find("Custom/CelestialBody");
        return _cachedShader;
    }

    static Texture2D GetMoonTexture()
    {
        if (_moonTexture != null)
            return _moonTexture;

        _moonTexture = Resources.Load<Texture2D>("Celestial/MoonSurface");
        if (_moonTexture != null)
            return _moonTexture;

#if UNITY_EDITOR
        _moonTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/code/Celestial/Textures/MoonSurface.png");
        if (_moonTexture != null)
            return _moonTexture;
#endif
        return _moonTexture;
    }

    void OnDestroy()
    {
        if (_runtimeMaterial != null)
            Destroy(_runtimeMaterial);
    }
}
