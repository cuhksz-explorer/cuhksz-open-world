using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 选车界面 3D 预览：离屏 stage + RenderTexture + 拖拽旋转 / 滚轮缩放。
/// </summary>
public class VehicleShopPreview3D : MonoBehaviour
{
    public static readonly Color PreviewBackground = new Color(0.14f, 0.14f, 0.14f, 1f);

    const float PreviewLookTargetLocalY = 0.55f;
    const int PreviewRenderWidth = 960;
    const int PreviewRenderHeight = 540;
    const float PreviewTargetMaxExtent = 4f;
    const float PreviewCameraDistance = 7f;
    const float PreviewDragSensitivity = 0.45f;
    const float PreviewZoomStep = 0.08f;
    const float PreviewZoomMin = 0.65f;
    const float PreviewZoomMax = 1.45f;

    [SerializeField] RawImage previewImage;

    Transform _previewStageRoot;
    Transform _previewLookTarget;
    Camera _previewCamera;
    RenderTexture _previewTexture;
    GameObject _previewInstance;
    VehicleData _currentData;
    float _previewYawOffset;
    float _previewZoom = 1f;
    bool _stagePersistent;

    public RawImage PreviewImage => previewImage;

    public void BindPreviewImage(RawImage image)
    {
        previewImage = image;
        BindPreviewTexture();
    }

    void OnDestroy()
    {
        DestroyPreviewStage(releaseTexture: true);
    }

    public void ShowVehicle(VehicleData data)
    {
        if (data == null || data.vehiclePrefab == null)
        {
            ClearPreview();
            return;
        }

        EnsurePreviewStage();
        if (_currentData != data)
            ResetPreviewInteraction();

        _currentData = data;

        if (_previewInstance != null)
            Destroy(_previewInstance);

        _previewInstance = Instantiate(data.vehiclePrefab, _previewStageRoot);
        PreparePreviewInstance(_previewInstance);
        int layer = _previewCamera != null ? _previewCamera.gameObject.layer : 0;
        SetLayerRecursively(_previewInstance, layer);
        FitPreviewModel(_previewInstance, data);
        ApplyPreviewView();
    }

    public void ClearPreview()
    {
        _currentData = null;
        if (_previewInstance != null)
        {
            Destroy(_previewInstance);
            _previewInstance = null;
        }
    }

    public void ReleaseWhenShopClosed()
    {
        ClearPreview();
        ResetPreviewInteraction();
    }

    internal void HandlePreviewDrag(float deltaX)
    {
        if (_previewInstance == null)
            return;
        _previewYawOffset -= deltaX * PreviewDragSensitivity;
        ApplyPreviewView();
    }

    internal void HandlePreviewScroll(float scrollY)
    {
        if (_previewInstance == null || Mathf.Approximately(scrollY, 0f))
            return;
        _previewZoom = Mathf.Clamp(_previewZoom + scrollY * PreviewZoomStep, PreviewZoomMin, PreviewZoomMax);
        ApplyPreviewView();
    }

    void ResetPreviewInteraction()
    {
        _previewYawOffset = 0f;
        _previewZoom = 1f;
    }

    void ApplyPreviewView()
    {
        if (_previewInstance == null || _currentData == null)
            return;

        _previewInstance.transform.localRotation = Quaternion.Euler(
            _currentData.previewPitchDegrees,
            _currentData.previewYawDegrees + _previewYawOffset,
            0f);

        if (_previewCamera != null)
        {
            float distance = PreviewCameraDistance / _previewZoom;
            _previewCamera.transform.localPosition = new Vector3(0f, PreviewLookTargetLocalY + 0.08f, distance);
            if (_previewLookTarget != null)
                _previewCamera.transform.LookAt(_previewLookTarget);
        }

        RenderPreviewFrame();
    }

    void FitPreviewModel(GameObject modelRoot, VehicleData data)
    {
        if (modelRoot == null || data == null)
            return;

        modelRoot.transform.localRotation = Quaternion.Euler(
            data.previewPitchDegrees,
            data.previewYawDegrees,
            0f);
        modelRoot.transform.localScale = Vector3.one;
        modelRoot.transform.localPosition = Vector3.zero;

        var renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            modelRoot.transform.localScale = Vector3.one * Mathf.Max(0.01f, data.previewScale);
            modelRoot.transform.localPosition = data.previewOffset;
            return;
        }

        float fitScale = data.previewScale;
        if (TryGetWorldBounds(renderers, out Bounds worldBounds))
        {
            float maxExtent = Mathf.Max(worldBounds.size.x, worldBounds.size.y, worldBounds.size.z);
            maxExtent = Mathf.Max(maxExtent, 0.01f);
            fitScale = (PreviewTargetMaxExtent / maxExtent) * Mathf.Max(0.01f, data.previewScale);
        }

        modelRoot.transform.localScale = Vector3.one * fitScale;
        AlignModelToPreviewCenter(modelRoot, data, renderers);
    }

    void AlignModelToPreviewCenter(GameObject modelRoot, VehicleData data, Renderer[] renderers)
    {
        if (renderers == null || renderers.Length == 0)
            return;

        Bounds worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                worldBounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 targetWorld = _previewStageRoot.position
            + new Vector3(0f, PreviewLookTargetLocalY, 0f)
            + data.previewOffset;

        if (worldBounds.size.sqrMagnitude >= 0.0001f)
            modelRoot.transform.position += targetWorld - worldBounds.center;
    }

    static bool TryGetWorldBounds(Renderer[] renderers, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;
            if (!hasBounds)
            {
                bounds = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }
        return hasBounds;
    }

    static void PreparePreviewInstance(GameObject instance)
    {
        if (instance == null)
            return;

        foreach (var rb in instance.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        foreach (var wc in instance.GetComponentsInChildren<WheelCollider>(true))
            wc.enabled = false;

        foreach (var iv in instance.GetComponentsInChildren<InteractableVehicle>(true))
            iv.enabled = false;

        foreach (var cc in instance.GetComponentsInChildren<Carbilili>(true))
            cc.enabled = false;

        foreach (var cam in instance.GetComponentsInChildren<Camera>(true))
            cam.enabled = false;

        foreach (var audio in instance.GetComponentsInChildren<AudioListener>(true))
            audio.enabled = false;

        foreach (var nb in instance.GetComponentsInChildren<NetworkBehaviour>(true))
            nb.enabled = false;

        foreach (var nid in instance.GetComponentsInChildren<NetworkIdentity>(true))
            nid.enabled = false;
    }

    void EnsurePreviewStage()
    {
        if (_previewStageRoot == null)
        {
            var stageGo = new GameObject("VehicleShopPreviewStage");
            _previewStageRoot = stageGo.transform;
            _previewStageRoot.SetPositionAndRotation(new Vector3(-500f, -500f, -500f), Quaternion.identity);
            DontDestroyOnLoad(stageGo);
            _stagePersistent = true;

            var lightGo = new GameObject("PreviewLight");
            lightGo.transform.SetParent(_previewStageRoot, false);
            lightGo.transform.localRotation = Quaternion.Euler(35f, -35f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;

            var fillGo = new GameObject("PreviewFillLight");
            fillGo.transform.SetParent(_previewStageRoot, false);
            fillGo.transform.localRotation = Quaternion.Euler(10f, 120f, 0f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.45f;

            var lookTargetGo = new GameObject("PreviewLookTarget");
            lookTargetGo.transform.SetParent(_previewStageRoot, false);
            lookTargetGo.transform.localPosition = new Vector3(0f, PreviewLookTargetLocalY, 0f);
            _previewLookTarget = lookTargetGo.transform;

            var camGo = new GameObject("PreviewCamera");
            camGo.transform.SetParent(_previewStageRoot, false);
            camGo.transform.localPosition = new Vector3(0f, PreviewLookTargetLocalY + 0.08f, PreviewCameraDistance);
            _previewCamera = camGo.AddComponent<Camera>();
            _previewCamera.transform.LookAt(_previewLookTarget);
            _previewCamera.clearFlags = CameraClearFlags.SolidColor;
            _previewCamera.backgroundColor = PreviewBackground;
            _previewCamera.fieldOfView = 34f;
            _previewCamera.nearClipPlane = 0.05f;
            _previewCamera.farClipPlane = 40f;
            _previewCamera.enabled = true;
        }

        if (_previewTexture == null
            || _previewTexture.width != PreviewRenderWidth
            || _previewTexture.height != PreviewRenderHeight)
        {
            if (_previewTexture != null)
            {
                _previewTexture.Release();
                Destroy(_previewTexture);
            }
            _previewTexture = CreatePreviewRenderTexture();
        }

        if (_previewCamera != null)
        {
            _previewCamera.targetTexture = _previewTexture;
            _previewCamera.aspect = (float)PreviewRenderWidth / PreviewRenderHeight;
            _previewCamera.backgroundColor = PreviewBackground;
        }

        BindPreviewTexture();
    }

    void BindPreviewTexture()
    {
        if (previewImage != null && _previewTexture != null)
            previewImage.texture = _previewTexture;
    }

    void RenderPreviewFrame()
    {
        if (_previewCamera != null && _previewCamera.targetTexture != null)
            _previewCamera.Render();
    }

    static RenderTexture CreatePreviewRenderTexture()
    {
        var texture = new RenderTexture(PreviewRenderWidth, PreviewRenderHeight, 16, RenderTextureFormat.ARGB32);
        texture.Create();
        return texture;
    }

    void DestroyPreviewStage(bool releaseTexture)
    {
        ClearPreview();
        if (_previewStageRoot != null && _stagePersistent)
        {
            Destroy(_previewStageRoot.gameObject);
            _previewStageRoot = null;
            _previewLookTarget = null;
            _previewCamera = null;
        }

        if (releaseTexture && _previewTexture != null)
        {
            _previewTexture.Release();
            Destroy(_previewTexture);
            _previewTexture = null;
        }
    }

    static void SetLayerRecursively(GameObject go, int layer)
    {
        if (go == null)
            return;
        go.layer = layer;
        var t = go.transform;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i).gameObject, layer);
    }
}

sealed class VehiclePreviewInput : MonoBehaviour, IDragHandler, IScrollHandler
{
    VehicleShopPreview3D _owner;

    public void Initialize(VehicleShopPreview3D owner)
    {
        _owner = owner;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_owner == null)
            return;
        _owner.HandlePreviewDrag(eventData.delta.x);
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (_owner == null)
            return;
        _owner.HandlePreviewScroll(eventData.scrollDelta.y);
    }
}
