using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 按 <b>O</b> 打开：查看/参与旗帜竞拍；胜者在本周期「自定义」阶段可改字与颜色（最多 20 字）。
/// </summary>
[DisallowMultipleComponent]
public class FlagAuctionPanelController : MonoBehaviour
{
    public static FlagAuctionPanelController Instance { get; private set; }

    const string TmpFontResource = "Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF";

    [SerializeField] GameObject panelRoot;
    [SerializeField] bool ensureRuntimeCanvas = true;
    [SerializeField] bool lockCursorWhenClosed = true;

    TextMeshProUGUI _titleGold;
    Transform _rowsParent;
    readonly Dictionary<string, RowWidgets> _rows = new Dictionary<string, RowWidgets>();

    FlagAuctionStateResponse _last;
    int _myUserId;
    int _dormGold = -1;
    bool _loadedIdentity;

    public bool IsPanelOpen => panelRoot != null && panelRoot.activeSelf;

    public static bool BlocksGameplayInput => Instance != null && Instance.IsPanelOpen;

    class RowWidgets
    {
        public TextMeshProUGUI info;
        public TMP_InputField bidInput;
        public Button bidBtn;
        public GameObject customizeBlock;
        public TMP_InputField textInput;
        public TMP_InputField r, g, b, a;
        public Button applyBtn;
        public string slotId;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        if (ensureRuntimeCanvas && panelRoot == null)
            BuildRuntimeUi();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        if (FlagAuctionStatePoller.Instance != null)
            FlagAuctionStatePoller.Instance.OnStateUpdated -= OnGlobalState;
    }

    void OnEnable()
    {
        if (FlagAuctionStatePoller.Instance != null)
            FlagAuctionStatePoller.Instance.OnStateUpdated += OnGlobalState;
    }

    void OnDisable()
    {
        if (FlagAuctionStatePoller.Instance != null)
            FlagAuctionStatePoller.Instance.OnStateUpdated -= OnGlobalState;
    }

    void OnGlobalState(FlagAuctionStateResponse r)
    {
        _last = r;
        if (IsPanelOpen)
            RefreshPanel();
    }

    public void TogglePanel()
    {
        if (panelRoot == null && ensureRuntimeCanvas)
            BuildRuntimeUi();
        if (panelRoot == null) return;
        bool open = !panelRoot.activeSelf;
        panelRoot.SetActive(open);
        if (open)
        {
            if (_rowsParent != null)
                RebuildRowsFromScene();
            StartCoroutine(CoLoadIdentity());
            if (FlagAuctionStatePoller.Instance != null)
                FlagAuctionStatePoller.Instance.RequestImmediateRefresh();
            RefreshPanel();
        }
        else if (lockCursorWhenClosed)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (open)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    IEnumerator CoLoadIdentity()
    {
        _loadedIdentity = false;
        yield return FlagAuctionApi.CoFetchMyUserId(
            id =>
            {
                _myUserId = id;
                _loadedIdentity = true;
            },
            _ => { _loadedIdentity = true; });
        yield return FlagAuctionApi.CoFetchDormGold(
            g =>
            {
                _dormGold = g;
                if (_titleGold != null)
                    _titleGold.text = Loc.F("寝室金币：{0}", "Dorm gold: {0}", g);
            },
            _ =>
            {
                if (_titleGold != null)
                    _titleGold.text = Loc.Choose("未登录（仅可观看旗帜与竞拍信息）", "Not logged in (view only)");
            });
        RefreshPanel();
    }

    void RefreshPanel()
    {
        if (_titleGold != null && _dormGold >= 0 && GameServerSync.Instance != null &&
            GameServerSync.Instance.IsLoggedIn)
            _titleGold.text = Loc.F("寝室金币：{0}", "Dorm gold: {0}", _dormGold);

        if (_rowsParent == null || _last?.slots == null) return;
        foreach (var kv in _rows)
        {
            if (kv.Value?.info == null) continue;
            var w = FindWire(kv.Key);
            if (w == null)
            {
                kv.Value.info.text = Loc.F("{0}\n（暂无服务器数据，请稍后）", "{0}\n(No server data yet. Please wait.)", kv.Key);
                continue;
            }

            FillRow(kv.Value, w);
        }
    }

    FlagAuctionSlotWire FindWire(string slotId)
    {
        if (_last?.slots == null) return null;
        for (int i = 0; i < _last.slots.Length; i++)
        {
            var s = _last.slots[i];
            if (s != null && string.Equals(s.slot_id, slotId, StringComparison.Ordinal))
                return s;
        }

        return null;
    }

    void FillRow(RowWidgets row, FlagAuctionSlotWire w)
    {
        float st = 0f;
        if (FlagAuctionStatePoller.Instance != null && FlagAuctionStatePoller.Instance.LastState != null)
            st = FlagAuctionStatePoller.Instance.LastState.server_time;
        float left = Mathf.Max(0f, w.phase_ends_at - st);

        string phaseCn = (w.phase ?? "").ToLowerInvariant() == "customize"
            ? Loc.Choose("自定义", "Customize")
            : Loc.Choose("竞拍", "Auction");
        row.info.text = Loc.F(
            "{0}\n阶段：{1}  剩余约 {2:F0}s\n当前价：{3}  领先：{4}\n旗面字：{5}",
            "{0}\nPhase: {1}  ~{2:F0}s left\nCurrent bid: {3}  Leader: {4}\nFlag text: {5}",
            w.slot_id, phaseCn, left, w.bid_amount, w.high_bidder_nickname, w.display_text);

        bool auction = (w.phase ?? "").ToLowerInvariant() == "auction";
        row.bidInput.gameObject.SetActive(auction);
        row.bidBtn.gameObject.SetActive(auction);

        bool canCustomize = (w.phase ?? "").ToLowerInvariant() == "customize" &&
                            _loadedIdentity &&
                            w.customize_user_id > 0 &&
                            w.customize_user_id == _myUserId;
        row.customizeBlock.SetActive(canCustomize);
        if (canCustomize)
        {
            if (string.IsNullOrEmpty(row.textInput.text))
                row.textInput.text = w.display_text ?? "";
            row.r.text = w.color_r.ToString("0.###");
            row.g.text = w.color_g.ToString("0.###");
            row.b.text = w.color_b.ToString("0.###");
            row.a.text = w.color_a.ToString("0.###");
        }
    }

    public void BuildRuntimeUi()
    {
        var canvasGo = new GameObject("FlagAuctionCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 78;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        DontDestroyOnLoad(canvasGo);

        panelRoot = new GameObject("FlagAuctionPanel");
        panelRoot.transform.SetParent(canvasGo.transform, false);
        var bg = panelRoot.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.1f, 0.14f, 0.94f);
        var rt = panelRoot.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(720, 640);
        rt.anchoredPosition = Vector2.zero;

        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(panelRoot.transform, false);
        _titleGold = titleGo.AddComponent<TextMeshProUGUI>();
        _titleGold.fontSize = 22;
        _titleGold.alignment = TextAlignmentOptions.Center;
        _titleGold.text = Loc.Choose("旗帜竞拍（O 关闭）", "Flag auction (O to close)");
        TryBindFont(_titleGold);
        var trt = titleGo.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.sizeDelta = new Vector2(0, 44);
        trt.anchoredPosition = new Vector2(0, -8);

        var closeGo = new GameObject("Close");
        closeGo.transform.SetParent(panelRoot.transform, false);
        var closeBtn = closeGo.AddComponent<Button>();
        var closeImg = closeGo.AddComponent<Image>();
        closeImg.color = new Color(0.35f, 0.35f, 0.38f, 1f);
        var crt = closeGo.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(1f, 1f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(1f, 1f);
        crt.sizeDelta = new Vector2(40, 40);
        crt.anchoredPosition = new Vector2(-6, -6);
        closeBtn.onClick.AddListener(() =>
        {
            panelRoot.SetActive(false);
            if (lockCursorWhenClosed)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        });

        var scrollGo = new GameObject("Scroll");
        scrollGo.transform.SetParent(panelRoot.transform, false);
        var srt = scrollGo.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.02f, 0.06f);
        srt.anchorMax = new Vector2(0.98f, 0.92f);
        srt.offsetMin = Vector2.zero;
        srt.offsetMax = Vector2.zero;
        var scroll = scrollGo.AddComponent<ScrollRect>();
        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollGo.transform, false);
        var vimg = viewport.AddComponent<Image>();
        vimg.color = new Color(0.12f, 0.14f, 0.18f, 0.5f);
        var mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        var vrt = viewport.GetComponent<RectTransform>();
        vrt.anchorMin = Vector2.zero;
        vrt.anchorMax = Vector2.one;
        vrt.offsetMin = Vector2.zero;
        vrt.offsetMax = Vector2.zero;
        _rowsParent = new GameObject("Content").transform;
        _rowsParent.SetParent(viewport.transform, false);
        var contentRt = _rowsParent.gameObject.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        var vlg = _rowsParent.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 10f;
        vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.childControlHeight = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        var fit = _rowsParent.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = contentRt;
        scroll.viewport = vrt;
        scroll.vertical = true;
        scroll.horizontal = false;

        RebuildRowsFromScene();
        panelRoot.SetActive(false);
    }

    void TryBindFont(TextMeshProUGUI t)
    {
        var f = Resources.Load<TMP_FontAsset>(TmpFontResource);
        if (f != null)
            t.font = f;
    }

    void RebuildRowsFromScene()
    {
        foreach (Transform c in _rowsParent)
            Destroy(c.gameObject);
        _rows.Clear();
        var views = FindObjectsOfType<FlagAuctionSlotView>(true);
        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] == null) continue;
            string sid = views[i].SlotId;
            if (string.IsNullOrEmpty(sid)) continue;
            if (_rows.ContainsKey(sid)) continue;
            _rows[sid] = CreateRow(sid);
        }

        if (_rows.Count == 0)
        {
            var hint = new GameObject("Hint");
            hint.transform.SetParent(_rowsParent, false);
            var te = hint.AddComponent<TextMeshProUGUI>();
            te.fontSize = 18;
            te.text = Loc.Choose(
                "场景中暂无带 FlagAuctionSlotView 的旗帜，或 slotId 为空。",
                "No flags with FlagAuctionSlotView found in scene, or slotId is empty.");
            TryBindFont(te);
            var le = hint.AddComponent<LayoutElement>();
            le.minHeight = 40f;
        }
    }

    RowWidgets CreateRow(string slotId)
    {
        var rowGo = new GameObject("Row_" + slotId);
        rowGo.transform.SetParent(_rowsParent, false);
        var rowLe = rowGo.AddComponent<LayoutElement>();
        rowLe.minHeight = 180f;

        var v = rowGo.AddComponent<VerticalLayoutGroup>();
        v.spacing = 6f;
        v.childControlWidth = true;

        var rw = new RowWidgets { slotId = slotId };

        var infoGo = new GameObject("Info");
        infoGo.transform.SetParent(rowGo.transform, false);
        rw.info = infoGo.AddComponent<TextMeshProUGUI>();
        rw.info.fontSize = 16;
        rw.info.text = slotId;
        TryBindFont(rw.info);
        var infLe = infoGo.AddComponent<LayoutElement>();
        infLe.minHeight = 72f;

        var bidRow = new GameObject("BidRow");
        bidRow.transform.SetParent(rowGo.transform, false);
        var hl = bidRow.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 8f;
        hl.childForceExpandWidth = false;
        hl.childControlWidth = true;

        var bidIfGo = new GameObject("BidInput");
        bidIfGo.transform.SetParent(bidRow.transform, false);
        rw.bidInput = bidIfGo.AddComponent<TMP_InputField>();
        var bidBg = bidIfGo.AddComponent<Image>();
        bidBg.color = new Color(0.2f, 0.22f, 0.26f, 1f);
        var bidRt = bidIfGo.GetComponent<RectTransform>();
        bidRt.sizeDelta = new Vector2(200, 36);
        var bidText = new GameObject("Text");
        bidText.transform.SetParent(bidIfGo.transform, false);
        var bidTmp = bidText.AddComponent<TextMeshProUGUI>();
        bidTmp.fontSize = 16;
        TryBindFont(bidTmp);
        var bidPlaceholder = new GameObject("Placeholder");
        bidPlaceholder.transform.SetParent(bidIfGo.transform, false);
        var ph = bidPlaceholder.AddComponent<TextMeshProUGUI>();
        ph.fontSize = 15;
        ph.color = new Color(1f, 1f, 1f, 0.35f);
        ph.text = Loc.Choose("出价金额", "Bid amount");
        TryBindFont(ph);
        rw.bidInput.textComponent = bidTmp;
        rw.bidInput.placeholder = ph;
        rw.bidInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        var bidLe = bidIfGo.AddComponent<LayoutElement>();
        bidLe.minWidth = 200f;
        bidLe.preferredHeight = 36f;

        var bidBtnGo = new GameObject("BidBtn");
        bidBtnGo.transform.SetParent(bidRow.transform, false);
        rw.bidBtn = bidBtnGo.AddComponent<Button>();
        bidBtnGo.AddComponent<Image>().color = new Color(0.2f, 0.45f, 0.55f, 1f);
        var btxt = new GameObject("Txt");
        btxt.transform.SetParent(bidBtnGo.transform, false);
        var btmp = btxt.AddComponent<TextMeshProUGUI>();
        btmp.fontSize = 16;
        btmp.alignment = TextAlignmentOptions.Center;
        btmp.text = Loc.Choose("出价", "Place bid");
        TryBindFont(btmp);
        var btnRt = bidBtnGo.GetComponent<RectTransform>();
        btnRt.sizeDelta = new Vector2(100, 36);
        var btnLe = bidBtnGo.AddComponent<LayoutElement>();
        btnLe.minWidth = 100f;
        btnLe.preferredHeight = 36f;
        string sidCopy = slotId;
        rw.bidBtn.onClick.AddListener(() => OnClickBid(sidCopy, rw.bidInput));

        rw.customizeBlock = new GameObject("Customize");
        rw.customizeBlock.transform.SetParent(rowGo.transform, false);
        var cv = rw.customizeBlock.AddComponent<VerticalLayoutGroup>();
        cv.spacing = 4f;
        var cLe = rw.customizeBlock.AddComponent<LayoutElement>();
        cLe.minHeight = 100f;

        var tGo = new GameObject("TextField");
        tGo.transform.SetParent(rw.customizeBlock.transform, false);
        rw.textInput = tGo.AddComponent<TMP_InputField>();
        tGo.AddComponent<Image>().color = new Color(0.18f, 0.2f, 0.24f, 1f);
        var tArea = new GameObject("Text");
        tArea.transform.SetParent(tGo.transform, false);
        var tTmp = tArea.AddComponent<TextMeshProUGUI>();
        tTmp.fontSize = 16;
        TryBindFont(tTmp);
        rw.textInput.textComponent = tTmp;
        rw.textInput.characterLimit = 20;
        var tRt = tGo.GetComponent<RectTransform>();
        tRt.sizeDelta = new Vector2(0, 36);
        var tLe = tGo.AddComponent<LayoutElement>();
        tLe.minHeight = 36f;

        var rgbaRow = new GameObject("RgbaRow");
        rgbaRow.transform.SetParent(rw.customizeBlock.transform, false);
        var rgbaHl = rgbaRow.AddComponent<HorizontalLayoutGroup>();
        rgbaHl.spacing = 6f;
        rgbaHl.childForceExpandWidth = true;
        rgbaHl.childControlWidth = true;
        rw.r = CreateFloatField(rgbaRow.transform, "R");
        rw.g = CreateFloatField(rgbaRow.transform, "G");
        rw.b = CreateFloatField(rgbaRow.transform, "B");
        rw.a = CreateFloatField(rgbaRow.transform, "A");

        var applyGo = new GameObject("Apply");
        applyGo.transform.SetParent(rw.customizeBlock.transform, false);
        rw.applyBtn = applyGo.AddComponent<Button>();
        applyGo.AddComponent<Image>().color = new Color(0.25f, 0.5f, 0.35f, 1f);
        var apt = new GameObject("Txt");
        apt.transform.SetParent(applyGo.transform, false);
        var apTmp = apt.AddComponent<TextMeshProUGUI>();
        apTmp.fontSize = 15;
        apTmp.alignment = TextAlignmentOptions.Center;
        apTmp.text = Loc.Choose("应用旗帜", "Apply flag");
        TryBindFont(apTmp);
        var apRt = applyGo.GetComponent<RectTransform>();
        apRt.sizeDelta = new Vector2(160, 32);
        var apLe = applyGo.AddComponent<LayoutElement>();
        apLe.minHeight = 32f;
        rw.applyBtn.onClick.AddListener(() => OnClickApply(sidCopy, rw));

        rw.customizeBlock.SetActive(false);
        return rw;
    }

    TMP_InputField CreateFloatField(Transform parent, string label)
    {
        var col = new GameObject("Col_" + label);
        col.transform.SetParent(parent, false);
        var v = col.AddComponent<VerticalLayoutGroup>();
        v.childControlWidth = true;
        col.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var lab = new GameObject("Lab");
        lab.transform.SetParent(col.transform, false);
        var lt = lab.AddComponent<TextMeshProUGUI>();
        lt.fontSize = 13;
        lt.text = label + " 0~1";
        TryBindFont(lt);
        var ifGo = new GameObject("Input");
        ifGo.transform.SetParent(col.transform, false);
        var field = ifGo.AddComponent<TMP_InputField>();
        ifGo.AddComponent<Image>().color = new Color(0.18f, 0.2f, 0.24f, 1f);
        var txt = new GameObject("Text");
        txt.transform.SetParent(ifGo.transform, false);
        var tmp = txt.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 15;
        TryBindFont(tmp);
        field.textComponent = tmp;
        field.contentType = TMP_InputField.ContentType.DecimalNumber;
        ifGo.AddComponent<LayoutElement>().minHeight = 32f;
        return field;
    }

    void OnClickBid(string slotId, TMP_InputField input)
    {
        if (GameServerSync.Instance == null || !GameServerSync.Instance.IsLoggedIn)
        {
            Debug.LogWarning("[旗帜竞拍] 请先登录后再出价。");
            return;
        }

        if (!int.TryParse(input.text, out int amt) || amt < 1)
        {
            Debug.LogWarning("[旗帜竞拍] 请输入有效整数金额。");
            return;
        }

        StartCoroutine(FlagAuctionApi.CoBid(
            GameServerSync.Instance.AuthTokenForExtensions,
            slotId,
            amt,
            () =>
            {
                StartCoroutine(CoLoadIdentity());
                if (FlagAuctionStatePoller.Instance != null)
                    FlagAuctionStatePoller.Instance.RequestImmediateRefresh();
            },
            err => Debug.LogWarning("[旗帜竞拍] " + err)));
    }

    void OnClickApply(string slotId, RowWidgets rw)
    {
        if (GameServerSync.Instance == null || !GameServerSync.Instance.IsLoggedIn) return;
        string t = (rw.textInput.text ?? "").Trim();
        if (t.Length > 20)
            t = t.Substring(0, 20);
        float cr = Parse01(rw.r?.text, 1f);
        float cg = Parse01(rw.g?.text, 1f);
        float cb = Parse01(rw.b?.text, 1f);
        float ca = Parse01(rw.a?.text, 1f);
        var col = new Color(cr, cg, cb, ca);
        StartCoroutine(FlagAuctionApi.CoSetDisplay(
            GameServerSync.Instance.AuthTokenForExtensions,
            slotId,
            t,
            col,
            () =>
            {
                if (FlagAuctionStatePoller.Instance != null)
                    FlagAuctionStatePoller.Instance.RequestImmediateRefresh();
            },
            err => Debug.LogWarning("[旗帜竞拍] " + err)));
    }

    static float Parse01(string s, float def)
    {
        if (string.IsNullOrWhiteSpace(s)) return Mathf.Clamp01(def);
        if (!float.TryParse(s.Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float v))
            return Mathf.Clamp01(def);
        return Mathf.Clamp01(v);
    }
}
