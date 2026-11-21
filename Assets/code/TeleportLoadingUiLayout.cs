using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 将场景内传送 loading 层（transferpanel / RawImage 等）拉伸为铺满屏幕；
/// 视频按「覆盖」模式裁剪 UV，铺满可视区域且不超过屏幕边界。
/// </summary>
public static class TeleportLoadingUiLayout
{
    public static void ApplyFullscreen(GameObject loadingScreen, RawImage videoRawImage, Graphic progressBar = null)
    {
        if (loadingScreen != null)
            ApplyFullscreenToRoot(loadingScreen);

        if (videoRawImage != null)
        {
            if (loadingScreen == null || !videoRawImage.transform.IsChildOf(loadingScreen.transform))
            {
                var canvas = videoRawImage.canvas;
                if (canvas != null)
                    ApplyFullscreenToRoot(canvas.gameObject);
            }

            StretchHierarchyToCanvasRoot(videoRawImage.rectTransform);
            DisableAspectRatioFitters(videoRawImage.gameObject);
            StretchFull(videoRawImage.rectTransform);
            videoRawImage.rectTransform.localScale = Vector3.one;
            ApplyVideoCoverUv(videoRawImage);
        }

        if (loadingScreen != null)
        {
            foreach (var raw in loadingScreen.GetComponentsInChildren<RawImage>(true))
            {
                if (videoRawImage != null && raw == videoRawImage)
                    continue;
                StretchFull(raw.rectTransform);
                raw.rectTransform.localScale = Vector3.one;
                ApplyVideoCoverUv(raw);
            }
        }

        if (progressBar != null && progressBar.rectTransform != null)
            progressBar.transform.SetAsLastSibling();

        Canvas.ForceUpdateCanvases();
    }

    static void ApplyFullscreenToRoot(GameObject root)
    {
        var screenRt = root.GetComponent<RectTransform>();
        if (screenRt != null)
        {
            StretchHierarchyToCanvasRoot(screenRt);
            screenRt.localScale = Vector3.one;
            root.transform.SetAsLastSibling();
        }

        var backdrop = root.GetComponent<Image>();
        if (backdrop != null)
            backdrop.color = Color.black;

        foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
        {
            var rt = canvas.GetComponent<RectTransform>();
            if (rt != null)
            {
                StretchFull(rt);
                rt.localScale = Vector3.one;
            }

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
            }

            canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 30000);
        }

        DisableAspectRatioFitters(root);
    }

    /// <summary>object-fit: cover — 铺满显示区域，超出部分裁切，不拉伸变形。</summary>
    public static void ApplyVideoCoverUv(RawImage raw, int videoWidth = 0, int videoHeight = 0)
    {
        if (raw == null)
            return;

        if (!TryGetVideoPixelSize(raw, ref videoWidth, ref videoHeight))
            return;

        float displayW = Screen.width;
        float displayH = Screen.height;
        var rect = raw.rectTransform.rect;
        if (rect.width > 1f && rect.height > 1f)
        {
            displayW = rect.width;
            displayH = rect.height;
        }

        if (displayW < 1f || displayH < 1f)
            return;

        float screenAspect = displayW / displayH;
        float videoAspect = videoWidth / (float)videoHeight;

        if (videoAspect > screenAspect)
        {
            float uvW = screenAspect / videoAspect;
            raw.uvRect = new Rect((1f - uvW) * 0.5f, 0f, uvW, 1f);
        }
        else if (videoAspect < screenAspect)
        {
            float uvH = videoAspect / screenAspect;
            raw.uvRect = new Rect(0f, (1f - uvH) * 0.5f, 1f, uvH);
        }
        else
        {
            raw.uvRect = new Rect(0f, 0f, 1f, 1f);
        }
    }

    public static bool TryGetVideoPixelSize(RawImage raw, ref int width, ref int height)
    {
        if (width > 0 && height > 0)
            return true;

        if (raw == null)
            return false;

        if (raw.texture != null)
        {
            width = raw.texture.width;
            height = raw.texture.height;
            if (width > 0 && height > 0)
                return true;
        }

        var vp = raw.GetComponent<VideoPlayer>();
        if (vp != null && vp.clip != null)
        {
            width = (int)vp.clip.width;
            height = (int)vp.clip.height;
            if (width > 0 && height > 0)
                return true;
        }

        return false;
    }

    static void StretchHierarchyToCanvasRoot(RectTransform rt)
    {
        while (rt != null)
        {
            StretchFull(rt);
            rt.localScale = Vector3.one;

            if (rt.GetComponent<Canvas>() != null)
                break;

            rt = rt.parent as RectTransform;
        }
    }

    static void DisableAspectRatioFitters(GameObject root)
    {
        if (root == null)
            return;

        var fitters = root.GetComponentsInChildren<AspectRatioFitter>(true);
        for (int i = 0; i < fitters.Length; i++)
            fitters[i].enabled = false;
    }

    static void StretchFull(RectTransform rt)
    {
        if (rt == null)
            return;

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
