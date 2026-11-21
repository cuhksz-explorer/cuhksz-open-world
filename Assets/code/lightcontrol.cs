using UnityEngine;
using UnityEngine.UI;

public class TimeBasedLightController : MonoBehaviour
{
    [Header("����ģʽ����")]
    public ControlMode controlMode = ControlMode.Auto; // �Զ�/�ֶ�ģʽ
    public enum ControlMode
    {
        Auto,   // ʱ���Զ��л���֧�ֳ������ȣ�
        Manual  // �ֶ����ɵ��ڣ�֧�ֳ������ȣ�
    }

    [Header("ʱ��ϵͳ����")]
    public GameObject sunSimulationObject; // ����SunSimulation������
    private SunSimulation sunSimulation;   // ʱ��ϵͳ����

    [Header("��Ҫ���Ƶĵƹ��б�")]
    public Light[] lightsToControl;

    [Header("ʱ��-����ӳ�䣨�Զ�ģʽ���ģ�֧�ֳ��")]
    [Tooltip("���쿪ʼʱ�䣨24Сʱ�ƣ���6=6���ճ���")]
    public float dayStartTime = 6f;
    [Tooltip("�������ʱ�䣨24Сʱ�ƣ���18=18�����䣩")]
    public float dayEndTime = 18f;
    [Tooltip("����������ȱ�������>1����1=ԭʼ���ȣ�1.5=1.5�����")]
    public float dayMaxMultiplier = 1f;    // ����Ĭ�ϲ����1����
    [Tooltip("����������ȱ�������>1����2=2��ԭʼ���ȣ�3=3�����")]
    public float nightMaxMultiplier = 2f;  // ����Ĭ��2������
    [Tooltip("���ȹ���ʱ�䣨�룩������ͻ��")]
    public float timeTransitionDuration = 3f;

    [Header("�ֶ����Ʋ�����֧�ֳ��")]
    [Tooltip("�ֶ����ȱ�����0=�أ�1=ԭʼ���ȣ�>1=���")]
    [Range(0f, 10f)] public float manualBrightnessMultiplier = 1f;
    [Tooltip("�ֶ�ģʽ��������������������ޣ���10=10����")]
    public float maxManualMultiplier = 10f;
    [Tooltip("�ֶ������Ƿ�ƽ������")]
    public bool smoothManualTransition = true;
    [Tooltip("�ֶ������ٶȣ�Խ��Խ�죩")]
    public float manualTransitionSpeed = 5f;

    [Header("����UI�ؼ����ã���ѡ��")]
    public Toggle autoManualToggle;    // ģʽ�л�����
    public Slider brightnessSlider;    // ���ȵ��ڻ���
    public Text modeStatusText;        // ģʽ+ʱ��+������ʾ
    public Text brightnessValueText;   // ���ȱ����ı�

    // �ڲ�״̬
    private float targetMultiplier;    // Ŀ�����ȱ������Զ�/�ֶ����ã�
    private float currentMultiplier;   // ��ǰ���ȱ�����ƽ�������ã�
    private float[] originalIntensities; // �ƹ�ԭʼ������ȣ�������׼��
    private float lastCheckedHours;    // ��һ�μ���ʱ�䣨����Ƶ�����㣩

    void Start()
    {
        // ��֤ʱ��ϵͳ
        if (sunSimulationObject == null)
        {
            Debug.LogError("��ָ������SunSimulation�ű������壡");
            controlMode = ControlMode.Manual;
            UpdateModeUI();
            return;
        }
        sunSimulation = sunSimulationObject.GetComponent<SunSimulation>();
        if (sunSimulation == null)
        {
            Debug.LogError($"{sunSimulationObject.name}��SunSimulation�ű���");
            controlMode = ControlMode.Manual;
            UpdateModeUI();
            return;
        }

        // ��ʼ��ԭʼ���ȣ���¼ÿ���ƹ��������ȣ�
        InitializeOriginalIntensities();

        // ��ʼ������״̬
        currentMultiplier = 0f;
        lastCheckedHours = sunSimulation.currentTimeOfDay;
        // ���ݵ�ǰģʽ���ó�ʼĿ�걶��
        UpdateTargetMultiplierByMode();

        // ��UI�ؼ�
        BindUIControls();
        UpdateModeUI();
        UpdateLightIntensity();
    }

    void Update()
    {
        if (lightsToControl == null || lightsToControl.Length == 0) return;

        // ���ݵ�ǰģʽ����Ŀ�걶�����Զ�ģʽ��ʱ��䣬�ֶ�ģʽ�����ñ䣩
        UpdateTargetMultiplierByMode();

        // ƽ�����ɵ�Ŀ�걶��
        LerpToTargetMultiplier();

        // Ӧ�����ȵ��ƹ�
        UpdateLightIntensity();

        // ʵʱ����UI״̬
        UpdateModeUI();
    }

    /// <summary>
    /// ��¼ÿ���ƹ��ԭʼ������ȣ���Ϊ������׼��
    /// </summary>
    private void InitializeOriginalIntensities()
    {
        if (lightsToControl == null || lightsToControl.Length == 0)
        {
            Debug.LogWarning("δ�����κο��Ƶƹ⣡");
            originalIntensities = new float[0];
            return;
        }

        originalIntensities = new float[lightsToControl.Length];
        for (int i = 0; i < lightsToControl.Length; i++)
        {
            if (lightsToControl[i] != null)
            {
                originalIntensities[i] = lightsToControl[i].intensity; // ��¼ԭʼ����
                lightsToControl[i].intensity = 0; // ��ʼ�ر�
                lightsToControl[i].enabled = false;
            }
            else
            {
                originalIntensities[i] = 1f;
                Debug.LogWarning($"�ƹ������{i}��Ϊ�գ�");
            }
        }
    }

    /// <summary>
    /// ���ݵ�ǰģʽ����Ŀ�����ȱ���
    /// </summary>
    private void UpdateTargetMultiplierByMode()
    {
        switch (controlMode)
        {
            case ControlMode.Auto:
                // �Զ�ģʽ����ʱ���л�������/���ϲ�ͬ�������
                UpdateTargetMultiplierByTime();
                break;

            case ControlMode.Manual:
                // �ֶ�ģʽ�����û����ã����ɳ��
                manualBrightnessMultiplier = Mathf.Clamp(manualBrightnessMultiplier, 0f, maxManualMultiplier);
                targetMultiplier = smoothManualTransition
                    ? Mathf.Lerp(targetMultiplier, manualBrightnessMultiplier, Time.deltaTime * manualTransitionSpeed)
                    : manualBrightnessMultiplier;
                break;
        }
    }

    /// <summary>
    /// �Զ�ģʽ���ģ����ݵ�ǰʱ�����Ŀ�����ȱ�����֧�ֳ��
    /// </summary>
    private void UpdateTargetMultiplierByTime()
    {
        if (sunSimulation == null) return;

        float currentHours = sunSimulation.currentTimeOfDay;
        // ʱ��仯С��0.01Сʱ��Լ3.6�룩ʱ�����㣬����Ƶ������
        if (Mathf.Abs(currentHours - lastCheckedHours) < 0.01f) return;
        lastCheckedHours = currentHours;

        float target = 0f;
        bool isDayTime = IsDayTime(currentHours);

        // 1. ȷ����ǰʱ��ε�Ŀ�걶��������/���ϣ�
        float currentPhaseMax = isDayTime ? dayMaxMultiplier : nightMaxMultiplier;
        // 2. ������ɽ׶Σ��ճ�/����ǰ�󣩵ı���
        float transitionProgress = CalculateTransitionProgress(currentHours);

        if (transitionProgress >= 0f)
        {
            // �����У��������׶ε������֮���ֵ
            float otherPhaseMax = isDayTime ? nightMaxMultiplier : dayMaxMultiplier;
            target = Mathf.Lerp(otherPhaseMax, currentPhaseMax, transitionProgress);
        }
        else
        {
            // �ǹ��ɣ�ֱ��ʹ�õ�ǰ�׶ε������
            target = currentPhaseMax;
        }

        targetMultiplier = target;
        //Debug.Log($"[�Զ�ģʽ] ʱ�䣺{currentHours:F1}�� | ģʽ��{(isDayTime ? "����" : "����")} | Ŀ�걶����{target:F2}��");
    }

    /// <summary>
    /// �жϵ�ǰʱ���Ƿ�Ϊ����
    /// </summary>
    private bool IsDayTime(float currentHours)
    {
        // �����߼��������6-18�㣬����18-6�㣩
        if (dayStartTime < dayEndTime)
        {
            return currentHours >= dayStartTime && currentHours < dayEndTime;
        }
        else
        {
            return currentHours >= dayStartTime || currentHours < dayEndTime;
        }
    }

    /// <summary>
    /// ������ɽ��ȣ��ճ�/����ǰ�󣩣�0=��ʼ���ɣ�1=���ɽ�����-1=�ǹ���
    /// </summary>
    private float CalculateTransitionProgress(float currentHours)
    {
        float sunriseTransitionStart = dayStartTime - timeTransitionDuration / 3600f * 24f; // �ճ�ǰ���ɿ�ʼ
        float sunriseTransitionEnd = dayStartTime + timeTransitionDuration / 3600f * 24f;   // �ճ�����ɽ���
        float sunsetTransitionStart = dayEndTime - timeTransitionDuration / 3600f * 24f;    // ����ǰ���ɿ�ʼ
        float sunsetTransitionEnd = dayEndTime + timeTransitionDuration / 3600f * 24f;      // ����������ɽ���

        // �������죨���ճ�0��ǰ������24��ǰ��
        sunriseTransitionStart = NormalizeHours(sunriseTransitionStart);
        sunriseTransitionEnd = NormalizeHours(sunriseTransitionEnd);
        sunsetTransitionStart = NormalizeHours(sunsetTransitionStart);
        sunsetTransitionEnd = NormalizeHours(sunsetTransitionEnd);

        // �ճ�����
        if (IsTimeInRange(currentHours, sunriseTransitionStart, sunriseTransitionEnd))
        {
            return CalculateProgressInRange(currentHours, sunriseTransitionStart, sunriseTransitionEnd);
        }
        // �������
        else if (IsTimeInRange(currentHours, sunsetTransitionStart, sunsetTransitionEnd))
        {
            return CalculateProgressInRange(currentHours, sunsetTransitionStart, sunsetTransitionEnd);
        }
        // �ǹ��ɽ׶�
        else
        {
            return -1f;
        }
    }

    /// <summary>
    /// ��׼��ʱ�䵽0-24Сʱ
    /// </summary>
    private float NormalizeHours(float hours)
    {
        if (hours < 0f) return hours + 24f;
        if (hours >= 24f) return hours - 24f;
        return hours;
    }

    /// <summary>
    /// �ж�ʱ���Ƿ���ָ����Χ�ڣ�֧�ֿ��죩
    /// </summary>
    private bool IsTimeInRange(float current, float start, float end)
    {
        if (start < end)
        {
            return current >= start && current < end;
        }
        else
        {
            return current >= start || current < end;
        }
    }

    /// <summary>
    /// ����ʱ���ڷ�Χ�ڵĽ��ȣ�0-1��
    /// </summary>
    private float CalculateProgressInRange(float current, float start, float end)
    {
        if (start < end)
        {
            return (current - start) / (end - start);
        }
        else
        {
            // ���췶Χ�������μ���
            float part1 = (24f - start) / (end + 24f - start);
            if (current >= start)
            {
                return (current - start) / (24f - start) * part1;
            }
            else
            {
                return part1 + (current / end) * (1f - part1);
            }
        }
    }

    /// <summary>
    /// ƽ�����ɵ�Ŀ�걶��
    /// </summary>
    private void LerpToTargetMultiplier()
    {
        if (Mathf.Abs(currentMultiplier - targetMultiplier) > 0.001f)
        {
            float lerpSpeed = controlMode == ControlMode.Auto
                ? Time.deltaTime / (timeTransitionDuration / 2f) // �Զ�ģʽ�����ٶ�
                : Time.deltaTime * manualTransitionSpeed;       // �ֶ�ģʽ�����ٶ�

            currentMultiplier = Mathf.Lerp(currentMultiplier, targetMultiplier, lerpSpeed);
        }
    }

    /// <summary>
    /// Ӧ���������ȵ��ƹ⣨ԭʼ���� �� ��ǰ������֧�ֳ��
    /// </summary>
    private void UpdateLightIntensity()
    {
        for (int i = 0; i < lightsToControl.Length; i++)
        {
            Light light = lightsToControl[i];
            if (light == null) continue;

            // �������� = ԭʼ���� �� ��ǰ������������>1��ͻ��ԭʼ������ȣ�
            float finalIntensity = originalIntensities[i] * currentMultiplier;
            light.intensity = finalIntensity;
            light.enabled = finalIntensity > 0.01f; // ���ȼ���ʱ�رգ���ʡ����
        }
    }

    /// <summary>
    /// ��UI�ؼ������鷶Χͬ���������ģʽ�л��¼���
    /// </summary>
    private void BindUIControls()
    {
        // ģʽ�л�����
        if (autoManualToggle != null)
        {
            autoManualToggle.isOn = controlMode == ControlMode.Auto;
            autoManualToggle.onValueChanged.AddListener((isAuto) =>
            {
                controlMode = isAuto ? ControlMode.Auto : ControlMode.Manual;
                Debug.Log($"ģʽ�л���{controlMode}");
                // �л�ʱƽ������Ŀ�걶��
                UpdateTargetMultiplierByMode();
            });
        }

        // ���Ȼ��飨��Χ0-maxManualMultiplier��֧�ֳ��
        if (brightnessSlider != null)
        {
            brightnessSlider.minValue = 0f;
            brightnessSlider.maxValue = maxManualMultiplier;
            brightnessSlider.value = manualBrightnessMultiplier;
            brightnessSlider.onValueChanged.AddListener((value) =>
            {
                manualBrightnessMultiplier = value;
                if (controlMode == ControlMode.Manual)
                {
                    targetMultiplier = value;
                }
            });
        }
    }

    /// <summary>
    /// ����UI״̬��ʾ��ģʽ��ʱ�䡢������
    /// </summary>
    private void UpdateModeUI()
    {
        if (modeStatusText != null)
        {
            string timeInfo = sunSimulation != null ? $"��ǰʱ�䣺{sunSimulation.currentTimeOfDay:F1}��" : "ʱ��ϵͳδ��";
            string phaseInfo = controlMode == ControlMode.Auto
                ? $"ʱ��Σ�{(IsDayTime(lastCheckedHours) ? "����" : "����")}"
                : "�ֶ�����ģʽ";
            string brightnessInfo = $"���ȱ�����{currentMultiplier:F2}����ԭʼ���ȡ�{currentMultiplier:F2}��";

            modeStatusText.text = $"{timeInfo}\n{phaseInfo}\n{brightnessInfo}";
        }

        if (brightnessValueText != null)
        {
            brightnessValueText.text = $"{currentMultiplier:F2}��";
        }

        // ͬ�����鷶Χ��ֵ
        if (brightnessSlider != null)
        {
            brightnessSlider.maxValue = maxManualMultiplier;
            if (controlMode == ControlMode.Manual)
            {
                brightnessSlider.value = manualBrightnessMultiplier;
            }
            else
            {
                // �Զ�ģʽ�»�����ʾ��ǰ������ֻ������ͨ�����������Ƿ������϶���
                brightnessSlider.value = currentMultiplier;
            }
        }
    }

    /// <summary>
    /// �༭��ģʽ��ʵʱ���£��޸Ĳ���������Ч��
    /// </summary>
    void OnValidate()
    {
        if (!Application.isPlaying) return;

        // ���Ʋ�����Χ
        manualBrightnessMultiplier = Mathf.Clamp(manualBrightnessMultiplier, 0f, maxManualMultiplier);
        maxManualMultiplier = Mathf.Max(maxManualMultiplier, 1f);
        dayMaxMultiplier = Mathf.Max(dayMaxMultiplier, 0f);
        nightMaxMultiplier = Mathf.Max(nightMaxMultiplier, 0f);
        timeTransitionDuration = Mathf.Max(timeTransitionDuration, 0.1f);

        // ͬ��UI
        UpdateModeUI();

        // ����Ŀ�걶��
        UpdateTargetMultiplierByMode();
    }

    // ------------------- �ⲿ���ýӿ� -------------------
    /// <summary>
    /// �л�����ģʽ
    /// </summary>
    public void SetControlMode(ControlMode newMode)
    {
        controlMode = newMode;
        UpdateTargetMultiplierByMode();
        UpdateModeUI();
    }

    /// <summary>
    /// �����ֶ����ȱ�����֧�ֳ��
    /// </summary>
    public void SetManualBrightnessMultiplier(float multiplier)
    {
        manualBrightnessMultiplier = Mathf.Clamp(multiplier, 0f, maxManualMultiplier);
        if (controlMode == ControlMode.Manual)
        {
            targetMultiplier = manualBrightnessMultiplier;
        }
        if (brightnessSlider != null)
        {
            brightnessSlider.value = manualBrightnessMultiplier;
        }
    }

    /// <summary>
    /// �޸��Զ�ģʽ�İ���/���������
    /// </summary>
    public void SetAutoModeMaxMultipliers(float dayMultiplier, float nightMultiplier)
    {
        dayMaxMultiplier = Mathf.Max(dayMultiplier, 0f);
        nightMaxMultiplier = Mathf.Max(nightMultiplier, 0f);
        if (controlMode == ControlMode.Auto)
        {
            UpdateTargetMultiplierByTime();
        }
    }

    /// <summary>
    /// �޸��ֶ�ģʽ�����������
    /// </summary>
    public void SetMaxManualMultiplier(float maxMultiplier)
    {
        maxManualMultiplier = Mathf.Max(maxMultiplier, 1f);
        if (brightnessSlider != null)
        {
            brightnessSlider.maxValue = maxManualMultiplier;
        }
        manualBrightnessMultiplier = Mathf.Clamp(manualBrightnessMultiplier, 0f, maxManualMultiplier);
    }
}