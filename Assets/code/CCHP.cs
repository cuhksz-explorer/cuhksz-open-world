using UnityEngine;

public class HoverCamera : MonoBehaviour
{
    [Tooltip("需要跟踪的目标")]
    public Transform target;

    [Tooltip("摄像机距离目标的垂直高度")]
    public float height = 50f;

    [Tooltip("移动平滑速度")]
    public float smoothSpeed = 5f;

    [Tooltip("摄像机固定朝向角度（X轴控制俯视角度，Y轴控制水平旋转）")]
    public Vector3 fixedRotation = new Vector3(90f, 0f, 0f); // 默认90度俯视

    private Camera _camera;

    void Awake()
    {
        _camera = GetComponent<Camera>();
        if (_camera == null)
        {
            _camera = gameObject.AddComponent<Camera>();
        }

        // 摄像机基础设置
        _camera.orthographic = false;
        _camera.fieldOfView = 60f;
        _camera.cullingMask = 1 << LayerMask.NameToLayer("Default") | 1 << LayerMask.NameToLayer("Terrain");
        _camera.clearFlags = CameraClearFlags.Skybox;

        // 初始化摄像机旋转为固定角度
        transform.rotation = Quaternion.Euler(fixedRotation);
    }

    void LateUpdate()
    {
        if (target == null)
        {
            Debug.LogWarning("未指定跟踪目标");
            return;
        }

        // 计算目标位置（在目标上方height处）
        Vector3 targetPosition = new Vector3(target.position.x, target.position.y + height, target.position.z);

        // 平滑移动到目标位置
        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);

        // 移除LookAt，保持固定旋转（关键修改）
        // 保持初始设置的固定旋转角度，不随目标转动
        transform.rotation = Quaternion.Euler(fixedRotation);
    }

    public Camera GetCamera()
    {
        return _camera;
    }
}