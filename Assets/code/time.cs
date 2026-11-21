using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

[RequireComponent(typeof(Light))]
public class SunSimulation : MonoBehaviour
{
    [Header("???????????????????")]
    public Transform centerObject; // ???????????????????????????????

    [Header("??????????")]
    public float sunDistance = 100f;
    public float maxElevation = 70f;
    public float sunriseHour = 6f;
    public float sunsetHour = 18f;

    [Header("????????")]
    public Transform moon;
    public float moonDistance = 120f;
    public float moonMaxElevation = 50f;

    [Header("????????")]
    public ParticleSystem stars;
    public float starFadeDuration = 0.5f;

    [Header("??????")]
    public float dayLengthInSeconds = 60f;
    [Range(0, 24)] public float currentTimeOfDay = 6f;

    [Header("Celestial Visual")]
    public float celestialVisualScale = 50f;

    [Header("???????????")]
    public Gradient skyColorGradient;
    public Gradient sunColorGradient;
    public Gradient moonColorGradient;
    public float maxLightIntensity = 1f;
    public float maxMoonIntensity = 0.2f;

    [Header("UI?????TextMeshPro??")]
    public TMP_Text timeDisplayText;
    public TMP_Text dayLengthDisplayText;
    public Button startStopButton;
    public Button resetButton;
    public Slider dayLengthSlider;

    // ???????
    private Light sunLight;
    private Light moonLight;
    private Camera mainCamera;
    private ParticleSystem.EmissionModule starEmission;
    private float starAlpha = 0;
    private bool isRunning = true;
    private string startStopButtonText = "\u6682\u505c";
    private int _lastDisplayedMinute = -1;
    private int _lastDisplayedDayLength = -1;

    void Start()
    {
        sunLight = GetComponent<Light>();
        mainCamera = Camera.main ?? Camera.current;

        // ??????????????????????????????????
        if (centerObject == null)
        {
            centerObject = new GameObject("DefaultCenter").transform;
            Debug.LogWarning("\u672a\u6307\u5b9a\u4e2d\u5fc3\u7269\u4f53\uff0c\u9ed8\u8ba4\u4f7f\u7528\u65b0\u521b\u5efa\u7684\u539f\u70b9\u7269\u4f53\uff01");
        }

        if (moon != null)
        {
            moonLight = moon.GetComponent<Light>();
            if (moonLight == null)
            {
                moonLight = moon.gameObject.AddComponent<Light>();
                moonLight.type = LightType.Point;
                moonLight.range = 20;
            }
        }

        if (stars != null)
        {
            starEmission = stars.emission;
            starEmission.enabled = false;
        }

        if (dayLengthSlider != null)
        {
            dayLengthSlider.minValue = 10;
            dayLengthSlider.maxValue = 300;
            dayLengthSlider.value = dayLengthInSeconds;
            dayLengthSlider.onValueChanged.AddListener(OnDayLengthChanged);
        }

        if (startStopButton != null)
        {
            startStopButton.onClick.AddListener(ToggleSimulation);
            UpdateStartStopButtonText();
        }

        if (resetButton != null)
        {
            resetButton.onClick.AddListener(ResetTime);
            TMP_Text resetBtnText = resetButton.GetComponentInChildren<TMP_Text>();
            if (resetBtnText != null)
                resetBtnText.text = "\u91cd\u7f6e\u5230\u65e5\u51fa";
        }

        EnsureRuntimeDefaults();
        UpdateUI();
    }

    public bool IsRunning => isRunning;

    public static SunSimulation FindBestInScene(Scene scene)
    {
        SunSimulation best = null;
        foreach (var sim in Object.FindObjectsOfType<SunSimulation>(true))
        {
            if (sim == null || sim.gameObject.scene != scene)
                continue;
            if (best == null || sim.enabled)
                best = sim;
        }
        return best;
    }

    public void EnsureRuntimeDefaults()
    {
        if (centerObject == null)
        {
            var existing = GameObject.Find("SunCenter");
            if (existing != null)
                centerObject = existing.transform;
        }

        if (sunLight == null)
            sunLight = GetComponent<Light>();

        if (sunLight != null)
        {
            sunLight.type = LightType.Point;
            if (sunLight.range < 100f)
                sunLight.range = 10000f;
            sunLight.shadows = LightShadows.None;
        }

        CelestialBodyFactory.EnsureMoonForSunSimulation(this, gameObject.scene);
        CelestialBodyFactory.UpgradeSunVisual(transform, celestialVisualScale);
        if (moon != null)
        {
            if (moonLight == null)
                moonLight = moon.GetComponent<Light>();
            if (moonLight != null)
            {
                moonLight.type = LightType.Point;
                if (moonLight.range < 100f)
                    moonLight.range = 10000f;
                moonLight.shadows = LightShadows.None;
            }
            CelestialBodyFactory.UpgradeMoonVisual(moon, celestialVisualScale);
        }
    }

    public void ApplyHostTimeSnapshot(float timeOfDay, bool running, float dayLengthSeconds)
    {
        currentTimeOfDay = timeOfDay;
        isRunning = running;
        dayLengthInSeconds = dayLengthSeconds;
        startStopButtonText = isRunning ? "\u6682\u505c" : "\u5f00\u59cb";
        UpdateStartStopButtonText();
    }

    void Update()
    {
        if (isRunning)
        {
            float timeIncrement = (24f / dayLengthInSeconds) * Time.deltaTime;
            currentTimeOfDay += timeIncrement;
            if (currentTimeOfDay >= 24)
                currentTimeOfDay = 0;
        }

        UpdateSunPosition();
        UpdateMoonPosition();
        UpdateLighting();
        UpdateStars();
        UpdateUI();
    }

    // ????????????????????????????
    void UpdateSunPosition()
    {
        if (centerObject == null) return;

        float elevation = -20;
        float azimuth = 0;

        if (currentTimeOfDay >= sunriseHour && currentTimeOfDay <= sunsetHour)
        {
            float dayProgress = (currentTimeOfDay - sunriseHour) / (sunsetHour - sunriseHour);
            elevation = Mathf.Sin(dayProgress * Mathf.PI) * maxElevation;
            azimuth = 90 + dayProgress * 180;
        }

        float elevationRad = elevation * Mathf.Deg2Rad;
        float azimuthRad = azimuth * Mathf.Deg2Rad;

        // ???????????????????????
        float x = sunDistance * Mathf.Cos(elevationRad) * Mathf.Sin(azimuthRad);
        float y = sunDistance * Mathf.Sin(elevationRad);
        float z = sunDistance * Mathf.Cos(elevationRad) * Mathf.Cos(azimuthRad);

        // ???????? = ???????????? + ?????
        transform.position = centerObject.position + new Vector3(x, y, z);
    }

    // ?????????????????????????????
    void UpdateMoonPosition()
    {
        if (moon == null || centerObject == null) return;

        float elevation = -20;
        float azimuth = 0;

        if (currentTimeOfDay <= sunriseHour || currentTimeOfDay >= sunsetHour)
        {
            float nightProgress;
            if (currentTimeOfDay >= sunsetHour)
            {
                nightProgress = (currentTimeOfDay - sunsetHour) / (24 - sunsetHour + sunriseHour);
            }
            else
            {
                nightProgress = (currentTimeOfDay + (24 - sunsetHour)) / (24 - sunsetHour + sunriseHour);
            }

            elevation = Mathf.Sin(nightProgress * Mathf.PI) * moonMaxElevation;
            azimuth = 270 - nightProgress * 180;
        }

        float elevationRad = elevation * Mathf.Deg2Rad;
        float azimuthRad = azimuth * Mathf.Deg2Rad;

        float x = moonDistance * Mathf.Cos(elevationRad) * Mathf.Sin(azimuthRad);
        float y = moonDistance * Mathf.Sin(elevationRad);
        float z = moonDistance * Mathf.Cos(elevationRad) * Mathf.Cos(azimuthRad);

        moon.position = centerObject.position + new Vector3(x, y, z);
    }

    // ??????????UI?????????????????????
    void UpdateLighting()
    {
        float fullDayCycle = currentTimeOfDay / 24f;
        if (mainCamera != null)
        {
            mainCamera.backgroundColor = skyColorGradient.Evaluate(fullDayCycle);
        }

        bool isDaytime = currentTimeOfDay > sunriseHour && currentTimeOfDay < sunsetHour;
        if (isDaytime)
        {
            float dayProgress = (currentTimeOfDay - sunriseHour) / (sunsetHour - sunriseHour);
            sunLight.color = sunColorGradient.Evaluate(dayProgress);
            sunLight.intensity = Mathf.Sin(dayProgress * Mathf.PI) * maxLightIntensity;
            sunLight.enabled = true;
        }
        else
        {
            sunLight.enabled = false;
        }

        if (moonLight != null)
        {
            bool isNighttime = !isDaytime;
            if (isNighttime)
            {
                float nightProgress = 0;
                if (currentTimeOfDay >= sunsetHour)
                {
                    nightProgress = (currentTimeOfDay - sunsetHour) / (24 - sunsetHour);
                }
                else
                {
                    nightProgress = (currentTimeOfDay + (24 - sunsetHour)) / (24 - sunsetHour + sunriseHour);
                }

                moonLight.color = moonColorGradient.Evaluate(nightProgress);
                moonLight.intensity = Mathf.Sin(nightProgress * Mathf.PI) * maxMoonIntensity;
                moonLight.enabled = true;
            }
            else
            {
                moonLight.enabled = false;
            }
        }
    }

    void UpdateStars()
    {
        if (stars == null) return;

        bool isNighttime = currentTimeOfDay < sunriseHour || currentTimeOfDay > sunsetHour;
        float targetAlpha = isNighttime ? 1f : 0f;
        starAlpha = Mathf.MoveTowards(starAlpha, targetAlpha, Time.deltaTime / starFadeDuration);
        starEmission.enabled = starAlpha > 0.1f;

        var colorOverLifetime = stars.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient alphaGradient = new Gradient();
        alphaGradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f) },
            new GradientAlphaKey[] {
                new GradientAlphaKey(starAlpha, 0f),
                new GradientAlphaKey(starAlpha * 0.5f, 1f)
            }
        );
        colorOverLifetime.color = alphaGradient;

        if (starAlpha <= 0.01f)
        {
            stars.Clear();
        }
    }

    public void ToggleSimulation()
    {
        isRunning = !isRunning;
        startStopButtonText = isRunning ? "\u6682\u505c" : "\u5f00\u59cb";
        UpdateStartStopButtonText();
    }

    private void UpdateStartStopButtonText()
    {
        if (startStopButton != null)
        {
            TMP_Text btnText = startStopButton.GetComponentInChildren<TMP_Text>();
            if (btnText != null)
                btnText.text = startStopButtonText;
        }
    }

    public void OnDayLengthChanged(float value)
    {
        dayLengthInSeconds = value;
    }

    void UpdateUI()
    {
        if (timeDisplayText != null)
        {
            int hours = (int)currentTimeOfDay;
            int minutes = (int)((currentTimeOfDay - hours) * 60);
            int totalMinutes = hours * 60 + minutes;
            if (_lastDisplayedMinute != totalMinutes)
            {
                _lastDisplayedMinute = totalMinutes;
                timeDisplayText.text = $"\u5f53\u524d\u65f6\u95f4: {hours:D2}:{minutes:D2}";
            }
        }

        if (dayLengthDisplayText != null)
        {
            int roundedDayLength = Mathf.RoundToInt(dayLengthInSeconds);
            if (_lastDisplayedDayLength != roundedDayLength)
            {
                _lastDisplayedDayLength = roundedDayLength;
                dayLengthDisplayText.text = $"\u4e00\u5929\u957f\u5ea6: {roundedDayLength}\u79d2";
            }
        }
    }

    public void ResetTime()
    {
        currentTimeOfDay = sunriseHour;
    }
}