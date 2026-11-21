using UnityEngine;

/// <summary>
/// 将 WASD 动作控制器绑定到当前可见的角色模型 Animator 上。
/// </summary>
public static class PlayerCharacterAnimationUtility
{
    public const string MovementControllerResourcesPath = "PlayerCharacters/PlayerMovement";
    const string ReferenceFemalePrefabPath = "PlayerCharacters/hero_female";

    static Avatar _cachedReferenceAvatar;

    public static RuntimeAnimatorController LoadMovementController(PlayerCharacterCatalog catalog)
    {
        if (catalog != null && catalog.MovementAnimatorController != null)
            return catalog.MovementAnimatorController;

        return Resources.Load<RuntimeAnimatorController>(MovementControllerResourcesPath);
    }

    public static Avatar LoadReferenceHumanoidAvatar()
    {
        if (_cachedReferenceAvatar != null && _cachedReferenceAvatar.isValid)
            return _cachedReferenceAvatar;

        var femalePrefab = Resources.Load<GameObject>(ReferenceFemalePrefabPath);
        if (femalePrefab != null)
        {
            var femaleAnimator = femalePrefab.GetComponent<Animator>();
            if (femaleAnimator != null && femaleAnimator.avatar != null && femaleAnimator.avatar.isValid)
            {
                _cachedReferenceAvatar = femaleAnimator.avatar;
                return _cachedReferenceAvatar;
            }
        }

        return null;
    }

    public static void BindMovementAnimator(
        Transform playerRoot,
        GameObject visualModel,
        RuntimeAnimatorController controller)
    {
        if (playerRoot == null || visualModel == null)
            return;

        DisableRootAnimator(playerRoot);

        var animator = visualModel.GetComponent<Animator>();
        if (animator == null)
            animator = visualModel.AddComponent<Animator>();

        if (controller != null)
            animator.runtimeAnimatorController = controller;

        var referenceAvatar = LoadReferenceHumanoidAvatar();
        if (referenceAvatar != null
            && (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman))
            animator.avatar = referenceAvatar;

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.enabled = true;

        var keyInput = playerRoot.GetComponent<KeyInputController>();
        if (keyInput != null)
            keyInput.BindAnimator(animator);
    }

    static void DisableRootAnimator(Transform playerRoot)
    {
        var rootAnimator = playerRoot.GetComponent<Animator>();
        if (rootAnimator != null)
            rootAnimator.enabled = false;
    }
}
