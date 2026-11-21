using UnityEngine;

/// <summary>
/// 根据当前可见角色模型计算第一人称视点（对齐头部/眼部高度）。
/// </summary>
public static class PlayerCharacterCameraUtility
{
    const string VisualRootName = "PlayerCharacterVisual";
    const float EyeHeightFromBoundsRatio = 0.93f;
    const float DefaultEyeHeightWorld = 1.62f;
    public const float DefaultEyeForwardOffsetLocal = 0.22f;

    public static bool TryGetEyeLocalPositionOnPlayer(
        Transform playerRoot,
        float fallbackEyeHeightWorld,
        out Vector3 localEyePos,
        float forwardOffsetLocal = DefaultEyeForwardOffsetLocal)
    {
        localEyePos = Vector3.zero;
        if (playerRoot == null)
            return false;

        float forwardOffset = ResolveEyeForwardOffset(playerRoot, forwardOffsetLocal);
        Transform visualRoot = FindVisualRoot(playerRoot);
        if (visualRoot != null)
        {
            if (TryGetHeadWorldPosition(visualRoot, out Vector3 headWorld))
            {
                localEyePos = playerRoot.InverseTransformPoint(headWorld);
                localEyePos.x = 0f;
                localEyePos.z = forwardOffset;
                return true;
            }

            if (TryGetBoundsEyeWorld(visualRoot, out headWorld))
            {
                localEyePos = playerRoot.InverseTransformPoint(headWorld);
                localEyePos.x = 0f;
                localEyePos.z = forwardOffset;
                return true;
            }
        }

        var controller = playerRoot.GetComponent<CharacterController>();
        if (controller != null)
        {
            float eyeLocalY = controller.center.y + controller.height * 0.38f;
            localEyePos = new Vector3(0f, eyeLocalY, forwardOffset);
            return true;
        }

        float scaleY = Mathf.Max(0.01f, Mathf.Abs(playerRoot.lossyScale.y));
        float worldHeight = fallbackEyeHeightWorld > 0f ? fallbackEyeHeightWorld : DefaultEyeHeightWorld;
        localEyePos = new Vector3(0f, worldHeight / scaleY, forwardOffset);
        return true;
    }

    static float ResolveEyeForwardOffset(Transform playerRoot, float baseForwardOffset)
    {
        if (baseForwardOffset <= 0f)
            return 0f;

        Transform visualRoot = FindVisualRoot(playerRoot);
        if (visualRoot == null)
            return baseForwardOffset;

        float scale = Mathf.Max(0.01f, visualRoot.lossyScale.y);
        return baseForwardOffset * scale;
    }

    static Transform FindVisualRoot(Transform playerRoot)
    {
        for (int i = 0; i < playerRoot.childCount; i++)
        {
            var child = playerRoot.GetChild(i);
            if (child == null)
                continue;
            if (string.Equals(child.name, VisualRootName, System.StringComparison.Ordinal))
                return child;
            if (child.GetComponent<PlayerCharacterVisualMarker>() != null)
                return child;
        }

        return null;
    }

    static bool TryGetHeadWorldPosition(Transform visualRoot, out Vector3 headWorld)
    {
        headWorld = Vector3.zero;
        var animators = visualRoot.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            var animator = animators[i];
            if (animator == null || !animator.isHuman)
                continue;

            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head == null)
                continue;

            headWorld = head.position;
            return true;
        }

        return false;
    }

    static bool TryGetBoundsEyeWorld(Transform visualRoot, out Vector3 eyeWorld)
    {
        eyeWorld = Vector3.zero;
        var renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
            return false;

        eyeWorld = new Vector3(
            bounds.center.x,
            bounds.min.y + bounds.size.y * EyeHeightFromBoundsRatio,
            bounds.center.z);
        return true;
    }
}
