using TMPro;
using UnityEngine;

/// <summary>
/// 联机玩家头顶显示账号名与用户 ID（数据来自 <see cref="CoopNetworkPlayer"/> 的 SyncVar）。
/// </summary>
[RequireComponent(typeof(CoopNetworkPlayer))]
public class CoopPlayerNameplate : MonoBehaviour
{
    [SerializeField] float heightOffset = 2.15f;
    [SerializeField] Vector2 canvasWorldSize = new Vector2(2.4f, 0.5f);
    [SerializeField] float pixelsPerUnit = 100f;
    [SerializeField] Color textColor = new Color(1f, 1f, 1f, 0.92f);
    [SerializeField] int fontSize = 28;

    CoopNetworkPlayer _net;
    Transform _canvasRoot;
    TMP_Text _text;
    string _lastText;
    Camera _billboardCam;

    void Awake()
    {
        _net = GetComponent<CoopNetworkPlayer>();
    }

    void Start()
    {
        Build();
    }

    void LateUpdate()
    {
        if (_text == null || _canvasRoot == null)
            return;

        string label = FormatLabel();
        if (!string.Equals(label, _lastText, System.StringComparison.Ordinal))
        {
            _lastText = label;
            _text.text = label;
        }

        Color idColor = _net.GetRoomIdentityColor();
        idColor.a = textColor.a;
        _text.color = idColor;

        var cam = ResolveBillboardCamera();
        if (cam == null)
            return;
        try
        {
            _canvasRoot.position = transform.position + Vector3.up * heightOffset;
            _canvasRoot.rotation = Quaternion.LookRotation(_canvasRoot.position - cam.transform.position, Vector3.up);
        }
        catch (MissingReferenceException)
        {
            // 网络对象销毁竞态：本帧被销毁后仍触发一次 LateUpdate，直接忽略。
        }
    }

    Camera ResolveBillboardCamera()
    {
        if (_billboardCam != null && _billboardCam.isActiveAndEnabled)
            return _billboardCam;

        _billboardCam = Camera.main;
        if (_billboardCam != null && _billboardCam.isActiveAndEnabled)
            return _billboardCam;

        var cams = Camera.allCameras;
        for (int i = 0; i < cams.Length; i++)
        {
            var c = cams[i];
            if (c == null || !c.isActiveAndEnabled) continue;
            _billboardCam = c;
            return _billboardCam;
        }

        return null;
    }

    string FormatLabel()
    {
        int id = _net.userId;
        string nick = (_net.nickname ?? "").Trim();
        string name = (_net.username ?? "").Trim();

        if (_net.isLocalPlayer && id <= 0 && string.IsNullOrEmpty(name) && string.IsNullOrEmpty(nick))
        {
            var saved = GameServerSync.Instance != null ? GameServerSync.Instance.SavedUsername : "";
            if (!string.IsNullOrEmpty(saved))
                name = saved;
            var savedNick = GameServerSync.Instance != null ? GameServerSync.Instance.SavedNickname : "";
            if (!string.IsNullOrEmpty(savedNick))
                nick = savedNick;
        }

        string display = !string.IsNullOrEmpty(nick) ? nick : name;
        if (!string.IsNullOrEmpty(display) && id > 0)
            return $"昵称: {display}  ID: {id}";
        if (!string.IsNullOrEmpty(display))
            return $"昵称: {display}";
        if (id > 0)
            return $"ID: {id}";
        return "…";
    }

    void Build()
    {
        if (!this || gameObject == null)
            return;

        try
        {
            var go = new GameObject("CoopNameplateCanvas");
            _canvasRoot = go.transform;
            if (_canvasRoot == null || !_canvasRoot)
                return;
            _canvasRoot.SetParent(transform, false);
            _canvasRoot.localPosition = Vector3.zero;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 200;

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = canvasWorldSize * pixelsPerUnit;
            float s = 1f / pixelsPerUnit;
            _canvasRoot.localScale = new Vector3(s, s, s);

            var scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.dynamicPixelsPerUnit = pixelsPerUnit;

            go.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var tr = textGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;

            _text = textGo.AddComponent<TextMeshProUGUI>();
            _text.alignment = TextAlignmentOptions.Midline;
            _text.color = textColor;
            _text.fontSize = fontSize;
            _text.enableWordWrapping = false;
            _text.overflowMode = TextOverflowModes.Overflow;
            _text.raycastTarget = false;

            _lastText = FormatLabel();
            _text.text = _lastText;
        }
        catch (MissingReferenceException)
        {
            // 生成铭牌过程中宿主网络对象被销毁，忽略即可。
            _canvasRoot = null;
            _text = null;
        }
    }
}
