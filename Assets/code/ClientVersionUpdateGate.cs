using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Loading 场景进游戏前的客户端版本校验与热更新。
/// </summary>
public class ClientVersionUpdateGate : MonoBehaviour
{
    public static ClientVersionUpdateGate Instance { get; private set; }

    [Header("行为")]
    [SerializeField] bool runOnStart = true;
    [SerializeField] bool blockUntilReady = true;
    [SerializeField] bool skipWhenOfflineMode = true;

    [Header("UI")]
    [SerializeField] bool autoCreateProgressUi = true;
    [SerializeField] int progressCanvasSortOrder = 50000;

    [Header("引用")]
    [SerializeField] GameServerSync gameServerSync;

    GameObject _uiRoot;
    Slider _progressSlider;
    Text _progressLabel;
    Button _restartButton;
    Coroutine _running;

    public static bool IsReady => Instance == null || ClientVersionUpdateService.IsReady;
    public static bool IsBusy => ClientVersionUpdateService.IsBusy;

    public static ClientVersionUpdateGate EnsureExists()
    {
        if (Instance != null)
            return Instance;

        var existing = FindObjectOfType<ClientVersionUpdateGate>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        var go = new GameObject("ClientVersionUpdateGate");
        return go.AddComponent<ClientVersionUpdateGate>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        if (gameServerSync == null)
            gameServerSync = FindObjectOfType<GameServerSync>();
    }

    void OnDestroy()
    {
        ClientVersionUpdateService.OnProgress -= HandleProgress;
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        if (runOnStart)
            BeginUpdate();
    }

    public void BeginUpdate()
    {
        if (_running != null)
            StopCoroutine(_running);
        _running = StartCoroutine(CoRunGate());
    }

    IEnumerator CoRunGate()
    {
        ClientVersionUpdateService.ResetState();
        ClientVersionUpdateService.OnProgress -= HandleProgress;
        ClientVersionUpdateService.OnProgress += HandleProgress;

        if (autoCreateProgressUi && _uiRoot == null)
            BuildProgressUi();

        SetUiVisible(true);
        SetRestartVisible(false);
        HandleProgress(0f, "正在检查客户端版本…");

        bool offline = skipWhenOfflineMode &&
                       gameServerSync != null &&
                       gameServerSync.AllowLocalPlayWithoutGameServer;

        string baseUrl = gameServerSync != null
            ? gameServerSync.BaseUrlTrimmed
            : GameServerEndpoint.DefaultBaseUrl;

        yield return ClientVersionUpdateService.EnsureLatestClientCoroutine(baseUrl, offline);

        if (ClientVersionUpdateService.UpdateAppliedPendingRestart)
        {
            string hint = ClientVersionUpdateService.RestartHint ??
                            "更新已就绪，请重启游戏以应用补丁。";
            HandleProgress(1f, hint);
            SetRestartVisible(true);
            _running = null;
            yield break;
        }

        if (!ClientVersionUpdateService.IsReady)
        {
            string err = ClientVersionUpdateService.LastError;
            HandleProgress(0f, string.IsNullOrEmpty(err) ? "客户端更新失败" : ("客户端更新失败: " + err));
            if (blockUntilReady)
            {
                _running = null;
                yield break;
            }
        }
        else
        {
            HandleProgress(1f, $"客户端版本就绪 (v{ClientBuildVersion.LocalVersion})");
        }

        SetUiVisible(false);
        ClientVersionUpdateService.OnProgress -= HandleProgress;
        _running = null;
    }

    void HandleProgress(float value, string message)
    {
        if (_progressSlider != null)
            _progressSlider.value = value;
        if (_progressLabel != null)
            _progressLabel.text = message ?? "";
    }

    void OnRestartClicked()
    {
        if (_restartButton != null)
            _restartButton.interactable = false;
        HandleProgress(1f, "正在退出并应用更新，请稍候…");
        ClientVersionUpdateService.LaunchPendingUpdateAndQuit();
    }

    void BuildProgressUi()
    {
        _uiRoot = new GameObject("ClientUpdateProgressUI");
        _uiRoot.transform.SetParent(transform, false);

        var canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(_uiRoot.transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = progressCanvasSortOrder;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        var panel = new GameObject("Panel");
        panel.transform.SetParent(canvasGo.transform, false);
        var panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.78f);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(panel.transform, false);
        _progressLabel = labelGo.AddComponent<Text>();
        _progressLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _progressLabel.fontSize = 28;
        _progressLabel.alignment = TextAnchor.MiddleCenter;
        _progressLabel.color = Color.white;
        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.08f, 0.55f);
        labelRect.anchorMax = new Vector2(0.92f, 0.72f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var sliderGo = new GameObject("Slider");
        sliderGo.transform.SetParent(panel.transform, false);
        _progressSlider = sliderGo.AddComponent<Slider>();
        _progressSlider.minValue = 0f;
        _progressSlider.maxValue = 1f;
        var sliderRect = sliderGo.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.15f, 0.42f);
        sliderRect.anchorMax = new Vector2(0.85f, 0.48f);
        sliderRect.offsetMin = Vector2.zero;
        sliderRect.offsetMax = Vector2.zero;

        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(sliderGo.transform, false);
        var bgImage = bgGo.AddComponent<Image>();
        bgImage.color = new Color(1f, 1f, 1f, 0.25f);
        var bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderGo.transform, false);
        var fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = Vector2.zero;
        fillAreaRect.offsetMax = Vector2.zero;

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(fillArea.transform, false);
        var fillImage = fillGo.AddComponent<Image>();
        fillImage.color = new Color(0.2f, 0.65f, 0.95f, 0.95f);
        var fillRect = fillGo.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        _progressSlider.fillRect = fillRect;
        _progressSlider.targetGraphic = fillImage;

        var restartGo = new GameObject("RestartButton");
        restartGo.transform.SetParent(panel.transform, false);
        var restartRect = restartGo.AddComponent<RectTransform>();
        restartRect.anchorMin = new Vector2(0.32f, 0.24f);
        restartRect.anchorMax = new Vector2(0.68f, 0.34f);
        restartRect.offsetMin = Vector2.zero;
        restartRect.offsetMax = Vector2.zero;
        var restartImage = restartGo.AddComponent<Image>();
        restartImage.color = new Color(0.18f, 0.55f, 0.92f, 0.95f);
        _restartButton = restartGo.AddComponent<Button>();
        _restartButton.targetGraphic = restartImage;
        _restartButton.onClick.AddListener(OnRestartClicked);

        var restartTextGo = new GameObject("Text");
        restartTextGo.transform.SetParent(restartGo.transform, false);
        var restartText = restartTextGo.AddComponent<Text>();
        restartText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        restartText.fontSize = 24;
        restartText.alignment = TextAnchor.MiddleCenter;
        restartText.color = Color.white;
        restartText.text = "重启游戏";
        var restartTextRect = restartTextGo.GetComponent<RectTransform>();
        restartTextRect.anchorMin = Vector2.zero;
        restartTextRect.anchorMax = Vector2.one;
        restartTextRect.offsetMin = Vector2.zero;
        restartTextRect.offsetMax = Vector2.zero;

        restartGo.SetActive(false);
    }

    void SetUiVisible(bool visible)
    {
        if (_uiRoot != null)
            _uiRoot.SetActive(visible);
    }

    void SetRestartVisible(bool visible)
    {
        if (_restartButton != null)
        {
            _restartButton.gameObject.SetActive(visible);
            _restartButton.interactable = visible;
        }
    }
}
