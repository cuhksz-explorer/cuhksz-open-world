using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Restores library scene CanvasScaler behaviour for historical GUID c5a30502...
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasScaler))]
public sealed class UiReferenceResolutionScaler : MonoBehaviour
{
    [SerializeField] Vector2 _referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField] bool _dynamicMatchByAspect = true;
    [SerializeField] float _staticMatchWidthOrHeight = 0.5f;
    [SerializeField] bool _applyOnAwake = true;
    [SerializeField] bool _refreshOnScreenSizeChange = true;

    CanvasScaler _scaler;

    void Awake()
    {
        if (_applyOnAwake)
            Apply();
    }

    void OnRectTransformDimensionsChange()
    {
        if (_refreshOnScreenSizeChange)
            Apply();
    }

    public void Apply()
    {
        if (_scaler == null)
            _scaler = GetComponent<CanvasScaler>();
        if (_scaler == null)
            return;

        _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        _scaler.referenceResolution = _referenceResolution;
        if (_dynamicMatchByAspect)
            _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        _scaler.matchWidthOrHeight = _staticMatchWidthOrHeight;
    }
}
