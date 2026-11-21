#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Mirror;

namespace Multiplayer.EditorTools
{
    public static class CoopPlayerPrefabCreator
    {
        const string PrefabPath = "Assets/code/Multiplayer/Resources/CoopPlayer.prefab";

        [MenuItem("Tools/联机/Create Coop Player Prefab")]
        public static void CreatePrefab()
        {
            var root = new GameObject("CoopPlayer");
            root.AddComponent<NetworkIdentity>();
            root.AddComponent<NetworkTransformReliable>();
            root.AddComponent<CoopNetworkPlayer>();
            root.AddComponent<CoopPlayerRoomStats>();
            root.AddComponent<CoopQuestStateHub>();
            root.AddComponent<CoopQuestRelay>();
            root.SetActive(false);

            EnsureDirectory("Assets/code/Multiplayer/Resources");
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.Refresh();
            Debug.Log("[联机] 已生成 " + PrefabPath + "。请在 LanCoopNetworkManager 上指定 playerPrefab，或使用 Resources 自动加载。");
        }

        static void EnsureDirectory(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
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
