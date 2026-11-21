using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 独立命名，避免与主项目现有的 VehicleHUD 同名冲突。
public class CarSystemVehicleHUD : MonoBehaviour
{
    static string DefaultControlsText => Loc.Choose(
        "<b>操控方式</b>\n\n" +
        "W  —  前进\n" +
        "S  —  倒车\n" +
        "A  —  左转\n" +
        "D  —  右转\n" +
        "Space — 刹车\n" +
        "Esc — 下车",
        "<b>Controls</b>\n\n" +
        "W  —  Forward\n" +
        "S  —  Reverse\n" +
        "A  —  Turn left\n" +
        "D  —  Turn right\n" +
        "Space — Brake\n" +
        "Esc — Exit vehicle");

    [Header("UI 绑定")]
    public GameObject root;
    public GameObject speedPanel;
    public GameObject promptPanel;
    public GameObject controlsPanel;
    public TMP_Text speedText;
    public TMP_Text promptText;
    public TMP_Text controlsText;

    [Header("共享交互提示")]
    public bool preferSharedPrompt = true;
    public bool autoFindSharedPrompt = true;
    public RectTransform sharedPromptPanel;
    public TextMeshProUGUI sharedPromptNameText;
    public TextMeshProUGUI sharedPromptTypeText;

    [Header("速度表")]
    [Min(0.001f)]
    public float displayScale = 1f;
    public string speedSuffix = " km/h";

    [Header("提示")]
    [Tooltip("与 InteractableVehicle.interactDistance + distanceBuffer 大致一致，远处即显示上车提示")]
    public float promptMaxDistance = 10f;

    Rigidbody _vehicleRb;
    bool _driving;
    int _sharedPromptGroupId;

    void OnEnable() => Loc.OnLanguageChanged += RefreshControlsText;
    void OnDisable() => Loc.OnLanguageChanged -= RefreshControlsText;
    void RefreshControlsText()
    {
        if (controlsText != null)
            controlsText.text = DefaultControlsText;
    }

    void Awake()
    {
        if (root == null)
            root = gameObject;

        ResolveSharedPrompt();
        EnsureControlsPanelBuilt();

        if (root != null && root != gameObject)
            root.SetActive(false);
        SetPanelVisible(speedPanel, false);
        SetPanelVisible(promptPanel, false);
        SetPanelVisible(controlsPanel, false);
    }

    void LateUpdate()
    {
        if (_driving || !preferSharedPrompt)
            return;

        ResolveSharedPrompt();
        if (_sharedPromptGroupId != 0)
            InteractPromptSharedLabelFrame.ApplyBestToSharedTmp(sharedPromptNameText, sharedPromptTypeText);
    }

    public void EnterDriving(Rigidbody vehicleRb, string controlsContent = null)
    {
        _vehicleRb = vehicleRb;
        _driving = true;
        UIInteractionState.SetDrivingHudActive(true);
        UIInteractionState.SetNpcShopOpen(false);

        if (root != null && root != gameObject)
            root.SetActive(true);
        SetPanelVisible(speedPanel, true);
        SetPanelVisible(promptPanel, false);
        SetPanelVisible(controlsPanel, controlsText != null);

        if (promptText != null)
            promptText.text = "";
        if (sharedPromptNameText != null)
            sharedPromptNameText.text = "";
        if (sharedPromptTypeText != null)
            sharedPromptTypeText.text = "";
        if (controlsText != null)
            controlsText.text = string.IsNullOrWhiteSpace(controlsContent) ? DefaultControlsText : controlsContent;
        if (speedText != null && string.IsNullOrWhiteSpace(speedText.text))
            speedText.text = "0" + speedSuffix;
    }

    public void ExitDriving()
    {
        _driving = false;
        _vehicleRb = null;
        UIInteractionState.SetDrivingHudActive(false);
        SetPanelVisible(speedPanel, false);
        SetPanelVisible(controlsPanel, false);
        ClearPrompt();
    }

    public void UpdateSpeed(float speedKmh)
    {
        if (speedText == null)
            return;

        int v = Mathf.Max(0, Mathf.RoundToInt(speedKmh * displayScale));
        speedText.text = v + speedSuffix;
    }

    public void OfferPrompt(float distance, string name, string typeLine, Vector3? worldAnchor = null)
    {
        if (_driving || UIInteractionState.BlocksWorldPrompt || distance > promptMaxDistance)
            return;

        if (TryOfferSharedPrompt(distance, name, typeLine, worldAnchor))
            return;

        if (promptText == null)
            return;

        if (root != null && root != gameObject && !root.activeSelf)
            root.SetActive(true);
        SetPanelVisible(promptPanel, true);
        SetPanelVisible(speedPanel, false);
        SetPanelVisible(controlsPanel, false);

        if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(typeLine))
        {
            promptText.text = "";
            return;
        }

        if (string.IsNullOrEmpty(name))
            promptText.text = typeLine;
        else if (string.IsNullOrEmpty(typeLine))
            promptText.text = name;
        else
            promptText.text = name + "\n" + typeLine;
    }

    public void ClearPrompt(bool hideRootWhenIdle = true)
    {
        if (promptText != null)
            promptText.text = "";

        SetPanelVisible(promptPanel, false);

        if (_driving)
            return;

        SetPanelVisible(speedPanel, false);
        SetPanelVisible(controlsPanel, false);
        if (hideRootWhenIdle && root != null && root != gameObject)
            root.SetActive(false);
    }

    void Update()
    {
        if (!_driving || _vehicleRb == null)
            return;

        Vector3 v = _vehicleRb.velocity;
        v.y = 0f;
        UpdateSpeed(v.magnitude * 3.6f);
    }

    bool TryOfferSharedPrompt(float distance, string name, string typeLine, Vector3? worldAnchor)
    {
        if (!preferSharedPrompt)
            return false;

        ResolveSharedPrompt();
        if (_sharedPromptGroupId == 0)
            return false;

        InteractPromptSharedLabelFrame.Offer(_sharedPromptGroupId, distance, name, typeLine, worldAnchor);
        return true;
    }

    void ResolveSharedPrompt()
    {
        if (!preferSharedPrompt)
        {
            _sharedPromptGroupId = 0;
            return;
        }

        if (autoFindSharedPrompt)
            InteractPromptSharedUiLocator.TryAutoBind(ref sharedPromptPanel, ref sharedPromptNameText, ref sharedPromptTypeText);

        _sharedPromptGroupId = InteractPromptSharedLabelFrame.ResolveOfferGroupId(sharedPromptNameText, sharedPromptTypeText);
    }

    void EnsureControlsPanelBuilt()
    {
        if (controlsPanel != null && controlsText != null)
            return;
        if (root == null)
            return;

        var rootRt = root.GetComponent<RectTransform>();
        if (rootRt == null)
            return;

        if (controlsPanel == null)
        {
            controlsPanel = new GameObject("ControlsPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            controlsPanel.transform.SetParent(root.transform, false);

            var rt = controlsPanel.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-20f, 20f);
            rt.sizeDelta = new Vector2(240f, 220f);

            var image = controlsPanel.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.55f);
        }

        if (controlsText == null)
        {
            var textGo = new GameObject("ControlsText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(controlsPanel.transform, false);

            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(16f, 12f);
            textRt.offsetMax = new Vector2(-16f, -12f);

            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = 16f;
            tmp.enableWordWrapping = true;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.text = DefaultControlsText;
            controlsText = tmp;
        }
    }

    void SetPanelVisible(GameObject panel, bool visible)
    {
        if (panel != null)
            panel.SetActive(visible);
    }
}

