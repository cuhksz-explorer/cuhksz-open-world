using UnityEngine;

public class CarCamera : MonoBehaviour
{
    [Header("跟随目标")]
    public Transform target;
    public Vector3 offset = new Vector3(0f, 1.5f, -4f);

    [Header("平滑跟随")]
    public float positionSmoothSpeed = 5f;
    public float rotationSmoothSpeed = 3f;

    [Header("动态视野")]
    public Camera cam;
    public float baseFOV = 60f;
    public float maxFOV = 80f;
    public float speedForMaxFOV = 30f;

    [Header("波动效果")]
    public float rollAmount = 5f;
    public float pitchAmount = 3f;
    public float tiltSmoothSpeed = 4f;

    [Header("动态模糊 (原生)")]
    public Material blurMaterial;                  // 拖拽自定义模糊材质（可选）
    public float motionBlurStartSpeed = 8.33f;     // 30 km/h
    public float motionBlurMaxSpeed = 27.78f;      // 100 km/h
    [Range(0f, 0.5f)] public float maxBlurIntensity = 0.3f;

    private Rigidbody carRb;
    private float currentRoll;
    private float currentPitch;
    private Material internalBlurMaterial;
    private bool useBlur = false;

    void Start()
    {
        if (target == null)
            target = GetComponentInParent<Transform>();
        if (cam == null)
            cam = GetComponent<Camera>();
        carRb = target?.GetComponent<Rigidbody>();

        // 准备模糊材质
        if (blurMaterial != null)
        {
            internalBlurMaterial = blurMaterial;
            useBlur = true;
        }
        else
        {
            // 尝试使用 Unity 内置的运动模糊 Shader（存在于某些 Unity 版本）
            Shader builtinBlur = Shader.Find("Hidden/Internal-MotionBlur");
            if (builtinBlur != null && builtinBlur.isSupported)
            {
                internalBlurMaterial = new Material(builtinBlur);
                useBlur = true;
                Debug.Log("使用内置运动模糊 Shader");
            }
            else
            {
                Debug.LogWarning("未找到内置运动模糊 Shader，动态模糊已禁用。你可以手动创建一个模糊材质并拖拽到 blurMaterial 字段。");
                useBlur = false;
            }
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 位置跟随
        Vector3 desiredPos = target.position + target.TransformDirection(offset);
        transform.position = Vector3.Lerp(transform.position, desiredPos, positionSmoothSpeed * Time.deltaTime);

        // 动态FOV
        if (cam != null && carRb != null)
        {
            float speed = carRb.velocity.magnitude;
            float t = Mathf.Clamp01(speed / speedForMaxFOV);
            cam.fieldOfView = Mathf.Lerp(baseFOV, maxFOV, t);
        }

        // 波动效果
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        float speedFactor = (carRb != null) ? Mathf.Clamp01(carRb.velocity.magnitude / 20f) : 1f;
        float targetRoll = -h * rollAmount * speedFactor;
        float targetPitch = -v * pitchAmount;
        currentRoll = Mathf.Lerp(currentRoll, targetRoll, tiltSmoothSpeed * Time.deltaTime);
        currentPitch = Mathf.Lerp(currentPitch, targetPitch, tiltSmoothSpeed * Time.deltaTime);

        Vector3 lookDir = target.position - transform.position;
        if (lookDir != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDir);
            Quaternion tilt = Quaternion.Euler(currentPitch, 0f, currentRoll);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot * tilt, rotationSmoothSpeed * Time.deltaTime);
        }
    }

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (!useBlur || carRb == null || internalBlurMaterial == null)
        {
            Graphics.Blit(src, dest);
            return;
        }

        float speed = carRb.velocity.magnitude;
        if (speed <= motionBlurStartSpeed)
        {
            Graphics.Blit(src, dest);
            return;
        }

        float intensity = (speed - motionBlurStartSpeed) / (motionBlurMaxSpeed - motionBlurStartSpeed);
        intensity = Mathf.Clamp01(intensity) * maxBlurIntensity;

        // 传递给材质（不同内置Shader的参数名可能不同，尝试常见名称）
        if (internalBlurMaterial.HasProperty("_Intensity"))
            internalBlurMaterial.SetFloat("_Intensity", intensity);
        else if (internalBlurMaterial.HasProperty("_BlurAmount"))
            internalBlurMaterial.SetFloat("_BlurAmount", intensity);
        else if (internalBlurMaterial.HasProperty("_Amount"))
            internalBlurMaterial.SetFloat("_Amount", intensity);

        Graphics.Blit(src, dest, internalBlurMaterial);
    }

    void OnDestroy()
    {
        if (internalBlurMaterial != null && internalBlurMaterial != blurMaterial)
            Destroy(internalBlurMaterial);
    }
}