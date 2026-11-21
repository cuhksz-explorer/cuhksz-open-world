using UnityEngine;

/// <summary>
/// 让3D物体（如任务点文本）始终面向主相机，避免视角问题
/// </summary>
public class Billboard : MonoBehaviour
{
    [Tooltip("要朝向的相机（默认主相机）")]
    public Camera targetCamera;

    private void Awake()
    {
        // 自动绑定主相机
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    // LateUpdate确保在相机移动后更新朝向
    private void LateUpdate()
    {
        if (targetCamera == null) return;

        // 面向相机，但保持Y轴垂直（避免文本倒过来）
        transform.rotation = Quaternion.LookRotation(
            targetCamera.transform.forward,
            Vector3.up
        );
    }
}