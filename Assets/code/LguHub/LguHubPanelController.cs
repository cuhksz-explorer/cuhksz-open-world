using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 图书馆场景按 P 打开 LGUHUB 原生面板；自动复用 GameServer 登录并换取 LGUHUB token。
/// </summary>
public class LguHubPanelController : MonoBehaviour
{
    public static LguHubPanelController Instance { get; private set; }
    public static bool BlocksGameplayInput => Instance != null && Instance._isOpen;

    static readonly string[] AllowedScenes = { "libraryfloor1", "libraryfloor2", "libraryfloor4" };

    [SerializeField] KeyCode toggleKey = KeyCode.P;
    [SerializeField] int canvasSortOrder = 3500;

    bool _isOpen;
    GameObject _root;
    RectTransform _contentArea;
    TMP_Text _statusText;
    TMP_Text _pointsText;
    LguHubApiClient _api;
    string _currentPage = "community";
    string _communityQuery = "";
    int _selectedQuestionId;

    public static bool IsAllowedScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;
        for (int i = 0; i < AllowedScenes.Length; i++)
        {
            if (string.Equals(AllowedScenes[i], sceneName, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void BootstrapInLibraryScene()
    {
        TryBootstrapInActiveScene();
    }

    static void TryBootstrapInActiveScene()
    {
        if (Instance != null)
            return;
        var scene = SceneManager.GetActiveScene();
        if (!IsAllowedScene(scene.name))
            return;

        var host = new GameObject("LguHub_Runtime");
        AttachToPersistentHost(host.transform);
        host.AddComponent<LguHubApiClient>();
        host.AddComponent<LguHubPanelController>();
    }

    static void AttachToPersistentHost(Transform host)
    {
        var sync = GameServerSync.Instance;
        if (sync != null)
            host.SetParent(sync.transform, false);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        AttachToPersistentHost(transform);
        _api = GetComponent<LguHubApiClient>();
        if (_api == null)
            _api = gameObject.AddComponent<LguHubApiClient>();
        _api.OnError += msg => SetStatus(msg);
        BuildUiIfNeeded();
        _root.SetActive(false);
        DisableConflictingLoginToggle();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        Loc.OnLanguageChanged += OnLanguageChanged;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Loc.OnLanguageChanged -= OnLanguageChanged;
    }

    void OnLanguageChanged()
    {
        if (_isOpen)
            ShowPage(_currentPage);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        DisableConflictingLoginToggle();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this)
            Instance = null;
        RestoreCursorIfNeeded();
    }

    void DisableConflictingLoginToggle()
    {
        if (!IsAllowedScene(SceneManager.GetActiveScene().name))
            return;
        var toggles = FindObjectsOfType<GameServerLoginPanelToggle>(true);
        for (int i = 0; i < toggles.Length; i++)
        {
            if (toggles[i] != null)
                toggles[i].enabled = false;
        }
    }

    void Update()
    {
        if (!IsAllowedScene(SceneManager.GetActiveScene().name))
            return;
        if (!Input.GetKeyDown(toggleKey))
            return;
        if (GameplayUiHotkeyGuard.ShouldBlockGameplayHotkeys() && !_isOpen)
            return;
        Toggle();
    }

    void LateUpdate()
    {
        if (_isOpen)
            EnsureCursor();
    }

    public void Toggle()
    {
        if (_isOpen)
            Close();
        else
            Open();
    }

    public void Open()
    {
        if (_isOpen) return;
        if (!IsAllowedScene(SceneManager.GetActiveScene().name))
            return;

        _root.SetActive(true);
        _isOpen = true;
        EnsureCursor();
        ClearContent();

        if (GameServerSync.Instance == null || !GameServerSync.Instance.IsLoggedIn)
        {
            LguHubUiBuilder.AddText(_contentArea, Loc.Choose("请先登录游戏账号", "Please log in first"), 24, TextAlignmentOptions.Center, LguHubUiBuilder.TextMain);
            SetStatus(Loc.Choose("需要登录后才能使用 LGUHUB", "Login required to use LGUHUB"));
            return;
        }

        SetStatus(Loc.Choose("正在连接 LGUHUB…", "Connecting to LGUHUB…"));
        StartCoroutine(CoOpenFlow());
    }

    public void Close()
    {
        _isOpen = false;
        if (_root != null)
            _root.SetActive(false);
        _api?.ClearSession();
        RestoreCursorIfNeeded();
    }

    IEnumerator CoOpenFlow()
    {
        yield return _api.CoEnsureSession(ok =>
        {
            if (!ok)
                return;
            SetStatus("");
            RefreshPoints();
            ShowPage(_currentPage);
        });
    }

    void BuildUiIfNeeded()
    {
        if (_root != null) return;
        _root = LguHubUiBuilder.CreateCanvasRoot("LguHubPanel", canvasSortOrder);
        _root.transform.SetParent(transform, false);

        LguHubUiBuilder.AddPanel(_root.transform, new Color(0, 0, 0, 0.55f), Vector2.zero, Vector2.one);

        var shell = new GameObject("Shell", typeof(RectTransform), typeof(Image));
        shell.transform.SetParent(_root.transform, false);
        var shellRt = shell.GetComponent<RectTransform>();
        shellRt.anchorMin = new Vector2(0.05f, 0.05f);
        shellRt.anchorMax = new Vector2(0.95f, 0.95f);
        shellRt.offsetMin = Vector2.zero;
        shellRt.offsetMax = Vector2.zero;
        shell.GetComponent<Image>().color = LguHubUiBuilder.Bg;

        var top = new GameObject("TopBar", typeof(RectTransform), typeof(Image));
        top.transform.SetParent(shell.transform, false);
        var topRt = top.GetComponent<RectTransform>();
        topRt.anchorMin = new Vector2(0, 1);
        topRt.anchorMax = new Vector2(1, 1);
        topRt.pivot = new Vector2(0.5f, 1);
        topRt.sizeDelta = new Vector2(0, 56);
        top.GetComponent<Image>().color = LguHubUiBuilder.Primary;
        var brand = LguHubUiBuilder.AddText(top.transform, "LGU HUB", 28, TextAlignmentOptions.MidlineLeft, Color.white);
        brand.rectTransform.anchorMin = new Vector2(0, 0);
        brand.rectTransform.anchorMax = new Vector2(0, 1);
        brand.rectTransform.pivot = new Vector2(0, 0.5f);
        brand.rectTransform.offsetMin = new Vector2(20, 0);
        _pointsText = LguHubUiBuilder.AddText(top.transform, "🪙 --", 22, TextAlignmentOptions.MidlineRight, Color.white);
        _pointsText.rectTransform.anchorMin = new Vector2(1, 0);
        _pointsText.rectTransform.anchorMax = new Vector2(1, 1);
        _pointsText.rectTransform.offsetMax = new Vector2(-120, 0);
        var closeBtn = LguHubUiBuilder.AddButton(top.transform, Loc.Choose("关闭", "Close"), new Color(0.2f, 0.2f, 0.2f, 0.35f), Close);
        closeBtn.GetComponent<RectTransform>().anchorMin = new Vector2(1, 0.5f);
        closeBtn.GetComponent<RectTransform>().anchorMax = new Vector2(1, 0.5f);
        closeBtn.GetComponent<RectTransform>().pivot = new Vector2(1, 0.5f);
        closeBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-16, 0);
        closeBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(90, 36);

        var body = new GameObject("Body", typeof(RectTransform));
        body.transform.SetParent(shell.transform, false);
        var bodyRt = body.GetComponent<RectTransform>();
        LguHubUiBuilder.StretchFull(bodyRt, new Vector2(0, 28), new Vector2(0, -56));

        var sidebar = new GameObject("Sidebar", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        sidebar.transform.SetParent(body.transform, false);
        var sideRt = sidebar.GetComponent<RectTransform>();
        sideRt.anchorMin = new Vector2(0, 0);
        sideRt.anchorMax = new Vector2(0, 1);
        sideRt.pivot = new Vector2(0, 0.5f);
        sideRt.sizeDelta = new Vector2(220, 0);
        sideRt.anchoredPosition = Vector2.zero;
        sidebar.GetComponent<Image>().color = Color.white;
        var sideVlg = sidebar.GetComponent<VerticalLayoutGroup>();
        sideVlg.padding = new RectOffset(10, 10, 14, 14);
        sideVlg.spacing = 8;
        sideVlg.childAlignment = TextAnchor.UpperCenter;
        sideVlg.childControlWidth = true;
        sideVlg.childControlHeight = true;
        sideVlg.childForceExpandWidth = true;
        sideVlg.childForceExpandHeight = false;
        AddNav(sidebar.transform, Loc.Choose("🌐 社区", "🌐 Community"), () => ShowPage("community"));
        AddNav(sidebar.transform, Loc.Choose("📚 课程", "📚 Courses"), () => ShowPage("courses"));
        AddNav(sidebar.transform, Loc.Choose("❓ 问答", "❓ Q&A"), () => ShowPage("questions"));
        AddNav(sidebar.transform, Loc.Choose("📤 上传", "📤 Upload"), () => ShowPage("upload"));
        AddNav(sidebar.transform, Loc.Choose("👤 我的", "👤 Profile"), () => ShowPage("profile"));

        var main = new GameObject("Main", typeof(RectTransform), typeof(Image));
        main.transform.SetParent(body.transform, false);
        var mainRt = main.GetComponent<RectTransform>();
        LguHubUiBuilder.StretchFull(mainRt, new Vector2(228, 0), new Vector2(-12, 0));
        main.GetComponent<Image>().color = LguHubUiBuilder.Card;
        _contentArea = mainRt;

        var statusGo = new GameObject("Status", typeof(RectTransform));
        statusGo.transform.SetParent(shell.transform, false);
        var stRt = statusGo.GetComponent<RectTransform>();
        stRt.anchorMin = new Vector2(0, 0);
        stRt.anchorMax = new Vector2(1, 0);
        stRt.pivot = new Vector2(0.5f, 0);
        stRt.sizeDelta = new Vector2(0, 28);
        _statusText = statusGo.AddComponent<TextMeshProUGUI>();
        _statusText.fontSize = 16;
        _statusText.color = LguHubUiBuilder.TextMuted;
        _statusText.alignment = TextAlignmentOptions.Center;
        LguHubUiBuilder.ApplyFont(_statusText);
        LguHubUiBuilder.ApplyFont(brand);
        LguHubUiBuilder.ApplyFont(_pointsText);
    }

    void AddNav(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        var btn = LguHubUiBuilder.AddButton(parent, label, new Color(0.95f, 0.95f, 0.98f, 1f), onClick, LguHubUiBuilder.TextMain);
        var rt = btn.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 44);
        var le = btn.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 44;
        le.preferredHeight = 44;
        le.flexibleWidth = 1f;
        var colors = btn.colors;
        colors.normalColor = new Color(0.95f, 0.95f, 0.98f, 1f);
        colors.highlightedColor = LguHubUiBuilder.Primary * 0.85f;
        colors.pressedColor = LguHubUiBuilder.Primary;
        colors.selectedColor = LguHubUiBuilder.Primary * 0.9f;
        btn.colors = colors;
    }

    void ShowPage(string page)
    {
        _currentPage = page;
        ClearContent();
        switch (page)
        {
            case "community": BuildCommunityPage(); break;
            case "courses": BuildCoursesPage(); break;
            case "questions": BuildQuestionsPage(); break;
            case "profile": BuildProfilePage(); break;
            case "upload": BuildUploadPage(); break;
            case "question_detail": BuildQuestionDetailPage(); break;
        }
    }

    void ClearContent()
    {
        for (int i = _contentArea.childCount - 1; i >= 0; i--)
            Destroy(_contentArea.GetChild(i).gameObject);
    }

    void SetStatus(string msg)
    {
        if (_statusText != null)
            _statusText.text = msg ?? "";
    }

    void RefreshPoints()
    {
        StartCoroutine(_api.CoFetchPoints(dto =>
        {
            if (dto != null && dto.success && _pointsText != null)
                _pointsText.text = "🪙 " + dto.points;
        }));
    }

    #region Community

    TMP_InputField _communitySearchInput;

    void BuildCommunityPage()
    {
        var toolbar = LguHubUiBuilder.CreateToolbar(_contentArea);
        _communitySearchInput = LguHubUiBuilder.AddInput(toolbar, Loc.Choose("搜索资料…", "Search materials…"));
        var searchBtn = LguHubUiBuilder.AddButton(toolbar, Loc.Choose("搜索", "Search"), LguHubUiBuilder.Primary, () =>
        {
            _communityQuery = "q=" + UnityEngine.Networking.UnityWebRequest.EscapeURL(_communitySearchInput.text ?? "");
            StartCoroutine(LoadCommunity());
        });
        LguHubUiBuilder.AddLayoutHeight(searchBtn.gameObject, 44);

        var listHost = LguHubUiBuilder.CreateBodyHost(_contentArea);
        listHost.name = "CommunityListHost";
        StartCoroutine(LoadCommunity());
    }

    IEnumerator LoadCommunity()
    {
        var host = _contentArea.Find("CommunityListHost");
        if (host == null) yield break;
        for (int i = host.childCount - 1; i >= 0; i--)
            Destroy(host.GetChild(i).gameObject);
        SetStatus(Loc.Choose("加载社区…", "Loading community…"));
        yield return _api.CoFetchCommunity(_communityQuery, dto =>
        {
            SetStatus("");
            if (dto == null || !dto.success)
            {
                SetStatus(dto?.error ?? Loc.Choose("加载失败", "Load failed"));
                return;
            }
            var scroll = LguHubUiBuilder.AddScroll(host, out var content);
            if (dto.items == null) return;
            foreach (var b in dto.items)
                AddBatchCard(content, b);
        });
    }

    void AddBatchCard(Transform parent, LguHubCommunityBatchDto b)
    {
        var card = new GameObject("Batch", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        card.transform.SetParent(parent, false);
        card.GetComponent<Image>().color = new Color(0.98f, 0.98f, 1f, 1f);
        var vlg = card.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(12, 12, 12, 12);
        vlg.spacing = 6;
        var title = string.IsNullOrEmpty(b.description) ? $"批次 #{b.id}" : b.description;
        if (title.Length > 80) title = title.Substring(0, 80) + "…";
        LguHubUiBuilder.AddText(card.transform, title, 20, TextAlignmentOptions.TopLeft, LguHubUiBuilder.TextMain);
        LguHubUiBuilder.AddText(card.transform,
            $"{b.username} · {b.course_code} · ⭐{b.avg_rating:F1} · ⬇{b.total_downloads}",
            16, TextAlignmentOptions.TopLeft, LguHubUiBuilder.TextMuted);
        var row = new GameObject("Actions", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(card.transform, false);
        var rowH = row.GetComponent<HorizontalLayoutGroup>();
        rowH.spacing = 8;
        rowH.childControlWidth = true;
        rowH.childForceExpandWidth = false;
        rowH.childControlHeight = true;
        int bid = b.id;
        var favBtn = LguHubUiBuilder.AddButton(row.transform, b.is_favorited ? Loc.Choose("已收藏", "Favorited") : Loc.Choose("收藏", "Favorite"), LguHubUiBuilder.Primary,
            () => StartCoroutine(_api.CoFavorite(bid, _ => LoadCommunity())));
        LguHubUiBuilder.AddLayoutHeight(favBtn.gameObject, 40, 0f);
        var dlBtn = LguHubUiBuilder.AddButton(row.transform, Loc.Choose("下载", "Download"), new Color(0.2f, 0.6f, 0.4f, 1f), () =>
        {
            if (b.download_urls != null && !string.IsNullOrEmpty(b.download_urls.batch_one_click))
                Application.OpenURL(b.download_urls.batch_one_click);
        });
        LguHubUiBuilder.AddLayoutHeight(dlBtn.gameObject, 40, 0f);
        var commentInput = LguHubUiBuilder.AddInput(row.transform, Loc.Choose("评论…", "Comment…"));
        var sendBtn = LguHubUiBuilder.AddButton(row.transform, Loc.Choose("发送", "Send"), LguHubUiBuilder.Primary, () =>
        {
            var txt = commentInput.text;
            if (string.IsNullOrWhiteSpace(txt)) return;
            StartCoroutine(_api.CoComment(bid, txt, _ => LoadCommunity()));
        });
        LguHubUiBuilder.AddLayoutHeight(sendBtn.gameObject, 40, 0f);
    }

    #endregion

    #region Courses

    TMP_InputField _courseSearchInput;

    void BuildCoursesPage()
    {
        var toolbar = LguHubUiBuilder.CreateToolbar(_contentArea);
        _courseSearchInput = LguHubUiBuilder.AddInput(toolbar, Loc.Choose("搜索课程代码…", "Search course code…"));
        var searchBtn = LguHubUiBuilder.AddButton(toolbar, Loc.Choose("搜索", "Search"), LguHubUiBuilder.Primary,
            () => StartCoroutine(LoadCourses(_courseSearchInput.text)));
        LguHubUiBuilder.AddLayoutHeight(searchBtn.gameObject, 44);

        var host = LguHubUiBuilder.CreateBodyHost(_contentArea);
        host.name = "CoursesHost";
        StartCoroutine(LoadCourses(""));
    }

    IEnumerator LoadCourses(string q)
    {
        var host = _contentArea.Find("CoursesHost");
        if (host == null) yield break;
        for (int i = host.childCount - 1; i >= 0; i--)
            Destroy(host.GetChild(i).gameObject);
        yield return _api.CoFetchCourses(q, dto =>
        {
            if (dto == null || !dto.success) { SetStatus(dto?.error ?? Loc.Choose("加载失败", "Load failed")); return; }
            var scroll = LguHubUiBuilder.AddScroll(host, out var content);
            if (dto.courses == null) return;
            foreach (var c in dto.courses)
            {
                var line = $"{c.course_code}  📁{c.batch_count}  💬{c.question_count}";
                if (!string.IsNullOrEmpty(c.school)) line += "  " + c.school;
                LguHubUiBuilder.AddText(content, line, 22, TextAlignmentOptions.TopLeft, LguHubUiBuilder.TextMain);
            }
        });
    }

    #endregion

    #region Questions

    void BuildQuestionsPage()
    {
        var toolbar = LguHubUiBuilder.CreateToolbar(_contentArea, 52f);
        var allBtn = LguHubUiBuilder.AddButton(toolbar, Loc.Choose("全部", "All"), LguHubUiBuilder.Primary, () => StartCoroutine(LoadQuestions("all")));
        var bountyBtn = LguHubUiBuilder.AddButton(toolbar, Loc.Choose("悬赏", "Bounty"), LguHubUiBuilder.Primary, () => StartCoroutine(LoadQuestions("bounty")));
        var newBtn = LguHubUiBuilder.AddButton(toolbar, Loc.Choose("发帖", "New post"), new Color(0.15f, 0.55f, 0.35f, 1f), ShowNewQuestionForm);
        LguHubUiBuilder.AddLayoutHeight(allBtn.gameObject, 40);
        LguHubUiBuilder.AddLayoutHeight(bountyBtn.gameObject, 40);
        LguHubUiBuilder.AddLayoutHeight(newBtn.gameObject, 40);

        var host = LguHubUiBuilder.CreateBodyHost(_contentArea, 52f);
        host.name = "QuestionsHost";
        StartCoroutine(LoadQuestions("all"));
    }

    void ShowNewQuestionForm()
    {
        ClearContent();
        var scroll = LguHubUiBuilder.AddScroll(_contentArea, out var content);
        LguHubUiBuilder.AddText(content, Loc.Choose("发布问题", "New Question"), 24, TextAlignmentOptions.TopLeft, LguHubUiBuilder.TextMain);
        var title = LguHubUiBuilder.AddInput(content, Loc.Choose("标题", "Title"));
        var body = LguHubUiBuilder.AddInput(content, Loc.Choose("内容", "Content"), true);
        var course = LguHubUiBuilder.AddInput(content, Loc.Choose("课程代码（可选）", "Course code (optional)"));
        var row = new GameObject("Actions", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(content, false);
        var rowH = row.GetComponent<HorizontalLayoutGroup>();
        rowH.spacing = 12;
        rowH.childControlWidth = true;
        rowH.childForceExpandWidth = true;
        LguHubUiBuilder.AddButton(row.transform, Loc.Choose("提交", "Submit"), LguHubUiBuilder.Primary, () =>
        {
            StartCoroutine(_api.CoCreateQuestion(title.text, body.text, course.text, false, "",
                dto =>
                {
                    if (dto != null && dto.success)
                        ShowPage("questions");
                    else
                        SetStatus(dto?.error ?? Loc.Choose("发布失败", "Post failed"));
                }));
        });
        LguHubUiBuilder.AddButton(row.transform, Loc.Choose("返回", "Back"), new Color(0.5f, 0.5f, 0.5f, 1f), () => ShowPage("questions"));
    }

    IEnumerator LoadQuestions(string filter)
    {
        var host = _contentArea.Find("QuestionsHost");
        if (host == null) yield break;
        for (int i = host.childCount - 1; i >= 0; i--)
            Destroy(host.GetChild(i).gameObject);
        yield return _api.CoFetchQuestions(filter, "", dto =>
        {
            if (dto == null || !dto.success) { SetStatus(dto?.error ?? Loc.Choose("加载失败", "Load failed")); return; }
            var scroll = LguHubUiBuilder.AddScroll(host, out var content);
            if (dto.questions == null) return;
            foreach (var q in dto.questions)
            {
                int qid = q.id;
                var btn = LguHubUiBuilder.AddButton(content, $"[{q.answer_count}] {q.title}", new Color(0.96f, 0.96f, 0.99f, 1f),
                    () => { _selectedQuestionId = qid; ShowPage("question_detail"); });
                btn.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 48);
            }
        });
    }

    void BuildQuestionDetailPage()
    {
        var toolbar = LguHubUiBuilder.CreateToolbar(_contentArea, 48f);
        var backBtn = LguHubUiBuilder.AddButton(toolbar, Loc.Choose("← 返回列表", "← Back to list"), new Color(0.5f, 0.5f, 0.5f, 1f), () => ShowPage("questions"));
        LguHubUiBuilder.AddLayoutHeight(backBtn.gameObject, 40);

        var host = LguHubUiBuilder.CreateBodyHost(_contentArea, 48f);
        host.name = "DetailHost";
        StartCoroutine(LoadQuestionDetail());
    }

    IEnumerator LoadQuestionDetail()
    {
        var host = _contentArea.Find("DetailHost");
        if (host == null) yield break;
        yield return _api.CoFetchQuestionDetail(_selectedQuestionId, dto =>
        {
            if (dto == null || !dto.success) { SetStatus(dto?.error ?? Loc.Choose("加载失败", "Load failed")); return; }
            var scroll = LguHubUiBuilder.AddScroll(host, out var content);
            if (dto.question != null)
            {
                LguHubUiBuilder.AddText(content, dto.question.title, 24, TextAlignmentOptions.TopLeft, LguHubUiBuilder.TextMain);
                LguHubUiBuilder.AddText(content, dto.question.content, 18, TextAlignmentOptions.TopLeft, LguHubUiBuilder.TextMuted);
            }
            if (dto.answers != null)
            {
                foreach (var a in dto.answers)
                {
                    var prefix = a.is_accepted ? "✅ " : "";
                    LguHubUiBuilder.AddText(content, $"{prefix}{a.username}: {a.content}", 18,
                        TextAlignmentOptions.TopLeft, LguHubUiBuilder.TextMain);
                }
            }
            var answerInput = LguHubUiBuilder.AddInput(content, Loc.Choose("写下你的回答…", "Write your answer…"), true);
            LguHubUiBuilder.AddButton(content, Loc.Choose("提交回答", "Submit answer"), LguHubUiBuilder.Primary, () =>
            {
                StartCoroutine(_api.CoCreateAnswer(_selectedQuestionId, answerInput.text,
                    r => { if (r != null && r.success) StartCoroutine(LoadQuestionDetail()); }));
            });
        });
    }

    #endregion

    #region Profile

    void BuildProfilePage()
    {
        StartCoroutine(_api.CoFetchMe(dto =>
        {
            if (dto == null || !dto.success) { SetStatus(dto?.error ?? Loc.Choose("加载失败", "Load failed")); return; }
            var scroll = LguHubUiBuilder.AddScroll(_contentArea, out var content);
            LguHubUiBuilder.AddText(content, Loc.Choose("个人中心", "Profile"), 26, TextAlignmentOptions.TopLeft, LguHubUiBuilder.TextMain);
            LguHubUiBuilder.AddText(content, Loc.F("账号：{0}", "Account: {0}", dto.username), 20, TextAlignmentOptions.TopLeft, LguHubUiBuilder.TextMuted);
            LguHubUiBuilder.AddText(content, Loc.F("昵称：{0}", "Nickname: {0}", dto.nickname), 20, TextAlignmentOptions.TopLeft, LguHubUiBuilder.TextMuted);
            var bioInput = LguHubUiBuilder.AddInput(content, Loc.Choose("简介", "Bio"), true);
            bioInput.text = dto.bio ?? "";
            var nickInput = LguHubUiBuilder.AddInput(content, Loc.Choose("昵称", "Nickname"));
            nickInput.text = dto.nickname ?? "";
            LguHubUiBuilder.AddButton(content, Loc.Choose("保存资料", "Save profile"), LguHubUiBuilder.Primary, () =>
            {
                StartCoroutine(_api.CoUpdateProfile(bioInput.text, nickInput.text,
                    r => SetStatus(r != null && r.success ? Loc.Choose("已保存", "Saved") : (r?.error ?? Loc.Choose("保存失败", "Save failed")))));
            });
            LguHubUiBuilder.AddButton(content, dto.checked_in ? Loc.Choose("今日已签到", "Checked in today") : Loc.Choose("签到领积分", "Check in for points"),
                dto.checked_in ? new Color(0.6f, 0.6f, 0.6f, 1f) : new Color(0.15f, 0.55f, 0.35f, 1f),
                () =>
                {
                    if (dto.checked_in) return;
                    StartCoroutine(_api.CoCheckin(r =>
                    {
                        if (r != null && r.success)
                        {
                            RefreshPoints();
                            BuildProfilePage();
                        }
                        else
                            SetStatus(r?.error ?? Loc.Choose("签到失败", "Check-in failed"));
                    }));
                });
            LguHubUiBuilder.AddText(content, Loc.F("积分：{0}", "Points: {0}", dto.points), 22, TextAlignmentOptions.TopLeft, LguHubUiBuilder.Primary);
        }));
    }

    #endregion

    #region Upload

    void BuildUploadPage()
    {
        var scroll = LguHubUiBuilder.AddScroll(_contentArea, out var content);
        LguHubUiBuilder.AddText(content, Loc.Choose("上传资料", "Upload"), 26, TextAlignmentOptions.TopLeft, LguHubUiBuilder.TextMain);
        LguHubUiBuilder.AddText(content,
            Loc.Choose(
                "完整上传（多文件、标签）请在浏览器打开 LGUHUB 网站操作。\n下方可打开官网上传页。",
                "For full uploads (multiple files, tags), open the LGUHUB website in your browser.\nUse the button below to open the upload page."),
            18, TextAlignmentOptions.TopLeft, LguHubUiBuilder.TextMuted);
        LguHubUiBuilder.AddButton(content, Loc.Choose("在浏览器中打开 LGUHUB", "Open LGUHUB in browser"), LguHubUiBuilder.Primary, () =>
        {
            var baseUrl = _api.LguHubBaseUrl;
            if (!string.IsNullOrEmpty(baseUrl))
                Application.OpenURL(baseUrl.TrimEnd('/') + "/upload-page");
        });
    }

    #endregion

    void EnsureCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void RestoreCursorIfNeeded()
    {
        if (GameplayUiHotkeyGuard.BlocksGameplayBecauseMultiplayerUi())
            return;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
