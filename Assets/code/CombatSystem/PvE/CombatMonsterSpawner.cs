using UnityEngine;

namespace CombatSystem
{
    public class CombatMonsterSpawner : MonoBehaviour
    {
        public CombatMonster prefab;
        public int count = 3;

        void Start()
        {
            if (prefab == null)
                return;
            for (int i = 0; i < count; i++)
            {
                var pos = transform.position + new Vector3((i - 1) * 4f, 0f, i * 2f);
                Instantiate(prefab, pos, Quaternion.identity);
            }
        }
    }
}
