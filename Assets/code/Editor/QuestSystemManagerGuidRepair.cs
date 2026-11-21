#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 修复场景中误将 QuestSystemManager 序列化为 StoryQuestRuntime GUID 的问题。
/// </summary>
public static class QuestSystemManagerGuidRepair
{
    const string MenuPath = "Tools/Quest System/Repair QuestSystemManager Script GUIDs In Scenes";
    const string StoryRuntimeGuid = "cb49cc38d4a14b8a8ef5a2a9ee0d577c";
    const string QuestManagerGuid = "ec84147cd91d44b0b07ebe99bc45f0d7";

    static readonly string[] QuestManagerFieldHints =
    {
        "questChains:",
        "gameServerSync:",
        "autoCreateProgressWhenEmpty:",
        "autoCheckDistanceCompletion:",
        "enableLocalProgressSave:",
        "initialGold:",
        "verboseLog:"
    };

    [MenuItem(MenuPath, false, 12)]
    public static void RepairFromMenu()
    {
        int files = RepairAllSceneFilesUnderAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "QuestSystemManager GUID 修复",
            $"已扫描并修复 {files} 个场景/预制体文件。\n请重新打开场景并进入 Play 验证任务栏进度。",
            "确定");
    }

    public static int RepairAllSceneFilesUnderAssets()
    {
        string assets = Application.dataPath;
        int fixedFiles = 0;
        foreach (string path in Directory.EnumerateFiles(assets, "*.*", SearchOption.AllDirectories))
        {
            string ext = Path.GetExtension(path);
            if (ext != ".unity" && ext != ".scene" && ext != ".prefab")
                continue;
            if (path.Replace('\\', '/').Contains("/Backups/"))
                continue;

            if (RepairSceneYamlFile(path))
                fixedFiles++;
        }

        return fixedFiles;
    }

    public static bool RepairSceneYamlFile(string filePath)
    {
        string text = File.ReadAllText(filePath);
        string repaired = RepairYaml(text, out int blocks);
        if (blocks <= 0 || repaired == text)
            return false;

        File.WriteAllText(filePath, repaired);
        Debug.Log($"[QuestSystemManagerGuidRepair] {filePath}：修复 {blocks} 处脚本 GUID。");
        return true;
    }

    public static string RepairYaml(string yaml, out int fixedBlocks)
    {
        int blockCount = 0;
        var goNameById = BuildGameObjectNames(yaml);

        var pattern = new Regex(
            @"--- !u!114 &(\d+)\nMonoBehaviour:(.*?)(?=\n--- |\z)",
            RegexOptions.Singleline);

        string result = pattern.Replace(yaml, match =>
        {
            string block = match.Value;
            if (!block.Contains(StoryRuntimeGuid))
                return block;

            bool hasManagerFields = false;
            for (int i = 0; i < QuestManagerFieldHints.Length; i++)
            {
                if (block.Contains(QuestManagerFieldHints[i]))
                {
                    hasManagerFields = true;
                    break;
                }
            }

            bool namedManager = false;
            var goMatch = Regex.Match(block, @"m_GameObject: \{fileID: (\d+)\}");
            if (goMatch.Success && goNameById.TryGetValue(goMatch.Groups[1].Value, out string goName))
                namedManager = string.Equals(goName, "QuestSystemManager", System.StringComparison.Ordinal);

            if (!hasManagerFields && !namedManager)
                return block;

            blockCount++;
            return block.Replace(StoryRuntimeGuid, QuestManagerGuid);
        });

        fixedBlocks = blockCount;
        return result;
    }

    static System.Collections.Generic.Dictionary<string, string> BuildGameObjectNames(string yaml)
    {
        var names = new System.Collections.Generic.Dictionary<string, string>();
        string currentId = null;
        var lines = yaml.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.StartsWith("--- !u!1 &", System.StringComparison.Ordinal))
            {
                currentId = line.Substring("--- !u!1 &".Length).Trim();
                continue;
            }

            if (currentId == null)
                continue;

            string trimmed = line.TrimStart();
            if (!trimmed.StartsWith("m_Name:", System.StringComparison.Ordinal))
                continue;

            names[currentId] = trimmed.Substring("m_Name:".Length).Trim();
            currentId = null;
        }

        return names;
    }
}
#endif
