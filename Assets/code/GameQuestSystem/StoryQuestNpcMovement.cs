using UnityEngine;
using UnityEngine.AI;

namespace GameQuestSystem
{
    /// <summary>
    /// 剧情 NPC 游走：同场景仅 1 个 StoryQuestNpc 时随机移动；多个时保持静止。
    /// </summary>
    [DisallowMultipleComponent]
    public class StoryQuestNpcMovement : MonoBehaviour
    {
        [Tooltip("仅 1 人时随机游走半径（米）")]
        public float walkRadius = 12f;

        StoryQuestNpc _storyNpc;
        NavMeshAgent _agent;
        RandomWalkNPC _walker;

        void Awake()
        {
            _storyNpc = GetComponent<StoryQuestNpc>();
            EnsureComponents();
        }

        void OnEnable()
        {
            StoryQuestNpcMovementRegistry.Register(this);
        }

        void OnDisable()
        {
            StoryQuestNpcMovementRegistry.Unregister(this);
        }

        void EnsureComponents()
        {
            _agent = GetComponent<NavMeshAgent>();
            if (_agent == null)
                _agent = gameObject.AddComponent<NavMeshAgent>();

            _agent.speed = 1.2f;
            _agent.angularSpeed = 360f;
            _agent.acceleration = 4f;
            _agent.stoppingDistance = 0.35f;

            _walker = GetComponent<RandomWalkNPC>();
            if (_walker == null)
                _walker = gameObject.AddComponent<RandomWalkNPC>();
            _walker.walkRadius = walkRadius;
        }

        internal void ApplyMovementPolicy(bool allowMove)
        {
            EnsureComponents();

            if (_walker != null)
            {
                _walker.enabled = allowMove;
                _walker.walkRadius = walkRadius;
                if (allowMove)
                    _walker.ResumeWandering();
                else
                    _walker.StopWandering();
            }

            if (_agent != null)
            {
                if (!allowMove)
                    _agent.updateRotation = false;
                else
                    _agent.updateRotation = true;
                _agent.enabled = allowMove;
                if (!allowMove && _agent.isOnNavMesh)
                    _agent.ResetPath();
            }
        }

        public bool ShouldParticipateInMovement()
        {
            if (_storyNpc != null && !_storyNpc.gameObject.activeInHierarchy)
                return false;
            if (_storyNpc != null && string.IsNullOrWhiteSpace(_storyNpc.documentId))
                return false;
            return true;
        }
    }

    public static class StoryQuestNpcMovementRegistry
    {
        public static void RefreshScenePolicy() => RefreshAll();

        static readonly System.Collections.Generic.HashSet<StoryQuestNpcMovement> Active =
            new System.Collections.Generic.HashSet<StoryQuestNpcMovement>();

        public static void Register(StoryQuestNpcMovement item)
        {
            if (item == null)
                return;
            Active.Add(item);
            RefreshAll();
        }

        public static void Unregister(StoryQuestNpcMovement item)
        {
            if (item == null)
                return;
            Active.Remove(item);
            RefreshAll();
        }

        public static void RefreshAll()
        {
            int movers = 0;
            foreach (var m in Active)
            {
                if (m != null && m.ShouldParticipateInMovement())
                    movers++;
            }

            bool allowMove = movers == 1;
            foreach (var m in Active)
            {
                if (m == null)
                    continue;
                m.ApplyMovementPolicy(allowMove && m.ShouldParticipateInMovement());
            }
        }
    }
}
