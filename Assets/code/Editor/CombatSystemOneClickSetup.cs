#if UNITY_EDITOR
using CombatSystem;
using Mirror;
using UnityEditor;
using UnityEngine;

namespace CombatSystem.EditorTools
{
    public static class CombatSystemOneClickSetup
    {
        const string CoopPlayerPath = "Assets/code/Multiplayer/Resources/CoopPlayer.prefab";
        const string MonsterPrefabPath = "Assets/code/CombatSystem/Prefabs/CombatMonster.prefab";

        [MenuItem("Tools/Combat/Setup All (Database + Coop + Monster Prefab)")]
        [MenuItem("Tools/战斗系统/一键配置战斗系统", false, 0)]
        public static void SetupAll()
        {
            WeaponDatabaseFactory.CreateOrUpdateDatabase();
            WeaponDatabaseFactory.GeneratePlaceholderIcons();
            EnsureCoopPlayerCombat();
            EnsureMonsterPrefab();
            Debug.Log("[Combat] 一键配置完成。");
        }

        [MenuItem("Tools/Combat/Place Test Monsters In Active Scene")]
        public static void PlaceTestMonstersInScene()
        {
            EnsureMonsterPrefab();
            var prefab = AssetDatabase.LoadAssetAtPath<CombatMonster>(MonsterPrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[Combat] 未找到怪物预制体。");
                return;
            }

            CreateMonsterInstance(prefab, "练习靶", new Vector3(0, 1, 8), 50, 0, 5, new Color(0.5f, 0.8f, 0.5f));
            CreateMonsterInstance(prefab, "文具守卫", new Vector3(4, 1, 10), 120, 8, 25, new Color(0.9f, 0.6f, 0.2f));
            CreateMonsterInstance(prefab, "精英镇纸魔", new Vector3(-4, 1, 12), 300, 15, 80, new Color(0.8f, 0.2f, 0.2f));
            Debug.Log("[Combat] 已在当前场景放置 3 只测试怪物。");
        }

        static void CreateMonsterInstance(CombatMonster prefab, string name, Vector3 pos, float hp, float dmg, int gold, Color color)
        {
            var inst = (CombatMonster)PrefabUtility.InstantiatePrefab(prefab);
            inst.displayName = name;
            inst.maxHealth = hp;
            inst.attackDamage = dmg;
            inst.goldReward = gold;
            inst.transform.position = pos;
            inst.name = name;
            var r = inst.GetComponentInChildren<Renderer>();
            if (r != null && r.sharedMaterial != null)
            {
                var mat = new Material(r.sharedMaterial);
                mat.color = color;
                r.sharedMaterial = mat;
            }
        }

        static void EnsureCoopPlayerCombat()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CoopPlayerPath);
            if (prefab == null)
            {
                Debug.LogWarning("[Combat] 未找到 CoopPlayer.prefab，请先运行 Tools/联机/Create Coop Player Prefab。");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(CoopPlayerPath);
            if (root.GetComponent<CoopCombatRelay>() == null)
                root.AddComponent<CoopCombatRelay>();

            var hitbox = root.transform.Find("CombatHitbox");
            if (hitbox == null)
            {
                var hbGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                hbGo.name = "CombatHitbox";
                hbGo.transform.SetParent(root.transform, false);
                hbGo.transform.localPosition = new Vector3(0, 1f, 0);
                hbGo.transform.localScale = new Vector3(0.9f, 1f, 0.9f);
                Object.DestroyImmediate(hbGo.GetComponent<MeshRenderer>());
                var col = hbGo.GetComponent<CapsuleCollider>();
                col.isTrigger = false;
                hbGo.AddComponent<CoopCombatHitbox>();
                hbGo.layer = LayerMask.NameToLayer("Default");
            }

            PrefabUtility.SaveAsPrefabAsset(root, CoopPlayerPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static void EnsureMonsterPrefab()
        {
            EnsureFolder("Assets/code/CombatSystem/Prefabs");
            var existing = AssetDatabase.LoadAssetAtPath<CombatMonster>(MonsterPrefabPath);
            if (existing != null)
                return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "CombatMonster";
            go.AddComponent<CombatMonster>();
            PrefabUtility.SaveAsPrefabAsset(go, MonsterPrefabPath);
            Object.DestroyImmediate(go);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parts = path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
#endif
