using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 避免 Screen Space Camera 且 worldCamera 为空时，TMP_InputField 拖拽报 NullReference。
/// </summary>
public static class UiCanvasOverlayFix
{
    public static void EnsureOverlayCanvases(Transform root)
    {
        if (root == null)
            return;

        var canvases = root.GetComponentsInParent<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            var canvas = canvases[i];
            if (canvas == null)
                continue;
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
            }
        }
    }
}
