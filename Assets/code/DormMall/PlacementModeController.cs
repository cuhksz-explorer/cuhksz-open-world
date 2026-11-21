using GameQuestSystem;
using Mirror;
using UnityEngine;

namespace DormMall
{
    /// <summary>
    /// 基于第一人称视角的放置控制器：
    /// 进入放置模式后继续使用当前视角旋转，物体预览始终跟随屏幕中心射线。
    /// </summary>
    public class PlacementModeController : MonoBehaviour
    {
        public static PlacementModeController Instance { get; private set; }

        /// <summary>是否处于放置模式（用于屏蔽其它热键等）。</summary>
        public bool IsPlacementActive => _active;

        [Header("视角")]
        [Tooltip("优先使用你的 FirstPersonCamera；为空时自动查找")]
        public global::FirstPersonCamera firstPersonCamera;
        [Tooltip("放置时使用的相机；为空时优先取 FirstPersonCamera 上的 Camera")]
        public Camera placementCamera;
        [Tooltip("进入放置模式时是否自动启用 FirstPersonCamera")]
        public bool autoActivateFirstPersonCamera = true;
        [Tooltip("进入放置模式时是否禁用其他相机")]
        public bool disableOtherCamerasOnPlace = true;

        [Header("放置参数")]
        [Tooltip("额外缩放倍率（归一化关闭时：= 最终相对预制体的倍率；归一化开启时：在归一化到目标最长边之后再乘该倍率）")]
        public float placementScale = 1f;
        [Tooltip("按模型渲染/碰撞包围盒统一尺寸：将最长边缩放到「目标最长边」，避免 GLB 导入后大小不一")]
        public bool normalizePlacedScaleByBounds = true;
        [Tooltip("归一化后包围盒最长边长度（世界单位，约等于米）")]
        public float placedTargetMaxExtent = 2f;
        [Tooltip("射线检测层")]
        public LayerMask placementRaycastLayers = -1;
        [Tooltip("重叠检测层，建议排除 Ignore Raycast")]
        public LayerMask overlapCheckLayers = ~(1 << 2);
        [Tooltip("预览物所在层")]
        public int ghostLayer = 2;
        [Tooltip("贴合表面的偏移，避免初始穿插")]
        public float surfacePadding = 0.01f;
        [Tooltip("从地面放置时，物体朝向跟随当前视角的 Y 旋转")]
        public bool alignYawToCameraOnFloor = true;
        [Tooltip("命中法线与世界 Up 的点积大于该值时，视为地面/桌面")]
        [Range(-1f, 1f)]
        public float floorUpDotThreshold = 0.6f;
        [Tooltip("是否让薄片类物体自动贴合命中的墙面或地面")]
        public bool autoFitFlatItemsToSurface = true;
        [Tooltip("当最薄轴 / 最厚轴 小于该值时，视为薄片类物体（海报、挂画、地毯等）")]
        [Range(0.01f, 1f)]
        public float flatItemThicknessRatioThreshold = 0.2f;

        [Header("放置物碰撞")]
        [Tooltip("为每个 MeshFilter 确保有 MeshCollider（凸包），再开启物理；无网格时仍回退 BoxCollider")]
        public bool ensureMeshCollidersFromMeshes = true;

        [Header("放置后物理")]
        [Tooltip("放在有效表面上后默认固定，不立即下落")]
        public bool keepPlacedObjectStatic = true;
        [Tooltip("当未稳定落在朝上表面时，才启用重力下落")]
        public bool enableGravityWhenUnsupported = true;
        [Tooltip("检测是否悬空的距离")]
        public float supportCheckDistance = 0.15f;

        [Header("预览颜色")]
        public Color validTint = new Color(0.2f, 1f, 0.2f, 0.6f);
        public Color invalidTint = new Color(1f, 0.2f, 0.2f, 0.6f);

        [Header("操作")]
        [Tooltip("确认放置键，默认右键")]
        public KeyCode confirmKey = KeyCode.Mouse1;
        [Tooltip("取消放置键")]
        public KeyCode cancelKey = KeyCode.Escape;

        [Header("放置 · 旋转微调")]
        [Tooltip("R：世界竖直轴左右旋转；再按一次 R 关闭")]
        public KeyCode rotationTuneYawKey = KeyCode.R;
        [Tooltip("E：绕相机右轴上下旋转；再按一次 E 关闭")]
        public KeyCode rotationTunePitchKey = KeyCode.E;
        [Tooltip("T：绕相机前轴前后滚转；再按一次 T 关闭")]
        public KeyCode rotationTuneRollKey = KeyCode.T;
        [Tooltip("微调开启时，鼠标移动旋转速度（度 / Mouse X 或 Y 的 GetAxis 数值）")]
        public float rotationTuneDegreesPerMouseUnit = 3f;

        [Header("放置 · 缩放微调")]
        [Tooltip("Y：开关缩放模式；滚轮与鼠标上下移动放大/缩小；再按一次 Y 关闭")]
        public KeyCode scaleTuneToggleKey = KeyCode.Y;
        [Tooltip("滚轮 Input「Mouse ScrollWheel」对倍率的增量（约 0.1 为一格）")]
        public float scaleTuneScrollMultiplier = 0.12f;
        [Tooltip("缩放模式开启时，鼠标上移放大、下移缩小（每帧与滚轮叠加）")]
        public float scaleTuneMouseYMultiplier = 0.025f;
        [Tooltip("相对基准缩放的最小倍率")]
        public float scaleTuneMinMultiplier = 0.2f;
        [Tooltip("相对基准缩放的最大倍率")]
        public float scaleTuneMaxMultiplier = 5f;

        [Header("说明面板")]
        [Tooltip("进入放置模式时在屏幕顶端显示操作说明；菜单：GameObject → DormMall → 创建放置模式说明面板")]
        public PlacementHelpPanel placementHelpPanel;
        [Tooltip("未拖入说明面板时，运行时自动查找场景中的 PlacementHelpPanel")]
        public bool autoFindHelpPanel = true;

        [Header("调试")]
        public bool debugLog = true;

        private bool _active;
        private GameObject _ghost;
        private Collider[] _ghostColliders;
        private Renderer[] _ghostRenderers;
        private PurchasedItemData _currentItem;
        private int _currentInventoryIndex = -1;
        private string _currentPrefabPath;
        private Camera[] _otherCameras;
        private bool[] _cameraEnabledStates;
        private RaycastHit _lastHit;
        private bool _hasPlacementPoint;
        private bool _isFloorLikeSurface;
        private bool _activatedFirstPersonForPlacement;
        private bool _firstPersonWasActive;
        private bool _firstPersonWasCursorLocked;
        private Quaternion _userRotationOffset = Quaternion.identity;
        private PlacementRotationTuneMode _rotationTuneMode = PlacementRotationTuneMode.None;
        private bool _scaleTuneActive;
        private Vector3 _baseGhostScale = Vector3.one;
        private float _userScaleMultiplier = 1f;
        /// <summary>预制体实例挂在此子节点下，已平移使父级原点 = 几何中心；旋转父级即绕几何中心转。</summary>
        private Transform _ghostVisual;

        enum PlacementRotationTuneMode
        {
            None,
            YawWorld,
            PitchCameraRight,
            RollCameraForward
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            AutoBindReferences();
        }

        void OnDestroy()
        {
            if (_active)
                ExitPlacement(keepFirstPersonActive: false);
            if (Instance == this) Instance = null;
            CleanupGhost();
        }

        void EnsureHelpPanelBound()
        {
            if (placementHelpPanel == null && autoFindHelpPanel)
                placementHelpPanel = FindObjectOfType<PlacementHelpPanel>(true);
        }

        void ShowPlacementHelp()
        {
            EnsureHelpPanelBound();
            if (placementHelpPanel != null)
                placementHelpPanel.ShowFor(this);
        }

        void HidePlacementHelp()
        {
            if (placementHelpPanel != null)
                placementHelpPanel.HidePanel();
        }

        void AutoBindReferences()
        {
            if (firstPersonCamera == null)
                firstPersonCamera = FindObjectOfType<global::FirstPersonCamera>(true);

            if (placementCamera == null && firstPersonCamera != null)
                placementCamera = firstPersonCamera.GetComponent<Camera>();

            if (placementCamera == null)
                placementCamera = Camera.main;
        }

        public void StartPlacement(PurchasedItemData item, int inventoryIndex, string prefabPath)
        {
            if (_active) return;

            if (!DormPlacementZoneService.CanEnterPlacementMode(out string blockReason))
            {
                Debug.LogWarning("[PlacementMode] " + blockReason);
                return;
            }

            AutoBindReferences();

            if (debugLog)
                Debug.Log($"[PlacementMode] 开始放置: id={item?.id}, name={item?.productName}, inventoryIndex={inventoryIndex}, prefabPath='{prefabPath}'");

            var prefab = Resources.Load<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning(
                    $"[PlacementMode] 无法加载预制体。\n" +
                    $"商品: id={item?.id}, name={item?.productName}\n" +
                    $"当前配置的 placeablePrefabPath: '{prefabPath}'");
                return;
            }

            _currentItem = item;
            _currentInventoryIndex = inventoryIndex;
            _currentPrefabPath = prefabPath;

            _firstPersonWasActive = firstPersonCamera != null && firstPersonCamera.IsActive;
            _firstPersonWasCursorLocked = firstPersonCamera != null && firstPersonCamera.IsCursorLocked;
            _activatedFirstPersonForPlacement = false;
            if (autoActivateFirstPersonCamera && firstPersonCamera != null)
            {
                firstPersonCamera.Activate();
                firstPersonCamera.SetCursorLocked(true);
                _activatedFirstPersonForPlacement = !_firstPersonWasActive;
            }

            AutoBindReferences();
            if (placementCamera == null)
            {
                Debug.LogWarning("[PlacementMode] 未找到可用于放置的相机");
                ResetPlacementState();
                return;
            }

            if (disableOtherCamerasOnPlace)
                DisableOtherCameras(placementCamera);

            CreateGhost(prefab);
            _active = true;

            ShowPlacementHelp();

            if (debugLog)
                Debug.Log("[PlacementMode] 已进入放置模式，移动鼠标转视角；R/E/T 角度微调；Y 缩放微调（滚轮/鼠标上下，再按 Y 关闭）；右键确认，ESC 取消");
        }

        void Update()
        {
            if (!_active || _ghost == null) return;

            // 先于相机解析处理取消：避免 cam 为空或短暂失效时无法 Esc 退出
            if (Input.GetKeyDown(cancelKey))
            {
                CancelPlacement();
                return;
            }

            var cam = ResolvePlacementCamera();
            if (cam == null) return;

            HandleScaleTuneToggle();
            HandleRotationTuneToggle();
            if (_rotationTuneMode != PlacementRotationTuneMode.None)
                _scaleTuneActive = false;

            if (firstPersonCamera != null)
                firstPersonCamera.suppressMouseLook =
                    _rotationTuneMode != PlacementRotationTuneMode.None || _scaleTuneActive;

            if (_rotationTuneMode != PlacementRotationTuneMode.None)
                ApplyRotationTuning(cam);

            if (_scaleTuneActive)
                ApplyScaleTuning();

            UpdatePreview(cam);

            bool inDormZone = _hasPlacementPoint &&
                DormPlacementZoneService.IsPlacementAllowedAt(_ghost.transform.position);
            bool canPlace = inDormZone;
            UpdateGhostTint(canPlace ? validTint : invalidTint);

            if (Input.GetKeyDown(confirmKey))
            {
                if (canPlace) ConfirmPlace();
                return;
            }
        }

        void CreateGhost(GameObject prefab)
        {
            CleanupGhost();

            var root = new GameObject("PlacementGhost_" + (prefab.name ?? "Item"));
            _ghost = root;

            var visual = Instantiate(prefab);
            visual.name = (prefab.name ?? "Item") + "_Visual";
            visual.transform.SetParent(root.transform, false);

            ApplyPlacedItemScale(visual, prefab);
            _ghostVisual = visual.transform;
            AlignChildPivotToBoundsCenter(root.transform, _ghostVisual);

            _baseGhostScale = _ghostVisual.localScale;

            SetLayerRecursive(root.transform, ghostLayer);

            _ghostColliders = _ghost.GetComponentsInChildren<Collider>(true);
            foreach (var c in _ghostColliders)
                c.enabled = false;

            var rb = _ghost.GetComponentInChildren<Rigidbody>(true);
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }

            _ghostRenderers = _ghost.GetComponentsInChildren<Renderer>(true);
            _userRotationOffset = Quaternion.identity;
            _rotationTuneMode = PlacementRotationTuneMode.None;
            _userScaleMultiplier = 1f;
            _scaleTuneActive = false;
            UpdateGhostTint(invalidTint);
        }

        void HandleScaleTuneToggle()
        {
            if (!Input.GetKeyDown(scaleTuneToggleKey)) return;
            _scaleTuneActive = !_scaleTuneActive;
            if (_scaleTuneActive)
                _rotationTuneMode = PlacementRotationTuneMode.None;
        }

        void ApplyScaleTuning()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            float my = Input.GetAxis("Mouse Y");
            float delta = scroll * scaleTuneScrollMultiplier + my * scaleTuneMouseYMultiplier;
            if (Mathf.Abs(delta) < 1e-7f) return;

            _userScaleMultiplier = Mathf.Clamp(
                _userScaleMultiplier + delta,
                scaleTuneMinMultiplier,
                scaleTuneMaxMultiplier);
        }

        void HandleRotationTuneToggle()
        {
            if (Input.GetKeyDown(rotationTuneYawKey))
                _rotationTuneMode = _rotationTuneMode == PlacementRotationTuneMode.YawWorld
                    ? PlacementRotationTuneMode.None
                    : PlacementRotationTuneMode.YawWorld;
            if (Input.GetKeyDown(rotationTunePitchKey))
                _rotationTuneMode = _rotationTuneMode == PlacementRotationTuneMode.PitchCameraRight
                    ? PlacementRotationTuneMode.None
                    : PlacementRotationTuneMode.PitchCameraRight;
            if (Input.GetKeyDown(rotationTuneRollKey))
                _rotationTuneMode = _rotationTuneMode == PlacementRotationTuneMode.RollCameraForward
                    ? PlacementRotationTuneMode.None
                    : PlacementRotationTuneMode.RollCameraForward;
        }

        void ApplyRotationTuning(Camera cam)
        {
            float s = rotationTuneDegreesPerMouseUnit;
            float mx = Input.GetAxis("Mouse X");
            float my = Input.GetAxis("Mouse Y");

            switch (_rotationTuneMode)
            {
                case PlacementRotationTuneMode.YawWorld:
                    _userRotationOffset = Quaternion.AngleAxis(mx * s, Vector3.up) * _userRotationOffset;
                    break;
                case PlacementRotationTuneMode.PitchCameraRight:
                    _userRotationOffset = Quaternion.AngleAxis(-my * s, cam.transform.right) * _userRotationOffset;
                    break;
                case PlacementRotationTuneMode.RollCameraForward:
                    _userRotationOffset = Quaternion.AngleAxis(mx * s, cam.transform.forward) * _userRotationOffset;
                    break;
            }
        }

        void UpdatePreview(Camera cam)
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, placementRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                _hasPlacementPoint = false;
                return;
            }

            _lastHit = hit;
            _hasPlacementPoint = true;
            _isFloorLikeSurface = Vector3.Dot(hit.normal, Vector3.up) >= floorUpDotThreshold;

            if (_ghostVisual != null)
            {
                _ghostVisual.localScale = _baseGhostScale * _userScaleMultiplier;
                AlignChildPivotToBoundsCenter(_ghost.transform, _ghostVisual);
            }

            Quaternion targetRotation = GetPlacementRotation(hit, cam.transform);
            _ghost.transform.rotation = targetRotation * _userRotationOffset;
            _ghost.transform.position = hit.point;
            SnapObjectToSurface(_ghost, hit.point, hit.normal);
        }

        Quaternion GetPlacementRotation(RaycastHit hit, Transform viewTransform)
        {
            if (autoFitFlatItemsToSurface && TryGetFlatItemSurfaceRotation(hit, viewTransform, out Quaternion autoRotation))
                return autoRotation;

            if (_isFloorLikeSurface)
            {
                if (alignYawToCameraOnFloor)
                    return Quaternion.Euler(0f, viewTransform.eulerAngles.y, 0f);

                return Quaternion.identity;
            }

            return Quaternion.LookRotation(-hit.normal, Vector3.up);
        }

        bool TryGetFlatItemSurfaceRotation(RaycastHit hit, Transform viewTransform, out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            if (_ghost == null) return false;
            if (!TryGetLocalBounds(_ghost, out Bounds localBounds)) return false;

            Vector3 size = localBounds.size;
            float minSize = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
            float maxSize = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (maxSize <= Mathf.Epsilon) return false;
            if (minSize / maxSize > flatItemThicknessRatioThreshold) return false;

            Vector3 localNormalAxis = GetSmallestAxisVector(size);
            rotation = Quaternion.FromToRotation(localNormalAxis, -hit.normal);

            Vector3 desiredUpOnPlane = Vector3.ProjectOnPlane(Vector3.up, hit.normal);
            if (_isFloorLikeSurface)
            {
                Vector3 desiredForward = Vector3.ProjectOnPlane(viewTransform.forward, hit.normal);
                if (desiredForward.sqrMagnitude > 0.0001f)
                {
                    Vector3 currentForwardOnPlane = Vector3.ProjectOnPlane(rotation * Vector3.forward, hit.normal);
                    if (currentForwardOnPlane.sqrMagnitude > 0.0001f)
                    {
                        float angle = Vector3.SignedAngle(currentForwardOnPlane, desiredForward, hit.normal);
                        rotation = Quaternion.AngleAxis(angle, hit.normal) * rotation;
                    }
                }
            }
            else if (desiredUpOnPlane.sqrMagnitude > 0.0001f)
            {
                Vector3 currentUpOnPlane = Vector3.ProjectOnPlane(rotation * Vector3.up, hit.normal);
                if (currentUpOnPlane.sqrMagnitude > 0.0001f)
                {
                    float angle = Vector3.SignedAngle(currentUpOnPlane, desiredUpOnPlane, hit.normal);
                    rotation = Quaternion.AngleAxis(angle, hit.normal) * rotation;
                }
            }

            return true;
        }

        bool WouldOverlap(GameObject obj)
        {
            if (!TryGetWorldBounds(obj, out Bounds bounds))
                return true;

            Vector3 halfExtents = bounds.extents * 0.98f;
            Collider[] hits = Physics.OverlapBox(
                bounds.center,
                halfExtents,
                obj.transform.rotation,
                overlapCheckLayers,
                QueryTriggerInteraction.Ignore);

            foreach (var c in hits)
            {
                if (c == null) continue;
                if (_ghost != null && c.transform.IsChildOf(_ghost.transform)) continue;
                return true;
            }
            return false;
        }

        void ConfirmPlace()
        {
            if (!_hasPlacementPoint || string.IsNullOrEmpty(_currentPrefabPath))
            {
                CancelPlacement();
                return;
            }

            if (!DormPlacementZoneService.IsPlacementAllowedAt(_ghost.transform.position))
            {
                if (debugLog)
                    Debug.LogWarning("[PlacementMode] 当前位置不在您的寝室放置区域内，无法确认放置。");
                return;
            }

            var prefab = Resources.Load<GameObject>(_currentPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning(
                    $"[PlacementMode] 确认放置时再次加载预制体失败。\n" +
                    $"商品: id={_currentItem?.id}, name={_currentItem?.productName}\n" +
                    $"当前配置的 placeablePrefabPath: '{_currentPrefabPath}'");
                CancelPlacement();
                return;
            }

            var placedRoot = new GameObject("Placed_" + (prefab.name ?? "Item"));
            var placedVisual = Instantiate(prefab);
            placedVisual.name = (prefab.name ?? "Item") + "_Visual";
            placedVisual.transform.SetParent(placedRoot.transform, false);

            ApplyPlacedItemScale(placedVisual, prefab);
            if (_ghostVisual != null)
                placedVisual.transform.localScale = _ghostVisual.localScale;
            AlignChildPivotToBoundsCenter(placedRoot.transform, placedVisual.transform);

            placedRoot.transform.rotation = _ghost.transform.rotation;
            placedRoot.transform.position = _ghost.transform.position;

            PrepareCollidersForPlacedObject(placedRoot);
            SnapObjectToSurface(placedRoot, _lastHit.point, _lastHit.normal);
            Physics.SyncTransforms();

            ApplyPlacementPhysics(placedRoot);

            ConsumeInventorySlotAfterSuccessfulPlace();

            if (QuestSystemManager.Instance != null)
                QuestSystemManager.Instance.ReportEvent("placement_success");

            if (debugLog)
                Debug.Log($"[PlacementMode] 已放置: {_currentItem?.productName}");

            if (NetworkClient.active && CoopSessionState.IsSessionHost && placedVisual != null)
            {
                var coop = FindObjectOfType<CoopNetworkPlayer>();
                if (coop != null && coop.isLocalPlayer)
                    coop.CmdSyncDormPlacement(_currentPrefabPath, placedRoot.transform.position, placedRoot.transform.rotation, placedVisual.transform.localScale);
            }

            // 本地持久化：下次进入同场景时恢复已放置物（按账号 + 场景隔离）。
            string layoutSceneKey = UserDormRoomService.GetActiveLayoutSceneKey();
            string placedRecordId = DormPlacementPersistence.SavePlacedItem(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                _currentItem,
                _currentPrefabPath,
                placedRoot.transform,
                placedVisual != null ? placedVisual.transform.localScale : Vector3.one);
            if (placedRoot != null)
            {
                var marker = placedRoot.GetComponent<DormPlacedRuntimeMarker>();
                if (marker == null) marker = placedRoot.AddComponent<DormPlacedRuntimeMarker>();
                marker.recordId = placedRecordId ?? "";
                marker.prefabPath = _currentPrefabPath ?? "";
                marker.sceneName = layoutSceneKey;
                marker.ownerUserBucket = (GameServerSync.Instance != null ? GameServerSync.Instance.SavedUsername : "") ?? "";
            }

            ExitPlacement(keepFirstPersonActive: true);
        }

        /// <summary>仅在成功实例化并放置到场景后扣背包；校验索引与 id，避免误删。</summary>
        void ConsumeInventorySlotAfterSuccessfulPlace()
        {
            int idx = _currentInventoryIndex;
            var items = InventoryData.GetItems();
            if (_currentItem == null || idx < 0 || idx >= items.Count)
            {
                if (debugLog)
                    Debug.LogWarning($"[PlacementMode] 背包扣减跳过：索引无效 idx={idx}, count={items.Count}");
                return;
            }

            if (items[idx].id != _currentItem.id)
            {
                if (debugLog)
                    Debug.LogWarning("[PlacementMode] 背包索引与当前放置物品 id 不一致，按 id 回退查找");
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].id == _currentItem.id)
                    {
                        InventoryData.RemoveItemAt(i);
                        return;
                    }
                }
                if (debugLog) Debug.LogWarning("[PlacementMode] 未在背包中找到对应 id，未扣减");
                return;
            }

            InventoryData.RemoveItemAt(idx);
        }

        void ApplyPlacementPhysics(GameObject placed)
        {
            bool shouldFall = enableGravityWhenUnsupported && ShouldDropAfterPlacement(placed);
            if (keepPlacedObjectStatic && !shouldFall)
            {
                var existingRb = placed.GetComponentInChildren<Rigidbody>(true);
                if (existingRb != null) existingRb.isKinematic = true;
                return;
            }

            var rb = placed.GetComponentInChildren<Rigidbody>(true);
            if (rb == null) rb = placed.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        bool ShouldDropAfterPlacement(GameObject placed)
        {
            if (!_isFloorLikeSurface) return false;
            if (!TryGetWorldBounds(placed, out Bounds bounds)) return false;

            Vector3 origin = bounds.center + Vector3.up * 0.02f;
            Vector3 halfExtents = bounds.extents * 0.95f;
            bool hasSupport = Physics.BoxCast(
                origin,
                halfExtents,
                Vector3.down,
                out _,
                placed.transform.rotation,
                supportCheckDistance,
                placementRaycastLayers,
                QueryTriggerInteraction.Ignore);

            return !hasSupport;
        }

        /// <summary>场景切换或离开寝室时强制结束放置（DontDestroy 背包可能仍引用本控制器）。</summary>
        public void ForceExitIfActive()
        {
            if (_active)
                ExitPlacement(keepFirstPersonActive: false);
        }

        void CancelPlacement()
        {
            ExitPlacement(keepFirstPersonActive: false);
        }

        void ExitPlacement(bool keepFirstPersonActive)
        {
            HidePlacementHelp();

            RestoreCameras();
            if (firstPersonCamera != null)
            {
                firstPersonCamera.suppressMouseLook = false;
                if (keepFirstPersonActive)
                {
                    firstPersonCamera.Activate();
                    firstPersonCamera.SetCursorLocked(true);
                }
                else if (_activatedFirstPersonForPlacement)
                {
                    firstPersonCamera.Deactivate();
                }
                else if (_firstPersonWasActive)
                {
                    firstPersonCamera.SetCursorLocked(_firstPersonWasCursorLocked);
                }
            }

            CleanupGhost();
            ResetPlacementState();

            if (debugLog)
                Debug.Log("[PlacementMode] 已退出放置模式");
        }

        void ResetPlacementState()
        {
            _active = false;
            _currentItem = null;
            _currentInventoryIndex = -1;
            _currentPrefabPath = null;
            _hasPlacementPoint = false;
            _isFloorLikeSurface = false;
            _firstPersonWasActive = false;
            _firstPersonWasCursorLocked = false;
            _userRotationOffset = Quaternion.identity;
            _rotationTuneMode = PlacementRotationTuneMode.None;
            _scaleTuneActive = false;
            _userScaleMultiplier = 1f;
            _ghostVisual = null;
        }

        void CleanupGhost()
        {
            _ghostColliders = null;
            _ghostRenderers = null;
            _ghostVisual = null;
            if (_ghost != null)
            {
                Destroy(_ghost);
                _ghost = null;
            }
        }

        /// <summary>
        /// 将 visual 相对 pivotRoot 平移，使 pivotRoot 世界空间原点与 visual 子树渲染/碰撞包围盒中心对齐。
        /// 之后对 pivotRoot 施加旋转即绕几何中心旋转。
        /// </summary>
        void AlignChildPivotToBoundsCenter(Transform pivotRoot, Transform visualTransform)
        {
            if (pivotRoot == null || visualTransform == null) return;
            visualTransform.localPosition = Vector3.zero;
            if (!TryGetWorldBounds(visualTransform.gameObject, out Bounds b)) return;
            Vector3 localInPivot = pivotRoot.InverseTransformPoint(b.center);
            visualTransform.localPosition = -localInPivot;
        }

        Camera ResolvePlacementCamera()
        {
            if (placementCamera != null && placementCamera.enabled)
                return placementCamera;

            AutoBindReferences();
            if (placementCamera != null)
                return placementCamera;

            return Camera.main;
        }

        void DisableOtherCameras(Camera activeCamera)
        {
            _otherCameras = FindObjectsOfType<Camera>(true);
            _cameraEnabledStates = new bool[_otherCameras.Length];
            for (int i = 0; i < _otherCameras.Length; i++)
            {
                var cam = _otherCameras[i];
                if (cam == null) continue;
                _cameraEnabledStates[i] = cam.enabled;
                if (cam == activeCamera) continue;
                cam.enabled = false;
            }

            activeCamera.enabled = true;
        }

        void RestoreCameras()
        {
            if (_otherCameras == null) return;
            for (int i = 0; i < _otherCameras.Length; i++)
            {
                var cam = _otherCameras[i];
                if (cam == null)
                    continue;
                if (!cam)
                    continue;
                cam.enabled = _cameraEnabledStates != null && i < _cameraEnabledStates.Length
                    ? _cameraEnabledStates[i]
                    : true;
            }
            _otherCameras = null;
            _cameraEnabledStates = null;

            if (firstPersonCamera != null)
                firstPersonCamera.EnsureGameplayReady();
            GameplayCameraInputRestorer.ScheduleRestore(2);
        }

        void ApplyPlacedItemScale(GameObject instance, GameObject prefabSource)
        {
            Vector3 baseScale = prefabSource.transform.localScale;
            instance.transform.localScale = baseScale;

            if (!normalizePlacedScaleByBounds)
            {
                instance.transform.localScale = baseScale * placementScale;
                return;
            }

            if (!TryGetWorldBounds(instance, out Bounds wb))
            {
                instance.transform.localScale = baseScale * placementScale;
                return;
            }

            float maxDim = Mathf.Max(wb.size.x, Mathf.Max(wb.size.y, wb.size.z));
            if (maxDim <= 1e-5f)
            {
                instance.transform.localScale = baseScale * placementScale;
                return;
            }

            float uniform = placedTargetMaxExtent / maxDim;
            instance.transform.localScale = baseScale * uniform * placementScale;
        }

        void PrepareCollidersForPlacedObject(GameObject obj)
        {
            if (ensureMeshCollidersFromMeshes)
            {
                var meshFilters = obj.GetComponentsInChildren<MeshFilter>(true);
                foreach (var mf in meshFilters)
                {
                    if (mf == null || mf.sharedMesh == null) continue;
                    var mc = mf.gameObject.GetComponent<MeshCollider>();
                    if (mc == null) mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = true;
                    mc.enabled = true;
                }
            }

            var colliders = obj.GetComponentsInChildren<Collider>(true);
            foreach (var c in colliders)
                c.enabled = true;

            var meshColliders = obj.GetComponentsInChildren<MeshCollider>(true);
            foreach (var mesh in meshColliders)
            {
                if (!mesh.convex) mesh.convex = true;
            }

            if (colliders.Length > 0) return;

            if (!TryGetWorldBounds(obj, out Bounds bounds)) return;

            var fallback = obj.AddComponent<BoxCollider>();
            fallback.center = obj.transform.InverseTransformPoint(bounds.center);

            Vector3 lossy = obj.transform.lossyScale;
            fallback.size = new Vector3(
                lossy.x != 0f ? bounds.size.x / Mathf.Abs(lossy.x) : bounds.size.x,
                lossy.y != 0f ? bounds.size.y / Mathf.Abs(lossy.y) : bounds.size.y,
                lossy.z != 0f ? bounds.size.z / Mathf.Abs(lossy.z) : bounds.size.z);

            if (debugLog)
                Debug.Log("[PlacementMode] 无法从网格生成 MeshCollider，已自动添加 BoxCollider");
        }

        void SnapObjectToSurface(GameObject obj, Vector3 surfacePoint, Vector3 surfaceNormal)
        {
            if (!TryGetWorldBounds(obj, out Bounds bounds)) return;

            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            float minDot = float.PositiveInfinity;

            for (int xi = 0; xi < 2; xi++)
            {
                for (int yi = 0; yi < 2; yi++)
                {
                    for (int zi = 0; zi < 2; zi++)
                    {
                        Vector3 corner = new Vector3(
                            xi == 0 ? min.x : max.x,
                            yi == 0 ? min.y : max.y,
                            zi == 0 ? min.z : max.z);
                        float dot = Vector3.Dot(surfaceNormal, corner - surfacePoint);
                        if (dot < minDot) minDot = dot;
                    }
                }
            }

            float pushDistance = surfacePadding - minDot;
            if (pushDistance > 0f)
                obj.transform.position += surfaceNormal * pushDistance;
        }

        bool TryGetWorldBounds(GameObject obj, out Bounds bounds)
        {
            var colliders = obj.GetComponentsInChildren<Collider>(true);
            if (colliders.Length > 0)
            {
                bounds = colliders[0].bounds;
                for (int i = 1; i < colliders.Length; i++)
                    bounds.Encapsulate(colliders[i].bounds);
                return true;
            }

            var renderers = obj.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);
                return true;
            }

            bounds = default;
            return false;
        }

        bool TryGetLocalBounds(GameObject obj, out Bounds bounds)
        {
            bool hasBounds = false;
            bounds = default;

            var renderers = obj.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                EncapsulateWorldBoundsInLocalSpace(obj.transform, renderer.bounds, ref hasBounds, ref bounds);
            }

            if (hasBounds) return true;

            var colliders = obj.GetComponentsInChildren<Collider>(true);
            foreach (var collider in colliders)
            {
                EncapsulateWorldBoundsInLocalSpace(obj.transform, collider.bounds, ref hasBounds, ref bounds);
            }

            return hasBounds;
        }

        void EncapsulateWorldBoundsInLocalSpace(Transform root, Bounds sourceBounds, ref bool hasBounds, ref Bounds localBounds)
        {
            Vector3 min = sourceBounds.min;
            Vector3 max = sourceBounds.max;

            for (int xi = 0; xi < 2; xi++)
            {
                for (int yi = 0; yi < 2; yi++)
                {
                    for (int zi = 0; zi < 2; zi++)
                    {
                        Vector3 worldCorner = new Vector3(
                            xi == 0 ? min.x : max.x,
                            yi == 0 ? min.y : max.y,
                            zi == 0 ? min.z : max.z);
                        Vector3 localCorner = root.InverseTransformPoint(worldCorner);
                        if (!hasBounds)
                        {
                            localBounds = new Bounds(localCorner, Vector3.zero);
                            hasBounds = true;
                        }
                        else
                        {
                            localBounds.Encapsulate(localCorner);
                        }
                    }
                }
            }
        }

        Vector3 GetSmallestAxisVector(Vector3 size)
        {
            if (size.x <= size.y && size.x <= size.z) return Vector3.right;
            if (size.y <= size.x && size.y <= size.z) return Vector3.up;
            return Vector3.forward;
        }

        void UpdateGhostTint(Color color)
        {
            if (_ghostRenderers == null) return;
            foreach (var renderer in _ghostRenderers)
            {
                if (renderer == null) continue;
                foreach (var mat in renderer.materials)
                {
                    if (mat.HasProperty("_Color")) mat.color = color;
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                }
            }
        }

        static void SetLayerRecursive(Transform target, int layer)
        {
            target.gameObject.layer = layer;
            for (int i = 0; i < target.childCount; i++)
                SetLayerRecursive(target.GetChild(i), layer);
        }
    }
}
