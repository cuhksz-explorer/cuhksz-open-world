using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 在 NavMesh 上随机游走（队友 NPC 包逻辑）。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class RandomWalkNPC : MonoBehaviour
{
    public float walkRadius = 30f;
    public float waitTimeMin = 1f;
    public float waitTimeMax = 4f;

    NavMeshAgent _agent;
    float _waitTimer;
    bool _waiting;

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
    }

    void OnEnable()
    {
        _waiting = false;
        if (_agent != null && _agent.isOnNavMesh)
            SetNewDestination();
    }

    void Update()
    {
        if (_agent == null || !_agent.isOnNavMesh)
            return;

        if (_waiting)
        {
            _waitTimer -= Time.deltaTime;
            if (_waitTimer <= 0f)
            {
                _waiting = false;
                SetNewDestination();
            }

            return;
        }

        if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance)
        {
            _waiting = true;
            _waitTimer = Random.Range(waitTimeMin, waitTimeMax);
        }
    }

    public void StopWandering()
    {
        _waiting = true;
        if (_agent != null && _agent.isOnNavMesh)
            _agent.ResetPath();
    }

    public void ResumeWandering()
    {
        _waiting = false;
        SetNewDestination();
    }

    void SetNewDestination()
    {
        if (_agent == null)
            return;

        Vector3 randomDirection = Random.insideUnitSphere * walkRadius;
        randomDirection += transform.position;

        if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, walkRadius, NavMesh.AllAreas))
            _agent.SetDestination(hit.position);
    }
}
