using Multiplayer;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace DormMall
{
    /// <summary>
    /// 场景内已放置家具编辑：
    /// H 进入/退出编辑（默认不用 M，避免与大地图冲突）；左键选中并拖动；Enter 确认移动；Delete 删除；Esc 退出编辑。
    /// </summary>
    public class PlacedFurnitureEditController : MonoBehaviour
    {
        public static PlacedFurnitureEditController Instance { get; private set; }

        [Header("输入")]
        [Tooltip("默认 H；勿用 M（PlayerMovement 已绑定大地图）。")]
        [SerializeField] KeyCode toggleEditModeKey = KeyCode.H;
        [SerializeField] KeyCode confirmMoveKey = KeyCode.Return;
        [SerializeField] KeyCode deleteKey = KeyCode.Delete;
        [SerializeField] KeyCode cancelKey = KeyCode.Escape;
        [SerializeField] KeyCode undoKey = KeyCode.Z;
        [SerializeField] KeyCode redoKey = KeyCode.Y;
        [SerializeField] bool requireCtrlForUndoRedo = true;

        [Header("射线与拖动")]
        [SerializeField] LayerMask dragSurfaceLayers = -1;
        [SerializeField] float maxRayDistance = 1000f;
        [SerializeField] float surfacePadding = 0.01f;
        [SerializeField] bool enableGridSnap = true;
        [SerializeField] float gridSize = 0.25f;
        [SerializeField] bool snapYToGrid;
        [Header("碰撞校验")]
        [SerializeField] LayerMask overlapCheckLayers = ~(1 << 2);
        [SerializeField] bool validateAgainstPlacedFurnitureOnly = true;
        [SerializeField] bool blockConfirmOnInvalidPlacement = true;
        [SerializeField, Range(0.5f, 1f)] float overlapBoundsShrink = 0.92f;
        [Header("吸附预判")]
        [SerializeField] bool enablePredictiveSnap = true;
        [SerializeField, Range(1, 8)] int predictiveSnapMaxGridSteps = 3;
        [SerializeField] bool preferDragDirectionForPredictiveSnap = true;
        [SerializeField, Range(0f, 1f)] float predictiveSnapDirectionScoreThreshold = 0.18f;

        [Header("高亮与预览")]
        [SerializeField] Color selectedTint = new Color(0.75f, 0.95f, 1f, 1f);
        [SerializeField] Color dragPreviewTint = new Color(0.6f, 0.9f, 1f, 0.42f);
        [SerializeField] Color invalidSelectedTint = new Color(1f, 0.55f, 0.55f, 1f);
        [SerializeField] Color invalidDragPreviewTint = new Color(1f, 0.45f, 0.45f, 0.42f);
        [SerializeField] float emissionBoost = 0.6f;

        [SerializeField] bool debugLog;

        bool _editMode;
        bool _dragging;
        DormPlacedRuntimeMarker _selected;
        Camera _cam;
        CursorLockMode _cursorLockBeforeEdit;
        bool _cursorVisibleBeforeEdit;
        bool _cursorCached;
        readonly List<RendererState> _selectedRendererStates = new List<RendererState>();
        readonly Stack<string> _undoSnapshots = new Stack<string>();
        readonly Stack<string> _redoSnapshots = new Stack<string>();
        Vector3 _dragStartPosition;
        Quaternion _dragStartRotation;
        Vector3 _dragStartVisualScale = Vector3.one;
        bool _hasDragBaseline;
        bool _isSelectedPlacementValid = true;
        Vector3 _lastResolvedDragPosition;
        bool _hasLastResolvedDragPosition;

        struct MaterialState
        {
            public Material material;
            public bool hasColor;
            public Color color;
            public bool hasBaseColor;
            public Color baseColor;
            public bool hasEmission;
            public Color emissionColor;
        }

        class RendererState
        {
            public MaterialState[] materialStates;
        }

        struct SnapCandidate
        {
            public Vector3 position;
            public float distance;
            public float directionScore;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var existing = Object.FindObjectOfType<PlacedFurnitureEditController>(true);
            if (existing != null)
                return;
            var go = new GameObject("[PlacedFurnitureEditController]");
            go.AddComponent<PlacedFurnitureEditController>();
            Object.DontDestroyOnLoad(go);
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive)
                return;
            if (!DormSceneConfig.IsDormScene(scene.name))
                ForceExitEditModeIfNeeded();
        }

        /// <summary>离开 dorm 后关闭编辑模式并恢复光标（本组件为 DontDestroyOnLoad）。</summary>
        public void ForceExitEditModeIfNeeded()
        {
            if (_editMode)
                SetEditMode(false);
        }

        void OnDestroy()
        {
            if (_editMode)
                SetEditMode(false);
            if (Instance == this)
                Instance = null;
            ClearSelectedVisuals();
        }

        void Update()
        {
            if (GameplayUiHotkeyGuard.ShouldBlockGameplayHotkeys())
                return;
            if (PlacementModeController.Instance != null && PlacementModeController.Instance.IsPlacementActive)
                return;
            if (CoopSessionState.IsSessionGuest)
                return;
            if (!_editMode && IsBigMapOpen())
                return;
            if (!_editMode && Time.timeScale <= 0f)
                return;

            if (Input.GetKeyDown(toggleEditModeKey))
            {
                SetEditMode(!_editMode);
                return;
            }

            if (!_editMode)
                return;

            if (Input.GetKeyDown(cancelKey))
            {
                SetEditMode(false);
                return;
            }

            if (IsUndoTriggered())
            {
                Undo();
                return;
            }
            if (IsRedoTriggered())
            {
                Redo();
                return;
            }

            EnsureCamera();
            if (_cam == null)
                return;

            bool pointerOverUi = IsPointerOverUi();
            if (!pointerOverUi && Input.GetMouseButtonDown(0))
                TrySelectUnderMouse();

            if (_selected == null)
                return;

            if (!pointerOverUi && Input.GetMouseButtonDown(0))
            {
                _dragging = true;
                CaptureDragBaseline();
            }
            if (Input.GetMouseButtonUp(0))
                _dragging = false;

            if (_dragging)
            {
                DragSelected();
                _isSelectedPlacementValid = EvaluateSelectedPlacementValidity();
                ApplySelectedVisuals(previewMode: true);
            }
            else
            {
                _isSelectedPlacementValid = EvaluateSelectedPlacementValidity();
                ApplySelectedVisuals(previewMode: false);
            }

            if (Input.GetKeyDown(confirmMoveKey))
                ConfirmSelectedMove();
            else if (Input.GetKeyDown(deleteKey))
                DeleteSelected();
        }

        void SetEditMode(bool enabled)
        {
            if (enabled && !DormSceneConfig.IsActiveDormScene())
            {
                if (debugLog)
                    Debug.LogWarning("[DormEdit] 仅能在 dorm 寝室场景中编辑已放置家具。");
                enabled = false;
            }
            _editMode = enabled;
            _dragging = false;
            _hasDragBaseline = false;
            if (!_editMode)
            {
                ClearSelectedVisuals();
                _selected = null;
                _isSelectedPlacementValid = true;
                _hasLastResolvedDragPosition = false;
            }

            if (enabled)
            {
                if (!_cursorCached)
                {
                    _cursorLockBeforeEdit = Cursor.lockState;
                    _cursorVisibleBeforeEdit = Cursor.visible;
                    _cursorCached = true;
                }
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (_cursorCached)
            {
                Cursor.lockState = _cursorLockBeforeEdit;
                Cursor.visible = _cursorVisibleBeforeEdit;
                _cursorCached = false;
            }

            if (debugLog)
                Debug.Log("[DormEdit] 编辑模式=" + (_editMode ? "开" : "关"));
        }

        void EnsureCamera()
        {
            if (_cam != null && _cam.enabled)
                return;
            _cam = Camera.main;
            if (_cam == null)
            {
                var cams = FindObjectsOfType<Camera>(true);
                for (int i = 0; i < cams.Length; i++)
                {
                    if (cams[i] != null && cams[i].enabled)
                    {
                        _cam = cams[i];
                        break;
                    }
                }
            }
        }

        void TrySelectUnderMouse()
        {
            var ray = _cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, ~0, QueryTriggerInteraction.Ignore))
                return;
            var marker = hit.collider != null ? hit.collider.GetComponentInParent<DormPlacedRuntimeMarker>() : null;
            if (marker == null)
                return;
            if (_selected != marker)
                ClearSelectedVisuals();
            _selected = marker;
            _isSelectedPlacementValid = EvaluateSelectedPlacementValidity();
            _lastResolvedDragPosition = _selected.transform.position;
            _hasLastResolvedDragPosition = true;
            ApplySelectedVisuals(previewMode: false);
            if (debugLog)
                Debug.Log("[DormEdit] 选中: " + _selected.recordId);
        }

        void DragSelected()
        {
            if (_selected == null || _selected.gameObject == null)
                return;
            var ray = _cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, dragSurfaceLayers, QueryTriggerInteraction.Ignore))
                return;
            var tr = _selected.transform;
            Vector3 pos = hit.point + hit.normal * surfacePadding;
            tr.position = ResolveDraggedTargetPosition(pos);
            _lastResolvedDragPosition = tr.position;
            _hasLastResolvedDragPosition = true;
        }

        void ConfirmSelectedMove()
        {
            if (_selected == null || _selected.gameObject == null)
                return;
            if (blockConfirmOnInvalidPlacement && !_isSelectedPlacementValid)
            {
                if (debugLog)
                    Debug.LogWarning("[DormEdit] 当前位置与其他物体重叠，已禁止确认移动");
                return;
            }
            CaptureUndoSnapshot();
            Transform visual = FindVisualTransform(_selected.transform);
            Vector3 visualScale = visual != null ? visual.localScale : Vector3.one;
            if (!HasSelectedTransformChanged(_selected.transform, visualScale))
            {
                if (debugLog)
                    Debug.Log("[DormEdit] 位置未变化，跳过保存");
                return;
            }
            DormPlacementPersistence.NotifyPlacedObjectMoved(_selected.transform, visualScale);
            ApplySelectedVisuals(previewMode: false);
            CaptureDragBaseline();
            _isSelectedPlacementValid = true;
            if (debugLog)
                Debug.Log("[DormEdit] 已确认移动: " + _selected.recordId);
        }

        void DeleteSelected()
        {
            if (_selected == null || _selected.gameObject == null)
                return;
            CaptureUndoSnapshot();
            var go = _selected.gameObject;
            DormPlacementPersistence.NotifyPlacedObjectRemoved(go);
            Destroy(go);
            if (debugLog)
                Debug.Log("[DormEdit] 已删除家具");
            ClearSelectedVisuals();
            _selected = null;
            _dragging = false;
            _isSelectedPlacementValid = true;
            _hasLastResolvedDragPosition = false;
        }

        void CaptureUndoSnapshot()
        {
            string sceneName = UserDormRoomService.GetActiveLayoutSceneKey();
            string snapshot = DormPlacementPersistence.ExportSceneLayoutSnapshotJson(sceneName);
            if (string.IsNullOrEmpty(snapshot))
                return;
            _undoSnapshots.Push(snapshot);
            _redoSnapshots.Clear();
            if (_undoSnapshots.Count > 50)
            {
                // keep latest 50 by rebuilding
                var arr = _undoSnapshots.ToArray();
                _undoSnapshots.Clear();
                for (int i = Mathf.Min(49, arr.Length - 1); i >= 0; i--)
                    _undoSnapshots.Push(arr[i]);
            }
        }

        void Undo()
        {
            if (_undoSnapshots.Count == 0)
                return;
            string sceneName = UserDormRoomService.GetActiveLayoutSceneKey();
            string current = DormPlacementPersistence.ExportSceneLayoutSnapshotJson(sceneName);
            if (!string.IsNullOrEmpty(current))
                _redoSnapshots.Push(current);
            string prev = _undoSnapshots.Pop();
            DormPlacementPersistence.ImportSceneLayoutSnapshotJson(sceneName, prev, triggerCloudSync: true);
            _selected = null;
            _dragging = false;
            _hasLastResolvedDragPosition = false;
            ClearSelectedVisuals();
            if (debugLog) Debug.Log("[DormEdit] Undo");
        }

        void Redo()
        {
            if (_redoSnapshots.Count == 0)
                return;
            string sceneName = UserDormRoomService.GetActiveLayoutSceneKey();
            string current = DormPlacementPersistence.ExportSceneLayoutSnapshotJson(sceneName);
            if (!string.IsNullOrEmpty(current))
                _undoSnapshots.Push(current);
            string next = _redoSnapshots.Pop();
            DormPlacementPersistence.ImportSceneLayoutSnapshotJson(sceneName, next, triggerCloudSync: true);
            _selected = null;
            _dragging = false;
            _hasLastResolvedDragPosition = false;
            ClearSelectedVisuals();
            if (debugLog) Debug.Log("[DormEdit] Redo");
        }

        Vector3 SnapPositionToGrid(Vector3 p)
        {
            float g = Mathf.Max(0.01f, gridSize);
            p.x = Mathf.Round(p.x / g) * g;
            p.z = Mathf.Round(p.z / g) * g;
            if (snapYToGrid)
                p.y = Mathf.Round(p.y / g) * g;
            return p;
        }

        Vector3 ResolveDraggedTargetPosition(Vector3 rawPosition)
        {
            if (_selected == null || _selected.gameObject == null)
                return rawPosition;
            Vector3 candidate = enableGridSnap ? SnapPositionToGrid(rawPosition) : rawPosition;
            if (!enablePredictiveSnap)
                return candidate;
            if (!enableGridSnap)
                return candidate;
            if (IsPlacementValidAtPosition(candidate))
                return candidate;
            return TryFindNearestValidGridPosition(candidate, out Vector3 nearestValid)
                ? nearestValid
                : candidate;
        }

        bool TryFindNearestValidGridPosition(Vector3 anchor, out Vector3 validPosition)
        {
            validPosition = anchor;
            int maxSteps = Mathf.Max(1, predictiveSnapMaxGridSteps);
            float g = Mathf.Max(0.01f, gridSize);
            float dirThreshold = Mathf.Clamp01(predictiveSnapDirectionScoreThreshold);
            Vector3 dragDir = Vector3.zero;
            if (preferDragDirectionForPredictiveSnap && _hasLastResolvedDragPosition)
            {
                dragDir = anchor - _lastResolvedDragPosition;
                dragDir.y = 0f;
                if (dragDir.sqrMagnitude > 0.000001f)
                    dragDir.Normalize();
                else
                    dragDir = Vector3.zero;
            }
            for (int step = 1; step <= maxSteps; step++)
            {
                var offsets = new List<Vector2Int>(step * 8);
                for (int dx = -step; dx <= step; dx++)
                {
                    for (int dz = -step; dz <= step; dz++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != step)
                            continue;
                        offsets.Add(new Vector2Int(dx, dz));
                    }
                }
                var candidates = new List<SnapCandidate>(offsets.Count);
                for (int i = 0; i < offsets.Count; i++)
                {
                    Vector2Int off = offsets[i];
                    Vector3 test = new Vector3(anchor.x + off.x * g, anchor.y, anchor.z + off.y * g);
                    if (!snapYToGrid)
                        test.y = anchor.y;
                    else
                        test.y = Mathf.Round(anchor.y / g) * g;
                    if (!IsPlacementValidAtPosition(test))
                        continue;
                    float distance = Vector3.Distance(test, anchor);
                    Vector3 delta = test - anchor;
                    delta.y = 0f;
                    float directionScore = 0f;
                    if (dragDir.sqrMagnitude > 0.000001f && delta.sqrMagnitude > 0.000001f)
                        directionScore = Vector3.Dot(delta.normalized, dragDir);
                    candidates.Add(new SnapCandidate
                    {
                        position = test,
                        distance = distance,
                        directionScore = directionScore
                    });
                }
                if (candidates.Count == 0)
                    continue;

                candidates.Sort((a, b) => a.distance.CompareTo(b.distance));
                SnapCandidate bestByDistance = candidates[0];

                if (dragDir.sqrMagnitude <= 0.000001f)
                {
                    validPosition = bestByDistance.position;
                    return true;
                }

                SnapCandidate bestByDirection = bestByDistance;
                for (int i = 1; i < candidates.Count; i++)
                {
                    if (candidates[i].directionScore > bestByDirection.directionScore)
                        bestByDirection = candidates[i];
                }

                float directionGain = bestByDirection.directionScore - bestByDistance.directionScore;
                if (directionGain >= dirThreshold)
                    validPosition = bestByDirection.position;
                else
                    validPosition = bestByDistance.position;
                return true;
            }
            return false;
        }

        bool IsUndoTriggered()
        {
            if (!Input.GetKeyDown(undoKey))
                return false;
            if (!requireCtrlForUndoRedo)
                return true;
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            return ctrl && !shift;
        }

        bool IsRedoTriggered()
        {
            if (!requireCtrlForUndoRedo)
                return Input.GetKeyDown(redoKey);
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool ctrlY = Input.GetKeyDown(redoKey) && ctrl;
            bool ctrlShiftZ = Input.GetKeyDown(undoKey) && ctrl && shift;
            return ctrlY || ctrlShiftZ;
        }

        bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        void CaptureDragBaseline()
        {
            if (_selected == null || _selected.gameObject == null)
            {
                _hasDragBaseline = false;
                return;
            }
            _dragStartPosition = _selected.transform.position;
            _dragStartRotation = _selected.transform.rotation;
            Transform visual = FindVisualTransform(_selected.transform);
            _dragStartVisualScale = visual != null ? visual.localScale : Vector3.one;
            _hasDragBaseline = true;
        }

        bool HasSelectedTransformChanged(Transform tr, Vector3 visualScale)
        {
            if (tr == null)
                return false;
            if (!_hasDragBaseline)
                return true;
            bool posChanged = Vector3.Distance(_dragStartPosition, tr.position) > 0.0005f;
            bool rotChanged = Quaternion.Angle(_dragStartRotation, tr.rotation) > 0.05f;
            bool scaleChanged = Vector3.Distance(_dragStartVisualScale, visualScale) > 0.0005f;
            return posChanged || rotChanged || scaleChanged;
        }

        bool EvaluateSelectedPlacementValidity()
        {
            if (_selected == null || _selected.gameObject == null)
                return true;
            return IsPlacementValidAtPosition(_selected.transform.position);
        }

        bool IsPlacementValidAtPosition(Vector3 worldPosition)
        {
            if (!DormPlacementZoneService.IsPlacementAllowedAt(worldPosition))
                return false;
            if (_selected == null || _selected.gameObject == null)
                return true;
            var tr = _selected.transform;
            Vector3 oldPosition = tr.position;
            tr.position = worldPosition;
            bool valid = IsCurrentSelectedPlacementValid();
            tr.position = oldPosition;
            return valid;
        }

        bool IsCurrentSelectedPlacementValid()
        {
            if (_selected == null || _selected.gameObject == null)
                return true;
            if (!TryGetWorldBounds(_selected.gameObject, out Bounds bounds))
                return true;
            Vector3 halfExtents = bounds.extents * overlapBoundsShrink;
            if (halfExtents.x <= 0f || halfExtents.y <= 0f || halfExtents.z <= 0f)
                return true;
            var hits = Physics.OverlapBox(
                bounds.center,
                halfExtents,
                _selected.transform.rotation,
                overlapCheckLayers,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                var c = hits[i];
                if (c == null || c.isTrigger)
                    continue;
                if (c.transform.IsChildOf(_selected.transform))
                    continue;
                if (validateAgainstPlacedFurnitureOnly)
                {
                    var marker = c.GetComponentInParent<DormPlacedRuntimeMarker>();
                    if (marker == null || marker == _selected)
                        continue;
                }
                return false;
            }
            return true;
        }

        static bool TryGetWorldBounds(GameObject obj, out Bounds bounds)
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

        void ApplySelectedVisuals(bool previewMode)
        {
            if (_selected == null || _selected.gameObject == null)
                return;
            if (_selectedRendererStates.Count == 0)
                CaptureSelectedVisuals();
            Color tint;
            if (!_isSelectedPlacementValid)
                tint = previewMode ? invalidDragPreviewTint : invalidSelectedTint;
            else
                tint = previewMode ? dragPreviewTint : selectedTint;
            for (int i = 0; i < _selectedRendererStates.Count; i++)
            {
                var rs = _selectedRendererStates[i];
                if (rs == null || rs.materialStates == null)
                    continue;
                for (int j = 0; j < rs.materialStates.Length; j++)
                {
                    var ms = rs.materialStates[j];
                    var mat = ms.material;
                    if (mat == null)
                        continue;
                    if (ms.hasColor)
                        mat.color = tint;
                    if (ms.hasBaseColor)
                        mat.SetColor("_BaseColor", tint);
                    if (ms.hasEmission)
                    {
                        var boosted = new Color(
                            tint.r * (1f + emissionBoost),
                            tint.g * (1f + emissionBoost),
                            tint.b * (1f + emissionBoost),
                            1f);
                        mat.SetColor("_EmissionColor", boosted);
                    }
                }
            }
        }

        void CaptureSelectedVisuals()
        {
            _selectedRendererStates.Clear();
            if (_selected == null || _selected.gameObject == null)
                return;
            var renderers = _selected.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null)
                    continue;
                var mats = r.materials;
                var rs = new RendererState
                {
                    materialStates = new MaterialState[mats.Length]
                };
                for (int j = 0; j < mats.Length; j++)
                {
                    var m = mats[j];
                    var ms = new MaterialState
                    {
                        material = m,
                        hasColor = m != null && m.HasProperty("_Color"),
                        hasBaseColor = m != null && m.HasProperty("_BaseColor"),
                        hasEmission = m != null && m.HasProperty("_EmissionColor")
                    };
                    if (m != null)
                    {
                        if (ms.hasColor) ms.color = m.color;
                        if (ms.hasBaseColor) ms.baseColor = m.GetColor("_BaseColor");
                        if (ms.hasEmission) ms.emissionColor = m.GetColor("_EmissionColor");
                    }
                    rs.materialStates[j] = ms;
                }
                _selectedRendererStates.Add(rs);
            }
        }

        void ClearSelectedVisuals()
        {
            for (int i = 0; i < _selectedRendererStates.Count; i++)
            {
                var rs = _selectedRendererStates[i];
                if (rs == null || rs.materialStates == null)
                    continue;
                for (int j = 0; j < rs.materialStates.Length; j++)
                {
                    var ms = rs.materialStates[j];
                    var mat = ms.material;
                    if (mat == null)
                        continue;
                    if (ms.hasColor)
                        mat.color = ms.color;
                    if (ms.hasBaseColor)
                        mat.SetColor("_BaseColor", ms.baseColor);
                    if (ms.hasEmission)
                        mat.SetColor("_EmissionColor", ms.emissionColor);
                }
            }
            _selectedRendererStates.Clear();
        }

        static Transform FindVisualTransform(Transform root)
        {
            if (root == null)
                return null;
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c != null && c.name.EndsWith("_Visual"))
                    return c;
            }
            return root.childCount > 0 ? root.GetChild(0) : root;
        }

        static bool IsBigMapOpen()
        {
            var pm = Object.FindObjectOfType<PlayerMovement>(true);
            if (pm == null || !pm.isMovementPaused)
                return false;
            if (pm.mapSystem == null || pm.mapSystem.mapWindow == null)
                return pm.isMovementPaused;
            return pm.mapSystem.mapWindow.activeInHierarchy;
        }
    }
}
