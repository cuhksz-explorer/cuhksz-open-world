using UnityEngine;

public class NPCPortraitCameraController : MonoBehaviour
{
    [Header("要跟随的目标（NPC 的 PortraitAnchor）")]
    public Transform target;

    [Header("相机在目标前/后的相对偏移")]
    public Vector3 offset = new Vector3(0, 0, 1.5f);

    void LateUpdate()
    {
        if (target == null) return;

        // 把相机放在 target 周围的某个位置（你可以自己调 offset）
        transform.position = target.position + target.rotation * offset;

        // 永远看向 target
        transform.LookAt(target);
    }

    // 在开始对话时由外部脚本调用
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}

