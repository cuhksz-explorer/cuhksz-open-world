using UnityEngine;
using UnityEngine.AI;

public class NPCControl : MonoBehaviour
{
    [Header("����")]
    public Transform player;
    public float detectionRange = 5f;
    public float wanderRadius = 10f;
    public float wanderInterval = 3f;
    public bool approachPlayer = true;

    [Header("NavMesh")]
    [Tooltip("�������㲻����ɫ NavMesh �ϣ��ڴ˾�����Ѱ��������ߵ㲢 Warp���ף�")]
    [SerializeField] float warpToNavMeshSearchRadius = 12f;

    NavMeshAgent agent;
    float timer;
    bool _warnedNotOnNavMesh;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogWarning(
                "[NPCControl] ������ȱ�� NavMeshAgent���� Add Component �� Nav Mesh Agent����ɾ�����ű����ѽ���: " + gameObject.name,
                this);
            enabled = false;
            return;
        }

        timer = wanderInterval;
        TryWarpOntoNavMesh();
    }

    /// <summary>�Ѵ����ŵ�����Ŀ����� NavMesh �ϣ����� SetDestination �ᱨ����</summary>
    bool TryWarpOntoNavMesh()
    {
        if (agent.isOnNavMesh)
            return true;

        if (NavMesh.SamplePosition(
                transform.position,
                out NavMeshHit hit,
                Mathf.Max(0.5f, warpToNavMeshSearchRadius),
                NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            return agent.isOnNavMesh;
        }

        return false;
    }

    void Update()
    {
        if (agent == null || player == null)
            return;

        if (!agent.isActiveAndEnabled)
            return;

        if (!agent.isOnNavMesh)
        {
            if (!TryWarpOntoNavMesh())
            {
                if (!_warnedNotOnNavMesh)
                {
                    _warnedNotOnNavMesh = true;
                    Debug.LogWarning(
                        "[NPCControl] ��ɫ���� NavMesh ���Ҹ����Ҳ������ߵ㡣�룺1��Window��AI��Navigation �Ե��� Bake��2���� NPC �ŵ���ɫ�����ϣ�3������ Warp To Nav Mesh Search Radius������: " +
                        gameObject.name,
                        this);
                }
                return;
            }
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer < detectionRange)
        {
            if (approachPlayer)
                agent.SetDestination(player.position);
            else
            {
                Vector3 fleeDirection = transform.position - player.position;
                Vector3 destination = transform.position + fleeDirection;
                agent.SetDestination(destination);
            }
        }
        else
        {
            timer += Time.deltaTime;

            if (timer >= wanderInterval)
            {
                Vector3 randomDestination = GetRandomPoint(transform.position, wanderRadius);
                agent.SetDestination(randomDestination);
                timer = 0f;
            }
        }
    }

    public static Vector3 GetRandomPoint(Vector3 center, float radius)
    {
        Vector3 randomDir = Random.insideUnitSphere * radius;
        randomDir += center;
        if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, Mathf.Max(radius, 2f), NavMesh.AllAreas))
            return hit.position;
        if (NavMesh.SamplePosition(center, out hit, Mathf.Max(radius, 2f), NavMesh.AllAreas))
            return hit.position;
        return center;
    }
}
