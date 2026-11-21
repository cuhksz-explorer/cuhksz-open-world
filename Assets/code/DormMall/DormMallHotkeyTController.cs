using System.Collections;
using GameControls;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace DormMall
{
    /// <summary>
    /// 全局热键：任意时候按 T 切换寝室商城面板（但登录界面不响应）。
    /// 建议配合使用现有的 DormMallUI（通常挂在 DormMallCanvas 根物体上）。
    /// </summary>
    public class DormMallHotkeyTController : MonoBehaviour
    {
        [Header("登录界面屏蔽")]
        [Tooltip("若场景里存在同名物体且 activeSelf==true，则不响应热键。")]
        [SerializeField] string loginPanelName = "GameServer_LoginPanel";

        [Header("切换行为")]
        [SerializeField] bool toggleWhenOpen = true;
         
        [Header("输入/光标")]
        [Tooltip("当光标正在输入框（TMP_InputField / InputField）时，不响应热键。")]
        [SerializeField] bool disableWhenTyping = true;

        [Tooltip("仅在「按 T 关闭商城」时锁定光标；不要在切场景时锁鼠标，否则会破坏 Loading/登录界面输入。")]
        [SerializeField] bool lockCursorWhenClosed = true;

        [Header("服务器商品目录")]
        [Tooltip("为 true 时在后台从 GameServer 刷新目录；打开商城不再等待网络（先显示本地/缓存，再自动刷新列表）。")]
        [SerializeField] bool loadCatalogFromGameServer = true;

        [Tooltip("进入场景且已配置 GameServer 时，后台预拉取商品目录，减轻第一次按 T 的等待。")]
        [SerializeField] bool prefetchCatalogOnSceneLoad = true;

        // 缓存你“最初配置好”的 database：用于在其它场景也能正确显示商品列表
        ProductDatabase _cachedDatabase;

        bool _catalogRefreshInFlight;

        static DormMallHotkeyTController _instance;

        /// <summary>全局单例（由 RuntimeInitialize 创建）；供 NPC 商人等调用 <see cref="TryOpenMallFromMerchant"/>。</summary>
        public static DormMallHotkeyTController Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (_instance != null)
                return;

            var go = new GameObject("[DormMallHotkeyTController]");
            _instance = go.AddComponent<DormMallHotkeyTController>();
            DontDestroyOnLoad(go);
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        /// <summary>
        /// NPC / 场景交互调用：仅打开寝室商城，与按 T 「打开」分支一致（拉目录、RebuildUI、鼠标）。
        /// 已处于营业界面时不会重复请求。
        /// </summary>
        public void TryOpenMallFromMerchant()
        {
            if (PlacementModeController.Instance != null && PlacementModeController.Instance.IsPlacementActive)
                return;
            if (IsLoginPanelVisible())
                return;

            var mall = FindPrimaryDormMall();
            if (mall == null)
                return;
            if (mall.IsMainShopPanelOpen())
                return;

            RequestOpenMallUi(mall);
        }

        void OnEnable()
        {
            GameServerSync.OnAuthSuccess += OnAuthSuccess;
        }

        void OnDisable()
        {
            GameServerSync.OnAuthSuccess -= OnAuthSuccess;
        }

        void Start()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyDormMallClosedForActiveScene();
            TryPrefetchCatalog();
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            GameServerSync.OnAuthSuccess -= OnAuthSuccess;
        }

        void OnAuthSuccess(string _, string __)
        {
            TryPrefetchCatalog();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 切场景后强制关闭：确保“默认不在场景显示，只有按 T 才出现”
            ApplyDormMallClosedForActiveScene();

            // 尝试缓存：只要场景里某个 DormMallUI 的 database 不为空，就认为它是“你最初配好的那套”
            if (_cachedDatabase == null)
            {
                var anyMall = FindPrimaryDormMall();
                if (anyMall != null && anyMall.database != null)
                    _cachedDatabase = anyMall.database;
            }

            if (prefetchCatalogOnSceneLoad)
                TryPrefetchCatalog();
        }

        void Update()
        {
            if (!GameInput.GetKeyDown(GameControlAction.OpenShop))
                return;

            if (GameplayUiHotkeyGuard.ShouldBlockDontDestroyOnLoadHotkeys())
                return;

            if (PlacementModeController.Instance != null && PlacementModeController.Instance.IsPlacementActive)
                return;

            if (IsLoginPanelVisible())
                return;

            if (disableWhenTyping && IsCurrentlyTyping())
                return;

            // 只在需要时才查找面板
            var mall = FindPrimaryDormMall();
            if (mall == null)
                return;

            bool isOpen = mall.gameObject.activeSelf;
            if (isOpen && toggleWhenOpen == false)
                return;

            if (isOpen)
            {
                mall.gameObject.SetActive(false);
                ApplyCursorClosed();
            }
            else
            {
                RequestOpenMallUi(mall);
            }
        }

        void RequestOpenMallUi(DormMallUI mall)
        {
            PrepareMallDatabaseForDisplay(mall);
            OpenMallLocal(mall);

            if (ShouldRefreshCatalogFromServer())
                StartCoroutine(RefreshCatalogInBackground(mall));
        }

        void PrepareMallDatabaseForDisplay(DormMallUI mall)
        {
            if (mall == null)
                return;

            if (DormMallRemoteCatalog.LastFetchOk && DormMallRemoteCatalog.CachedRuntimeDatabase != null)
                mall.database = DormMallRemoteCatalog.CachedRuntimeDatabase;
            else if (_cachedDatabase != null)
                mall.database = _cachedDatabase;
            else if (mall.database == null)
                mall.database = Resources.Load<ProductDatabase>("DormMall/ProductDatabase");

            if (mall.database != null)
                _cachedDatabase = mall.database;
        }

        bool ShouldRefreshCatalogFromServer()
        {
            return loadCatalogFromGameServer &&
                   GameServerSync.Instance != null &&
                   !string.IsNullOrEmpty(GameServerSync.Instance.BaseUrlTrimmed);
        }

        void TryPrefetchCatalog()
        {
            if (!prefetchCatalogOnSceneLoad || !ShouldRefreshCatalogFromServer())
                return;
            if (DormMallRemoteCatalog.LastFetchOk && DormMallRemoteCatalog.CachedRuntimeDatabase != null)
                return;
            StartCoroutine(PrefetchCatalogCoroutine());
        }

        IEnumerator PrefetchCatalogCoroutine()
        {
            if (_catalogRefreshInFlight)
                yield break;
            _catalogRefreshInFlight = true;
            try
            {
                string baseUrl = GameServerSync.Instance.BaseUrlTrimmed;
                yield return DormMallRemoteCatalog.FetchCoroutine(baseUrl, null, null);
                if (DormMallRemoteCatalog.LastFetchOk && DormMallRemoteCatalog.CachedRuntimeDatabase != null)
                    _cachedDatabase = DormMallRemoteCatalog.CachedRuntimeDatabase;
            }
            finally
            {
                _catalogRefreshInFlight = false;
            }
        }

        void OpenMallLocal(DormMallUI mall)
        {
            mall.gameObject.SetActive(true);
            mall.RebuildUI();
            ApplyCursorOpen();
        }

        IEnumerator RefreshCatalogInBackground(DormMallUI mall)
        {
            while (_catalogRefreshInFlight)
                yield return null;

            if (!DormMallRemoteCatalog.LastFetchOk || DormMallRemoteCatalog.CachedRuntimeDatabase == null)
            {
                _catalogRefreshInFlight = true;
                try
                {
                    string baseUrl = GameServerSync.Instance.BaseUrlTrimmed;
                    yield return DormMallRemoteCatalog.FetchCoroutine(baseUrl, null, null);
                }
                finally
                {
                    _catalogRefreshInFlight = false;
                }
            }

            ApplyRemoteCatalogToOpenMall(mall);
        }

        void ApplyRemoteCatalogToOpenMall(DormMallUI mall)
        {
            if (mall == null || !mall.gameObject.activeSelf)
                return;
            if (!DormMallRemoteCatalog.LastFetchOk || DormMallRemoteCatalog.CachedRuntimeDatabase == null)
                return;
            _cachedDatabase = DormMallRemoteCatalog.CachedRuntimeDatabase;
            mall.database = DormMallRemoteCatalog.CachedRuntimeDatabase;
            mall.RebuildUI();
        }

        void ApplyDormMallClosedForActiveScene()
        {
            var malls = FindObjectsOfType<DormMallUI>(true);
            DormMallUI cachedMall = null;
            for (int i = 0; i < malls.Length; i++)
            {
                var mall = malls[i];
                if (mall == null) continue;
                if (cachedMall == null && !mall.isFoodShopPanel)
                    cachedMall = mall;
                if (mall.gameObject.activeSelf)
                    mall.gameObject.SetActive(false);
            }

            // 注意：此处禁止调用 ApplyCursorClosed()。
            // 本方法在每次 sceneLoaded / Start 都会执行；若在登录、Loading 场景锁光标，
            // 会导致鼠标消失、无法点击输入框。锁光标只应在玩家主动按 T 关闭商城时进行。

            // 额外缓存一次：有些场景可能先通过别的方式初始化 DormMallUI
            if (_cachedDatabase == null && cachedMall != null && cachedMall.database != null)
                _cachedDatabase = cachedMall.database;
        }

        DormMallUI FindPrimaryDormMall()
        {
            var malls = FindObjectsOfType<DormMallUI>(true);
            DormMallUI first = null;
            for (int i = 0; i < malls.Length; i++)
            {
                var m = malls[i];
                if (m == null) continue;
                if (first == null) first = m;
                if (!m.isFoodShopPanel)
                    return m;
            }
            return first;
        }

        bool IsLoginPanelVisible()
        {
            var login = GameObject.Find(loginPanelName);
            return login != null && login.activeSelf;
        }

        bool IsCurrentlyTyping()
        {
            var es = EventSystem.current;
            if (es == null)
                return false;

            var selected = es.currentSelectedGameObject;
            if (selected == null)
                return false;

            // 检查是否选中了输入框
            if (selected.GetComponent<TMP_InputField>() != null) return true;
            if (selected.GetComponent<InputField>() != null) return true;

            // 有时选中的是子物体
            if (selected.GetComponentInParent<TMP_InputField>() != null) return true;
            if (selected.GetComponentInParent<InputField>() != null) return true;

            return false;
        }

        void ApplyCursorOpen()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void ApplyCursorClosed()
        {
            if (!lockCursorWhenClosed)
                return;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}

