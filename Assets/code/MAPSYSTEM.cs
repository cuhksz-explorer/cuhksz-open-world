using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Collections;
using System.Collections.Generic;
using Mirror;

public class MapSystem : MonoBehaviour
{
    [Header("???UI????")]
    public GameObject mapWindow; // ???????????????Inspector?????
    public RectTransform mapContainer; // ?????????????RectTransform?????????????
    public GameObject locationButtonPrefab; // ???????????????Button??Image?????
    public Sprite defaultLocationIcon; // ???????????????

    [Header("大地图玩家点位")]
    [Tooltip("在 M 键打开的大地图上显示玩家点位（自己+联机玩家）")]
    public bool showPlayersOnBigMap = true;
    [Tooltip("使用仿射映射：ui = a·worldX + b·worldZ + c（与地点图钉同一套 anchored 坐标系）。若关闭则退回按 worldMin/Max 铺满 mapContainer 的旧逻辑。")]
    public bool worldToMapUseAffineCalibration = true;
    [Tooltip("当 mapLocations 至少有 3 个条目时，用其 worldPosition 与 uiPosition 最小二乘拟合仿射系数（便于与地图图钉一致）；失败或未勾选时使用下方手写系数。")]
    public bool worldToMapAffineAutoFitFromLocations = true;
    [Tooltip("ux = x·worldX + y·worldZ + z。默认值为 td/sportsground/shaw/ai 四处校准点拟合结果。")]
    public Vector3 worldToMapAffineUx = new Vector3(-0.2373253f, 0.0038661f, -171.3824f);
    [Tooltip("uy = x·worldX + y·worldZ + z")]
    public Vector3 worldToMapAffineUy = new Vector3(-0.0029717f, -0.2839725f, 292.2616f);
    [Tooltip("世界坐标映射范围（X,Z）最小值（仅当未使用仿射校准时生效）")]
    public Vector2 worldMinXZ = new Vector2(-2000f, -2000f);
    [Tooltip("世界坐标映射范围（X,Z）最大值（仅当未使用仿射校准时生效）")]
    public Vector2 worldMaxXZ = new Vector2(2000f, 2000f);
    [Tooltip("若坐标范围未手动配置，可根据 mapLocations 自动估算（仅旧映射模式使用）")]
    public bool autoEstimateWorldBoundsFromLocations = true;
    public Color selfPlayerDotColor = new Color(1f, 0.25f, 0.25f, 0.95f);
    public Color remotePlayerDotColor = new Color(0.2f, 0.95f, 1f, 0.95f);
    [Tooltip("联机时大地图圆点使用房间色轮槽位颜色（与头顶名、小地图一致）；关闭则用上方固定颜色。")]
    public bool useCoopRoomPaletteForPlayerDots = true;
    [Range(4, 24)] public int playerDotSize = 10;

    [Header("????????UI")]
    public GameObject settingsUI; // ?????????????UI??????????????

    [Header("???????????")]
    public GameObject loadingScreen; // ???????????????????????????
    public Image loadingProgressBar; // ????????????????
    public float loadingDuration = 1.5f; // ????????????1-2??????

    [Header("???????????")]
    public VideoPlayer videoPlayer; // ???????????????????Inspector??????
    public RawImage videoRawImage; // ????????RawImage UI????????Inspector??????
    [Tooltip("?????????????????????????????��?????????")]
    public VideoClip[] teleportVideoClipVariants;
    [Tooltip("????????????????????????? VideoPlayer ????????? Clip")]
    public VideoClip teleportVideoClip;
    public float teleportationDuration = 2f; // ????????????????????????

    [Header("????????????")]
    public AudioClip teleportSound; // ???????????????????WAV/MP3??
    [Range(0.1f, 1f)]
    public float teleportSoundVolume = 0.8f; // ????????????
    private AudioSource teleportAudioSource; // ??????????????????????

    [Header("???????????")]
    public PlayerMovement playerMovement; // ??????????????Teleport??????
    public CameraFollow cameraFollow; // ????????????????????????????
    public List<MapLocation> mapLocations = new List<MapLocation>(); // ?????????????

    private List<Button> locationButtons = new List<Button>(); // ??????????????
    private RectTransform _playersDotsRoot;
    private Image _selfPlayerDot;
    private readonly Dictionary<uint, Image> _remotePlayerDots = new Dictionary<uint, Image>();
    private readonly HashSet<uint> _bigMapAliveRemoteIds = new HashSet<uint>();
    private readonly List<uint> _bigMapRemoteIdsToRemove = new List<uint>();
    private CoopNetworkPlayer[] _cachedBigMapCoopPlayers;
    private float _nextBigMapPlayerScanTime;
    private const float BigMapPlayerScanInterval = 0.5f;

    Coroutine _loadingTeleportRoutine;

    /// <summary>大地图打开时屏蔽行走视角、光标重锁等（供 GameplayUiHotkeyGuard / GameplayCursorLock 查询）。</summary>
    public static bool BlocksGameplayInput { get; private set; }

    bool _mapInputSessionActive;
    bool _gameplayCursorLockedBeforeMap;
    bool _playerDisableMouseLookBeforeMap;
    FirstPersonCamera _fpsCameraForMap;
    bool _fpsSuppressMouseLookBeforeMap;
    bool _fpsCursorLockedBeforeMap;
    WideViewCamera _wideCameraForMap;
    bool _wideEnableMouseRotationBeforeMap;

    public bool IsMapWindowOpen =>
        mapWindow != null && mapWindow.activeSelf;

    /// <summary>场景是否配置了可用的大地图（map 窗口存在且由本 MapSystem 管理）。</summary>
    public bool IsMapFeatureAvailable =>
        mapWindow != null && mapWindow.scene.IsValid() && mapWindow.scene == gameObject.scene;

    /// <summary>当前场景是否存在可用的 MapSystem（用于 M 键是否生效）。</summary>
    public static bool IsMapHotkeyAvailableInActiveScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return false;

        var all = Resources.FindObjectsOfTypeAll<MapSystem>();
        for (int i = 0; i < all.Length; i++)
        {
            var map = all[i];
            if (map == null || !map.isActiveAndEnabled)
                continue;
            if (map.gameObject.scene != scene)
                continue;
            if (map.IsMapFeatureAvailable)
                return true;
        }

        return false;
    }

    /// <summary>在当前场景解析第一个可用 MapSystem。</summary>
    public static MapSystem FindAvailableInScene(Scene scene)
    {
        if (!scene.IsValid())
            return null;

        var all = Resources.FindObjectsOfTypeAll<MapSystem>();
        MapSystem fallback = null;
        for (int i = 0; i < all.Length; i++)
        {
            var map = all[i];
            if (map == null || !map.isActiveAndEnabled || map.gameObject.scene != scene)
                continue;
            if (map.IsMapFeatureAvailable)
                return map;
            if (fallback == null)
                fallback = map;
        }

        return fallback;
    }

    private void Start()
    {
        // ?????UI??????????????????????????????UI??
        mapWindow?.SetActive(false);
        settingsUI?.SetActive(true);
        loadingScreen?.SetActive(false);

        // ????????????????
        InitTeleportAudioSource();

        // ?????????????????
        AutoFindCoreComponents();
        AutoResolveMapUiReferences();

        // ??????????????
        CreateLocationButtons();
        CreateOrRebuildPlayerDotsLayer();
        TryAutoEstimateWorldBounds();
        TryComputeAffineFromMapLocations();
    }

    /// <summary>
    /// ?????????????AudioSource???????????????????????
    /// </summary>
    private void InitTeleportAudioSource()
    {
        // ????????AudioSource???????
        teleportAudioSource = GetComponent<AudioSource>();
        if (teleportAudioSource == null)
        {
            teleportAudioSource = gameObject.AddComponent<AudioSource>();
        }

        // ????????????????????????????????
        teleportAudioSource.loop = false; // ?????????
        teleportAudioSource.volume = teleportSoundVolume;
        teleportAudioSource.spatialBlend = 0f; // 2D?????????????????
        teleportAudioSource.playOnAwake = false; // ?????????
        teleportAudioSource.mute = false; // ??????
    }

    /// <summary>
    /// ?????????????????????????????????????????
    /// </summary>
    private void AutoFindCoreComponents()
    {
        // ?????????????
        if (playerMovement == null)
        {
            playerMovement = FindObjectOfType<PlayerMovement>();
            if (playerMovement == null)
                Debug.LogError("MapSystem??????? PlayerMovement ?????????????????????????");
        }

        // ??????????????
        if (cameraFollow == null)
        {
            // ??????????????
            if (Camera.main != null)
                cameraFollow = Camera.main.GetComponent<CameraFollow>();

            // ??????????CameraFollow
            if (cameraFollow == null)
                cameraFollow = FindObjectOfType<CameraFollow>();

            if (cameraFollow == null)
                Debug.LogWarning("MapSystem??????? CameraFollow ???????????????????????????");
        }
    }

    void AutoResolveMapUiReferences()
    {
        if (mapWindow == null)
            mapWindow = FindMapWindowInLoadedScenes();

        if (mapContainer == null && mapWindow != null)
        {
            var imageRt = mapWindow.transform.Find("Image") as RectTransform;
            if (imageRt != null)
                mapContainer = imageRt;
            else
            {
                var rt = mapWindow.GetComponent<RectTransform>();
                if (rt != null)
                    mapContainer = rt;
            }
        }

        ResolveTeleportVideoReferences();
    }

    void ResolveTeleportVideoReferences()
    {
        var scene = gameObject.scene;

        if (loadingScreen == null)
            loadingScreen = FindUiRootInScene(scene, "transferpanel", "TransferPanel", "loadingScreen", "LoadingScreen");

        if (videoRawImage == null)
        {
            if (loadingScreen != null)
                videoRawImage = loadingScreen.GetComponentInChildren<RawImage>(true);

            if (videoRawImage == null)
                videoRawImage = FindTeleportRawImageInScene(scene);
        }

        if (videoPlayer == null)
        {
            if (videoRawImage != null)
                videoPlayer = videoRawImage.GetComponent<VideoPlayer>();
            if (videoPlayer == null && loadingScreen != null)
                videoPlayer = loadingScreen.GetComponentInChildren<VideoPlayer>(true);
            if (videoPlayer == null)
                videoPlayer = FindTeleportVideoPlayerInScene(scene);
        }

        if (loadingScreen == null && videoRawImage != null && videoRawImage.canvas != null)
            loadingScreen = videoRawImage.canvas.gameObject;
    }

    static GameObject FindUiRootInScene(Scene scene, params string[] names)
    {
        if (!scene.IsValid())
            return null;

        var all = Resources.FindObjectsOfTypeAll<RectTransform>();
        for (int i = 0; i < all.Length; i++)
        {
            var rt = all[i];
            if (rt == null || !rt.gameObject.scene.IsValid() || rt.gameObject.scene != scene)
                continue;

            for (int n = 0; n < names.Length; n++)
            {
                if (string.Equals(rt.name, names[n], System.StringComparison.OrdinalIgnoreCase))
                    return rt.gameObject;
            }
        }

        return null;
    }

    static RawImage FindTeleportRawImageInScene(Scene scene)
    {
        if (!scene.IsValid())
            return null;

        var players = Resources.FindObjectsOfTypeAll<VideoPlayer>();
        RawImage fallback = null;
        for (int i = 0; i < players.Length; i++)
        {
            var vp = players[i];
            if (vp == null || !vp.gameObject.scene.IsValid() || vp.gameObject.scene != scene)
                continue;
            if (vp.renderMode != VideoRenderMode.RenderTexture)
                continue;

            var raw = vp.GetComponent<RawImage>();
            if (raw == null)
                continue;

            if (vp.clip != null || raw.texture != null)
                return raw;

            if (fallback == null)
                fallback = raw;
        }

        return fallback;
    }

    static VideoPlayer FindTeleportVideoPlayerInScene(Scene scene)
    {
        var raw = FindTeleportRawImageInScene(scene);
        return raw != null ? raw.GetComponent<VideoPlayer>() : null;
    }

    GameObject FindMapWindowInLoadedScenes()
    {
        var scene = gameObject.scene;
        var all = Resources.FindObjectsOfTypeAll<RectTransform>();
        GameObject fallback = null;
        for (int i = 0; i < all.Length; i++)
        {
            var rt = all[i];
            if (rt == null || !rt.gameObject.scene.IsValid() || rt.gameObject.scene != scene)
                continue;
            if (!string.Equals(rt.name, "map", System.StringComparison.OrdinalIgnoreCase))
                continue;

            if (rt.Find("Image") != null)
                return rt.gameObject;

            if (fallback == null)
                fallback = rt.gameObject;
        }

        return fallback;
    }

    /// <summary>
    /// ?????????????????????????????mapLocations??????????
    /// </summary>
    private void CreateLocationButtons()
    {
        // ????????????????
        foreach (var btn in locationButtons)
        {
            if (btn != null)
                Destroy(btn.gameObject);
        }
        locationButtons.Clear();

        // ???????????????????????
        if (mapContainer == null)
        {
            Debug.LogError("MapSystem???????? mapContainer?????????????");
            return;
        }
        if (locationButtonPrefab == null)
        {
            Debug.LogError("MapSystem???????? locationButtonPrefab???????????????");
            return;
        }

        // ?????????????????????
        foreach (var location in mapLocations)
        {
            GameObject btnObj = Instantiate(locationButtonPrefab, mapContainer);
            btnObj.SetActive(true);
            RectTransform btnRect = btnObj.GetComponent<RectTransform>();
            Button btn = btnObj.GetComponent<Button>();
            Image btnImg = btnObj.GetComponent<Image>();

            // ??????????????????????UI????????
            if (btnRect != null)
                btnRect.anchoredPosition = location.uiPosition;

            // ??????????????????????????????????
            if (btnImg != null)
                btnImg.sprite = location.locationIcon ?? defaultLocationIcon;

            // ??????????????????????
            if (btn != null)
            {
                int locationIndex = mapLocations.IndexOf(location);
                btn.onClick.AddListener(() => OnLocationButtonClick(locationIndex));
                locationButtons.Add(btn);
            }
            else
            {
                Debug.LogWarning($"MapSystem??????????????? Button ???????????????????");
                Destroy(btnObj);
            }
        }
    }

    private void Update()
    {
        if (!showPlayersOnBigMap)
            return;
        if (mapWindow == null || !mapWindow.activeSelf)
        {
            SetPlayersDotsVisible(false);
            return;
        }

        if (_playersDotsRoot == null)
            CreateOrRebuildPlayerDotsLayer();
        UpdateBigMapPlayerDots();
    }

    void CreateOrRebuildPlayerDotsLayer()
    {
        if (mapContainer == null)
            return;

        if (_playersDotsRoot == null)
        {
            var go = new GameObject("BigMapPlayersDots", typeof(RectTransform));
            go.transform.SetParent(mapContainer, false);
            _playersDotsRoot = go.GetComponent<RectTransform>();
            _playersDotsRoot.anchorMin = Vector2.zero;
            _playersDotsRoot.anchorMax = Vector2.one;
            _playersDotsRoot.offsetMin = Vector2.zero;
            _playersDotsRoot.offsetMax = Vector2.zero;
            _playersDotsRoot.SetAsLastSibling();
        }

        if (_selfPlayerDot == null)
        {
            var existing = _playersDotsRoot.Find("SelfPlayerDot");
            if (existing != null)
                _selfPlayerDot = existing.GetComponent<Image>();
        }

        if (_selfPlayerDot == null)
            _selfPlayerDot = CreatePlayerDot("SelfPlayerDot", selfPlayerDotColor);
    }

    Image CreatePlayerDot(string name, Color color)
    {
        if (_playersDotsRoot == null)
            return null;
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(_playersDotsRoot, false);
        var img = go.GetComponent<Image>();
        img.sprite = CreateCircleSprite(Mathf.Max(4, playerDotSize), Color.white);
        img.color = color;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(playerDotSize, playerDotSize);
        return img;
    }

    void TryAutoEstimateWorldBounds()
    {
        if (!autoEstimateWorldBoundsFromLocations || mapLocations == null || mapLocations.Count < 2)
            return;
        if (Mathf.Abs(worldMaxXZ.x - worldMinXZ.x) > 1f && Mathf.Abs(worldMaxXZ.y - worldMinXZ.y) > 1f)
            return;

        float minX = float.MaxValue, minZ = float.MaxValue;
        float maxX = float.MinValue, maxZ = float.MinValue;
        for (int i = 0; i < mapLocations.Count; i++)
        {
            var p = mapLocations[i].worldPosition;
            minX = Mathf.Min(minX, p.x);
            minZ = Mathf.Min(minZ, p.z);
            maxX = Mathf.Max(maxX, p.x);
            maxZ = Mathf.Max(maxZ, p.z);
        }
        if (maxX - minX < 1f || maxZ - minZ < 1f)
            return;
        float padX = Mathf.Max(20f, (maxX - minX) * 0.08f);
        float padZ = Mathf.Max(20f, (maxZ - minZ) * 0.08f);
        worldMinXZ = new Vector2(minX - padX, minZ - padZ);
        worldMaxXZ = new Vector2(maxX + padX, maxZ + padZ);
    }

    /// <summary>
    /// 用 mapLocations 的 (worldX, worldZ) 与 uiPosition 拟合仿射系数，使玩家点与地图图钉一致。
    /// </summary>
    void TryComputeAffineFromMapLocations()
    {
        if (!worldToMapAffineAutoFitFromLocations || mapLocations == null || mapLocations.Count < 3)
            return;

        if (!TryLeastSquaresAffine2D(mapLocations, out var ux, out var uy))
            return;

        worldToMapAffineUx = ux;
        worldToMapAffineUy = uy;
    }

    static bool TryLeastSquaresAffine2D(List<MapLocation> locs, out Vector3 coefUx, out Vector3 coefUy)
    {
        coefUx = coefUy = default;
        int n = locs.Count;
        float[,] ata = new float[3, 3];
        float[] atUx = new float[3];
        float[] atUy = new float[3];

        for (int i = 0; i < n; i++)
        {
            float wx = locs[i].worldPosition.x;
            float wz = locs[i].worldPosition.z;
            float r0 = wx, r1 = wz, r2 = 1f;
            float[] row = { r0, r1, r2 };
            for (int a = 0; a < 3; a++)
            for (int b = 0; b < 3; b++)
                ata[a, b] += row[a] * row[b];
            Vector2 ui = locs[i].uiPosition;
            atUx[0] += r0 * ui.x;
            atUx[1] += r1 * ui.x;
            atUx[2] += r2 * ui.x;
            atUy[0] += r0 * ui.y;
            atUy[1] += r1 * ui.y;
            atUy[2] += r2 * ui.y;
        }

        if (!Solve3x3GaussJordan(ata, atUx, out float ax, out float bx, out float cx))
            return false;
        if (!Solve3x3GaussJordan(ata, atUy, out float dy, out float ey, out float fy))
            return false;

        coefUx = new Vector3(ax, bx, cx);
        coefUy = new Vector3(dy, ey, fy);
        return true;
    }

    /// <summary>解 3x3 线性方程组（高斯—若尔当，部分主元）；不修改系数矩阵 a。</summary>
    static bool Solve3x3GaussJordan(float[,] a, float[] b, out float x0, out float x1, out float x2)
    {
        x0 = x1 = x2 = 0f;
        float[,] aug = new float[3, 4];
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
                aug[i, j] = a[i, j];
            aug[i, 3] = b[i];
        }

        for (int col = 0; col < 3; col++)
        {
            int piv = col;
            float best = Mathf.Abs(aug[col, col]);
            for (int r = col + 1; r < 3; r++)
            {
                float v = Mathf.Abs(aug[r, col]);
                if (v > best)
                {
                    best = v;
                    piv = r;
                }
            }
            if (best < 1e-20f)
                return false;
            if (piv != col)
            {
                for (int j = 0; j < 4; j++)
                {
                    float t = aug[col, j];
                    aug[col, j] = aug[piv, j];
                    aug[piv, j] = t;
                }
            }
            float div = aug[col, col];
            for (int j = col; j < 4; j++)
                aug[col, j] /= div;
            for (int r = 0; r < 3; r++)
            {
                if (r == col)
                    continue;
                float f = aug[r, col];
                if (Mathf.Abs(f) < 1e-30f)
                    continue;
                for (int j = col; j < 4; j++)
                    aug[r, j] -= f * aug[col, j];
            }
        }

        x0 = aug[0, 3];
        x1 = aug[1, 3];
        x2 = aug[2, 3];
        return true;
    }

    void UpdateBigMapPlayerDots()
    {
        if (_playersDotsRoot == null || mapContainer == null)
            return;

        Color selfTint = selfPlayerDotColor;
        if (useCoopRoomPaletteForPlayerDots && NetworkClient.active && NetworkClient.localPlayer != null)
        {
            var localCoop = NetworkClient.localPlayer.GetComponent<CoopNetworkPlayer>();
            if (localCoop != null)
            {
                Color c = localCoop.GetRoomIdentityColor();
                selfTint = new Color(c.r, c.g, c.b, selfPlayerDotColor.a);
            }
        }
        if (_selfPlayerDot != null)
            _selfPlayerDot.color = selfTint;

        if (_selfPlayerDot != null && playerMovement != null && TryWorldToMap(playerMovement.transform.position, out var selfPos))
        {
            _selfPlayerDot.rectTransform.anchoredPosition = selfPos;
            _selfPlayerDot.gameObject.SetActive(true);
        }
        else if (_selfPlayerDot != null)
            _selfPlayerDot.gameObject.SetActive(false);

        if (_cachedBigMapCoopPlayers == null || Time.unscaledTime >= _nextBigMapPlayerScanTime)
        {
            _cachedBigMapCoopPlayers = FindObjectsOfType<CoopNetworkPlayer>(true);
            _nextBigMapPlayerScanTime = Time.unscaledTime + BigMapPlayerScanInterval;
        }

        var all = _cachedBigMapCoopPlayers;
        _bigMapAliveRemoteIds.Clear();
        for (int i = 0; i < all.Length; i++)
        {
            var p = all[i];
            if (p == null || p.isLocalPlayer)
                continue;
            if (p.netIdentity == null)
                continue;
            uint key = p.netIdentity.netId;
            _bigMapAliveRemoteIds.Add(key);

            Color remoteTint = remotePlayerDotColor;
            if (useCoopRoomPaletteForPlayerDots)
            {
                Color c = p.GetRoomIdentityColor();
                remoteTint = new Color(c.r, c.g, c.b, remotePlayerDotColor.a);
            }

            if (!_remotePlayerDots.TryGetValue(key, out var dot) || dot == null)
            {
                dot = CreatePlayerDot("RemotePlayerDot_" + key, remoteTint);
                _remotePlayerDots[key] = dot;
            }
            else if (dot != null)
                dot.color = remoteTint;

            if (dot != null && TryWorldToMap(p.transform.position, out var pos))
            {
                if (_selfPlayerDot != null && _selfPlayerDot.gameObject.activeSelf
                    && Vector2.Distance(pos, _selfPlayerDot.rectTransform.anchoredPosition) < playerDotSize * 1.5f)
                {
                    dot.gameObject.SetActive(false);
                    continue;
                }

                dot.rectTransform.anchoredPosition = pos;
                dot.gameObject.SetActive(true);
            }
            else if (dot != null)
                dot.gameObject.SetActive(false);
        }

        _bigMapRemoteIdsToRemove.Clear();
        foreach (var key in _remotePlayerDots.Keys)
            _bigMapRemoteIdsToRemove.Add(key);
        for (int i = 0; i < _bigMapRemoteIdsToRemove.Count; i++)
        {
            uint id = _bigMapRemoteIdsToRemove[i];
            if (_bigMapAliveRemoteIds.Contains(id))
                continue;
            if (_remotePlayerDots[id] != null)
                Destroy(_remotePlayerDots[id].gameObject);
            _remotePlayerDots.Remove(id);
        }
    }

    bool TryWorldToMap(Vector3 world, out Vector2 anchored)
    {
        anchored = Vector2.zero;
        if (mapContainer == null)
            return false;

        Rect r = mapContainer.rect;
        float halfW = r.width * 0.5f;
        float halfH = r.height * 0.5f;

        if (worldToMapUseAffineCalibration)
        {
            float wx = world.x;
            float wz = world.z;
            float x = worldToMapAffineUx.x * wx + worldToMapAffineUx.y * wz + worldToMapAffineUx.z;
            float y = worldToMapAffineUy.x * wx + worldToMapAffineUy.y * wz + worldToMapAffineUy.z;
            anchored = new Vector2(Mathf.Clamp(x, -halfW, halfW), Mathf.Clamp(y, -halfH, halfH));
            return true;
        }

        float width = worldMaxXZ.x - worldMinXZ.x;
        float height = worldMaxXZ.y - worldMinXZ.y;
        if (width < 1f || height < 1f)
            return false;

        float tx = Mathf.InverseLerp(worldMinXZ.x, worldMaxXZ.x, world.x);
        float ty = Mathf.InverseLerp(worldMinXZ.y, worldMaxXZ.y, world.z);
        float lx = (tx - 0.5f) * r.width;
        float ly = (ty - 0.5f) * r.height;
        anchored = new Vector2(Mathf.Clamp(lx, -halfW, halfW), Mathf.Clamp(ly, -halfH, halfH));
        return true;
    }

    void SetPlayersDotsVisible(bool visible)
    {
        if (_selfPlayerDot != null)
            _selfPlayerDot.gameObject.SetActive(visible);
        foreach (var kv in _remotePlayerDots)
        {
            if (kv.Value != null)
                kv.Value.gameObject.SetActive(visible);
        }
    }

    static Sprite CreateCircleSprite(int size, Color color)
    {
        int s = Mathf.Max(4, size);
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        var arr = new Color[s * s];
        Vector2 center = new Vector2((s - 1) * 0.5f, (s - 1) * 0.5f);
        float radius = s * 0.5f - 0.5f;
        float r2 = radius * radius;
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float dx = x - center.x;
            float dy = y - center.y;
            arr[y * s + x] = (dx * dx + dy * dy) <= r2 ? color : Color.clear;
        }
        tex.SetPixels(arr);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
    }

    /// <summary>
    /// ???????????????????????????
    /// </summary>
    private void OnLocationButtonClick(int locationIndex)
    {
        // ??????????????
        if (locationIndex < 0 || locationIndex >= mapLocations.Count)
        {
            Debug.LogError("MapSystem??????????????????");
            return;
        }

        // ??????????????????????
        if (playerMovement == null)
        {
            Debug.LogError("MapSystem??????? PlayerMovement ?????????????");
            return;
        }

        // ?????????????????Time.timeScale???
        PlayTeleportSound();

        // ?????????????????????????????+????????
        if (loadingScreen == null)
        {
            ExecuteTeleport(locationIndex);
            // ????????????????
            ResetPlayerStepSound();
            HideMap(); // ???????????
            return;
        }

        // ???????????+???????????
        if (_loadingTeleportRoutine != null)
            StopCoroutine(_loadingTeleportRoutine);
        _loadingTeleportRoutine = StartCoroutine(LoadingAndTeleportCoroutine(locationIndex));
    }

    /// <summary>
    /// 玩家死亡时由 <see cref="PlayerMovement"/> 调用：中止未完成的地图传送加载，
    /// 否则协程可能在下一个 yield 之前继续执行，最终在复活后弹出 settings UI。
    /// </summary>
    public void CancelOngoingTeleportLoading()
    {
        if (_loadingTeleportRoutine == null)
            return;

        StopCoroutine(_loadingTeleportRoutine);
        _loadingTeleportRoutine = null;
        RestoreUiAfterTeleportCancelled();
    }

    void RestoreUiAfterTeleportCancelled()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        if (loadingScreen != null)
            loadingScreen.SetActive(false);
        if (videoRawImage != null)
            videoRawImage.gameObject.SetActive(false);
        if (videoPlayer != null)
            videoPlayer.Stop();

        HideMap();
        settingsUI?.SetActive(false);
    }

    /// <summary>
    /// ?????????????????????PlayOneShot??????Time.timeScale??????
    /// </summary>
    private void PlayTeleportSound()
    {
        if (teleportSound == null)
        {
            Debug.LogWarning("MapSystem????????????????????");
            return;
        }

        if (teleportAudioSource == null)
        {
            InitTeleportAudioSource(); // ????????????????????
        }

        // PlayOneShot??????Time.timeScale????????????????????
        teleportAudioSource.PlayOneShot(teleportSound, teleportSoundVolume);
        Debug.Log("MapSystem?????????????");
    }

    /// <summary>
    /// ???????+????????????????????????????????????????????
    /// </summary>
    private IEnumerator LoadingAndTeleportCoroutine(int locationIndex)
    {
        try
        {
        // 1. 全屏 loading，再显示
        ResolveTeleportVideoReferences();
        TeleportLoadingUiLayout.ApplyFullscreen(loadingScreen, videoRawImage, loadingProgressBar);
        if (loadingScreen != null)
            loadingScreen.SetActive(true);
        else if (videoRawImage != null)
            videoRawImage.gameObject.SetActive(true);
        HideMap();
        settingsUI.SetActive(false);

        // 2. ������Ƶ����汾�����
        if (videoPlayer != null && videoRawImage != null)
        {
            VideoClip fallback = teleportVideoClip != null ? teleportVideoClip : videoPlayer.clip;
            VideoClip clipToPlay = LoadingVideoRandomUtil.PickRandomClip(teleportVideoClipVariants, fallback);
            if (clipToPlay == null)
            {
                Debug.LogWarning("MapSystem: no teleport load video configured (variants / fallback / VideoPlayer.clip).");
            }
            else
            {
                videoPlayer.clip = clipToPlay;
                videoRawImage.gameObject.SetActive(true);
                TeleportLoadingUiLayout.ApplyVideoCoverUv(videoRawImage, (int)clipToPlay.width, (int)clipToPlay.height);
                videoPlayer.Play();
            }

            float elapsedTime = 0f;
            if (clipToPlay != null)
            {
                while (elapsedTime < teleportationDuration && videoPlayer.isPlaying)
                {
                    if (playerMovement != null && playerMovement.IsDeathReviveFlowActive)
                        yield break;
                    elapsedTime += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (!videoPlayer.isPlaying)
                    elapsedTime = teleportationDuration;
            }
        }

        if (playerMovement != null && playerMovement.IsDeathReviveFlowActive)
            yield break;

        // 3. ????????????????????????????
        playerMovement.isMovementPaused = true;
        Time.timeScale = 0; // ??????????????????????
        Time.fixedDeltaTime = 0.02f * Time.timeScale; // ??????????????

        // 4. ???????s??????????????+???????????????????????????
        ExecuteTeleport(locationIndex);
        // Die() 会 StopCoroutine，但当前栈仍会同步执行到此处之后；必须立即退出，否则会再次 timeScale=0 并最终打开 settingsUI。
        if (playerMovement != null && playerMovement.IsDeathReviveFlowActive)
            yield break;

        // 5. ??????????????????????????????????
        if (loadingProgressBar != null)
        {
            // ??????????????0??1??????
            float progress = 0f;
            while (progress < 1f)
            {
                if (playerMovement != null && playerMovement.IsDeathReviveFlowActive)
                    yield break;
                progress += Time.unscaledDeltaTime / teleportationDuration;
                loadingProgressBar.fillAmount = Mathf.Clamp01(progress);
                yield return null;
            }
            loadingProgressBar.fillAmount = 1f; // ?????????????
        }
        else
        {
            // ?????????????????????
            float waited = 0f;
            while (waited < teleportationDuration)
            {
                if (playerMovement != null && playerMovement.IsDeathReviveFlowActive)
                    yield break;
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        if (playerMovement != null && playerMovement.IsDeathReviveFlowActive)
            yield break;

        // 6. ????????????UI????????????????????
        loadingScreen.SetActive(false);
        settingsUI.SetActive(true);

        // ??????????????????????????????????
        playerMovement.isMovementPaused = false;
        Time.timeScale = 1;
        Time.fixedDeltaTime = 0.02f * Time.timeScale; // ??????????????

        // ???????????
        if (videoRawImage != null)
        {
            videoRawImage.gameObject.SetActive(false);
        }

        // ??????????????????????????????
        ResetPlayerStepSound();
        }
        finally
        {
            _loadingTeleportRoutine = null;
        }
    }

    /// <summary>
    /// ????????????????????+???????????
    /// </summary>
    private void ExecuteTeleport(int locationIndex)
    {
        MapLocation targetLocation = mapLocations[locationIndex];

        playerMovement.Teleport(targetLocation.worldPosition);
        Debug.Log($"MapSystem????????? {targetLocation.locationName}??????{targetLocation.worldPosition}??");

        // 第一人称由 MapTeleportCameraSnap 保持 CameraHolder 相对玩家的局部位姿；勿再改 Camera.main 世界坐标。
        if (!MapTeleportCameraSnap.ShouldUseLegacyFollowCameraSnap(playerMovement))
            return;

        if (cameraFollow != null)
        {
            cameraFollow.SetTarget(playerMovement.transform);

            Vector3 targetCameraPos = playerMovement.transform.position + cameraFollow.followOffset;

            if (cameraFollow.lockXAxis) targetCameraPos.x = cameraFollow.transform.position.x;
            if (cameraFollow.lockYAxis) targetCameraPos.y = cameraFollow.transform.position.y;
            if (cameraFollow.lockZAxis) targetCameraPos.z = cameraFollow.transform.position.z;
            if (cameraFollow.enableRangeLimit)
            {
                targetCameraPos.x = Mathf.Clamp(targetCameraPos.x, cameraFollow.minX, cameraFollow.maxX);
                targetCameraPos.y = Mathf.Clamp(targetCameraPos.y, cameraFollow.minY, cameraFollow.maxY);
                targetCameraPos.z = Mathf.Clamp(targetCameraPos.z, cameraFollow.minZ, cameraFollow.maxZ);
            }

            cameraFollow.transform.position = targetCameraPos;
        }
        else if (Camera.main != null && Camera.main.GetComponent<FirstPersonCamera>() == null)
        {
            Camera.main.transform.position = playerMovement.transform.position + new Vector3(0, 5, -10);
        }
    }

    /// <summary>
    /// ??????????????????????????????
    /// </summary>
    private void ResetPlayerStepSound()
    {
        if (playerMovement.TryGetComponent<PlayerStepSound>(out var stepSound))
        {
            stepSound.ResetStepSoundState();
            Debug.Log("MapSystem???????????????????????????");
        }
        else
        {
            Debug.LogWarning("MapSystem???????????????PlayerStepSound??????????????????");
        }
    }

    void EnterMapInputSession()
    {
        if (_mapInputSessionActive)
            return;
        _mapInputSessionActive = true;
        BlocksGameplayInput = true;

        AutoFindCoreComponents();

        _gameplayCursorLockedBeforeMap = GameplayCursorLock.IsLocked;
        GameplayCursorLock.SetLocked(false);

        if (playerMovement != null)
        {
            _playerDisableMouseLookBeforeMap = playerMovement.disableMouseLook;
            playerMovement.disableMouseLook = true;
        }

        _fpsCameraForMap = playerMovement != null && playerMovement.externalFirstPersonLook != null
            ? playerMovement.externalFirstPersonLook
            : FindObjectOfType<FirstPersonCamera>(true);
        if (_fpsCameraForMap != null)
        {
            _fpsSuppressMouseLookBeforeMap = _fpsCameraForMap.suppressMouseLook;
            _fpsCursorLockedBeforeMap = _fpsCameraForMap.IsCursorLocked;
            _fpsCameraForMap.suppressMouseLook = true;
            _fpsCameraForMap.SetCursorLocked(false);
        }

        _wideCameraForMap = FindObjectOfType<WideViewCamera>(true);
        if (_wideCameraForMap != null)
        {
            _wideEnableMouseRotationBeforeMap = _wideCameraForMap.enableMouseRotation;
            _wideCameraForMap.enableMouseRotation = false;
        }
    }

    void ExitMapInputSession()
    {
        if (!_mapInputSessionActive)
            return;
        _mapInputSessionActive = false;
        BlocksGameplayInput = false;

        if (_fpsCameraForMap != null)
        {
            _fpsCameraForMap.suppressMouseLook = _fpsSuppressMouseLookBeforeMap;
            _fpsCameraForMap.SetCursorLocked(_fpsCursorLockedBeforeMap);
            _fpsCameraForMap = null;
        }

        if (_wideCameraForMap != null)
        {
            _wideCameraForMap.enableMouseRotation = _wideEnableMouseRotationBeforeMap;
            _wideCameraForMap = null;
        }

        if (playerMovement != null)
            playerMovement.disableMouseLook = _playerDisableMouseLookBeforeMap;

        GameplayCursorLock.SetLocked(_gameplayCursorLockedBeforeMap);
    }

    /// <summary>
    /// ??????????????????????????????
    /// </summary>
    public void ShowMap()
    {
        AutoResolveMapUiReferences();
        if (!IsMapFeatureAvailable)
            return;

        GameplayUiHotkeyGuard.CloseOtherExclusivePanels(GameplayUiHotkeyGuard.ExclusivePanelKind.Map);
        mapWindow.SetActive(true);
        settingsUI?.SetActive(false);
        EnterMapInputSession();
        CreateOrRebuildPlayerDotsLayer();
        TryComputeAffineFromMapLocations();
        if (showPlayersOnBigMap)
            UpdateBigMapPlayerDots();
    }

    /// <summary>
    /// ???????????????????????????????
    /// </summary>
    public void HideMap()
    {
        mapWindow?.SetActive(false);
        settingsUI?.SetActive(true);
        SetPlayersDotsVisible(false);
        ExitMapInputSession();

        if (playerMovement != null && playerMovement.isMovementPaused)
        {
            playerMovement.isMovementPaused = false;
            Time.timeScale = 1f;
        }
    }

    /// <summary>
    /// ??????????????????????????
    /// </summary>
    public void AddMapLocation(string locationName, Vector3 worldPos, Vector2 uiPos, Sprite icon = null)
    {
        mapLocations.Add(new MapLocation
        {
            locationName = locationName,
            worldPosition = worldPos,
            uiPosition = uiPos,
            locationIcon = icon
        });
        CreateLocationButtons(); // ??????????????????
        TryAutoEstimateWorldBounds();
        TryComputeAffineFromMapLocations();
    }
}

/// <summary>
/// ????????????????????Inspector??????????
/// </summary>
[System.Serializable]
public class MapLocation
{
    public string locationName; // ?????????????????????
    public Vector3 worldPosition; // ???????????????
    public Vector2 uiPosition; // ???UI??????????????????
    public Sprite locationIcon; // ??????????????????
}