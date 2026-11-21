using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace GameQuestSystem
{
    /// <summary>
    /// 世界空间引导线：优先沿 NavMesh 绕行；高度不超过此前最高点（避免贴到楼顶）；
    /// 无可用路径时从起点高度直线穿过建筑区域。
    /// </summary>
    [DisallowMultipleComponent]
    public class QuestGroundGuidePath : MonoBehaviour
    {
        const float SampleStepMeters = 2f;
        const int MaxPoints = 96;
        const float GroundOffset = 0.12f;
        const float RayStartHeight = 400f;
        const float HideDistance = 5.5f;
        const float MaxClimbPerSample = 0.4f;
        const float NavMeshSampleRadius = 10f;

        [SerializeField] float lineWidth = 0.26f;
        [SerializeField] Color lineColor = new Color(1f, 1f, 1f, 0.9f);
        [SerializeField] LayerMask groundMask = ~0;

        LineRenderer _line;
        QuestSystemManager _questManager;
        Transform _player;
        Material _lineMaterial;
        readonly List<Vector3> _pathScratch = new List<Vector3>(MaxPoints);

        public static QuestGroundGuidePath EnsureExists()
        {
            var existing = FindObjectOfType<QuestGroundGuidePath>(true);
            if (existing != null)
                return existing;

            var go = new GameObject("QuestGroundGuidePath");
            return go.AddComponent<QuestGroundGuidePath>();
        }

        void Awake()
        {
            if (FindObjectsOfType<QuestGroundGuidePath>(true).Length > 1)
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);
            BuildLineRenderer();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_questManager != null)
                _questManager.OnQuestChanged -= OnQuestChanged;
            if (_lineMaterial != null)
                Destroy(_lineMaterial);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryBind();
            RefreshVisibility();
        }

        void Start()
        {
            TryBind();
            RefreshVisibility();
        }

        void OnEnable()
        {
            TryBind();
        }

        void LateUpdate()
        {
            if (PhoneUI.ImmersiveModeSettings.IsEnabled)
            {
                if (_line != null)
                    _line.enabled = false;
                return;
            }

            if (_line == null)
                return;

            if (!TryGetGuideEndpoints(out Vector3 start, out Vector3 end))
            {
                _line.enabled = false;
                return;
            }

            float flatDist = FlatDistance(start, end);
            if (flatDist < HideDistance)
            {
                _line.enabled = false;
                return;
            }

            _line.enabled = true;
            UpdateGroundPath(start, end, flatDist);
        }

        void TryBind()
        {
            if (_questManager == null)
                _questManager = QuestSystemManager.Instance ?? FindObjectOfType<QuestSystemManager>(true);

            if (_questManager != null)
            {
                _questManager.OnQuestChanged -= OnQuestChanged;
                _questManager.OnQuestChanged += OnQuestChanged;
            }

            var pm = FindObjectOfType<PlayerMovement>(true);
            _player = pm != null ? pm.transform : null;
        }

        void OnQuestChanged()
        {
            RefreshVisibility();
        }

        void RefreshVisibility()
        {
            if (_line == null)
                return;
            _line.enabled = ShouldShowGuidePath();
        }

        bool ShouldShowGuidePath()
        {
            if (PhoneUI.ImmersiveModeSettings.IsEnabled)
                return false;
            if (_questManager == null)
                return false;
            if (SceneManager.GetActiveScene().name.IndexOf("Loading", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            var sub = _questManager.GetTrackedSubTask();
            if (sub == null)
                return false;

            if (sub.triggerType == QuestTriggerType.ReachLocation ||
                sub.triggerType == QuestTriggerType.ManualEvent)
                return _questManager.CanGuideToSubTaskTargetPublic(sub);

            if (_questManager.IsNewUserTutorialActive() &&
                NewUserTutorialGuideCatalog.TryGet(sub.subTaskId, out var ui) &&
                ui.ShowGroundLine)
            {
                return _questManager.TryGetActiveQuestReachLocationWorldPosition(out _);
            }

            return false;
        }

        bool TryGetGuideEndpoints(out Vector3 start, out Vector3 end)
        {
            start = end = default;
            if (_player == null || _questManager == null)
                return false;
            if (!_questManager.TryGetActiveQuestReachLocationWorldPosition(out end))
                return false;

            start = _player.position;
            float startGroundY = SampleGroundY(start, start.y) + GroundOffset;
            start = new Vector3(start.x, startGroundY, start.z);
            return true;
        }

        void UpdateGroundPath(Vector3 start, Vector3 end, float flatDist)
        {
            _pathScratch.Clear();
            bool hasNavPath = TryBuildNavMeshPath(start, end, _pathScratch);

            if (!hasNavPath)
            {
                AppendStraightPath(start, end, flatDist, _pathScratch);
                ApplyConstantHeightLine(_pathScratch, start.y);
                return;
            }

            ApplyHeightCappedLine(_pathScratch, start.y);
        }

        void ApplyConstantHeightLine(List<Vector3> xzPath, float fixedY)
        {
            int count = Mathf.Min(xzPath.Count, MaxPoints);
            _line.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                Vector3 xz = xzPath[i];
                _line.SetPosition(i, new Vector3(xz.x, fixedY, xz.z));
            }
        }

        void ApplyHeightCappedLine(List<Vector3> xzPath, float startY)
        {
            float maxHeightSoFar = startY;
            int count = Mathf.Min(xzPath.Count, MaxPoints);
            _line.positionCount = count;

            for (int i = 0; i < count; i++)
            {
                Vector3 xz = xzPath[i];
                float groundY = SampleGroundY(new Vector3(xz.x, 0f, xz.z), maxHeightSoFar) + GroundOffset;
                float y;

                if (groundY <= maxHeightSoFar + MaxClimbPerSample)
                {
                    y = groundY;
                    maxHeightSoFar = Mathf.Max(maxHeightSoFar, y);
                }
                else
                    y = maxHeightSoFar;

                _line.SetPosition(i, new Vector3(xz.x, y, xz.z));
            }
        }

        bool TryBuildNavMeshPath(Vector3 start, Vector3 end, List<Vector3> outPoints)
        {
            if (!NavMesh.SamplePosition(start, out NavMeshHit startHit, NavMeshSampleRadius, NavMesh.AllAreas))
                return false;
            if (!NavMesh.SamplePosition(end, out NavMeshHit endHit, NavMeshSampleRadius, NavMesh.AllAreas))
                return false;

            var navPath = new NavMeshPath();
            if (!NavMesh.CalculatePath(startHit.position, endHit.position, NavMesh.AllAreas, navPath))
                return false;
            if (navPath.status != NavMeshPathStatus.PathComplete)
                return false;

            Vector3[] corners = navPath.corners;
            if (corners == null || corners.Length < 2)
                return false;

            outPoints.Add(ToXZ(start));
            Vector3 prev = ToXZ(corners[0]);
            if (Vector3.Distance(outPoints[0], prev) > 0.05f)
                AppendResampledSegment(outPoints[0], prev, outPoints);
            else
                prev = outPoints[0];

            for (int c = 1; c < corners.Length && outPoints.Count < MaxPoints; c++)
            {
                Vector3 next = ToXZ(corners[c]);
                AppendResampledSegment(prev, next, outPoints);
                prev = next;
            }

            Vector3 endXZ = ToXZ(endHit.position);
            if (FlatDistance(prev, endXZ) > 0.5f && outPoints.Count < MaxPoints)
                AppendResampledSegment(prev, endXZ, outPoints);

            return outPoints.Count >= 2;
        }

        void AppendStraightPath(Vector3 start, Vector3 end, float flatDist, List<Vector3> outPoints)
        {
            int segments = Mathf.Clamp(Mathf.CeilToInt(flatDist / SampleStepMeters), 2, MaxPoints - 1);
            Vector3 startXZ = ToXZ(start);
            Vector3 endXZ = ToXZ(end);

            for (int i = 0; i <= segments && outPoints.Count < MaxPoints; i++)
            {
                float t = i / (float)segments;
                outPoints.Add(Vector3.Lerp(startXZ, endXZ, t));
            }
        }

        void AppendResampledSegment(Vector3 fromXZ, Vector3 toXZ, List<Vector3> outPoints)
        {
            float segLen = Vector3.Distance(fromXZ, toXZ);
            if (segLen < 0.01f)
                return;

            int steps = Mathf.Max(1, Mathf.CeilToInt(segLen / SampleStepMeters));
            for (int s = 1; s <= steps && outPoints.Count < MaxPoints; s++)
            {
                float t = s / (float)steps;
                outPoints.Add(Vector3.Lerp(fromXZ, toXZ, t));
            }
        }

        float SampleGroundY(Vector3 world, float maxHeightSoFar)
        {
            float heightLimit = maxHeightSoFar + MaxClimbPerSample;
            var origins = new[]
            {
                new Vector3(world.x, world.y + RayStartHeight, world.z),
                new Vector3(world.x, RayStartHeight, world.z)
            };

            float bestY = float.MinValue;
            bool found = false;

            for (int o = 0; o < origins.Length; o++)
            {
                var hits = Physics.RaycastAll(origins[o], Vector3.down, RayStartHeight * 2f, groundMask,
                    QueryTriggerInteraction.Ignore);
                if (hits == null || hits.Length == 0)
                    continue;

                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                for (int i = 0; i < hits.Length; i++)
                {
                    if (IsPlayerCollider(hits[i].collider))
                        continue;

                    float y = hits[i].point.y;
                    if (y > heightLimit)
                        continue;

                    if (!found || y > bestY)
                    {
                        bestY = y;
                        found = true;
                    }
                }
            }

            return found ? bestY : maxHeightSoFar;
        }

        bool IsPlayerCollider(Collider col)
        {
            if (col == null)
                return false;
            if (_player != null && col.transform.IsChildOf(_player))
                return true;
            return col.CompareTag("Player");
        }

        static Vector3 ToXZ(Vector3 world) => new Vector3(world.x, 0f, world.z);

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        void BuildLineRenderer()
        {
            var lineGo = new GameObject("GroundGuideLine");
            lineGo.transform.SetParent(transform, false);
            _line = lineGo.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.loop = false;
            _line.numCornerVertices = 4;
            _line.numCapVertices = 4;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.widthMultiplier = lineWidth;
            _line.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
            _line.textureMode = LineTextureMode.Tile;

            _lineMaterial = new Material(Shader.Find("Sprites/Default"));
            _lineMaterial.color = lineColor;
            _line.material = _lineMaterial;
            _line.startColor = lineColor;
            _line.endColor = lineColor;
            _line.enabled = false;
        }
    }
}
