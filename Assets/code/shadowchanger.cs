using UnityEngine;

public class TimeBasedShadowController : MonoBehaviour
{
    [Header("ʱ��ϵͳ����")]
    public GameObject sunSimulationObject; // �������SunSimulation������
    private SunSimulation sunSimulation;

    [Header("�չ����ã����죩")]
    public Light sunLight; // ����̫��ƽ�й�
    public float sunriseAngle = 10f; // �ճ�ʱ̫��������
    public float noonAngle = 70f; // ����ʱ̫��������
    public float sunsetAngle = 10f; // ����ʱ̫��������
    public LightShadows sunShadowType = LightShadows.Soft; // ̫����Ӱ����

    [Header("�¹����ã�ҹ����")]
    public Light moonLight; // ��������ƽ�й⣨���ֶ�������
    public float moonAngle = 30f; // �����̶������ǣ�ҹ���������仯��
    public float moonStartYRotation = 270f; // ҹ����ʼʱ������λ�ǣ�������
    public float moonEndYRotation = 90f; // ҹ������ʱ������λ�ǣ�������
    public LightShadows moonShadowType = LightShadows.Hard; // ������Ӱ����
    public Color moonLightColor = new Color(0.9f, 0.95f, 1f); // �¹���ɫ�����ɫ��
    public float moonIntensity = 0.3f; // �¹�ǿ�ȣ���̫������
    private bool? _lastDaytimeState;

    [Header("ʱ������")]
    public float sunriseHour = 5f;  // �ճ�ʱ�䣨24Сʱ�ƣ�
    public float noonHour = 12f;    // ����ʱ��
    public float sunsetHour = 18f;  // ����ʱ��
    public float midnightHour = 0f; // ��ҹʱ�䣨�����¹���ɣ�

    void Start()
    {
        // ��֤ʱ��ϵͳ
        if (sunSimulationObject == null)
        {
            Debug.LogError("�����ù���SunSimulation�����壡");
            enabled = false;
            return;
        }
        sunSimulation = sunSimulationObject.GetComponent<SunSimulation>();
        if (sunSimulation == null)
        {
            Debug.LogError($"{sunSimulationObject.name}��δ�ҵ�SunSimulation�����");
            enabled = false;
            return;
        }

        // ��֤�չ��Դ
        if (sunLight == null || sunLight.type != LightType.Directional)
        {
            Debug.LogError("��ָ��һ��ƽ�й���Ϊ̫����");
            enabled = false;
            return;
        }

        // ��֤�¹��Դ������Ϊ�գ�Ϊ����ҹ��̫������������
        if (moonLight != null)
        {
            if (moonLight.type != LightType.Directional)
            {
                Debug.LogError("������Դ������ƽ�й⣡");
                moonLight = null;
            }
            else
            {
                // ��ʼ���¹����
                moonLight.color = moonLightColor;
                moonLight.intensity = moonIntensity;
                moonLight.shadows = moonShadowType;
                moonLight.enabled = false; // ��ʼ�رգ�ҹ���Զ�����
            }
        }
        else
        {
            Debug.LogWarning("δ����������Դ��ҹ����ʹ��̫����ԴͶ����Ӱ������ʵ�߼�����");
        }

        // ��ʼ���¹�Ӱ
        UpdateLightAndShadows();
    }

    void Update()
    {
        if (sunSimulation == null || sunLight == null) return;
        UpdateLightAndShadows();
    }

    // ���ģ����ݵ�ǰʱ�����̫��/�����ĽǶȺ���Ӱ
    private void UpdateLightAndShadows()
    {
        float currentHour = sunSimulation.currentTimeOfDay;

        // 1. �����߼����ճ�-���䣩
        if (currentHour >= sunriseHour && currentHour <= sunsetHour)
        {
            if (_lastDaytimeState != true)
            {
                sunLight.enabled = true;
                sunLight.shadows = sunShadowType;
                if (moonLight != null) moonLight.enabled = false;
                _lastDaytimeState = true;
            }

            // ����̫�������ǣ�x����ת������Ӱ�ӳ��ȣ�
            float xRotation = CalculateSunXRotation(currentHour);
            // ����̫����λ�ǣ�y����ת������Ӱ�ӷ���
            float yRotation = CalculateSunYRotation(currentHour);

            // Ӧ��̫����ת����Ӱ
            sunLight.transform.rotation = Quaternion.Euler(xRotation, yRotation, 0f);
        }
        // 2. ҹ���߼�������-�ճ���
        else
        {
            // �����������ر�̫��������������Դ��
            if (moonLight != null)
            {
                if (_lastDaytimeState != false)
                {
                    moonLight.enabled = true;
                    moonLight.shadows = moonShadowType;
                    sunLight.enabled = false;
                    _lastDaytimeState = false;
                }

                // ����������λ�ǣ�ҹ���������ƶ���
                float yRotation = CalculateMoonYRotation(currentHour);
                // Ӧ��������ת�������ǹ̶�����λ����ʱ��仯��
                moonLight.transform.rotation = Quaternion.Euler(moonAngle, yRotation, 0f);
            }
            // ��������Դʱ����̫����ҹ�������ض��Ƕȣ����÷�����
            else
            {
                if (_lastDaytimeState != false)
                {
                    sunLight.enabled = true;
                    sunLight.shadows = sunShadowType;
                    _lastDaytimeState = false;
                }
                // ҹ��̫���Ƕȣ�����������������£�Ͷ�䳤��Ӱ��
                sunLight.transform.rotation = Quaternion.Euler(-30f, 135f, 0f);
                sunLight.intensity = 0.2f; // ����̫��ǿ�ȣ�ģ��ҹ������
            }
        }
    }

    // ����̫�������ǣ��ճ�����������䣩
    private float CalculateSunXRotation(float currentHour)
    {
        float xRotation = 0f;
        if (currentHour >= sunriseHour && currentHour <= noonHour)
        {
            // �ճ������磺��sunriseAngle����noonAngle
            float progress = (currentHour - sunriseHour) / (noonHour - sunriseHour);
            xRotation = Mathf.Lerp(sunriseAngle, noonAngle, progress);
        }
        else if (currentHour > noonHour && currentHour <= sunsetHour)
        {
            // ���絽���䣺��noonAngle����sunsetAngle
            float progress = (currentHour - noonHour) / (sunsetHour - noonHour);
            xRotation = Mathf.Lerp(noonAngle, sunsetAngle, progress);
        }
        return xRotation;
    }

    // ����̫����λ�ǣ��ճ������䣺����������ת180�㣩
    private float CalculateSunYRotation(float currentHour)
    {
        float dayProgress = (currentHour - sunriseHour) / (sunsetHour - sunriseHour);
        return 90f + dayProgress * 180f; // 90��=������270��=����
    }

    // ����������λ�ǣ�������ճ�������������ת180�㣩
    private float CalculateMoonYRotation(float currentHour)
    {
        float nightProgress = 0f;
        // ��������ҹ������������24����ճ���
        if (currentHour >= sunsetHour || currentHour <= sunriseHour)
        {
            if (currentHour >= sunsetHour)
            {
                // �������ҹ����moonStartYRotation������������ҹ����
                nightProgress = (currentHour - sunsetHour) / (24f - sunsetHour + sunriseHour);
            }
            else
            {
                // ��ҹ���ճ�������ҹ��moonEndYRotation������������
                nightProgress = (currentHour + (24f - sunsetHour)) / (24f - sunsetHour + sunriseHour);
            }
        }
        // ������λ�ǣ�������270�㣩������90�㣩����ת180��
        return Mathf.Lerp(moonStartYRotation, moonEndYRotation, nightProgress);
    }
}