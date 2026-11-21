#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 图书馆 1/2/4 楼：从 Blender 源覆盖 .blend、强制 External 材质导入、重匹配 Materials/、从备份恢复 PrefabInstance。
/// 菜单：Tools/图书馆/1-2-4楼 从Blender重导并恢复场景布置
/// BatchMode: -executeMethod LibraryFloorsFullReimport.ExecuteBatch
/// </summary>
public static class LibraryFloorsFullReimport
{
    const string BackupDir = "Assets/code/Editor/library_reimport_backup";
    // 工程根目录（非 Assets），避免 Unity 导入 pending 模型
    const string PendingBlendDir = "_library_blend_pending";
    const string MaterialsFolder = "Assets/scene/Materials";

    static readonly FloorConfig[] Floors =
    {
        new FloorConfig(
            "Assets/scene/图书馆1楼.blend",
            "5e198423aa994ca4bace71cb259a7a4f",
            @"z:\微信\xwechat_files\wxid_iex8clusq8ee32_2373\msg\file\2026-05\图书馆1楼.blend",
            "Assets/scene/libraryfloor1.scene",
            "1508780668",
            null,
            LibraryFloor1DoubleSidedImport.ApplyImporterAndReimport,
            LibraryFloor1DoubleSidedImport.FixAllMaterialsAndRenderers),
        new FloorConfig(
            "Assets/scene/lib2f.blend",
            "7aa5c8e04a22f6c47918ec4d53021da8",
            @"z:\微信\xwechat_files\wxid_iex8clusq8ee32_2373\msg\file\2026-05\lib2f.blend",
            "Assets/scene/libraryfloor2.scene",
            "2099878117",
            "lib2f_外壳",
            () => LibraryFloor2DoubleSidedImport.ApplyImporterSettings(reimport: true),
            LibraryFloor2DoubleSidedImport.FixAllMaterialsAndRenderers),
        new FloorConfig(
            "Assets/scene/图书馆四楼.blend",
            "0fc0c835acb8857428e7673f5c1b0a62",
            @"z:\PHY1001 FINAL\图书馆四楼.blend",
            "Assets/scene/libraryfloor4.scene",
            "20395746",
            "图书馆四楼_外壳",
            () => LibraryFloor4DoubleSidedImport.ApplyImporterSettings(reimport: true),
            LibraryFloor4DoubleSidedImport.FixAllMaterialsAndRenderers),
    };

    readonly struct FloorConfig
    {
        public readonly string BlendAssetPath;
        public readonly string ModelGuid;
        public readonly string SourceBlendPath;
        public readonly string SceneAssetPath;
        public readonly string PrefabInstanceId;
        public readonly string ShellRootName;
        public readonly Action ApplyReimport;
        public readonly Func<int> FixMaterials;

        public FloorConfig(
            string blendAssetPath,
            string modelGuid,
            string sourceBlendPath,
            string sceneAssetPath,
            string prefabInstanceId,
            string shellRootName,
            Action applyReimport,
            Func<int> fixMaterials)
        {
            BlendAssetPath = blendAssetPath;
            ModelGuid = modelGuid;
            SourceBlendPath = sourceBlendPath;
            SceneAssetPath = sceneAssetPath;
            PrefabInstanceId = prefabInstanceId;
            ShellRootName = shellRootName;
            ApplyReimport = applyReimport;
            FixMaterials = fixMaterials;
        }
    }

    [MenuItem("Tools/图书馆/1-2-4楼 从Blender重导并恢复场景布置", false, 50)]
    public static void RunFromMenu() => RunFromMenuInternal(tryCopyBlendFromSource: true);

    [MenuItem("Tools/图书馆/仅 Reimport+材质+恢复布置（不复制 blend）", false, 51)]
    public static void RunReimportOnlyFromMenu() => RunFromMenuInternal(tryCopyBlendFromSource: false);

    [MenuItem("Tools/图书馆/将 pending 中的 blend 覆盖到 scene（需先关闭占用）", false, 52)]
    public static void ApplyPendingBlendsFromMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("图书馆重导", "请先退出 Play Mode。", "确定");
            return;
        }

        var results = ApplyAllPendingBlends();
        EditorUtility.DisplayDialog(
            "应用 pending blend",
            results.Count == 0 ? "没有待应用的 pending 文件。" : string.Join("\n", results),
            "确定");
        AssetDatabase.Refresh();
    }

    static void RunFromMenuInternal(bool tryCopyBlendFromSource)
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("图书馆重导", "请先退出 Play Mode。", "确定");
            return;
        }

        string copyLine = tryCopyBlendFromSource
            ? "1) 尝试用 Blender 源覆盖 .blend（被占用则写入 _library_blend_pending）\n"
            : "1) 跳过复制，仅处理工程内已有 .blend\n";

        if (!EditorUtility.DisplayDialog(
                "图书馆 1/2/4 楼重导",
                "将：\n" +
                copyLine +
                "2) External + 按材质名匹配 Assets/scene/Materials\n" +
                "3) Reimport 并重匹配材质\n" +
                "4) 恢复 PrefabInstance 布置\n\n" +
                "不修改 Plane / UI。继续？",
                "继续",
                "取消"))
            return;

        var copyNotes = RunInternal(tryCopyBlendFromSource);
        var sb = new StringBuilder("Reimport 与材质匹配已完成。\n请检查 libraryfloor1/2/4。\n");
        if (copyNotes.Count > 0)
        {
            sb.AppendLine("\n复制相关：");
            foreach (string note in copyNotes)
                sb.AppendLine("• " + note);
        }

        EditorUtility.DisplayDialog("图书馆重导", sb.ToString(), "确定");
    }

    /// <summary>BatchMode entry.</summary>
    public static void ExecuteBatch()
    {
        RunInternal(tryCopyBlendFromSource: true);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();
        EditorApplication.Exit(0);
    }

    static List<string> RunInternal(bool tryCopyBlendFromSource)
    {
        var copyNotes = new List<string>();
        try
        {
            LibraryFloorsSceneBackup.BackupAllScenes();
            Debug.Log("[LibraryFloors] 已完整备份 libraryfloor1/2/4.scene -> Assets/scene/Backups/");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[LibraryFloors] 完整场景备份失败（请手动 Tools/图书馆/备份三个场景）：" + ex.Message);
        }

        BackupAllPrefabBlocks();
        foreach (var floor in Floors)
            EnsureMetaGuid(floor.BlendAssetPath, floor.ModelGuid);

        var mapping = LibraryFloor2MaterialRemap.BuildGlobalMaterialMapping();
        if (mapping == null || mapping.Count == 0)
            throw new InvalidOperationException($"材质索引为空：{MaterialsFolder}");

        int totalRemapped = 0;
        foreach (var floor in Floors)
        {
            Debug.Log($"[LibraryFloors] === {floor.BlendAssetPath} ===");
            if (tryCopyBlendFromSource)
                copyNotes.AddRange(CopyBlendFromSource(floor));
            EnsureImporterMetaOnDisk(floor.BlendAssetPath);
            floor.ApplyReimport();
            totalRemapped += floor.FixMaterials();
            totalRemapped += RematchScene(floor, mapping);
        }

        RestoreAllPrefabBlocks();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[LibraryFloors] 全部完成，重匹配材质槽约 {totalRemapped} 次。");
        return copyNotes;
    }

    static void EnsureMetaGuid(string blendAssetPath, string expectedGuid)
    {
        string metaPath = ProjectPath(blendAssetPath + ".meta");
        if (!File.Exists(metaPath))
            return;

        string text;
        try
        {
            text = File.ReadAllText(metaPath);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LibraryFloors] 无法读取 meta: {metaPath}\n{ex.Message}");
            return;
        }
        var match = Regex.Match(text, @"^guid: (.+)$", RegexOptions.Multiline);
        if (!match.Success)
            return;

        string current = match.Groups[1].Value.Trim();
        bool guidOk = current == expectedGuid && Regex.IsMatch(current, @"^[0-9a-f]{32}$");
        if (guidOk && text.Contains("materialLocation: 1"))
            return;

        text = Regex.Replace(text, @"^guid: .+$", "guid: " + expectedGuid, RegexOptions.Multiline);
        if (!text.Contains("materialLocation: 1"))
        {
            text = Regex.Replace(
                text,
                @"  materials:\n    materialImportMode: \d+\n    materialName: \d+\n    materialSearch: \d+\n    materialLocation: \d+\n",
                "  materials:\n    materialImportMode: 1\n    materialName: 1\n    materialSearch: 2\n    materialLocation: 1\n");
        }

        try
        {
            File.WriteAllText(metaPath, text);
            Debug.Log($"[LibraryFloors] 已修复 meta GUID/importer: {blendAssetPath} ({current} -> {expectedGuid})");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LibraryFloors] 无法写入 meta: {metaPath}\n{ex.Message}");
        }
    }

    static string ProjectPath(string assetsRelativePath) =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetsRelativePath));

    static List<string> CopyBlendFromSource(FloorConfig floor)
    {
        var notes = new List<string>();
        string destFull = ProjectPath(floor.BlendAssetPath);
        string srcFull = Path.GetFullPath(floor.SourceBlendPath);
        if (!File.Exists(srcFull))
        {
            string msg = $"源不存在，跳过：{Path.GetFileName(srcFull)}";
            Debug.LogWarning("[LibraryFloors] " + msg);
            notes.Add(msg);
            return notes;
        }

        if (string.Equals(srcFull, destFull, StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log($"[LibraryFloors] 已是同源文件：{floor.BlendAssetPath}");
            notes.Add($"{Path.GetFileName(destFull)}：与源为同一文件，无需复制");
            return notes;
        }

        if (TryCopyFile(srcFull, destFull))
        {
            Debug.Log($"[LibraryFloors] 已复制 Blender 源 -> {floor.BlendAssetPath}");
            notes.Add($"{Path.GetFileName(destFull)}：已从源更新");
            return notes;
        }

        string pendingFull = Path.Combine(ProjectPath(PendingBlendDir), Path.GetFileName(destFull));
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(pendingFull) ?? ProjectPath(PendingBlendDir));
            if (TryCopyFile(srcFull, pendingFull))
            {
                string msg =
                    $"{Path.GetFileName(destFull)}：工程内文件被团结占用，已复制到 {PendingBlendDir}/。" +
                    "关闭编辑器后可用「将 pending 覆盖到 scene」，或手动覆盖原文件。";
                Debug.LogWarning("[LibraryFloors] " + msg);
                notes.Add(msg);
            }
            else
            {
                string msg =
                    $"{Path.GetFileName(destFull)}：无法覆盖（被占用）。将仅 Reimport 工程内旧文件。" +
                    $"可关闭团结后手动复制：\n{srcFull}\n->\n{destFull}";
                Debug.LogWarning("[LibraryFloors] " + msg);
                notes.Add(msg);
            }
        }
        catch (Exception ex)
        {
            string msg = $"{Path.GetFileName(destFull)}：复制失败（{ex.Message}），将仅 Reimport 现有文件。";
            Debug.LogWarning("[LibraryFloors] " + msg);
            notes.Add(msg);
        }

        return notes;
    }

    static bool TryCopyFile(string srcFull, string destFull)
    {
        try
        {
            File.Copy(srcFull, destFull, overwrite: true);
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.LogWarning($"[LibraryFloors] 无写入权限: {destFull}\n{ex.Message}");
            return false;
        }
        catch (IOException ex)
        {
            Debug.LogWarning($"[LibraryFloors] IO 错误: {destFull}\n{ex.Message}");
            return false;
        }
    }

    static List<string> ApplyAllPendingBlends()
    {
        var results = new List<string>();
        string pendingRoot = ProjectPath(PendingBlendDir);
        if (!Directory.Exists(pendingRoot))
        {
            results.Add("无 pending 目录。");
            return results;
        }

        foreach (var floor in Floors)
        {
            string fileName = Path.GetFileName(floor.BlendAssetPath);
            string pendingFull = Path.Combine(pendingRoot, fileName);
            string destFull = ProjectPath(floor.BlendAssetPath);
            if (!File.Exists(pendingFull))
                continue;

            if (TryCopyFile(pendingFull, destFull))
            {
                try
                {
                    File.Delete(pendingFull);
                }
                catch
                {
                    // ignore
                }

                AssetDatabase.ImportAsset(floor.BlendAssetPath, ImportAssetOptions.ForceUpdate);
                results.Add($"{fileName}：已从 pending 覆盖并 Reimport。");
            }
            else
            {
                results.Add($"{fileName}：仍无法写入（请先完全关闭团结/Unity 再点本菜单）。");
            }
        }

        return results;
    }

    static void EnsureImporterMetaOnDisk(string blendAssetPath)
    {
        var importer = AssetImporter.GetAtPath(blendAssetPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[LibraryFloors] 无 ModelImporter：{blendAssetPath}");
            return;
        }

        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.External;
        importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
        importer.materialSearch = ModelImporterMaterialSearch.Everywhere;
        importer.SaveAndReimport();
    }

    static int RematchScene(FloorConfig floor, System.Collections.Generic.Dictionary<string, Material> mapping)
    {
        if (!File.Exists(floor.SceneAssetPath))
            return 0;

        EditorSceneManager.OpenScene(floor.SceneAssetPath, OpenSceneMode.Single);
        var scene = SceneManager.GetActiveScene();
        int updated = 0;

        foreach (var root in scene.GetRootGameObjects())
        {
            if (!IsFloorModelRoot(root, floor))
                continue;

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || IsShellRenderer(renderer, floor.ShellRootName))
                    continue;
                updated += LibraryFloor2MaterialRemap.RemapRendererPublic(renderer, mapping);
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[LibraryFloors] {floor.SceneAssetPath} 重匹配 {updated} 个 Renderer");
        return updated;
    }

    static bool IsFloorModelRoot(GameObject go, FloorConfig floor)
    {
        if (go == null)
            return false;

        if (!PrefabUtility.IsPartOfPrefabInstance(go))
            return false;

        if (PrefabUtility.GetNearestPrefabInstanceRoot(go) != go)
            return false;

        string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
        return path == floor.BlendAssetPath;
    }

    static bool IsShellRenderer(Renderer renderer, string shellRootName)
    {
        if (renderer == null)
            return false;
        Transform t = renderer.transform;
        while (t != null)
        {
            if (!string.IsNullOrEmpty(shellRootName) && t.name == shellRootName)
                return true;
            if (t.name.StartsWith("Shell_"))
                return true;
            t = t.parent;
        }

        return false;
    }

    static void BackupAllPrefabBlocks()
    {
        foreach (var floor in Floors)
            BackupPrefabBlock(floor.SceneAssetPath, floor.PrefabInstanceId);
    }

    static void BackupPrefabBlock(string sceneAssetPath, string prefabInstanceId)
    {
        string sceneFull = ProjectPath(sceneAssetPath);
        if (!File.Exists(sceneFull))
            return;

        string text = File.ReadAllText(sceneFull);
        string pattern = $@"(--- !u!1001 &{prefabInstanceId}\nPrefabInstance:.*?)(?=\n--- !u!)";
        var match = System.Text.RegularExpressions.Regex.Match(text, pattern, System.Text.RegularExpressions.RegexOptions.Singleline);
        if (!match.Success)
        {
            Debug.LogWarning($"[LibraryFloors] 未找到 PrefabInstance &{prefabInstanceId} in {sceneAssetPath}");
            return;
        }

        string backupFull = ProjectPath(BackupDir);
        Directory.CreateDirectory(backupFull);
        string sceneStem = Path.GetFileNameWithoutExtension(sceneAssetPath);
        string outPath = Path.Combine(backupFull, $"{sceneStem}_prefab_{prefabInstanceId}.yaml");
        File.WriteAllText(outPath, match.Groups[1].Value);
        Debug.Log($"[LibraryFloors] 备份 PrefabInstance -> {outPath}");
    }

    static void RestoreAllPrefabBlocks()
    {
        foreach (var floor in Floors)
            RestorePrefabBlock(floor.SceneAssetPath, floor.PrefabInstanceId);
    }

    static void RestorePrefabBlock(string sceneAssetPath, string prefabInstanceId)
    {
        string sceneFull = ProjectPath(sceneAssetPath);
        string backupPath = Path.Combine(ProjectPath(BackupDir), $"{Path.GetFileNameWithoutExtension(sceneAssetPath)}_prefab_{prefabInstanceId}.yaml");
        if (!File.Exists(backupPath))
        {
            Debug.LogWarning($"[LibraryFloors] 无备份：{backupPath}");
            return;
        }

        string block = File.ReadAllText(backupPath);
        string text = File.ReadAllText(sceneFull);
        string pattern = $@"(--- !u!1001 &{prefabInstanceId}\nPrefabInstance:.*?)(?=\n--- !u!)";
        var match = System.Text.RegularExpressions.Regex.Match(text, pattern, System.Text.RegularExpressions.RegexOptions.Singleline);
        if (!match.Success)
        {
            Debug.LogWarning($"[LibraryFloors] 场景中无 PrefabInstance &{prefabInstanceId}");
            return;
        }

        File.WriteAllText(sceneFull, text.Remove(match.Index, match.Length).Insert(match.Index, block));
        Debug.Log($"[LibraryFloors] 已恢复 PrefabInstance &{prefabInstanceId} -> {sceneAssetPath}");
    }
}
#endif
