using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 使用 UnityCef 在 Windows 下在 RawImage 中显示网页。
/// 需先导入 UnityCef（.NET 4.x），见 UnityCef使用说明.md。
/// </summary>
public class WebViewUnityCef : MonoBehaviour
{
    [Header("要显示的网页（需 http/https）")]
    public string loadUrl = "https://www.baidu.com";

    [Header("可选：网址输入框")]
    [Tooltip("若指定，点击打开时将使用此处输入的网址；否则使用上方 Load Url。支持 Unity UI InputField 或 TextMeshPro TMP_InputField")]
    public TMP_InputField urlInputField;

    [Header("显示到该 RawImage")]
    public RawImage targetRawImage;

    [Header("可选：用于打开/关闭的按钮")]
    public Button openButton;
    public Button closeButton;

    [Header("显示颜色")]
    [Tooltip("网页显示时 RawImage 的颜色。若感觉像蒙了一层白膜，可把 R/G/B 略调低（如 0.95），A 保持 1 否则网页会消失")]
    public Color webViewDisplayColor = new Color(0.97f, 0.97f, 0.97f, 1f);

    [Header("渲染尺寸")]
    [Tooltip("勾选后强制使用下方宽高渲染，用于排查是程序计算问题还是 UnityCef 问题；不勾选则按 RawImage 尺寸")]
    public bool useFixedRenderSize = false;
    public int fixedRenderWidth = 1920;
    public int fixedRenderHeight = 1080;
    [Tooltip("不勾选固定尺寸时，浏览器视口至少为该尺寸，避免网页只渲染一小块并出现内部滚动条。设为 0 表示不设下限")]
    public int minBrowserWidth = 1280;
    public int minBrowserHeight = 720;

    [Header("显示校正")]
    [Tooltip("若网页只显示左侧一条且被拉伸，说明 UnityCef 可能把宽高用反了，勾选此项尝试交换宽高")]
    public bool swapWidthHeightForUnityCef = false;
    [Tooltip("翻转方式：UvRect=改UV，Scale=改物体缩放，若一种无效可换另一种")]
    public FlipMethod flipMethod = FlipMethod.Scale;
    [Tooltip("若网页上下颠倒选 OnlyVertical，左右镜像选 OnlyHorizontal，都反选 Both")]
    public FlipMode textureFlip = FlipMode.OnlyVertical;

    [Header("调试")]
    [Tooltip("勾选后在 Console 输出详细日志，便于排查 RawImage 变透明等问题")]
    public bool enableDebugLog;

    public enum FlipMethod { UvRect, Scale }
    public enum FlipMode { None, OnlyVertical, OnlyHorizontal, Both }

    private GameObject _browserGo;
    private Component _webBrowserComponent;
    private PropertyInfo _textureProp;
    private MethodInfo _loadUrlMethod;
    private MethodInfo _setSizeMethod;
    private object _browserIpcInstance;
    private MethodInfo _browserIpcResizeMethod;
    private bool _opened;
    private int _framesSinceOpen;
    private int _lastRenderWidth;
    private int _lastRenderHeight;
    private string _currentLoadUrl;

    static Texture2D _placeholderTexture;

    /// <summary>
    /// 获取或创建占位纹理（1x1 白图）。RawImage 未手动指定纹理时，脚本会自动用此占位，避免 UnassignedReferenceException。
    /// 也可在 Inspector 里给 RawImage 手动指定任意纹理（如一张白色图）代替。
    /// </summary>
    public static Texture2D GetOrCreatePlaceholderTexture()
    {
        if (_placeholderTexture != null) return _placeholderTexture;
        _placeholderTexture = new Texture2D(1, 1);
        _placeholderTexture.SetPixel(0, 0, Color.white);
        _placeholderTexture.Apply();
        _placeholderTexture.name = "WebViewUnityCef_Placeholder";
        return _placeholderTexture;
    }

    /// <summary>
    /// 强制应用显示校正。Scale 方式时固定为 (1, -1, 1)，避免出现 (-1,-1,1) 或每次点击翻转。
    /// </summary>
    void ApplyTextureFlip()
    {
        if (targetRawImage == null) return;
        var rt = targetRawImage.rectTransform;
        if (flipMethod == FlipMethod.Scale)
        {
            targetRawImage.uvRect = new Rect(0, 0, 1, 1);
            rt.localScale = new Vector3(1f, -1f, 1f);
        }
        else
        {
            rt.localScale = new Vector3(1f, -1f, 1f);
            switch (textureFlip)
            {
                case FlipMode.OnlyVertical: targetRawImage.uvRect = new Rect(0, 1, 1, -1); break;
                case FlipMode.OnlyHorizontal: targetRawImage.uvRect = new Rect(1, 0, -1, 1); break;
                case FlipMode.Both: targetRawImage.uvRect = new Rect(1, 1, -1, -1); break;
                default: targetRawImage.uvRect = new Rect(0, 0, 1, 1); break;
            }
        }
    }

    /// <summary>
    /// 取绑定的 RawImage 在屏幕上的实际像素尺寸，用于浏览器渲染，使网页完整显示在 RawImage 内。
    /// 布局未就绪或计算异常时回退 1920×1080，避免传 0 或过小导致网页显示不全。
    /// </summary>
    void GetRawImageSize(out int width, out int height)
    {
        width = 1920;
        height = 1080;
        if (targetRawImage == null || targetRawImage.rectTransform == null) return;
        Canvas.ForceUpdateCanvases();
        var rt = targetRawImage.rectTransform;
        var canvas = targetRawImage.canvas;
        if (canvas == null) return;
        float scale = canvas.renderMode != RenderMode.WorldSpace ? canvas.scaleFactor : 1f;
        float sx = Mathf.Abs(rt.lossyScale.x);
        float sy = Mathf.Abs(rt.lossyScale.y);
        var r = rt.rect;
        int w = Mathf.RoundToInt(r.width * sx * scale);
        int h = Mathf.RoundToInt(r.height * sy * scale);
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            Rect pixelRect = RectTransformUtility.PixelAdjustRect(rt, canvas);
            int pw = Mathf.RoundToInt(Mathf.Abs(pixelRect.width));
            int ph = Mathf.RoundToInt(Mathf.Abs(pixelRect.height));
            if (pw > 0 && ph > 0)
            {
                w = pw;
                h = ph;
            }
        }
        if (w > 0 && h > 0)
        {
            width = Mathf.Max(256, w);
            height = Mathf.Max(256, h);
        }
    }

    /// <summary>
    /// 网页渲染尺寸：可选固定尺寸或按 RawImage 实际像素尺寸，且不低于 minBrowser 下限，使整页渲染到显示区域、无内部滚动条。
    /// </summary>
    void GetRenderSize(out int width, out int height)
    {
        if (useFixedRenderSize)
        {
            width = Mathf.Clamp(fixedRenderWidth, 256, 4096);
            height = Mathf.Clamp(fixedRenderHeight, 256, 4096);
        }
        else
        {
            GetRawImageSize(out width, out height);
            if (minBrowserWidth > 0 || minBrowserHeight > 0)
            {
                width = Mathf.Max(width, Mathf.Clamp(minBrowserWidth, 256, 4096));
                height = Mathf.Max(height, Mathf.Clamp(minBrowserHeight, 256, 4096));
            }
        }
    }

    /// <summary>
    /// 根据 swapWidthHeightForUnityCef 得到传给 UnityCef 的 (宽, 高)，避免部分版本宽高用反导致只显示左侧一条。
    /// </summary>
    void GetSizeForBrowser(int w, int h, out int outW, out int outH)
    {
        if (swapWidthHeightForUnityCef) { outW = h; outH = w; }
        else { outW = w; outH = h; }
    }

    /// <summary>
    /// UnityCef WebBrowser 的 Width/Height 是 public 字段而非属性，必须用 GetField 读写，否则读为 0、写无效。
    /// </summary>
    static void SetWebBrowserSize(Component webBrowser, Type browserType, int w, int h)
    {
        var wf = browserType.GetField("Width", BindingFlags.Public | BindingFlags.Instance);
        var hf = browserType.GetField("Height", BindingFlags.Public | BindingFlags.Instance);
        if (wf != null) wf.SetValue(webBrowser, w);
        if (hf != null) hf.SetValue(webBrowser, h);
        if (wf == null && hf == null)
        {
            var wp = browserType.GetProperty("Width", BindingFlags.Public | BindingFlags.Instance);
            var hp = browserType.GetProperty("Height", BindingFlags.Public | BindingFlags.Instance);
            if (wp != null && wp.CanWrite) wp.SetValue(webBrowser, w, null);
            if (hp != null && hp.CanWrite) hp.SetValue(webBrowser, h, null);
        }
    }

    static void GetWebBrowserSize(Component webBrowser, Type browserType, out int w, out int h)
    {
        w = 0; h = 0;
        var wf = browserType.GetField("Width", BindingFlags.Public | BindingFlags.Instance);
        var hf = browserType.GetField("Height", BindingFlags.Public | BindingFlags.Instance);
        if (wf != null) w = (int)wf.GetValue(webBrowser);
        if (hf != null) h = (int)hf.GetValue(webBrowser);
        if (wf == null && hf == null)
        {
            var wp = browserType.GetProperty("Width", BindingFlags.Public | BindingFlags.Instance);
            var hp = browserType.GetProperty("Height", BindingFlags.Public | BindingFlags.Instance);
            if (wp != null && wp.CanRead) w = (int)wp.GetValue(webBrowser);
            if (hp != null && hp.CanRead) h = (int)hp.GetValue(webBrowser);
        }
    }

    public static bool IsUnityCefAvailable()
    {
        var t = FindWebBrowserType();
        return t != null;
    }

    static Type FindWebBrowserType()
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var t = asm.GetType("WebBrowser");
                if (t != null) return t;
                t = asm.GetType("UnityCef.WebBrowser");
                if (t != null) return t;
            }
            catch { /* ignore */ }
        }
        return null;
    }

    void Start()
    {
        // 无条件输出一条，便于确认脚本运行且 Console 未过滤 Log（若看不到请检查 Console 的 Log 开关）
        Debug.LogWarning("[WebViewUnityCef] 已启动 | Target Raw Image=" + (targetRawImage != null) + ", Open Button=" + (openButton != null));
        if (openButton != null)
            openButton.onClick.AddListener(Open);
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
        // 场景加载时若 RawImage 未赋纹理则先赋占位，避免一运行就报 UnassignedReferenceException
        if (targetRawImage != null && targetRawImage.texture == null)
            targetRawImage.texture = GetOrCreatePlaceholderTexture();
    }

    /// <summary>
    /// 打开网页并显示到 RawImage（可由按钮或脚本调用）
    /// </summary>
    public void Open()
    {
        Debug.LogWarning("[WebViewUnityCef] Open() 被调用。");
#if !UNITY_EDITOR_WIN && !UNITY_STANDALONE_WIN
        Debug.LogWarning("[WebViewUnityCef] 仅支持 Windows 平台。");
        return;
#endif
        if (targetRawImage == null)
        {
            Debug.LogError("[WebViewUnityCef] 请指定 Target Raw Image。");
            return;
        }
        string urlToLoad = loadUrl;
        if (urlInputField != null && !string.IsNullOrWhiteSpace(urlInputField.text))
        {
            string t = urlInputField.text.Trim();
            if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || t.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                urlToLoad = t;
        }
        _currentLoadUrl = urlToLoad;
        if (string.IsNullOrEmpty(_currentLoadUrl) || (!_currentLoadUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !_currentLoadUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            Debug.LogError("[WebViewUnityCef] 请填写有效 http/https 地址（或在网址输入框中输入）。");
            return;
        }

        if (enableDebugLog)
            Debug.Log($"[WebViewUnityCef] Open() 被调用 | URL={_currentLoadUrl} | RawImage.enabled={targetRawImage.enabled}");

        var browserType = FindWebBrowserType();
        if (browserType == null)
        {
            Debug.LogError("[WebViewUnityCef] 未检测到 UnityCef 的 WebBrowser 组件。请先导入 UnityCef 并设置 .NET 4.x。");
            Application.OpenURL(_currentLoadUrl);
            return;
        }
        if (enableDebugLog)
            Debug.Log($"[WebViewUnityCef] 已找到 WebBrowser 类型: {browserType.FullName}");

        if (_opened && _webBrowserComponent != null)
        {
            int reopenW, reopenH;
            GetRenderSize(out reopenW, out reopenH);
            int setW, setH;
            GetSizeForBrowser(reopenW, reopenH, out setW, out setH);
            Debug.LogWarning("[WebViewUnityCef] 再次打开，渲染尺寸: " + reopenW + "x" + reopenH + (swapWidthHeightForUnityCef ? "（已交换为 " + setW + "x" + setH + "）" : ""));
            // 再次打开时也强制 SetSize 或内部 Resize，否则浏览器会沿用首次的错误尺寸
            bool didResizeReopen = false;
            if (_setSizeMethod != null)
            {
                try { _setSizeMethod.Invoke(_webBrowserComponent, new object[] { setW, setH }); didResizeReopen = true; }
                catch (Exception ex) { if (enableDebugLog) Debug.LogWarning("[WebViewUnityCef] 再次打开 SetSize: " + ex.Message); }
            }
            if (!didResizeReopen && _browserIpcResizeMethod != null && _browserIpcInstance != null)
            {
                try { _browserIpcResizeMethod.Invoke(_browserIpcInstance, new object[] { setW, setH }); didResizeReopen = true; }
                catch (Exception ex) { if (enableDebugLog) Debug.LogWarning("[WebViewUnityCef] 再次打开 browserIpc.Resize: " + ex.Message); }
            }
            if (didResizeReopen) { _lastRenderWidth = reopenW; _lastRenderHeight = reopenH; }
            var bt = _webBrowserComponent.GetType();
            try
            {
                SetWebBrowserSize(_webBrowserComponent, bt, setW, setH);
            }
            catch { }
            TryCallLoadUrl(_webBrowserComponent, _currentLoadUrl);
            targetRawImage.enabled = true;
            targetRawImage.color = webViewDisplayColor;
            ApplyTextureFlip();
            return;
        }

        if (_browserGo != null)
        {
            Destroy(_browserGo);
            _browserGo = null;
            _webBrowserComponent = null;
            _browserIpcInstance = null;
            _browserIpcResizeMethod = null;
        }
        _loggedTextureSizeOnce = false;

        _browserGo = new GameObject("UnityCefWebBrowser");
        // 浏览器物体挂在场景根，避免挂在 Canvas/RawImage 下导致盖住 RawImage 或改变渲染顺序
        _browserGo.transform.SetParent(transform.root);
        _webBrowserComponent = _browserGo.AddComponent(browserType);
        if (_webBrowserComponent == null)
        {
            Debug.LogError("[WebViewUnityCef] 无法添加 WebBrowser 组件。");
            Destroy(_browserGo);
            return;
        }
        // 立即设置 Width/Height，让 UnityCef 在 Init 时使用正确视口尺寸，避免默认小视口导致网页只渲染一小块
        int wEarly, hEarly;
        GetRenderSize(out wEarly, out hEarly);
        int earlyW, earlyH;
        GetSizeForBrowser(wEarly, hEarly, out earlyW, out earlyH);
        try
        {
            SetWebBrowserSize(_webBrowserComponent, browserType, earlyW, earlyH);
        }
        catch (Exception ex) { if (enableDebugLog) Debug.LogWarning("[WebViewUnityCef] 提前设置 Width/Height: " + ex.Message); }

        _textureProp = browserType.GetProperty("Texture", BindingFlags.Public | BindingFlags.Instance);
        if (_textureProp == null)
            _textureProp = browserType.GetProperty("texture", BindingFlags.Public | BindingFlags.Instance);

        string[] loadMethodNames = { "LoadUrl", "LoadURL", "Load", "Navigate", "GoToUrl", "GoToURL", "SetUrl", "LoadUrlFromString", "OpenUrl", "OpenURL" };
        foreach (var name in loadMethodNames)
        {
            _loadUrlMethod = browserType.GetMethod(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase, null, new[] { typeof(int), typeof(int), typeof(string) }, null);
            if (_loadUrlMethod != null)
            {
                if (enableDebugLog) Debug.Log($"[WebViewUnityCef] 已找到加载方法(宽,高,URL): " + _loadUrlMethod.Name);
                break;
            }
        }
        if (_loadUrlMethod == null)
        {
            foreach (var name in loadMethodNames)
            {
                _loadUrlMethod = browserType.GetMethod(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase, null, new[] { typeof(string) }, null);
                if (_loadUrlMethod != null)
                {
                    if (enableDebugLog) Debug.Log($"[WebViewUnityCef] 已找到加载方法(URL): " + _loadUrlMethod.Name);
                    break;
                }
            }
        }
        if (_loadUrlMethod == null)
        {
            foreach (var m in browserType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                var ps = m.GetParameters();
                if (ps.Length == 3 && ps[0].ParameterType == typeof(int) && ps[1].ParameterType == typeof(int) && ps[2].ParameterType == typeof(string))
                {
                    var n = m.Name.ToLowerInvariant();
                    if (n.Contains("load") || n.Contains("url") || n.Contains("navigate") || n.Contains("open") || n.Contains("goto"))
                    {
                        _loadUrlMethod = m;
                        if (enableDebugLog) Debug.Log($"[WebViewUnityCef] 已通过名称匹配找到(宽,高,URL): " + m.Name);
                        break;
                    }
                }
                if (_loadUrlMethod == null && ps.Length == 1 && ps[0].ParameterType == typeof(string))
                {
                    var n = m.Name.ToLowerInvariant();
                    if (n.Contains("load") || n.Contains("url") || n.Contains("navigate") || n.Contains("open") || n.Contains("goto"))
                    {
                        _loadUrlMethod = m;
                        if (enableDebugLog) Debug.Log($"[WebViewUnityCef] 已通过名称匹配找到(URL): " + m.Name);
                        break;
                    }
                }
            }
        }

        if (_textureProp == null && enableDebugLog)
            Debug.LogWarning("[WebViewUnityCef] 未找到 WebBrowser 的 Texture/texture 属性，RawImage 将无法显示网页。");
        if (_loadUrlMethod == null)
        {
            if (enableDebugLog)
            {
                var methods = new System.Collections.Generic.List<string>();
                foreach (var m in browserType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                    if (m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string))
                        methods.Add(m.Name);
                Debug.LogWarning("[WebViewUnityCef] 未找到加载 URL 的方法。WebBrowser 中接受一个 string 的方法: " + string.Join(", ", methods.Count > 0 ? methods : new[] { "(无)" }));
            }
            else
                Debug.LogWarning("[WebViewUnityCef] 未找到 WebBrowser 的加载 URL 方法，请开启调试日志查看可用方法名。");
        }

        int w, h;
        GetRenderSize(out w, out h);
        Debug.LogWarning("[WebViewUnityCef] 渲染网页尺寸: " + w + " x " + h + "（若网页仍显示不全，可在 Inspector 勾选 Use Fixed Render Size 并设 1920x1080）");
        StartCoroutine(InitBrowserThenLoadUrl(w, h));
        // 若 RawImage 未赋纹理，先赋占位纹理，否则 enabled=true 时会报 UnassignedReferenceException
        if (targetRawImage.texture == null)
            targetRawImage.texture = GetOrCreatePlaceholderTexture();
        targetRawImage.enabled = true;
        targetRawImage.color = webViewDisplayColor;
        ApplyTextureFlip();
        _opened = true;
        _framesSinceOpen = 0;
        _loggedTextureOnce = false;
        _loggedTexturePropNull = false;
        if (enableDebugLog)
            Debug.Log("[WebViewUnityCef] WebBrowser 已创建并加载 URL，RawImage.color 已应用 webViewDisplayColor。");
    }

    System.Collections.IEnumerator InitBrowserThenLoadUrl(int width, int height)
    {
        for (int i = 0; i < 5; i++)
            yield return null;
        GetRenderSize(out width, out height);
        if (enableDebugLog)
            Debug.Log($"[WebViewUnityCef] 协程内使用渲染尺寸: {width} x {height}");
        var t = _webBrowserComponent != null ? _webBrowserComponent.GetType() : null;
        if (t != null)
        {
            try
            {
                var init = t.GetMethod("Init", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (init != null) init.Invoke(_webBrowserComponent, null);
            }
            catch (Exception e) { if (enableDebugLog) Debug.LogWarning("[WebViewUnityCef] Init: " + e.Message); }
        }
        // 必须先等 CEF 初始化完成再调用 SetSize/Navigate，否则 WebBrowser.Navigate 第 191 行 NullReferenceException
        for (int i = 0; i < 120; i++)
        {
            yield return null;
            if (_webBrowserComponent == null) yield break;
            var bt = _webBrowserComponent.GetType();
            var readyProp = bt.GetProperty("IsInitialized", BindingFlags.Public | BindingFlags.Instance) ?? bt.GetProperty("Initialized", BindingFlags.Public | BindingFlags.Instance);
            if (readyProp != null && readyProp.CanRead)
            {
                try
                {
                    if (true.Equals(readyProp.GetValue(_webBrowserComponent)))
                        break;
                }
                catch { /* ignore */ }
            }
        }
        // 初始化完成后再设置尺寸并加载 URL（UnityCef WebBrowser 无 SetSize，仅靠 Width/Height；内部 browserIpc 可能有 Resize）
        if (t != null && _webBrowserComponent != null)
        {
            try
            {
                int setW, setH;
                GetSizeForBrowser(width, height, out setW, out setH);
                SetWebBrowserSize(_webBrowserComponent, t, setW, setH);
                int readW, readH;
                GetWebBrowserSize(_webBrowserComponent, t, out readW, out readH);
                Debug.LogWarning("[WebViewUnityCef] 已设置 WebBrowser Width=" + readW + " Height=" + readH + "（期望 " + setW + "x" + setH + "）");
                var setSize = t.GetMethod("SetSize", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(int), typeof(int) }, null);
                if (setSize != null)
                {
                    _setSizeMethod = setSize;
                    setSize.Invoke(_webBrowserComponent, new object[] { setW, setH });
                }
                else
                {
                    // UnityCef 官方 WebBrowser 无 SetSize，尝试通过内部 browserIpc 调用 Resize
                    var browserIpcField = t.GetField("browserIpc", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (browserIpcField != null)
                    {
                        _browserIpcInstance = browserIpcField.GetValue(_webBrowserComponent);
                        if (_browserIpcInstance != null)
                        {
                            var ipcType = _browserIpcInstance.GetType();
                            _browserIpcResizeMethod = ipcType.GetMethod("Resize", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(int), typeof(int) }, null)
                                ?? ipcType.GetMethod("SetSize", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(int), typeof(int) }, null);
                            if (_browserIpcResizeMethod != null)
                            {
                                _browserIpcResizeMethod.Invoke(_browserIpcInstance, new object[] { setW, setH });
                                if (enableDebugLog) Debug.Log("[WebViewUnityCef] 已通过内部 browserIpc." + _browserIpcResizeMethod.Name + "(" + setW + "," + setH + ") 调整尺寸");
                            }
                        }
                    }
                }
                _lastRenderWidth = width;
                _lastRenderHeight = height;
            }
            catch (Exception e) { if (enableDebugLog) Debug.LogWarning("[WebViewUnityCef] SetSize/Resize: " + e.Message); }
        }
        for (int i = 0; i < 5; i++)
            yield return null;
        for (int retry = 0; retry < 3; retry++)
        {
            if (_webBrowserComponent != null && _loadUrlMethod != null)
                TryCallLoadUrl(_webBrowserComponent, _currentLoadUrl);
            if (!_loadUrlFailed) break;
            _loadUrlFailed = false;
            for (int i = 0; i < 30; i++) yield return null;
        }
    }

    void TryCallLoadUrl(Component browser, string url)
    {
        _loadUrlFailed = false;
        if (browser == null) return;
        var t = browser.GetType();
        MethodInfo method = _loadUrlMethod;
        object[] args = new object[] { url };
        if (method == null)
        {
            if (enableDebugLog) Debug.LogWarning("[WebViewUnityCef] LoadUrl 方法为空，未加载 URL。");
            return;
        }
        var ps = method.GetParameters();
        if (ps.Length == 3 && ps[0].ParameterType == typeof(int) && ps[1].ParameterType == typeof(int) && ps[2].ParameterType == typeof(string))
        {
            int w, h;
            GetRenderSize(out w, out h);
            int setW, setH;
            GetSizeForBrowser(w, h, out setW, out setH);
            _lastRenderWidth = w;
            _lastRenderHeight = h;
            args = new object[] { setW, setH, url };
        }
        try
        {
            method.Invoke(browser, args);
            if (enableDebugLog) Debug.Log($"[WebViewUnityCef] 已调用 LoadUrl: {url}");
        }
        catch (Exception e)
        {
            _loadUrlFailed = true;
            var inner = e.InnerException ?? e;
            Debug.LogWarning($"[WebViewUnityCef] LoadUrl 调用异常: {inner.Message}\n内部堆栈: {inner.StackTrace}");
            if (e != inner && enableDebugLog)
                Debug.LogWarning($"[WebViewUnityCef] 外层: {e.Message}");
        }
    }

    /// <summary>
    /// 关闭网页（隐藏 RawImage，不销毁浏览器）
    /// </summary>
    public void Close()
    {
        if (targetRawImage != null)
            targetRawImage.enabled = false;
    }

    private bool _loggedTextureOnce;
    private bool _loggedTexturePropNull;
    private bool _loggedTextureSizeOnce;
    private bool _loadUrlFailed;

    void LateUpdate()
    {
        if (!_opened || _webBrowserComponent == null || targetRawImage == null || !targetRawImage.enabled)
            return;
        if (_textureProp == null)
        {
            if (enableDebugLog && !_loggedTexturePropNull)
            {
                _loggedTexturePropNull = true;
                Debug.LogWarning("[WebViewUnityCef] Texture 属性为空，无法把网页赋给 RawImage。请确认 UnityCef 的 WebBrowser 有 Texture 或 texture 属性。");
            }
            return;
        }
        _framesSinceOpen++;
        try
        {
            var tex = _textureProp.GetValue(_webBrowserComponent) as Texture2D;
            if (enableDebugLog && _framesSinceOpen <= 15 && (_framesSinceOpen % 5 == 1 || tex != null))
            {
                if (tex != null)
                    Debug.Log($"[WebViewUnityCef] 帧{_framesSinceOpen} 纹理: {tex.name} {tex.width}x{tex.height} format={tex.format}");
                else if (!_loggedTextureOnce)
                {
                    _loggedTextureOnce = true;
                    Debug.Log($"[WebViewUnityCef] 帧{_framesSinceOpen} WebBrowser.Texture 为 null，可能尚未渲染。");
                }
            }
            if (tex != null && tex.width > 1 && tex.height > 1 && _framesSinceOpen > 5)
            {
                int curW, curH;
                GetRenderSize(out curW, out curH);
                // 首次拿到纹理时输出其尺寸及 WebBrowser 当前 Width/Height（便于排查“只渲染一小块”是否因尺寸或宽高反了）
                if (!_loggedTextureSizeOnce)
                {
                    _loggedTextureSizeOnce = true;
                    int wbW = 0, wbH = 0;
                    try { GetWebBrowserSize(_webBrowserComponent, _webBrowserComponent.GetType(), out wbW, out wbH); }
                    catch { }
                    Debug.LogWarning("[WebViewUnityCef] 浏览器纹理: " + tex.width + " x " + tex.height + " | WebBrowser.Width=" + wbW + " Height=" + wbH + "（若 Width/Height 与期望相反可勾选 Swap Width/Height）");
                }
                // 前约 2 秒内每帧调用 SetSize 或内部 Resize，强制 UnityCef 应用我们传入的视口尺寸
                bool sizeChanged = Mathf.Abs(curW - _lastRenderWidth) > 2 || Mathf.Abs(curH - _lastRenderHeight) > 2;
                if (sizeChanged || _framesSinceOpen <= 120)
                {
                    int setW, setH;
                    GetSizeForBrowser(curW, curH, out setW, out setH);
                    bool didResize = false;
                    if (_setSizeMethod != null && _webBrowserComponent != null)
                    {
                        try { _setSizeMethod.Invoke(_webBrowserComponent, new object[] { setW, setH }); didResize = true; }
                        catch (Exception ex) { if (enableDebugLog) Debug.LogWarning("[WebViewUnityCef] SetSize: " + ex.Message); }
                    }
                    if (!didResize && _browserIpcResizeMethod != null && _browserIpcInstance != null)
                    {
                        try { _browserIpcResizeMethod.Invoke(_browserIpcInstance, new object[] { setW, setH }); didResize = true; }
                        catch (Exception ex) { if (enableDebugLog) Debug.LogWarning("[WebViewUnityCef] browserIpc.Resize: " + ex.Message); }
                    }
                    if (didResize)
                    {
                        _lastRenderWidth = curW;
                        _lastRenderHeight = curH;
                        if (enableDebugLog && sizeChanged) Debug.Log($"[WebViewUnityCef] RawImage 尺寸变化，已更新渲染: {curW} x {curH}");
                    }
                }
                if (targetRawImage.texture != tex)
                {
                    targetRawImage.texture = tex;
                    targetRawImage.color = webViewDisplayColor;
                    try { tex.filterMode = FilterMode.Bilinear; } catch { }
                    if (enableDebugLog)
                        Debug.Log($"[WebViewUnityCef] 已把纹理赋给 RawImage。");
                }
                ApplyTextureFlip();
            }
        }
        catch (Exception e)
        {
            if (enableDebugLog) Debug.LogWarning($"[WebViewUnityCef] LateUpdate 取纹理异常: {e.Message}");
        }
    }

    void OnDestroy()
    {
        if (_browserGo != null)
            Destroy(_browserGo);
    }
}
