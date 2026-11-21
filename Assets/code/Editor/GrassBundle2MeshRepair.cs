#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 修复场景引用的 New Folder/GrassBundle2 预制体：重建 Tree 网格并同步材质。
/// 仅修改该 prefab 资产，不改动场景内其他物体。
/// </summary>
public static class GrassBundle2MeshRepair
{
    const string BrokenPrefabPath =
        "Assets/草坪/New Folder/Prefabs (Ready to use)/Plants and Foliage/GrassBundle2.prefab";

    const string WorkingPrefabPath =
        "Assets/草坪/Plants and Foliage/Ground Foliage/Plants and Foliage/GrassBundle2.prefab";

    [MenuItem("Tools/场景配置/重建 GrassBundle2 网格", false, 12)]
    public static void RebuildMeshMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("重建 GrassBundle2", "请先退出 Play Mode。", "确定");
            return;
        }

        RebuildResult result = RebuildPrefab(backupBeforeWrite: true);
        EditorUtility.DisplayDialog(
            "重建 GrassBundle2",
            result.Success
                ? $"完成。顶点 {result.VertexCount}，三角形约 {result.TriangleCount}。\n仅更新了 New Folder/GrassBundle2 预制体。"
                : result.Message,
            "确定");
    }

    /// <summary>BatchMode: Unity -executeMethod GrassBundle2MeshRepair.BatchRebuildGrassBundle2Prefab</summary>
    public static void BatchRebuildGrassBundle2Prefab()
    {
        string logPath = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "GrassBundle2Reimport.log");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            RebuildResult result = RebuildPrefab(backupBeforeWrite: true);
            File.WriteAllText(
                logPath,
                $"[GrassBundle2MeshRepair] success={result.Success} verts={result.VertexCount} tris={result.TriangleCount}\n{result.Message}\n");
            EditorApplication.Exit(result.Success ? 0 : 1);
        }
        catch (Exception ex)
        {
            File.AppendAllText(logPath, "[GrassBundle2MeshRepair] EXCEPTION: " + ex + "\n");
            Debug.LogException(ex);
            EditorApplication.Exit(2);
        }
    }

    struct RebuildResult
    {
        public bool Success;
        public int VertexCount;
        public int TriangleCount;
        public string Message;
    }

    static RebuildResult RebuildPrefab(bool backupBeforeWrite)
    {
        string brokenPath = BrokenPrefabPath.Replace('\\', '/');
        string workingPath = WorkingPrefabPath.Replace('\\', '/');

        if (!File.Exists(Path.GetFullPath(brokenPath)))
        {
            return new RebuildResult { Success = false, Message = "找不到预制体：\n" + brokenPath };
        }

        if (!File.Exists(Path.GetFullPath(workingPath)))
        {
            return new RebuildResult { Success = false, Message = "找不到参考预制体：\n" + workingPath };
        }

        if (backupBeforeWrite)
            BackupPrefabFile(Path.GetFullPath(brokenPath));

        GameObject brokenRoot = PrefabUtility.LoadPrefabContents(brokenPath);
        GameObject workingRoot = PrefabUtility.LoadPrefabContents(workingPath);
        if (brokenRoot == null || workingRoot == null)
        {
            if (brokenRoot != null) PrefabUtility.UnloadPrefabContents(brokenRoot);
            if (workingRoot != null) PrefabUtility.UnloadPrefabContents(workingRoot);
            return new RebuildResult { Success = false, Message = "LoadPrefabContents 失败。" };
        }

        try
        {
            var tree = brokenRoot.GetComponent<Tree>();
            if (tree != null)
                TryInvokeTreeEditorUpdateMesh(tree);

            var brokenFilter = brokenRoot.GetComponent<MeshFilter>();
            var brokenRenderer = brokenRoot.GetComponent<MeshRenderer>();
            var workingFilter = workingRoot.GetComponent<MeshFilter>();
            var workingRenderer = workingRoot.GetComponent<MeshRenderer>();

            int vertCount = brokenFilter != null && brokenFilter.sharedMesh != null
                ? brokenFilter.sharedMesh.vertexCount
                : 0;

            if (vertCount == 0 && workingFilter != null && workingFilter.sharedMesh != null)
            {
                brokenFilter.sharedMesh = workingFilter.sharedMesh;
                vertCount = workingFilter.sharedMesh.vertexCount;
            }

            if (brokenRenderer != null && workingRenderer != null &&
                (brokenRenderer.sharedMaterials == null || brokenRenderer.sharedMaterials.Length == 0))
            {
                brokenRenderer.sharedMaterials = workingRenderer.sharedMaterials;
            }

            vertCount = brokenFilter != null && brokenFilter.sharedMesh != null
                ? brokenFilter.sharedMesh.vertexCount
                : 0;
            int triCount = brokenFilter != null && brokenFilter.sharedMesh != null
                ? brokenFilter.sharedMesh.triangles.Length / 3
                : 0;

            if (vertCount == 0)
            {
                return new RebuildResult
                {
                    Success = false,
                    Message = "重建后网格仍为空。请确认参考预制体 " + workingPath + " 正常。",
                };
            }

            PrefabUtility.SaveAsPrefabAsset(brokenRoot, brokenPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(brokenPath, ImportAssetOptions.ForceUpdate);

            return new RebuildResult
            {
                Success = true,
                VertexCount = vertCount,
                TriangleCount = triCount,
                Message = "已修复 " + brokenPath,
            };
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(brokenRoot);
            PrefabUtility.UnloadPrefabContents(workingRoot);
        }
    }

    static void BackupPrefabFile(string fullPrefabPath)
    {
        string backupPath = fullPrefabPath + ".bak_reimport_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        File.Copy(fullPrefabPath, backupPath, overwrite: false);
        Debug.Log("[GrassBundle2MeshRepair] Backup: " + backupPath);
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
