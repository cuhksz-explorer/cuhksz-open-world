using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 登录弹窗 UI（参考 miHoYo 风格弹窗比例）：LGU EXPLORER 顶栏、双输入框、注册/忘记密码、协议勾选、进入游戏、手机快捷登录占位。
/// </summary>
public static class LguHubAuthUILayout
{
    public const int LayoutVersion = 11;

    public static readonly Color BgWhite = Color.white;
    public static readonly Color TextBlack = new Color(0.12f, 0.12f, 0.12f, 1f);
    public static readonly Color TextMuted = new Color(0.55f, 0.55f, 0.55f, 1f);
    public static readonly Color Gold = new Color(0.79f, 0.66f, 0.28f, 1f);
    public static readonly Color InputBg = new Color(0.98f, 0.98f, 0.98f, 1f);
    public static readonly Color InputBorder = new Color(0.88f, 0.88f, 0.88f, 1f);
    public static readonly Color PrimaryButtonBg = new Color(0.24f, 0.24f, 0.24f, 1f);
    public static readonly Color PrimaryButtonHighlight = new Color(0.32f, 0.32f, 0.32f, 1f);
    public static readonly Color CornerGold = new Color(0.82f, 0.68f, 0.32f, 1f);

    public const string BrandTitleText = "LGU EXPLORER";

    const string FontResourcesPath = "Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF";

    /// <summary>弹窗宽度占屏幕宽度比例（1/2 屏宽）。</summary>
    const float PanelWidthFraction = 0.5f;
    const float PanelAspectWidth = 4f;
    const float PanelAspectHeight = 3f;
    const int AgreementLabelFontSize = 16;
    const float AgreementToggleSize = AgreementLabelFontSize;
    const float HeaderHeight = 80f;
    const float FooterHeight = 52f;
    const float ContentPaddingH = 10f;
    const float ContentPaddingTop = 0f;
    const float ContentPaddingBottom = 0f;
    const float FormBlockMinSpacer = 28f;
    const float FieldHeight = 76f;
    const float InputBorderThickness = 1f;
    const float FieldSpacing = 24f;
    const float HintRowHeight = 38f;
    const float AgreementRowHeight = 36f;
    const float HorizontalRowSpacing = 8f;
    const float EnterButtonHeight = 54f;

    public struct BuiltLoginUi
    {
        public TMP_InputField usernameInput;
        public TMP_InputField passwordInput;
        public Button enterGameButton;
        public Button goToRegisterButton;
        public Button forgotPasswordButton;
        public Button closePanelButton;
        public Button mobileQuickLoginPlaceholder;
        public Toggle agreementToggle;
        public Button userAgreementLink;
        public Button privacyPolicyLink;
        public LguHubAgreementLineClick agreementLineClick;
        public TMP_Text statusText;
        public TMP_Text brandTitle;
        public Button offlinePlayButton;
    }

    public struct BuiltRegisterUi
    {
        public TMP_InputField usernameInput;
        public TMP_InputField passwordInput;
        public Button registerButton;
        public Button backToLoginButton;
        public TMP_Text statusText;
    }

    public static int CountAuthPageChildren(Transform panelRoot)
    {
        if (panelRoot == null)
            return 0;
        int count = 0;
        for (int i = 0; i < panelRoot.childCount; i++)
        {
            var child = panelRoot.GetChild(i);
            if (child == null)
                continue;
            if (child.name == "LoginPage" || child.name == "RegisterPage")
                count++;
        }
        return count;
    }

    public static void ClearAllPanelChildren(Transform panelRoot)
    {
        if (panelRoot == null)
            return;
        for (int i = panelRoot.childCount - 1; i >= 0; i--)
        {
            var child = panelRoot.GetChild(i);
            if (child != null)
                DestroyUiObject(child.gameObject, immediate: true);
        }
    }

    public static void PruneDuplicatePages(Transform panelRoot, GameObject keepLogin, GameObject keepRegister)
    {
        if (panelRoot == null)
            return;
        var keepLoginT = ResolveKeepPage(panelRoot, "LoginPage", keepLogin, HasReferenceLoginLayout);
        var keepRegisterT = ResolveKeepPage(panelRoot, "RegisterPage", keepRegister, null);
        for (int i = panelRoot.childCount - 1; i >= 0; i--)
        {
            var child = panelRoot.GetChild(i);
            if (child == null)
                continue;
            if (child.name == "LoginPage" && child != keepLoginT)
                DestroyUiObject(child);
            else if (child.name == "RegisterPage" && child != keepRegisterT)
                DestroyUiObject(child);
        }
    }

    static Transform ResolveKeepPage(
        Transform panelRoot,
        string pageName,
        GameObject keepRef,
        System.Func<Transform, bool> preferWhen)
    {
        Transform fallback = null;
        for (int i = 0; i < panelRoot.childCount; i++)
        {
            var child = panelRoot.GetChild(i);
            if (child == null || child.name != pageName)
                continue;
            if (preferWhen != null && preferWhen(child))
                return child;
            if (keepRef != null && child.gameObject == keepRef)
                fallback = child;
            else if (fallback == null)
                fallback = child;
        }
        return fallback;
    }

    public static void PruneStrayPanelChildren(Transform panelRoot)
    {
        if (panelRoot == null)
            return;
        for (int i = panelRoot.childCount - 1; i >= 0; i--)
        {
            var child = panelRoot.GetChild(i);
            if (child == null)
                continue;
            var name = child.name;
            if (name == "LoginPage" || name == "RegisterPage" || name == "PanelHeader" || name == "PanelFooter")
                continue;
            DestroyUiObject(child);
        }
    }

    public static bool HasValidLguHubPageLayout(Transform panelRoot)
    {
        if (panelRoot == null)
            return false;
        int loginPages = 0;
        int registerPages = 0;
        Transform loginPage = null;
        for (int i = 0; i < panelRoot.childCount; i++)
        {
            var child = panelRoot.GetChild(i);
            if (child == null)
                continue;
            if (child.name == "LoginPage")
            {
                loginPages++;
                loginPage = child;
            }
            else if (child.name == "RegisterPage")
                registerPages++;
        }
        if (panelRoot.Find("PanelHeader") == null || panelRoot.Find("PanelFooter") == null)
            return false;
        if (loginPages != 1 || registerPages != 1)
            return false;
        return HasReferenceLoginLayout(loginPage);
    }

    public static bool HasReferenceLoginLayout(Transform loginPage)
    {
        if (loginPage == null)
            return false;
        if (loginPage.Find("Title") != null || loginPage.Find("Box_BelowLogin") != null)
            return false;
        if (FindDeep(loginPage, "UsernameField") == null || FindDeep(loginPage, "PasswordField") == null)
            return false;
        if (FindDeep(loginPage, "UsernameField/BorderLeft") == null)
            return false;
        var checkmark = FindDeep(loginPage, "AgreementToggle/CircleBackground/Checkmark");
        if (checkmark == null || checkmark.GetComponent<Image>() == null)
            return false;
        if (FindDeep(loginPage, "Btn_EnterGame") == null || FindDeep(loginPage, "AgreementToggle") == null)
            return false;
        if (FindDeep(loginPage, "AgreementLine") == null)
            return false;
        if (FindDeep(loginPage, "AgreementToggle/CircleBackground") == null)
            return false;
        if (FindDeep(loginPage, "Link_GoRegister") == null || FindDeep(loginPage, "Link_ForgotPassword") == null)
            return false;
        var panel = loginPage.parent;
        if (panel == null)
            return false;
        if (panel.Find("PanelHeader/Btn_Close") == null || panel.Find("PanelHeader/BrandTitle") == null)
            return false;
        if (panel.Find("PanelFooter/Btn_MobileQuickLogin") == null)
            return false;
        if (panel.Find("PanelFooter/Btn_TapTapLogin") != null)
            return false;
        return true;
    }

    public static void ApplyToPanel(Transform panelRoot, GameServerAuthUI auth, GameServerSync sync)
    {
        if (panelRoot == null || auth == null)
            return;

        var panelGo = panelRoot.gameObject;
        var panelRt = panelGo.GetComponent<RectTransform>() ?? panelGo.AddComponent<RectTransform>();
        ApplyReferenceModalPanel(panelRt);

        var panelImg = panelGo.GetComponent<Image>() ?? panelGo.AddComponent<Image>();
        panelImg.color = BgWhite;
        panelImg.raycastTarget = true;

        var outline = panelGo.GetComponent<Outline>() ?? panelGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.08f);
        outline.effectDistance = new Vector2(1f, -1f);

        var mask = panelGo.GetComponent<RectMask2D>();
        if (mask != null)
            DestroyUiObject(mask, immediate: true);

        ClearAllPanelChildren(panelRoot);

        var header = BuildPanelHeader(panelRoot, out var brandTitle, out var closeBtn);
        var footer = BuildPanelFooter(panelRoot, out var mobileQuickBtn);
        var pageArea = CreatePageArea(panelRoot, HeaderHeight, FooterHeight);

        var loginPage = BuildLoginPage(pageArea, out var loginUi);
        var registerPage = BuildRegisterPage(pageArea, out var regUi);
        registerPage.gameObject.SetActive(false);

        loginUi.closePanelButton = closeBtn;
        loginUi.mobileQuickLoginPlaceholder = mobileQuickBtn;
        loginUi.brandTitle = brandTitle;

        TryResolveLoginUiReferences(panelRoot, ref loginUi);
        auth.ConfigureReferenceLayout(
            sync,
            loginPage.gameObject,
            registerPage.gameObject,
            loginUi,
            regUi,
            brandTitle);

        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRt);
    }

    public static void TryResolveLoginUiReferences(Transform panelRoot, ref BuiltLoginUi ui)
    {
        if (panelRoot == null)
            return;
        var loginPage = panelRoot.Find("LoginPage");
        if (loginPage == null)
            return;

        ui.usernameInput = FindComponentOnDeep<TMP_InputField>(loginPage, "UsernameField") ?? ui.usernameInput;
        ui.passwordInput = FindComponentOnDeep<TMP_InputField>(loginPage, "PasswordField") ?? ui.passwordInput;
        ui.enterGameButton = FindComponentOnDeep<Button>(loginPage, "Btn_EnterGame") ?? ui.enterGameButton;
        ui.goToRegisterButton = FindComponentOnDeep<Button>(loginPage, "Link_GoRegister") ?? ui.goToRegisterButton;
        ui.forgotPasswordButton = FindComponentOnDeep<Button>(loginPage, "Link_ForgotPassword") ?? ui.forgotPasswordButton;
        ui.userAgreementLink = FindComponentOnDeep<Button>(loginPage, "Link_UserAgreement") ?? ui.userAgreementLink;
        ui.privacyPolicyLink = FindComponentOnDeep<Button>(loginPage, "Link_PrivacyPolicy") ?? ui.privacyPolicyLink;
        ui.agreementToggle = FindComponentOnDeep<Toggle>(loginPage, "AgreementToggle") ?? ui.agreementToggle;
        ui.agreementLineClick = FindComponentOnDeep<LguHubAgreementLineClick>(loginPage, "AgreementLine") ?? ui.agreementLineClick;
        ui.userAgreementLink = FindComponentOnDeep<Button>(loginPage, "Link_UserAgreement") ?? ui.userAgreementLink;
        ui.privacyPolicyLink = FindComponentOnDeep<Button>(loginPage, "Link_PrivacyPolicy") ?? ui.privacyPolicyLink;
        ui.statusText = FindComponentOnDeep<TMP_Text>(loginPage, "Status") ?? ui.statusText;
        ui.offlinePlayButton = FindComponentOnDeep<Button>(loginPage, "Btn_OfflinePlay") ?? ui.offlinePlayButton;
        ui.closePanelButton = FindComponentOnDeep<Button>(panelRoot, "Btn_Close") ?? ui.closePanelButton;
        ui.mobileQuickLoginPlaceholder = FindComponentOnDeep<Button>(panelRoot, "Btn_MobileQuickLogin") ?? ui.mobileQuickLoginPlaceholder;
        ui.brandTitle = FindComponentOnDeep<TMP_Text>(panelRoot, "BrandTitle") ?? ui.brandTitle;
    }

    public static Transform FindDeep(Transform root, string objectName)
    {
        if (root == null)
            return null;
        if (root.name == objectName)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindDeep(root.GetChild(i), objectName);
            if (found != null)
                return found;
        }
        return null;
    }

    static T FindComponentOnDeep<T>(Transform root, string objectName) where T : Component
    {
        var t = FindDeep(root, objectName);
        return t != null ? t.GetComponent<T>() : null;
    }

    static RectTransform BuildPanelHeader(Transform panel, out TMP_Text brandTitle, out Button closeBtn)
    {
        var header = CreateTopBand(panel, "PanelHeader", HeaderHeight);
        CreateCornerDecor(header);

        var brandGo = new GameObject("BrandTitle", typeof(RectTransform));
        brandGo.transform.SetParent(header, false);
        var brandRt = brandGo.GetComponent<RectTransform>();
        StretchCenter(brandRt, new Vector2(-80f, -12f), new Vector2(80f, 12f));
        brandTitle = brandGo.AddComponent<TextMeshProUGUI>();
        brandTitle.text = BrandTitleText;
        brandTitle.fontSize = 38;
        brandTitle.fontStyle = FontStyles.Bold;
        brandTitle.alignment = TextAlignmentOptions.Center;
        brandTitle.color = TextBlack;
        ApplyFont(brandTitle);

        closeBtn = CreateIconTextButton(header, "Btn_Close", "×", TextBlack, 44f);
        var closeRt = closeBtn.GetComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(1f, 1f);
        closeRt.anchorMax = new Vector2(1f, 1f);
        closeRt.pivot = new Vector2(1f, 1f);
        closeRt.sizeDelta = new Vector2(44f, 44f);
        closeRt.anchoredPosition = new Vector2(-10f, -8f);

        return header;
    }

    static RectTransform BuildPanelFooter(Transform panel, out Button mobileQuickBtn)
    {
        var footer = CreateBottomBand(panel, "PanelFooter", FooterHeight);
        mobileQuickBtn = CreateTextButton(
            footer,
            "Btn_MobileQuickLogin",
            Loc.Choose("手机快捷登录", "Quick mobile login"),
            TextMuted,
            36f,
            TextAnchor.MiddleLeft);
        var mrt = mobileQuickBtn.GetComponent<RectTransform>();
        mrt.anchorMin = new Vector2(0f, 0.5f);
        mrt.anchorMax = new Vector2(0f, 0.5f);
        mrt.pivot = new Vector2(0f, 0.5f);
        mrt.sizeDelta = new Vector2(220f, 36f);
        mrt.anchoredPosition = new Vector2(ContentPaddingH, 0f);
        mobileQuickBtn.interactable = false;
        var mColors = mobileQuickBtn.colors;
        mColors.disabledColor = TextMuted;
        mobileQuickBtn.colors = mColors;
        return footer;
    }

    static RectTransform CreatePageArea(Transform panel, float headerH, float footerH)
    {
        var go = new GameObject("PageArea", typeof(RectTransform));
        go.transform.SetParent(panel, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(0f, footerH);
        rt.offsetMax = new Vector2(0f, -headerH);
        return rt;
    }

    static RectTransform BuildLoginPage(Transform pageArea, out BuiltLoginUi ui)
    {
        ui = default;
        var loginPage = CreateStretchPage(pageArea, "LoginPage");
        var content = CreateVerticalContent(
            loginPage,
            ContentPaddingH,
            ContentPaddingTop,
            ContentPaddingBottom,
            FieldSpacing,
            TextAnchor.MiddleCenter);

        AddVerticalFlexSpacer(content);
        var form = CreateFormBlock(content, ContentPaddingH, FieldSpacing);

        ui.usernameInput = CreateInputField(form, "UsernameField", Loc.Choose("输入手机号/邮箱", "Phone or email"), false);
        ui.passwordInput = CreateInputField(form, "PasswordField", Loc.Choose("输入密码", "Password"), true);
        ui.goToRegisterButton = CreateHintRow(form, out var forgotBtn);
        ui.forgotPasswordButton = forgotBtn;
        ui.agreementToggle = CreateAgreementRow(form, out ui.agreementLineClick);
        ui.userAgreementLink = null;
        ui.privacyPolicyLink = null;
        ui.statusText = CreateStatus(form, "Status");
        ui.enterGameButton = CreatePrimaryButton(form, "Btn_EnterGame", Loc.Choose("进入游戏", "Enter Game"));
        ui.enterGameButton.interactable = false;
        ui.offlinePlayButton = CreateTextButton(
            form,
            "Btn_OfflinePlay",
            Loc.Choose("离线游玩", "Play offline"),
            TextMuted,
            40f,
            TextAnchor.MiddleCenter,
            fontSize: 22);

        AddVerticalFlexSpacer(content);

        return loginPage;
    }

    public static Button EnsureOfflinePlayButton(Transform loginPage)
    {
        if (loginPage == null)
            return null;

        var existing = FindDeep(loginPage, "Btn_OfflinePlay")?.GetComponent<Button>();
        if (existing != null)
            return existing;

        Transform form = FindDeep(loginPage, "FormBlock");
        if (form == null)
        {
            var content = FindDeep(loginPage, "Content");
            form = content != null ? content : loginPage;
        }

        var btn = CreateTextButton(
            form,
            "Btn_OfflinePlay",
            Loc.Choose("离线游玩", "Play offline"),
            TextMuted,
            40f,
            TextAnchor.MiddleCenter,
            fontSize: 22);
        if (btn != null)
            btn.transform.SetAsLastSibling();
        return btn;
    }

    static RectTransform BuildRegisterPage(Transform pageArea, out BuiltRegisterUi ui)
    {
        ui = default;
        var registerPage = CreateStretchPage(pageArea, "RegisterPage");
        var content = CreateVerticalContent(registerPage, ContentPaddingH, ContentPaddingTop + 8f, ContentPaddingBottom, FieldSpacing);

        CreateMutedLabel(content, Loc.Choose("创建新账号", "Create Account"), 26f);
        ui.usernameInput = CreateInputField(content, "UsernameField", Loc.Choose("输入手机号/邮箱", "Phone or email"), false);
        ui.passwordInput = CreateInputField(content, "PasswordField", Loc.Choose("输入密码", "Password"), true);
        ui.statusText = CreateStatus(content, "Status");
        ui.registerButton = CreatePrimaryButton(content, "Btn_Register", Loc.Choose("注 册", "Register"));
        ui.backToLoginButton = CreateTextButton(content, "Link_BackLogin", Loc.Choose("已有账号？返回登录", "Have an account? Back to login"), Gold, 36f, TextAnchor.MiddleCenter);

        return registerPage;
    }

    static RectTransform CreateStretchPage(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        StretchFull(rt);
        return rt;
    }

    static RectTransform CreateVerticalContent(
        Transform page,
        float padH,
        float padTop,
        float padBottom,
        float spacing,
        TextAnchor childAlignment = TextAnchor.UpperCenter)
    {
        var go = new GameObject("Content", typeof(RectTransform));
        go.transform.SetParent(page, false);
        var rt = go.GetComponent<RectTransform>();
        StretchFull(rt);

        var v = go.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(
            Mathf.RoundToInt(padH),
            Mathf.RoundToInt(padH),
            Mathf.RoundToInt(padTop),
            Mathf.RoundToInt(padBottom));
        v.spacing = spacing;
        v.childAlignment = childAlignment;
        v.childControlWidth = true;
        v.childForceExpandWidth = true;
        v.childControlHeight = true;
        v.childForceExpandHeight = false;
        return rt;
    }

    static RectTransform CreateFormBlock(Transform parent, float padH, float spacing)
    {
        var go = new GameObject("FormBlock", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.flexibleHeight = 0f;
        le.flexibleWidth = 1f;

        var v = go.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(Mathf.RoundToInt(padH), Mathf.RoundToInt(padH), 0, 0);
        v.spacing = spacing;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true;
        v.childForceExpandWidth = true;
        v.childControlHeight = true;
        v.childForceExpandHeight = false;
        return go.GetComponent<RectTransform>();
    }

    static void AddVerticalFlexSpacer(Transform parent)
    {
        var go = new GameObject("FlexSpacer", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.flexibleHeight = 1f;
        le.minHeight = FormBlockMinSpacer;
        le.preferredHeight = FormBlockMinSpacer;
    }

    static void CreateCornerDecor(Transform header)
    {
        var decor = new GameObject("CornerDecor", typeof(RectTransform));
        decor.transform.SetParent(header, false);
        var rt = decor.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(36f, 36f);
        rt.anchoredPosition = new Vector2(12f, -10f);

        var h = new GameObject("BarH", typeof(RectTransform));
        h.transform.SetParent(decor.transform, false);
        var hrt = h.GetComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0f, 1f);
        hrt.anchorMax = new Vector2(0f, 1f);
        hrt.pivot = new Vector2(0f, 1f);
        hrt.sizeDelta = new Vector2(28f, 3f);
        hrt.anchoredPosition = Vector2.zero;
        h.AddComponent<Image>().color = CornerGold;

        var v = new GameObject("BarV", typeof(RectTransform));
        v.transform.SetParent(decor.transform, false);
        var vrt = v.GetComponent<RectTransform>();
        vrt.anchorMin = new Vector2(0f, 1f);
        vrt.anchorMax = new Vector2(0f, 1f);
        vrt.pivot = new Vector2(0f, 1f);
        vrt.sizeDelta = new Vector2(3f, 28f);
        vrt.anchoredPosition = Vector2.zero;
        v.AddComponent<Image>().color = CornerGold;
    }

    static RectTransform CreateTopBand(Transform parent, string name, float height)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, height);
        rt.anchoredPosition = Vector2.zero;
        return rt;
    }

    static RectTransform CreateBottomBand(Transform parent, string name, float height)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(0f, height);
        rt.anchoredPosition = Vector2.zero;
        return rt;
    }

    static TMP_InputField CreateInputField(Transform parent, string name, string placeholder, bool password)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        AddLayoutHeight(go, FieldHeight);

        var img = go.AddComponent<Image>();
        img.color = InputBg;
        img.raycastTarget = true;

        AddInputFieldBorderFrame(go.transform, InputBorder, InputBorderThickness);

        var input = go.AddComponent<TMP_InputField>();
        input.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;

        const float padH = 14f;
        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        StretchFull(textGo.GetComponent<RectTransform>(), new Vector2(padH, 6f), new Vector2(-padH, -6f));
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 20;
        tmp.color = TextBlack;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;
        ApplyFont(tmp);

        var phGo = new GameObject("Placeholder", typeof(RectTransform));
        phGo.transform.SetParent(go.transform, false);
        StretchFull(phGo.GetComponent<RectTransform>(), new Vector2(padH, 6f), new Vector2(-padH, -6f));
        var pht = phGo.AddComponent<TextMeshProUGUI>();
        pht.fontSize = 20;
        pht.fontStyle = FontStyles.Italic;
        pht.color = new Color(TextMuted.r, TextMuted.g, TextMuted.b, 0.9f);
        pht.text = placeholder;
        pht.alignment = TextAlignmentOptions.MidlineLeft;
        pht.enableWordWrapping = false;
        pht.raycastTarget = false;
        ApplyFont(pht);

        input.textComponent = tmp;
        input.placeholder = pht;
        return input;
    }

    static void AddInputFieldBorderFrame(Transform fieldRoot, Color borderColor, float thickness)
    {
        CreateBorderEdge(fieldRoot, "BorderLeft", borderColor, thickness, true, false, false, false);
        CreateBorderEdge(fieldRoot, "BorderRight", borderColor, thickness, false, true, false, false);
        CreateBorderEdge(fieldRoot, "BorderTop", borderColor, thickness, false, false, true, false);
        CreateBorderEdge(fieldRoot, "BorderBottom", borderColor, thickness, false, false, false, true);
    }

    static void CreateBorderEdge(
        Transform parent,
        string edgeName,
        Color color,
        float thickness,
        bool left,
        bool right,
        bool top,
        bool bottom)
    {
        var go = new GameObject(edgeName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.transform.SetAsLastSibling();
        var rt = go.GetComponent<RectTransform>();
        StretchFull(rt);

        if (left)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(thickness, 0f);
            rt.anchoredPosition = Vector2.zero;
        }
        else if (right)
        {
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(thickness, 0f);
            rt.anchoredPosition = Vector2.zero;
        }
        else if (top)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, thickness);
            rt.anchoredPosition = Vector2.zero;
        }
        else if (bottom)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0f, thickness);
            rt.anchoredPosition = Vector2.zero;
        }

        var edgeImg = go.AddComponent<Image>();
        edgeImg.color = color;
        edgeImg.raycastTarget = false;
    }

    static Button CreateHintRow(Transform parent, out Button forgotButton)
    {
        var row = new GameObject("Row_Hints", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        AddLayoutHeight(row, HintRowHeight);
        ConfigureHintRowLayoutGroup(row.AddComponent<HorizontalLayoutGroup>());

        const int fontSize = 18;
        string noAccount = Loc.Choose("没有账号？", "No account?");
        string registerNow = Loc.Choose("立即注册", "Register now");
        string forgotLabel = Loc.Choose("忘记密码", "Forgot password");

        CreateInlineLabel(row.transform, noAccount, TextMuted, fontSize);
        var regBtn = CreateTextButton(
            row.transform,
            "Link_GoRegister",
            registerNow,
            Gold,
            HintRowHeight,
            TextAnchor.MiddleLeft,
            horizontalRow: true,
            fontSize: fontSize);
        ApplyHintTextWidth(regBtn.gameObject);

        CreateHintRowSpacer(row.transform);

        forgotButton = CreateTextButton(
            row.transform,
            "Link_ForgotPassword",
            forgotLabel,
            Gold,
            HintRowHeight,
            TextAnchor.MiddleRight,
            horizontalRow: true,
            fontSize: fontSize);
        ApplyHintTextWidth(forgotButton.gameObject);

        return regBtn;
    }

    static void ConfigureHintRowLayoutGroup(HorizontalLayoutGroup rowH)
    {
        rowH.spacing = 6;
        rowH.childAlignment = TextAnchor.MiddleLeft;
        rowH.childControlWidth = true;
        rowH.childForceExpandWidth = false;
        rowH.childControlHeight = true;
        rowH.childForceExpandHeight = false;
    }

    static void CreateHintRowSpacer(Transform row)
    {
        var spacer = new GameObject("Spacer", typeof(RectTransform));
        spacer.transform.SetParent(row, false);
        var spacerLe = spacer.AddComponent<LayoutElement>();
        spacerLe.flexibleWidth = 1f;
        spacerLe.minWidth = 12f;
        spacerLe.preferredWidth = 0f;
        AddLayoutHeight(spacer, HintRowHeight);
    }

    static Toggle CreateAgreementRow(Transform parent, out LguHubAgreementLineClick lineClick)
    {
        lineClick = null;

        var row = new GameObject("Row_Agreement", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        AddLayoutHeight(row, AgreementRowHeight);

        var inner = new GameObject("AgreementInner", typeof(RectTransform));
        inner.transform.SetParent(row.transform, false);
        StretchFull(inner.GetComponent<RectTransform>());

        var h = inner.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 6;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = false;
        h.childForceExpandWidth = false;
        h.childControlHeight = false;
        h.childForceExpandHeight = false;
        h.padding = new RectOffset(0, 0, 0, 0);

        var toggleGo = new GameObject("AgreementToggle", typeof(RectTransform));
        toggleGo.transform.SetParent(inner.transform, false);
        var toggleRt = toggleGo.GetComponent<RectTransform>();
        toggleRt.sizeDelta = new Vector2(AgreementToggleSize, AgreementToggleSize);
        var toggleLe = toggleGo.AddComponent<LayoutElement>();
        toggleLe.preferredWidth = AgreementToggleSize;
        toggleLe.preferredHeight = AgreementToggleSize;
        toggleLe.minWidth = AgreementToggleSize;
        toggleLe.minHeight = AgreementToggleSize;
        toggleLe.flexibleWidth = 0f;
        toggleLe.flexibleHeight = 0f;

        var bg = new GameObject("CircleBackground", typeof(RectTransform));
        bg.transform.SetParent(toggleGo.transform, false);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0.5f, 0.5f);
        bgRt.anchorMax = new Vector2(0.5f, 0.5f);
        bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.sizeDelta = new Vector2(AgreementToggleSize, AgreementToggleSize);
        var bgImg = bg.AddComponent<Image>();
        bgImg.sprite = GetCircleSprite((int)AgreementToggleSize);
        bgImg.type = Image.Type.Simple;
        bgImg.preserveAspect = true;
        bgImg.color = Color.white;
        bgImg.raycastTarget = true;

        var check = new GameObject("Checkmark", typeof(RectTransform));
        check.transform.SetParent(bg.transform, false);
        var checkRt = check.GetComponent<RectTransform>();
        checkRt.anchorMin = new Vector2(0.5f, 0.5f);
        checkRt.anchorMax = new Vector2(0.5f, 0.5f);
        checkRt.pivot = new Vector2(0.5f, 0.5f);
        float markSize = AgreementToggleSize - 4f;
        checkRt.sizeDelta = new Vector2(markSize, markSize);
        var checkImg = check.AddComponent<Image>();
        checkImg.sprite = GetCheckmarkSprite((int)markSize);
        checkImg.type = Image.Type.Simple;
        checkImg.preserveAspect = true;
        checkImg.color = Gold;
        checkImg.raycastTarget = false;

        var toggle = toggleGo.AddComponent<Toggle>();
        toggle.targetGraphic = bgImg;
        toggle.graphic = checkImg;
        toggle.isOn = false;

        var lineGo = new GameObject("AgreementLine", typeof(RectTransform));
        lineGo.transform.SetParent(inner.transform, false);
        var lineLe = lineGo.AddComponent<LayoutElement>();
        lineLe.flexibleWidth = 1f;
        lineLe.minHeight = AgreementRowHeight;
        lineLe.preferredHeight = AgreementRowHeight;
        var lineRt = lineGo.GetComponent<RectTransform>();
        lineRt.anchorMin = new Vector2(0f, 0.5f);
        lineRt.anchorMax = new Vector2(1f, 0.5f);
        lineRt.pivot = new Vector2(0f, 0.5f);
        lineRt.sizeDelta = new Vector2(0f, AgreementRowHeight);

        string goldHex = ColorUtility.ToHtmlStringRGB(Gold);
        var lineTmp = lineGo.AddComponent<TextMeshProUGUI>();
        lineTmp.text = BuildAgreementLineText(goldHex);
        lineTmp.fontSize = AgreementLabelFontSize;
        lineTmp.color = TextMuted;
        lineTmp.alignment = TextAlignmentOptions.Left;
        lineTmp.enableWordWrapping = false;
        lineTmp.richText = true;
        lineTmp.raycastTarget = true;
        ApplyFont(lineTmp);

        lineClick = lineGo.AddComponent<LguHubAgreementLineClick>();

        return toggle;
    }

    static readonly System.Collections.Generic.Dictionary<int, Sprite> CircleSpriteCache =
        new System.Collections.Generic.Dictionary<int, Sprite>();
    static readonly System.Collections.Generic.Dictionary<int, Sprite> CheckmarkSpriteCache =
        new System.Collections.Generic.Dictionary<int, Sprite>();

    static Sprite GetCheckmarkSprite(int sizePx)
    {
        sizePx = Mathf.Clamp(sizePx, 10, 48);
        if (CheckmarkSpriteCache.TryGetValue(sizePx, out var cached) && cached != null)
            return cached;

        int size = sizePx;
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        var clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, clear);

        var mark = Color.white;
        int xA = Mathf.RoundToInt(size * 0.22f);
        int yA = Mathf.RoundToInt(size * 0.50f);
        int xB = Mathf.RoundToInt(size * 0.42f);
        int yB = Mathf.RoundToInt(size * 0.28f);
        int xC = Mathf.RoundToInt(size * 0.78f);
        int yC = Mathf.RoundToInt(size * 0.72f);
        DrawThickLine(tex, xA, yA, xB, yB, Mathf.Max(2, size / 8), mark);
        DrawThickLine(tex, xB, yB, xC, yC, Mathf.Max(2, size / 8), mark);

        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        CheckmarkSpriteCache[sizePx] = sprite;
        return sprite;
    }

    static void DrawThickLine(Texture2D tex, int x0, int y0, int x1, int y1, int thickness, Color color)
    {
        float dx = x1 - x0;
        float dy = y1 - y0;
        int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), 1);
        float half = thickness * 0.5f;
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            float px = x0 + dx * t;
            float py = y0 + dy * t;
            for (int oy = -thickness; oy <= thickness; oy++)
            {
                for (int ox = -thickness; ox <= thickness; ox++)
                {
                    if (ox * ox + oy * oy > half * half + 0.25f)
                        continue;
                    int sx = Mathf.RoundToInt(px) + ox;
                    int sy = Mathf.RoundToInt(py) + oy;
                    if (sx >= 0 && sx < tex.width && sy >= 0 && sy < tex.height)
                        tex.SetPixel(sx, sy, color);
                }
            }
        }
    }

    static Sprite GetCircleSprite(int sizePx)
    {
        sizePx = Mathf.Clamp(sizePx, 12, 64);
        if (CircleSpriteCache.TryGetValue(sizePx, out var cached) && cached != null)
            return cached;

        int size = sizePx;
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = (size - 1) * 0.5f;
        float outerR = center;
        float innerR = center - 1.2f;
        var fill = Color.white;
        var border = InputBorder;
        var clear = new Color(0f, 0f, 0f, 0f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist <= innerR)
                    tex.SetPixel(x, y, fill);
                else if (dist <= outerR)
                    tex.SetPixel(x, y, border);
                else
                    tex.SetPixel(x, y, clear);
            }
        }

        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        CircleSpriteCache[sizePx] = sprite;
        return sprite;
    }

    static Button CreateRightAnchoredTextButton(
        Transform row,
        string name,
        string label,
        Color textColor,
        float height,
        int fontSize)
    {
        var btn = CreateTextButton(row, name, label, textColor, height, TextAnchor.MiddleRight, horizontalRow: false, fontSize: fontSize);
        var le = btn.GetComponent<LayoutElement>();
        if (le != null)
            Object.Destroy(le);

        var rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 0.5f);
        float w = EstimateTextWidth(label, fontSize) + 8f;
        rt.offsetMin = new Vector2(-w, 0f);
        rt.offsetMax = Vector2.zero;
        return btn;
    }

    static Button CreatePrimaryButton(Transform parent, string name, string label)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        AddLayoutHeight(go, EnterButtonHeight);

        var img = go.AddComponent<Image>();
        img.color = PrimaryButtonBg;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = PrimaryButtonBg;
        colors.highlightedColor = PrimaryButtonHighlight;
        colors.pressedColor = new Color(0.18f, 0.18f, 0.18f, 1f);
        colors.selectedColor = PrimaryButtonBg;
        btn.colors = colors;

        var tgo = new GameObject("Text", typeof(RectTransform));
        tgo.transform.SetParent(go.transform, false);
        StretchFull(tgo.GetComponent<RectTransform>());
        var txt = tgo.AddComponent<TextMeshProUGUI>();
        txt.text = label;
        txt.fontSize = 24;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;
        txt.raycastTarget = false;
        ApplyFont(txt);
        return btn;
    }

    static Button CreateTextButton(
        Transform parent,
        string name,
        string label,
        Color textColor,
        float height,
        TextAnchor align,
        bool horizontalRow = false,
        int fontSize = 18)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        if (horizontalRow)
        {
            PrepareHorizontalLayoutChild(go.GetComponent<RectTransform>(), height);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
        }
        else
            AddLayoutHeight(go, height);

        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.01f);
        img.raycastTarget = true;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var tgo = new GameObject("Text", typeof(RectTransform));
        tgo.transform.SetParent(go.transform, false);
        StretchFull(tgo.GetComponent<RectTransform>());
        var txt = tgo.AddComponent<TextMeshProUGUI>();
        txt.text = label;
        txt.fontSize = fontSize;
        txt.alignment = align == TextAnchor.MiddleLeft
            ? TextAlignmentOptions.Left
            : align == TextAnchor.MiddleRight
                ? TextAlignmentOptions.Right
                : TextAlignmentOptions.Center;
        txt.color = textColor;
        txt.raycastTarget = false;
        txt.enableWordWrapping = false;
        txt.overflowMode = TextOverflowModes.Overflow;
        ApplyFont(txt);
        return btn;
    }

    static Button CreateIconTextButton(
        Transform parent,
        string name,
        string label,
        Color textColor,
        float size)
    {
        var btn = CreateTextButton(parent, name, label, textColor, size, TextAnchor.MiddleCenter);
        return btn;
    }

    static TMP_Text CreateInlineLabel(Transform parent, string text, Color color, int fontSize)
    {
        var go = new GameObject("Label_" + text, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        float rowH = fontSize <= 16 ? AgreementRowHeight : HintRowHeight;
        PrepareHorizontalLayoutChild(go.GetComponent<RectTransform>(), rowH);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = rowH;
        le.minHeight = rowH;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        ApplyFont(tmp);
        le.preferredWidth = MeasureTmp(tmp);
        return tmp;
    }

    static float MeasureTmp(TMP_Text tmp)
    {
        if (tmp == null)
            return HintRowHeight;
        tmp.enableWordWrapping = false;
        float w = tmp.GetPreferredValues(Mathf.Infinity, HintRowHeight).x;
        return Mathf.Ceil(Mathf.Max(tmp.fontSize * 1.1f, w + 10f));
    }

    static void ApplyHintTextWidth(GameObject go)
    {
        if (go == null)
            return;
        var tmp = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
        }
        SetLayoutElementWidth(go, MeasureTmp(tmp));
    }

    static float EstimateTextWidth(string text, int fontSize)
    {
        if (string.IsNullOrEmpty(text))
            return fontSize;

        float width = 0f;
        foreach (char c in text)
        {
            if (c == ' ')
                width += fontSize * 0.32f;
            else if (c < 128)
                width += fontSize * 0.62f;
            else
                width += fontSize * 0.95f;
        }

        return Mathf.Max(fontSize * 1.2f, width + fontSize * 0.45f);
    }

    static void SetLayoutElementWidth(GameObject go, float width)
    {
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.minWidth = width;
    }

    static void PrepareHorizontalLayoutChild(RectTransform rt, float height)
    {
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(rt.sizeDelta.x > 1f ? rt.sizeDelta.x : 40f, height);
    }

    static TMP_Text CreateMutedLabel(Transform parent, string text, float height)
    {
        var go = new GameObject("Subtitle", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        AddLayoutHeight(go, height);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 18;
        tmp.color = TextMuted;
        tmp.alignment = TextAlignmentOptions.Center;
        ApplyFont(tmp);
        return tmp;
    }

    static TMP_Text CreateStatus(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        AddLayoutHeight(go, 22f);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = "";
        tmp.fontSize = 16;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.75f, 0.2f, 0.2f, 1f);
        ApplyFont(tmp);
        return tmp;
    }

    static void AddLayoutHeight(GameObject go, float h)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
            PrepareLayoutChild(rt, h);
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        le.minHeight = h;
        le.preferredHeight = h;
        le.flexibleHeight = 0f;
    }

    static void PrepareLayoutChild(RectTransform rt, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, height);
    }

    /// <summary>居中弹窗：宽度占屏幕 1/2，高宽比 4:3。</summary>
    public static void ApplyReferenceModalPanel(RectTransform rt)
    {
        float widthNorm = PanelWidthFraction;
        float screenAspect = Screen.width > 0 && Screen.height > 0
            ? (float)Screen.width / Screen.height
            : 16f / 9f;
        float heightNorm = widthNorm * (PanelAspectHeight / PanelAspectWidth) * screenAspect;
        heightNorm = Mathf.Clamp(heightNorm, 0.2f, 0.95f);

        float marginX = (1f - widthNorm) * 0.5f;
        float marginY = (1f - heightNorm) * 0.5f;
        rt.anchorMin = new Vector2(marginX, marginY);
        rt.anchorMax = new Vector2(1f - marginX, 1f - marginY);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    public static void ApplyCenteredHalfScreenPanel(RectTransform rt) => ApplyReferenceModalPanel(rt);

    public static void RebuildLoginPageLayout(Transform loginPage)
    {
        if (loginPage == null)
            return;
        var content = loginPage.Find("Content");
        if (content != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(content as RectTransform);
    }

    static string BuildAgreementLineText(string goldHex)
    {
        return Loc.Choose(
            "同意" +
            $"<link=\"user\"><color=#{goldHex}>《用户协议》</color></link>" +
            "和" +
            $"<link=\"privacy\"><color=#{goldHex}>《隐私政策》</color></link>",
            "I agree to the " +
            $"<link=\"user\"><color=#{goldHex}>Terms of Service</color></link>" +
            " and " +
            $"<link=\"privacy\"><color=#{goldHex}>Privacy Policy</color></link>");
    }

    public static void RefreshLocalizedTexts(Transform panelRoot)
    {
        if (panelRoot == null)
            return;

        string goldHex = ColorUtility.ToHtmlStringRGB(Gold);
        var agreementLine = FindDeep(panelRoot, "AgreementLine")?.GetComponent<TextMeshProUGUI>();
        if (agreementLine != null)
            agreementLine.text = BuildAgreementLineText(goldHex);

        SetButtonLabel(FindDeep(panelRoot, "Btn_EnterGame"), Loc.Choose("进入游戏", "Enter Game"));
        SetButtonLabel(FindDeep(panelRoot, "Btn_OfflinePlay"), Loc.Choose("离线游玩", "Play offline"));
        SetButtonLabel(FindDeep(panelRoot, "Btn_Register"), Loc.Choose("注 册", "Register"));
        SetButtonLabel(FindDeep(panelRoot, "Btn_MobileQuickLogin"), Loc.Choose("手机快捷登录", "Quick mobile login"));
        SetButtonLabel(FindDeep(panelRoot, "Link_GoRegister"), Loc.Choose("立即注册", "Register now"));
        SetButtonLabel(FindDeep(panelRoot, "Link_ForgotPassword"), Loc.Choose("忘记密码", "Forgot password"));
        SetButtonLabel(FindDeep(panelRoot, "Link_BackLogin"), Loc.Choose("已有账号？返回登录", "Have an account? Back to login"));

        SetInputPlaceholder(FindDeep(panelRoot, "LoginPage/UsernameField"), Loc.Choose("输入手机号/邮箱", "Phone or email"));
        SetInputPlaceholder(FindDeep(panelRoot, "LoginPage/PasswordField"), Loc.Choose("输入密码", "Password"));
        SetInputPlaceholder(FindDeep(panelRoot, "RegisterPage/UsernameField"), Loc.Choose("输入手机号/邮箱", "Phone or email"));
        SetInputPlaceholder(FindDeep(panelRoot, "RegisterPage/PasswordField"), Loc.Choose("输入密码", "Password"));

        var regSubtitle = FindDeep(panelRoot, "RegisterPage/Subtitle")?.GetComponent<TextMeshProUGUI>();
        if (regSubtitle != null)
            regSubtitle.text = Loc.Choose("创建新账号", "Create Account");

        var noAccountLabel = FindDeep(panelRoot, "Label_" + Loc.Choose("没有账号？", "No account?"));
        if (noAccountLabel == null)
        {
            var hints = FindDeep(panelRoot, "Row_Hints");
            if (hints != null)
            {
                for (int i = 0; i < hints.childCount; i++)
                {
                    var child = hints.GetChild(i);
                    if (child.name.StartsWith("Label_", System.StringComparison.Ordinal))
                    {
                        var tmp = child.GetComponent<TextMeshProUGUI>();
                        if (tmp != null)
                            tmp.text = Loc.Choose("没有账号？", "No account?");
                        break;
                    }
                }
            }
        }
        else
        {
            var tmp = noAccountLabel.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
                tmp.text = Loc.Choose("没有账号？", "No account?");
        }

        RefreshHintRowLayout(panelRoot);
    }

    static void RefreshHintRowLayout(Transform panelRoot)
    {
        var hints = FindDeep(panelRoot, "Row_Hints");
        if (hints == null)
            return;

        var regBtn = FindDeep(panelRoot, "Link_GoRegister");
        var forgotBtn = FindDeep(panelRoot, "Link_ForgotPassword");
        if (!IsHintRowLayoutValid(hints, regBtn, forgotBtn))
            MigrateHintRowLayout(hints, regBtn, forgotBtn);

        ApplyHintRowTextWidths(hints, regBtn, forgotBtn);
        LayoutRebuilder.ForceRebuildLayoutImmediate(hints as RectTransform);
        if (hints.parent is RectTransform parentRt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRt);
    }

    static bool IsHintRowLayoutValid(Transform hints, Transform regBtn, Transform forgotBtn)
    {
        if (hints == null || regBtn == null || forgotBtn == null)
            return false;
        if (hints.GetComponent<HorizontalLayoutGroup>() == null)
            return false;
        if (hints.Find("Spacer") == null)
            return false;
        if (hints.Find("Left_Register") != null)
            return false;
        if (regBtn.parent != hints)
            return false;
        return forgotBtn.parent == hints;
    }

    static void MigrateHintRowLayout(Transform hints, Transform regBtn, Transform forgotBtn)
    {
        Transform label = FindHintRowLabel(hints);
        var left = hints.Find("Left_Register");
        if (left != null)
        {
            if (label == null)
                label = FindHintRowLabel(left);
            if (regBtn == null)
                regBtn = left.Find("Link_GoRegister");
        }

        var rowH = hints.GetComponent<HorizontalLayoutGroup>();
        if (rowH == null)
            rowH = hints.gameObject.AddComponent<HorizontalLayoutGroup>();
        ConfigureHintRowLayoutGroup(rowH);

        int nextIndex = 0;
        if (label != null)
        {
            label.SetParent(hints, false);
            PrepareHorizontalLayoutChild(label.GetComponent<RectTransform>(), HintRowHeight);
            label.SetSiblingIndex(nextIndex++);
        }

        if (regBtn != null)
        {
            regBtn.SetParent(hints, false);
            ResetHintButtonRect(regBtn);
            regBtn.SetSiblingIndex(nextIndex++);
        }

        var spacer = hints.Find("Spacer");
        if (spacer == null)
        {
            CreateHintRowSpacer(hints);
            spacer = hints.Find("Spacer");
        }
        if (spacer != null)
            spacer.SetSiblingIndex(nextIndex++);

        if (forgotBtn != null)
        {
            forgotBtn.SetParent(hints, false);
            ResetHintButtonRect(forgotBtn);
            forgotBtn.SetSiblingIndex(nextIndex);
        }

        if (left != null)
            DestroyUiObject(left.gameObject);
    }

    static Transform FindHintRowLabel(Transform root)
    {
        if (root == null)
            return null;
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child.name.StartsWith("Label_", System.StringComparison.Ordinal))
                return child;
        }
        return null;
    }

    static void ResetHintButtonRect(Transform btn)
    {
        if (btn == null)
            return;
        var le = btn.GetComponent<LayoutElement>() ?? btn.gameObject.AddComponent<LayoutElement>();
        le.flexibleWidth = 0f;
        le.flexibleHeight = 0f;
        PrepareHorizontalLayoutChild(btn.GetComponent<RectTransform>(), HintRowHeight);
    }

    static void ApplyHintRowTextWidths(Transform hints, Transform regBtn, Transform forgotBtn)
    {
        string noAccount = Loc.Choose("没有账号？", "No account?");
        string registerNow = Loc.Choose("立即注册", "Register now");
        string forgotLabel = Loc.Choose("忘记密码", "Forgot password");

        var label = FindHintRowLabel(hints);
        if (label != null)
        {
            var tmp = label.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
                tmp.text = noAccount;
            ApplyHintTextWidth(label.gameObject);
        }

        if (regBtn != null)
        {
            SetButtonLabel(regBtn, registerNow);
            ApplyHintTextWidth(regBtn.gameObject);
        }

        if (forgotBtn != null)
        {
            SetButtonLabel(forgotBtn, forgotLabel);
            ApplyHintTextWidth(forgotBtn.gameObject);
        }
    }

    static void SetButtonLabel(Transform btnT, string label)
    {
        if (btnT == null)
            return;
        var txt = btnT.GetComponentInChildren<TextMeshProUGUI>(true);
        if (txt != null)
            txt.text = label;
    }

    static void SetInputPlaceholder(Transform fieldT, string placeholder)
    {
        if (fieldT == null)
            return;
        var ph = fieldT.Find("Placeholder")?.GetComponent<TextMeshProUGUI>();
        if (ph != null)
            ph.text = placeholder;
    }
    static void StretchFull(RectTransform rt, Vector2 offsetMin = default, Vector2 offsetMax = default)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    static void StretchCenter(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    static void ApplyFont(TMP_Text tmp)
    {
        var f = Resources.Load<TMP_FontAsset>(FontResourcesPath);
        if (f != null)
            tmp.font = f;
    }

    static void DestroyUiObject(Object obj, bool immediate = false)
    {
        if (obj == null)
            return;
        if (immediate || !Application.isPlaying)
            Object.DestroyImmediate(obj);
        else
            Object.Destroy(obj);
    }
}
