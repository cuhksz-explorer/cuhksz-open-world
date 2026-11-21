using System;
using UnityEngine;
using TMPro;   // 需要导入 TextMeshPro 命名空间，如果不用 TMP 可改用 UI.Text

public class Carbilili : MonoBehaviour
{
    [Header("车轮组件")]
    public WheelCollider[] wheelColliders;   // 顺序: 0前左,1前右,2后左,3后右
    public Transform[] wheelModels;          // 对应的车轮模型

    [Header("动力参数")]
    public float motorTorqueMax = 1500f;     // 最大驱动力矩
    public float steerAngleMax = 45f;        // 最大转向角
    public float reverseTorqueScale = 0.45f; // 倒车扭矩占前进扭矩的比例

    [Header("系统1风格减速参数")]
    public float brakeForce = 10f;               // 倒车切前进时的缓冲减速
    public float forwardBrakeDeceleration = 32f; // 前进时按 S 的主刹车
    public float coastStopDeceleration = 18f;    // 松开油门后的自然减速
    [Tooltip("前进速度高于此值（m/s）时，S 仅刹车不倒车，避免刹停瞬间突然倒车。")]
    public float reverseEngageForwardSpeed = 0.35f;
    [Tooltip("允许在完全停稳后按住 S 倒车；关闭则 S 仅刹车。")]
    public bool allowReverse = true;
    public float wheelRadius = 0.33f;        // 车轮半径（米），需根据实际车轮大小填写

    [Header("速度显示")]
    public TextMeshProUGUI speedText;        // 左下角速度文本（TMP）
    public float speedUnit = 3.6f;           // m/s → km/h

    private Rigidbody rb;
    private float horizontalInput;
    private float verticalInput;
    private bool localInputEnabled = true;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
            Debug.LogError("Carbilili: 车辆缺少 Rigidbody 组件！");

        TryAutoRepairWheelColliders();

        int colliderCount = wheelColliders != null ? wheelColliders.Length : 0;
        int modelCount = wheelModels != null ? wheelModels.Length : 0;
        if (colliderCount < 4)
        {
            Debug.LogError($"Carbilili: {gameObject.name} 的 wheelColliders 配置不足（当前 {colliderCount}，需要 >= 4）。");
        }

        if (modelCount < 4)
        {
            Debug.LogWarning($"Carbilili: {gameObject.name} 的 wheelModels 配置不足（当前 {modelCount}，建议 >= 4）。");
        }

        // 如果未手动填写车轮半径，尝试从第一个 WheelCollider 获取
        if (colliderCount > 0 && wheelRadius == 0.33f)
            wheelRadius = wheelColliders[0].radius;
    }

    private void Update()
    {
        if (!HasUsableWheelSetup())
        {
            UpdateSpeedDisplay();
            return;
        }

        if (!localInputEnabled || UIInteractionState.IsNpcShopOpen)
        {
            horizontalInput = 0f;
            verticalInput = 0f;
            SetMotorTorque(0f);
            SetBrakeTorque(0f);
            UpdateWheelVisuals();
            UpdateSpeedDisplay();
            return;
        }

        // 获取输入
        horizontalInput = Input.GetAxis("Horizontal");
        verticalInput = Input.GetAxis("Vertical");

        // 前轮转向
        if (wheelColliders.Length >= 2)
        {
            wheelColliders[0].steerAngle = horizontalInput * steerAngleMax;
            wheelColliders[1].steerAngle = horizontalInput * steerAngleMax;
        }

        bool w = Input.GetKey(KeyCode.W);
        bool s = Input.GetKey(KeyCode.S);
        if (w && s)
        {
            w = false;
            s = false;
        }

        float signedSpeed = GetSignedForwardSpeed();
        if (w)
        {
            ApplyForwardDrive(signedSpeed);
        }
        else if (s)
        {
            ApplyBrakeOrReverse(signedSpeed);
        }
        else
        {
            ApplyCoastBrake();
        }

        // 更新车轮模型的视觉位置
        UpdateWheelVisuals();

        // 更新速度显示
        UpdateSpeedDisplay();
    }

    /// <summary>
    /// 设置后轮刹车扭矩（假设索引2,3为后轮）
    /// </summary>
    private void SetBrakeTorque(float torque)
    {
        if (HasUsableWheelSetup())
        {
            wheelColliders[2].brakeTorque = torque;
            wheelColliders[3].brakeTorque = torque;
        }
    }

    /// <summary>
    /// 设置后轮电机扭矩
    /// </summary>
    private void SetMotorTorque(float torque)
    {
        if (HasUsableWheelSetup())
        {
            wheelColliders[2].motorTorque = torque;
            wheelColliders[3].motorTorque = torque;
        }
    }

    void ApplyForwardDrive(float signedSpeed)
    {
        if (signedSpeed < -0.1f)
        {
            SetMotorTorque(0f);
            SetBrakeTorque(CalcBrakeTorqueForDeceleration(brakeForce));
            return;
        }

        SetBrakeTorque(0f);
        SetMotorTorque(motorTorqueMax);
    }

    void ApplyBrakeOrReverse(float signedSpeed)
    {
        float engageSpeed = Mathf.Max(0.05f, reverseEngageForwardSpeed);

        // 仍在明显前进：只刹车，不切倒车
        if (signedSpeed > engageSpeed)
        {
            SetMotorTorque(0f);
            SetBrakeTorque(CalcBrakeTorqueForDeceleration(forwardBrakeDeceleration));
            return;
        }

        // 低速滑行或未开启倒车：继续刹到停，避免 0.1m/s 就突然倒车
        if (!allowReverse || signedSpeed > 0.02f)
        {
            SetMotorTorque(0f);
            SetBrakeTorque(CalcBrakeTorqueForDeceleration(
                signedSpeed > 0.02f ? forwardBrakeDeceleration : coastStopDeceleration));
            return;
        }

        SetBrakeTorque(0f);
        float reverseInput = Mathf.Clamp01(-verticalInput);
        if (reverseInput < 0.01f)
            reverseInput = 1f;
        SetMotorTorque(-motorTorqueMax * reverseTorqueScale * reverseInput);
    }

    void ApplyCoastBrake()
    {
        SetMotorTorque(0f);
        SetBrakeTorque(CalcBrakeTorqueForDeceleration(coastStopDeceleration));
    }

    float CalcBrakeTorqueForDeceleration(float deceleration)
    {
        if (rb == null)
            return 0f;

        float decel = Mathf.Max(0f, deceleration);
        if (decel <= 0.001f)
            return 0f;

        float radius = Mathf.Max(0.01f, wheelRadius);
        float requiredForce = rb.mass * decel;
        return Mathf.Clamp((requiredForce * radius) / 2f, 0f, 20000f);
    }

    float GetSignedForwardSpeed()
    {
        if (rb == null)
            return 0f;

        Vector3 flatVelocity = rb.velocity;
        flatVelocity.y = 0f;
        if (flatVelocity.sqrMagnitude <= 1e-6f)
            return 0f;
        return Vector3.Dot(flatVelocity, transform.forward);
    }

    /// <summary>
    /// 更新车轮模型的视觉位置和旋转
    /// </summary>
    private void UpdateWheelVisuals()
    {
        if (wheelColliders == null) return;
        for (int i = 0; i < wheelColliders.Length; i++)
        {
            if (wheelColliders[i] == null) continue;
            if (wheelModels.Length > i && wheelModels[i] != null)
            {
                wheelColliders[i].GetWorldPose(out Vector3 pos, out Quaternion rot);
                wheelModels[i].position = pos;
                wheelModels[i].rotation = rot;
            }
        }
    }

    /// <summary>
    /// 更新左下角速度显示
    /// </summary>
    private void UpdateSpeedDisplay()
    {
        if (speedText == null || rb == null) return;
        float speedKmh = rb.velocity.magnitude * speedUnit;
        speedText.text = Mathf.FloorToInt(speedKmh).ToString() + " km/h";
    }

    public void StopImmediately()
    {
        if (!HasUsableWheelSetup() && rb == null)
            return;

        horizontalInput = 0f;
        verticalInput = 0f;
        SetMotorTorque(0f);
        SetBrakeTorque(0f);
        if (wheelColliders != null && wheelColliders.Length >= 2)
        {
            if (wheelColliders[0] != null) wheelColliders[0].steerAngle = 0f;
            if (wheelColliders[1] != null) wheelColliders[1].steerAngle = 0f;
        }

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    public void SetLocalInputEnabled(bool enabled, bool stopMotion = false)
    {
        localInputEnabled = enabled;
        if (!enabled && stopMotion)
            StopImmediately();
    }

    public void ApplyRemoteState(Vector3 linearVelocity, Vector3 angularVelocity)
    {
        if (rb == null)
            return;

        rb.velocity = linearVelocity;
        rb.angularVelocity = angularVelocity;
        UpdateWheelVisuals();
        UpdateSpeedDisplay();
    }

    void OnDisable()
    {
        StopImmediately();
    }

    bool HasUsableWheelSetup()
    {
        if (wheelColliders == null || wheelColliders.Length < 4)
            return false;

        for (int i = 0; i < 4; i++)
        {
            if (wheelColliders[i] == null)
                return false;
        }
        return true;
    }

    void TryAutoRepairWheelColliders()
    {
        if (HasUsableWheelSetup())
            return;

        var found = GetComponentsInChildren<WheelCollider>(true);
        if (found == null || found.Length < 4)
            return;

        Array.Sort(found, (a, b) => b.transform.localPosition.z.CompareTo(a.transform.localPosition.z));

        var frontA = found[0];
        var frontB = found[1];
        var rearA = found[2];
        var rearB = found[3];

        WheelCollider frontLeft = frontA.transform.localPosition.x <= frontB.transform.localPosition.x ? frontA : frontB;
        WheelCollider frontRight = frontA.transform.localPosition.x > frontB.transform.localPosition.x ? frontA : frontB;
        WheelCollider rearLeft = rearA.transform.localPosition.x <= rearB.transform.localPosition.x ? rearA : rearB;
        WheelCollider rearRight = rearA.transform.localPosition.x > rearB.transform.localPosition.x ? rearA : rearB;

        wheelColliders = new[] { frontLeft, frontRight, rearLeft, rearRight };
        Debug.LogWarning($"Carbilili: {gameObject.name} 的 wheelColliders 已自动修复绑定（FL/FR/RL/RR）。");
    }
}