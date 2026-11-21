using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// 按钮点击触发：物体生成→缓动移动+平滑旋转→延迟消失→场景切换
/// 扩展：支持自定义起始/结束旋转角度，移动与旋转同步过渡
/// </summary>
public class SpawnMoveRotateDisappearSceneSwitch : MonoBehaviour
{
    [Header("物体配置")]
    [Tooltip("要生成的物体预制体（拖拽赋值）")]
    public GameObject targetPrefab;

    [Header("位置配置")]
    [Tooltip("物体生成的起始坐标")]
    public Vector3 spawnPosition = new Vector3(0, 1, 0);
    [Tooltip("物体移动的目标坐标")]
    public Vector3 targetPosition = new Vector3(5, 1, 0);

    [Header("旋转配置")]
    [Tooltip("物体生成的起始旋转角度（欧拉角：X/Y/Z轴）")]
    public Vector3 startRotation = new Vector3(0, 0, 0); // 默认无旋转
    [Tooltip("物体到达目标点后的结束旋转角度（欧拉角：X/Y/Z轴）")]
    public Vector3 endRotation = new Vector3(0, 180, 0); // 默认绕Y轴旋转180度

    [Header("运动配置")]
    [Tooltip("物体从起始到目标的总耗时（秒），移动和旋转同步完成")]
    public float totalDuration = 2f; // 移动+旋转总时长
    [Tooltip("移动缓动平滑度（值越小越平滑，建议0.1-0.5）")]
    public float moveSmoothTime = 0.3f;
    [Tooltip("旋转缓动平滑度（值越小越平滑，建议0.1-0.5）")]
    public float rotateSmoothTime = 0.3f;

    [Header("时间配置")]
    [Tooltip("物体到达目标点后，等待多久消失（秒）")]
    public float waitBeforeDisappear = 10f;

    [Header("场景配置")]
    [Tooltip("要传送的目标场景名称（必须在Build Settings中添加）")]
    public string targetSceneName = "NewScene";

    /// <summary>
    /// 按钮点击触发的方法（绑定到UI按钮）
    /// </summary>
    public void OnButtonClick()
    {
        // 参数校验
        if (targetPrefab == null)
        {
            Debug.LogError("错误：请给 [targetPrefab] 赋值物体预制体！");
            return;
        }
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError("错误：请填写 [targetSceneName] 目标场景名称！");
            return;
        }
        if (totalDuration <= 0)
        {
            Debug.LogError("错误：[totalDuration] 总时长不能为0或负数！");
            totalDuration = 2f;
            return;
        }

        // 1. 生成物体（设置起始位置+起始旋转）
        GameObject spawnedObj = Instantiate(
            targetPrefab,
            spawnPosition,
            Quaternion.Euler(startRotation) // 将欧拉角转换为四元数（Unity旋转存储格式）
        );

        // 2. 启动协程：同步处理缓动移动+平滑旋转→等待→消失→场景切换
        StartCoroutine(MoveRotateDisappearCoroutine(spawnedObj));
    }

    /// <summary>
    /// 协程：同步控制物体缓动移动、平滑旋转、等待、消失、场景切换
    /// </summary>
    private IEnumerator MoveRotateDisappearCoroutine(GameObject obj)
    {
        // 移动相关变量
        Vector3 moveVelocity = Vector3.zero;
        float maxMoveStep = Vector3.Distance(spawnPosition, targetPosition) / totalDuration;

        // 旋转相关变量（将欧拉角转换为四元数，用于Slerp插值）
        Quaternion startRot = Quaternion.Euler(startRotation);
        Quaternion endRot = Quaternion.Euler(endRotation);
        Quaternion currentRot = startRot;
        Vector3 rotateVelocity = Vector3.zero;

        // 阶段1：同步缓动移动+平滑旋转（直到移动到位）
        while (Vector3.Distance(obj.transform.position, targetPosition) > 0.01f)
        {
            // --- 缓动移动（保持原有SmoothDamp效果）---
            obj.transform.position = Vector3.SmoothDamp(
                obj.transform.position,
                targetPosition,
                ref moveVelocity,
                moveSmoothTime,
                maxMoveStep,
                Time.deltaTime
            );

            // --- 平滑旋转（使用Slerp球面插值，旋转更自然）---
            // 计算旋转进度（与移动进度同步：0→1）
            float rotateProgress = Mathf.Clamp01(1 - (Vector3.Distance(obj.transform.position, targetPosition) / Vector3.Distance(spawnPosition, targetPosition)));

            // 方式1：Slerp球面插值（适合固定角度过渡，无回弹）
            currentRot = Quaternion.Slerp(startRot, endRot, rotateProgress);

            // 方式2：基于欧拉角的SmoothDamp（适合需要缓动回弹的场景，取消注释即可使用）
            // Vector3 currentEuler = Vector3.SmoothDamp(
            //     obj.transform.eulerAngles,
            //     endRotation,
            //     ref rotateVelocity,
            //     rotateSmoothTime,
            //     float.MaxValue,
            //     Time.deltaTime
            // );
            // currentRot = Quaternion.Euler(currentEuler);

            // 应用旋转
            obj.transform.rotation = currentRot;

            yield return null; // 等待下一帧
        }

        // 确保物体准确到达目标位置和旋转角度（避免精度问题）
        obj.transform.position = targetPosition;
        obj.transform.rotation = Quaternion.Euler(endRotation);

        // 阶段2：等待指定时间（默认10秒）
        yield return new WaitForSeconds(waitBeforeDisappear);

        // 阶段3：物体消失（销毁）
        Destroy(obj);

        // 阶段4：直接切换到目标场景（移除了视频播放步骤）
        SceneManager.LoadScene(targetSceneName);
    }
}