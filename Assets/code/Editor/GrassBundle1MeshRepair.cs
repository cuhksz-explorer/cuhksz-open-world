#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// GrassBundle1 预制体的 MeshFilter 网格被清空时，通过 TreeEditor 从 Tree Data 重新烘焙网格。
/// 仅修改预制体资产，不改动场景；保留 prefab GUID 与实例上的 Transform 等覆盖。
/// </summary>
public static class GrassBundle1MeshRepair
{
    const string PrefabPath =
        "Assets/草坪/Plants and Foliage/Ground Foliage/Plants and Foliage/GrassBundle1.prefab";

    /// <summary>BatchMode: Unity -executeMethod GrassBundle1MeshRepair.BatchRebuildGrassBundle1Prefab</summary>
    public static void BatchRebuildGrassBundle1Prefab()
    {
        string projectRoot = Directory.GetCurrentDirectory();
        string logPath = Path.Combine(projectRoot, "Logs", "GrassBundle1Reimport.log");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.WriteAllText(logPath, "[GrassBundle1MeshRepair] Batch rebuild started.\n");

            RebuildResult result = RebuildPrefabInternal(backupBeforeWrite: true);
            string line =
                $"[GrassBundle1MeshRepair] success={result.Success} verts={result.VertexCount} tris={result.TriangleCount}\n" +
                (string.IsNullOrEmpty(result.Message) ? "" : result.Message + "\n");
            File.AppendAllText(logPath, line);
            Debug.Log(line.TrimEnd());

            EditorApplication.Exit(result.Success ? 0 : 1);
        }
        catch (Exception ex)
        {
            try
            {
                File.AppendAllText(logPath, "[GrassBundle1MeshRepair] EXCEPTION: " + ex + "\n");
            }
            catch
            {
                // ignored
            }

            Debug.LogException(ex);
            EditorApplication.Exit(2);
        }
    }

    [MenuItem("Tools/场景配置/重建 GrassBundle1 网格", false, 11)]
    public static void RebuildMesh()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("重建 GrassBundle1", "请先退出 Play Mode。", "确定");
            return;
        }

        RebuildResult result = RebuildPrefabInternal(backupBeforeWrite: true);
        if (!result.Success)
        {
            EditorUtility.DisplayDialog("重建 GrassBundle1", result.Message, "确定");
            return;
        }

        EditorUtility.DisplayDialog(
            "重建 GrassBundle1",
            $"完成。顶点 {result.VertexCount}，三角形约 {result.TriangleCount}。\n" +
            "仅更新了预制体；场景中各实例的位置/旋转/缩放覆盖已保留。",
            "确定");
    }

    struct RebuildResult
    {
        public bool Success;
        public int VertexCount;
        public int TriangleCount;
        public string Message;
    }

    static RebuildResult RebuildPrefabInternal(bool backupBeforeWrite)
    {
        string assetPath = PrefabPath.Replace('\\', '/');
        string fullPath = Path.GetFullPath(assetPath);
        if (!File.Exists(fullPath))
        {
            return new RebuildResult
            {
                Success = false,
                Message = "找不到预制体：\n" + PrefabPath,
            };
        }

        if (backupBeforeWrite)
            BackupPrefabFile(fullPath);

        GameObject root = PrefabUtility.LoadPrefabContents(assetPath);
        if (root == null)
        {
            return new RebuildResult
            {
                Success = false,
                Message = "LoadPrefabContents 失败：" + PrefabPath,
            };
        }

        try
        {
            var tree = root.GetComponent<Tree>();
            if (tree == null)
            {
                return new RebuildResult
                {
                    Success = false,
                    Message = "预制体上没有 Tree 组件。",
                };
            }

            if (!TryInvokeTreeEditorUpdateMesh(tree))
            {
                return new RebuildResult
                {
                    Success = false,
                    Message =
                        "无法调用 TreeEditor 重建网格。\n" +
                        "请从 Ground Foliage Vol. 1 资源包重新导入 GrassBundle1，或手动在 Tree 组件中刷新。",
                };
            }

            Mesh mesh = root.GetComponent<MeshFilter>()?.sharedMesh;
            int vertCount = mesh != null ? mesh.vertexCount : 0;
            int triCount = mesh != null ? mesh.triangles.Length / 3 : 0;

            if (vertCount == 0)
            {
                return new RebuildResult
                {
                    Success = false,
                    Message =
                        "重建后网格仍为空（0 顶点）。\n" +
                        "Tree Data 可能已损坏，需从原始资源包覆盖 GrassBundle1.prefab（保留 .meta 中的 guid）。",
                };
            }

            PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            return new RebuildResult
            {
                Success = true,
                VertexCount = vertCount,
                TriangleCount = triCount,
                Message = "Backup written beside prefab as *.bak_reimport_*",
            };
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void BackupPrefabFile(string fullPrefabPath)
    {
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string backupPath = fullPrefabPath + ".bak_reimport_" + stamp;
        File.Copy(fullPrefabPath, backupPath, overwrite: false);
        Debug.Log("[GrassBundle1MeshRepair] Backup: " + backupPath);
    }

    static bool TryInvokeTreeEditorUpdateMesh(Tree tree)
    {
        Type treeEditorType = Type.GetType("UnityEditor.TreeEditor, UnityEditor");
        if (treeEditorType == null)
            return false;

        MethodInfo updateMesh = treeEditorType.GetMethod(
            "UpdateMesh",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
            null,
            new[] { typeof(Tree) },
            null);
        if (updateMesh != null)
        {
            updateMesh.Invoke(null, new object[] { tree });
            return true;
        }

        MethodInfo resetTree = treeEditorType.GetMethod(
            "ResetTree",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new[] { typeof(Tree) },
            null);
        if (resetTree != null)
        {
            resetTree.Invoke(null, new object[] { tree });
            return true;
        }

        return false;
    }
}
#endif
