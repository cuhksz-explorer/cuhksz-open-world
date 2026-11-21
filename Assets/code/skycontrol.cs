using UnityEngine;

public class MultiTimeSkyboxSwitcher : MonoBehaviour
{
    [Header("ʱ��ϵͳ����")]
    public GameObject sunSimulationObject; // �������SunSimulation������
    private SunSimulation sunSimulation;

    [Header("��ʱ����պв���")]
    public Material skyboxSunrise; // ����Skybox_Sunrise����
    public Material skyboxDay;     // ����Skybox_Day����
    public Material skyboxDusk;    // ����Skybox_Dusk����
    public Material skyboxNight;   // ����Skybox_Night����

    [Header("ʱ��ʱ������")]
    public float sunriseStart = 5f;   // �ճ���ʼ��5�㣩
    public float sunriseEnd = 7f;     // �ճ�������7�㣩
    public float dayEnd = 16f;        // ���������16�㣩
    public float duskEnd = 18f;       // ����������18�㣩
    private Material currentSkybox;

    private void Start()
    {
        // ��֤ʱ��ϵͳ
        if (sunSimulationObject == null)
        {
            Debug.LogError("���������SunSimulation�����壡");
            enabled = false;
            return;
        }
        sunSimulation = sunSimulationObject.GetComponent<SunSimulation>();
        if (sunSimulation == null)
        {
            Debug.LogError($"{sunSimulationObject.name}��û��SunSimulation�ű���");
            enabled = false;
            return;
        }

        // ��֤��պв���
        if (skyboxSunrise == null || skyboxDay == null || skyboxDusk == null || skyboxNight == null)
        {
            Debug.LogError("��ȷ��������պв�������ȷ��ֵ��");
            enabled = false;
            return;
        }

        // ��ʼ����պ�
        UpdateSkybox();
    }

    private void Update()
    {
        if (sunSimulation == null) return;
        // ʵʱ������պ�
        UpdateSkybox();
    }

    // ���ģ�����ʱ���л���պ�
    private void UpdateSkybox()
    {
        float currentHour = sunSimulation.currentTimeOfDay;

        Material nextSkybox;
        if (currentHour >= sunriseStart && currentHour < sunriseEnd)
            nextSkybox = skyboxSunrise;
        else if (currentHour >= sunriseEnd && currentHour < dayEnd)
            nextSkybox = skyboxDay;
        else if (currentHour >= dayEnd && currentHour < duskEnd)
            nextSkybox = skyboxDusk;
        else
            nextSkybox = skyboxNight;

        if (currentSkybox == nextSkybox)
            return;

        currentSkybox = nextSkybox;
        RenderSettings.skybox = nextSkybox;
    }

    // �༭��ʵʱԤ��
    private void OnValidate()
    {
        if (Application.isPlaying && sunSimulation != null)
        {
            UpdateSkybox();
        }
    }
}