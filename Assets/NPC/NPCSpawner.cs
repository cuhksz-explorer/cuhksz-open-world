using UnityEngine;
using UnityEngine.AI;

public class NPCSpawner : MonoBehaviour
{
    public GameObject npcPrefab;
    public int npcCount = 50;
    public float spawnRadius = 100f;

    void Start()
    {
        SpawnNPCs();
    }

    void SpawnNPCs()
    {
        int spawned = 0;
        int attempts = 0;

        while (spawned < npcCount && attempts < npcCount * 20)
        {
            attempts++;

            Vector3 randomPoint = transform.position + Random.insideUnitSphere * spawnRadius;
            randomPoint.y = transform.position.y;

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            {
                GameObject npc = Instantiate(npcPrefab, hit.position, Quaternion.identity);
                npc.name = "NPC_" + spawned;

                NavMeshAgent agent = npc.GetComponent<NavMeshAgent>();
                if (agent != null)
                    agent.Warp(hit.position);

                spawned++;
            }
        }

        Debug.Log("Spawned NPCs: " + spawned);
    }
}
