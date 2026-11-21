#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using GameQuestSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Content1StoryImporterMenu
{
    const string ContentFolder = "Assets/content1";
    public const string DatabasePath = "Assets/code/GameQuestSystem/Content1StoryDatabase.asset";
    const string StoryNpcFolder = "Assets/NPCs/Story";
    const string StoryQuestPrefix = "content1_";
    /// <summary>computerroom 场景 M1 选课/机房剧情锚点（与场景内机位对齐）。</summary>
    static readonly Vector3 ComputerRoomStoryPosition = new Vector3(-3.91f, 0.95f, -23.67f);
    static readonly Vector3 TeachingRoomAPosition = new Vector3(9.05f, 2.19f, 5.99f);
    static readonly Vector3 ClubClassroom1Position = new Vector3(-0.79f, 3.8f, -0.41f);
    static readonly Vector3 SeminarLearningRoomPosition = new Vector3(24.9084282f, 0.656000018f, -37.0740013f);
    static readonly Vector3 AiOfficePosition = new Vector3(-1474.88f, 15.3999996f, 1224.68005f);
    static readonly Vector3 AuditoriumPosition = new Vector3(-1744.19995f, 13.6999998f, 955.830017f);
    static readonly Vector3 CompetitionStoryPosition = new Vector3(-1674.54004f, 14.4899998f, 1070.95996f);
    static readonly Vector3 YifuConferenceFrontPosition = new Vector3(-1575.22998f, 23.5100002f, 949.070007f);
    static readonly Vector3 ConvenienceStorePosition = new Vector3(-922.400024f, 4.30999994f, 1190.52002f);
    static readonly Vector3 FlowerBedCenterPosition = new Vector3(-1359.56006f, 14.5799999f, 1075.28003f);
    static readonly Vector3 FlagpolePosition = new Vector3(-1359.56006f, 13.7399998f, 1109.88f);
    static readonly Vector3 CampusGatePosition = new Vector3(-1359.56006f, 4.1500001f, 1320.68005f);
    static readonly Vector3 LibraryPathPosition = new Vector3(-1183.68005f, 16.6900005f, 878.349976f);
    static readonly Vector3 LibraryFloor4SeminarPosition = new Vector3(-145.479996f, 1.63999999f, 88.0016403f);
    static readonly Vector3 LibraryFloor4StoryPosition = new Vector3(-68.1900024f, 1.63999999f, 439.160004f);
    static readonly Vector3 LibraryExitPosition = new Vector3(-1103.19995f, 13.7299995f, 944.960022f);
    static readonly Vector3 LibraryRooftopPosition = new Vector3(-1033.55005f, 29.9699993f, 842.419983f);
    static readonly Vector3 LibraryPoolPosition = new Vector3(-888.5f, 14.25f, 898.02002f);
    const string SeminarSceneName = "learning room";
    const string ClubClassroomSceneName = "classroom1";
    const string TeachingRoomASceneName = "teachingroomA";
    const string LibraryFloor4SceneName = "libraryfloor4";
    static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    [MenuItem("Tools/Quest System/导入 content1 剧情任务", false, 60)]
    public static void ImportContent1Story()
    {
        if (!AssetDatabase.IsValidFolder(ContentFolder))
        {
            EditorUtility.DisplayDialog("content1 剧情导入", "未找到 Assets/content1。", "确定");
            return;
        }

        EnsureFolder("Assets/NPCs");
        EnsureFolder(StoryNpcFolder);

        var documents = ParseDocuments();
        if (documents.Count == 0)
        {
            EditorUtility.DisplayDialog("content1 剧情导入", "Assets/content1 下没有可解析的 docx。", "确定");
            return;
        }

        InferChoiceTargets(documents);
        ApplyDocumentRuntimeFields(documents);

        var database = LoadOrCreateDatabase();
        database.sourceFolder = ContentFolder;
        database.generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        database.locations = BuildLocations();
        database.npcs = BuildNpcs(documents, database.locations);
        database.documents = documents.OrderBy(d => NaturalSortKey(d.documentId)).ToList();
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();

        CreateNpcPrefabs(database);
        EnsureStoryRuntime(database);
        ApplyQuestChainsToManager(database);
        PlaceRootStoryNpcsInActiveScene(database);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("[Content1Story] 已导入剧情文档 " + database.documents.Count + " 个，根任务 " +
                  database.documents.Count(d => d.isRootTask) + " 个。");
        EditorUtility.DisplayDialog("content1 剧情导入", "导入完成。请保存场景，并运行检查 M1-M5 流程。", "确定");
    }

    static List<StoryDialogueDocument> ParseDocuments()
    {
        var resultById = new Dictionary<string, StoryDialogueDocument>(StringComparer.OrdinalIgnoreCase);
        string fullRoot = Path.GetFullPath(ContentFolder);
        foreach (string assetPath in AssetDatabase.FindAssets("t:DefaultAsset", new[] { ContentFolder })
                     .Select(AssetDatabase.GUIDToAssetPath)
                     .Where(p => p.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)))
        {
            var doc = ParseDocx(assetPath);
            if (doc == null)
                continue;

            if (resultById.TryGetValue(doc.documentId, out var existing))
            {
                if (doc.pages.Count > existing.pages.Count)
                    resultById[doc.documentId] = doc;
                continue;
            }

            resultById.Add(doc.documentId, doc);
        }

        return resultById.Values.ToList();
    }

    static StoryDialogueDocument ParseDocx(string assetPath)
    {
        string fullPath = Path.GetFullPath(assetPath);
        List<string> paragraphs;
        try
        {
            paragraphs = ExtractParagraphs(fullPath);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Content1Story] 解析失败: " + assetPath + "\n" + ex.Message);
            return null;
        }

        if (paragraphs.Count == 0)
            return null;

        string rawId = Path.GetFileNameWithoutExtension(assetPath);
        string documentId = NormalizeDocumentId(rawId);
        string chapter = documentId.Split('-')[0];
        var doc = new StoryDialogueDocument
        {
            documentId = documentId,
            title = documentId,
            sourceAssetPath = assetPath.Replace('\\', '/'),
            chapterId = chapter,
            category = IsMainChapter(chapter) ? QuestCategory.Main : QuestCategory.Side,
            isBranchDocument = IsBranchDocumentId(documentId),
            isRootTask = IsRootDocumentId(documentId),
            completeEventId = BuildEventId(documentId),
            pages = new List<StoryDialoguePage>(),
            choices = new List<StoryDialogueChoice>()
        };

        ParsePagesAndChoices(paragraphs, doc);
        return doc;
    }

    static List<string> ExtractParagraphs(string fullPath)
    {
        var paragraphs = new List<string>();
        using (var archive = ZipFile.OpenRead(fullPath))
        {
            var entry = archive.GetEntry("word/document.xml");
            if (entry == null)
                return paragraphs;

            using (var stream = entry.Open())
            {
                var xml = XDocument.Load(stream);
                foreach (var p in xml.Descendants(W + "p"))
                {
                    string text = string.Concat(p.Descendants(W + "t").Select(t => (string)t));
                    text = text.Trim();
                    if (!string.IsNullOrEmpty(text))
                        paragraphs.Add(text);
                }
            }
        }

        return paragraphs;
    }

    static void ParsePagesAndChoices(List<string> paragraphs, StoryDialogueDocument doc)
    {
        int currentPage = 0;
        bool readingChoices = false;
        for (int i = 0; i < paragraphs.Count; i++)
        {
            string line = NormalizeStoryText(paragraphs[i]);
            if (string.IsNullOrWhiteSpace(line))
                continue;
            if (line.StartsWith("资源:", StringComparison.Ordinal) || line == "正文（分页）" ||
                line.StartsWith("节点", StringComparison.Ordinal) || line.StartsWith("choices:", StringComparison.Ordinal))
            {
                readingChoices = false;
                continue;
            }
            if (line == doc.documentId || line == Path.GetFileNameWithoutExtension(doc.sourceAssetPath))
                continue;
            if (line == "分支选项")
            {
                readingChoices = true;
                continue;
            }
            if (readingChoices)
            {
                doc.choices.Add(new StoryDialogueChoice { label = line, targetDocumentId = "" });
                continue;
            }

            var pageMatch = Regex.Match(line, @"^第\s*(\d+)\s*页$");
            if (pageMatch.Success)
            {
                int.TryParse(pageMatch.Groups[1].Value, out currentPage);
                continue;
            }

            if (currentPage <= 0)
                currentPage = doc.pages.Count + 1;

            SplitSpeaker(line, out var speaker, out var text);
            doc.pages.Add(new StoryDialoguePage
            {
                pageNumber = currentPage,
                speakerName = speaker,
                text = text
            });
            currentPage = 0;
        }
    }

    static void InferChoiceTargets(List<StoryDialogueDocument> documents)
    {
        var ids = new HashSet<string>(documents.Select(d => d.documentId), StringComparer.OrdinalIgnoreCase);
        foreach (var doc in documents)
        {
            if (doc.choices == null || doc.choices.Count == 0)
                continue;

            var targets = FindCandidateTargets(doc.documentId, doc.choices.Count, ids);
            for (int i = 0; i < doc.choices.Count && i < targets.Count; i++)
                doc.choices[i].targetDocumentId = targets[i];
        }
    }

    static List<string> FindCandidateTargets(string documentId, int count, HashSet<string> ids)
    {
        var result = new List<string>();
        AddManualChoiceTargets(documentId, count, ids, result);
        if (result.Count >= count)
            return result.Take(count).ToList();

        // 部分文件（如 M4-1-2）把分支写成同一节点的 a/b/c 后缀，而不是下一编号。
        result.AddRange(ids.Where(id => IsBranchOfPrefix(id, documentId) && !result.Contains(id, StringComparer.OrdinalIgnoreCase))
            .OrderBy(NaturalSortKey)
            .Take(count - result.Count));
        if (result.Count >= count)
            return result.Take(count).ToList();

        var parts = documentId.Split('-');
        if (parts.Length < 3)
            return result;

        string last = parts[parts.Length - 1];
        var numberMatch = Regex.Match(last, @"^(\d+)");
        if (!numberMatch.Success)
            return result;

        int number = int.Parse(numberMatch.Groups[1].Value) + 1;
        string prefix = string.Join("-", parts.Take(parts.Length - 1)) + "-" + number;
        result.AddRange(ids.Where(id => IsBranchOfPrefix(id, prefix) && !result.Contains(id, StringComparer.OrdinalIgnoreCase))
            .OrderBy(NaturalSortKey)
            .Take(count - result.Count));
        return result.Take(count).ToList();
    }

    static void AddManualChoiceTargets(string documentId, int count, HashSet<string> ids, List<string> result)
    {
        string[] manual = null;
        switch (documentId)
        {
            case "M4-1-2":
                manual = new[] { "M4-1-2a", "M4-1-2b", "M4-1-2c" };
                break;
            case "M4-4-3b":
                manual = new[] { "M4-4-4a", "M4-4-4b" };
                break;
        }

        if (manual == null)
            return;
        for (int i = 0; i < manual.Length && result.Count < count; i++)
        {
            if (ids.Contains(manual[i]))
                result.Add(manual[i]);
        }
    }

    static bool IsBranchOfPrefix(string id, string prefix)
    {
        if (!id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;
        if (id.Length <= prefix.Length)
            return false;
        char c = id[prefix.Length];
        return char.IsLetter(c) || c == '-';
    }

    static void ApplyDocumentRuntimeFields(List<StoryDialogueDocument> documents)
    {
        foreach (var doc in documents)
        {
            var loc = DetermineLocation(doc);
            doc.locationId = loc.locationId;
            doc.targetSceneName = loc.sceneName;
            doc.fallbackTargetPosition = loc.position;
            doc.finishLocationName = loc.displayName;
            doc.needsLocationConfirmation = loc.needsConfirmation;
            doc.npcId = DetermineNpcId(doc);
            doc.rewardGold = DetermineRewardGold(doc);
        }
    }

    public static StoryLocationDefinition ResolveStoryTaskLocation(string documentId)
    {
        return DetermineLocation(new StoryDialogueDocument
        {
            documentId = documentId ?? "",
            pages = new List<StoryDialoguePage>()
        });
    }

    public static StoryLocationDefinition ResolveStoryTaskLocation(StoryDialogueDocument doc)
    {
        if (doc == null || string.IsNullOrWhiteSpace(doc.documentId))
            return null;
        if (doc.pages != null && doc.pages.Count > 0)
            return DetermineLocation(doc);
        return ResolveStoryTaskLocation(doc.documentId);
    }

    static StoryLocationDefinition DetermineLocation(StoryDialogueDocument doc)
    {
        string id = doc.documentId;
        string text = string.Join("\n", doc.pages.Select(p => p.text));

        if (id == "M0-1-1")
            return KnownLocation("m0_gate", "校门", "undergarden", new Vector3(-1373f, 3.16f, 1329f), false);
        if (id == "M0-1-3" || id == "M0-3-1")
            return KnownLocation("m0_dorm_uppergarden", "上园宿舍区", "uppergarden", new Vector3(350.13f, 44.51f, -625.67f), true);
        if (id == "M0-2-1")
            return KnownLocation("m0_library_reading", "图书馆公共阅读区", "undergarden", new Vector3(-1014.69f, 14.2200003f, 979.669983f), false);

        if (LibraryFloor1StoryLocations.TryGetSpot(id, out var librarySpot))
            return LibraryFloor1StoryLocations.ToDefinition(librarySpot);

        if (ComputerRoomStoryLocations.TryGetEntry(id, out var computerRoomEntry))
            return ComputerRoomStoryLocations.ToDefinition(computerRoomEntry);

        if (UndergardenStoryLocations.TryGetEntry(id, out var undergardenEntry))
            return UndergardenStoryLocations.ToDefinition(undergardenEntry);

        var explicitLocation = DetermineExplicitM1ToM5Location(doc, text);
        if (explicitLocation != null)
            return explicitLocation;

        if (text.Contains("图书馆"))
            return KnownLocation("library_placeholder", "图书馆", "library", Vector3.zero, true);
        if (text.Contains("研讨室"))
            return SeminarRoomLocation();
        if (text.Contains("电脑") || text.Contains("机房"))
            return KnownLocation("computerroom_placeholder", "机房", "computerroom", ComputerRoomStoryPosition, true);
        if (text.Contains("宿舍") || text.Contains("寝室"))
            return KnownLocation("dorm_placeholder", "宿舍区", "uppergarden", new Vector3(350.13f, 44.51f, -625.67f), true);
        if (text.Contains("行政") || text.Contains("系楼") || text.Contains("办公楼"))
            return KnownLocation("admin_placeholder", "行政楼", "undergarden", new Vector3(1310.01001f, 16.1900005f, 987.219971f), true);
        if (text.Contains("教室"))
            return ClassroomLocation();
        if (text.Contains("操场"))
            return KnownLocation("playground_placeholder", "操场", "undergarden", Vector3.zero, true);

        return KnownLocation("story_placeholder", "待确认剧情地点", "undergarden", new Vector3(-1359.02002f, 16.1900005f, 1085.17004f), true);
    }

    static StoryLocationDefinition DetermineExplicitM1ToM5Location(StoryDialogueDocument doc, string text)
    {
        string id = doc.documentId;

        if (id.StartsWith("M1-", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(id, "M1-3-3", StringComparison.OrdinalIgnoreCase))
                return KnownLocation("library_path_m1_3_3", "图书馆外小道", "undergarden", LibraryPathPosition, false);
            if (text.Contains("选课") || text.Contains("电脑") || text.Contains("系统"))
                return KnownLocation("computerroom_placeholder", "机房/选课系统", "computerroom", ComputerRoomStoryPosition, true);
            if (text.Contains("旧图书馆") || text.Contains("图书馆"))
                return KnownLocation("library_placeholder", "图书馆", "library", new Vector3(0f, 1f, 0f), true);
            if (text.Contains("宿舍") || text.Contains("书院"))
                return KnownLocation("dorm_placeholder", "上园宿舍区", "uppergarden", new Vector3(350.13f, 44.51f, -625.67f), true);
            return KnownLocation("m0_longxiang", "龙翔大道", "undergarden", new Vector3(-1359.02002f, 16.1900005f, 1085.17004f), false);
        }

        if (id.StartsWith("M2-", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(id, "M2-2-1沈", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(id, "M2-2-3沈", StringComparison.OrdinalIgnoreCase))
                return KnownLocation("libraryfloor4_seminar", "四楼研讨区", LibraryFloor4SceneName, LibraryFloor4SeminarPosition, false);
            if (string.Equals(id, "M2-3-1", StringComparison.OrdinalIgnoreCase))
                return KnownLocation("libraryfloor4_m2_3_1", "图书馆四楼", LibraryFloor4SceneName, LibraryFloor4StoryPosition, false);
            if (string.Equals(id, "M2-4-1沈", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(id, "M2-5-1", StringComparison.OrdinalIgnoreCase))
                return KnownLocation("library_exit_path", "图书馆外小道", "undergarden", LibraryExitPosition, false);
            if (text.Contains("TA3") || text.Contains("楼梯间"))
                return KnownLocation("ta3_stair_placeholder", "TA3楼梯间", TeachingRoomASceneName, TeachingRoomAPosition, false);
            if (text.Contains("图书馆"))
                return KnownLocation("library_5f_placeholder", "图书馆五楼", "library", new Vector3(0f, 1f, 0f), true);
            if (text.Contains("深圳湾") || text.Contains("中环") || text.Contains("餐厅露台") || text.Contains("跨境"))
                return KnownLocation("offcampus_placeholder", "校外剧情触发点", "undergarden", new Vector3(-1359.02002f, 16.1900005f, 1085.17004f), true);
            return KnownLocation("m0_library_reading", "图书馆公共阅读区", "undergarden", new Vector3(-1014.69f, 14.2200003f, 979.669983f), false);
        }

        if (id.StartsWith("M3-", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(id, "M3-2-1R", StringComparison.OrdinalIgnoreCase))
                return SeminarRoomLocation();
            if (text.Contains("研讨室"))
                return SeminarRoomLocation();
            if (text.Contains("宿舍"))
                return KnownLocation("dorm_placeholder", "上园宿舍区", "uppergarden", new Vector3(350.13f, 44.51f, -625.67f), true);
            if (text.Contains("社团") || text.Contains("预订的教室"))
                return ClubClassroomLocation();
            if (text.Contains("书院休息室"))
                return KnownLocation("college_lounge_placeholder", "书院休息室", "uppergarden", new Vector3(350.13f, 44.51f, -625.67f), true);
            return ClassroomLocation();
        }

        if (id.StartsWith("M4-", StringComparison.OrdinalIgnoreCase))
        {
            if (id == "M4-1-1" || id == "M4-1-2")
                return SeminarRoomLocation();
            if (id == "M4-2-1" || id == "M4-2-2" || id == "M4-4-1" || id == "M4-4-5")
                return AiOfficeLocation();
            if (id == "M4-3-1")
                return KnownLocation("library_rooftop_m4_3_1", "图书馆五楼露台", "undergarden", LibraryRooftopPosition, false);
            if (id == "M4-3-3")
                return AuditoriumLocation();
            if (id == "M4-4-3")
                return CompetitionStoryLocation();
            if (text.Contains("研讨室"))
                return SeminarRoomLocation();
            if (text.Contains("行政楼 304") || text.Contains("行政楼304"))
                return TxcAdminLocation();
            if (text.Contains("人工智能系办公楼") || text.Contains("院办") || text.Contains("导师") || text.Contains("教授办公室"))
                return AiOfficeLocation();
            if (text.Contains("图书馆五楼露台"))
                return KnownLocation("library_rooftop_placeholder", "图书馆五楼露台", "library", new Vector3(0f, 1f, 0f), true);
            if (text.Contains("学校大礼堂外") || text.Contains("大礼堂"))
                return AuditoriumLocation();
            if (text.Contains("操场"))
                return KnownLocation("playground_placeholder", "操场", "undergarden", new Vector3(0f, 1f, 0f), true);
            return CompetitionStoryLocation();
        }

        if (id.StartsWith("M5-", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(id, "M5-3-1", StringComparison.OrdinalIgnoreCase))
                return KnownLocation("m5_flower_bed", "花坛中心", "undergarden", FlowerBedCenterPosition, false);
            if (string.Equals(id, "M5-4-1", StringComparison.OrdinalIgnoreCase))
                return KnownLocation("m5_flagpole", "国旗旗杆下", "undergarden", FlagpolePosition, false);
            if (string.Equals(id, "M5-5-1-A", StringComparison.OrdinalIgnoreCase))
                return KnownLocation("library_pool_m5_5_1_a", "图书馆边水池", "undergarden", LibraryPoolPosition, false);
            if (string.Equals(id, "M5-2-1", StringComparison.OrdinalIgnoreCase))
                return ConvenienceStoreLocation();
            if (string.Equals(id, "M5-1-1", StringComparison.OrdinalIgnoreCase))
                return YifuConferenceFrontLocation();
            if (text.Contains("便利店"))
                return ConvenienceStoreLocation();
            if (text.Contains("行政大楼") || text.Contains("顶层") || text.Contains("露台") || text.Contains("逸夫"))
                return YifuConferenceFrontLocation();
            if (text.Contains("钟") || text.Contains("楼梯间"))
                return KnownLocation("clocktower_placeholder", "薯塔/楼梯间", "undergarden", new Vector3(-1359.02002f, 16.1900005f, 1085.17004f), true);
            if (text.Contains("图书馆") || text.Contains("喷泉"))
                return KnownLocation("library_fountain_placeholder", "图书馆露台/喷泉池", "library", new Vector3(0f, 1f, 0f), true);
            if (text.Contains("花坛"))
                return KnownLocation("m5_flower_bed", "花坛中心", "undergarden", FlowerBedCenterPosition, false);
            if (text.Contains("国旗") || text.Contains("旗杆"))
                return KnownLocation("m5_flagpole", "国旗旗杆下", "undergarden", FlagpolePosition, false);
            if (text.Contains("校门") || text.Contains("大门"))
                return KnownLocation("m5_campus_gate", "校园大门", "undergarden", CampusGatePosition, false);
            return KnownLocation("m5_final_placeholder", "终章剧情触发点", "undergarden", CampusGatePosition, false);
        }

        return null;
    }

    static int DetermineRewardGold(StoryDialogueDocument doc)
    {
        if (doc == null)
            return 0;
        if (doc.category == QuestCategory.Main)
            return doc.chapterId == "M5" ? 40 : 25;
        if (doc.chapterId == "M4")
            return 80;
        return 65;
    }

    static StoryLocationDefinition KnownLocation(string id, string name, string scene, Vector3 pos, bool needsConfirmation)
    {
        return new StoryLocationDefinition
        {
            locationId = id,
            displayName = name,
            sceneName = scene,
            position = pos,
            needsConfirmation = needsConfirmation
        };
    }

    static StoryLocationDefinition SeminarRoomLocation()
    {
        return KnownLocation("seminar_room_placeholder", "TXC行政楼/研讨室", SeminarSceneName, SeminarLearningRoomPosition, false);
    }

    static StoryLocationDefinition TxcAdminLocation()
    {
        return KnownLocation("txc_downstairs_placeholder", "TXC行政楼", SeminarSceneName, SeminarLearningRoomPosition, false);
    }

    static StoryLocationDefinition AiOfficeLocation()
    {
        return KnownLocation("ai_office_placeholder", "人工智能系办公楼", "undergarden", AiOfficePosition, false);
    }

    static StoryLocationDefinition AuditoriumLocation()
    {
        return KnownLocation("auditorium_placeholder", "学校大礼堂外", "undergarden", AuditoriumPosition, false);
    }

    static StoryLocationDefinition CompetitionStoryLocation()
    {
        return KnownLocation("admin_placeholder", "竞赛剧情点", "undergarden", CompetitionStoryPosition, false);
    }

    static StoryLocationDefinition YifuConferenceFrontLocation()
    {
        return KnownLocation("admin_rooftop_placeholder", "逸夫国际会议中心前", "undergarden", YifuConferenceFrontPosition, false);
    }

    static StoryLocationDefinition ConvenienceStoreLocation()
    {
        return KnownLocation("convenience_store_placeholder", "便利店", "undergarden", ConvenienceStorePosition, false);
    }

    static StoryLocationDefinition ClassroomLocation()
    {
        return KnownLocation("classroom_placeholder", "教室", TeachingRoomASceneName, TeachingRoomAPosition, false);
    }

    static StoryLocationDefinition ClubClassroomLocation()
    {
        return KnownLocation("club_classroom_placeholder", "社团教室", ClubClassroomSceneName, ClubClassroom1Position, false);
    }

    static List<StoryLocationDefinition> BuildLocations()
    {
        var locations = new List<StoryLocationDefinition>
        {
            KnownLocation("m0_gate", "校门", "undergarden", new Vector3(-1373f, 3.16f, 1329f), false),
            KnownLocation("m0_welcome", "迎新点/报到领卡", "undergarden", new Vector3(-939.32f, 13.75f, 1166.37f), false),
            KnownLocation("m0_gym", "体育馆", "undergarden", new Vector3(35.8f, 4.4f, 1103.65f), false),
            KnownLocation("m0_longxiang", "龙翔大道", "undergarden", new Vector3(-1359.02002f, 16.1900005f, 1085.17004f), false),
            KnownLocation("m0_admin", "行政楼", "undergarden", new Vector3(1310.01001f, 16.1900005f, 987.219971f), false),
            KnownLocation("m0_archive", "档案室", "computerroom", Vector3.zero, true),
            KnownLocation("m0_library_reading", "图书馆公共阅读区", "undergarden", new Vector3(-1014.69f, 14.2200003f, 979.669983f), false),
            KnownLocation("m0_dorm_uppergarden", "上园宿舍区", "uppergarden", new Vector3(350.13f, 44.51f, -625.67f), true),
            KnownLocation("library_path_m1_3_3", "图书馆外小道", "undergarden", LibraryPathPosition, false),
            KnownLocation("libraryfloor4_seminar", "四楼研讨区", LibraryFloor4SceneName, LibraryFloor4SeminarPosition, false),
            KnownLocation("libraryfloor4_m2_3_1", "图书馆四楼", LibraryFloor4SceneName, LibraryFloor4StoryPosition, false),
            KnownLocation("library_exit_path", "图书馆外小道", "undergarden", LibraryExitPosition, false),
            KnownLocation("library_rooftop_m4_3_1", "图书馆五楼露台", "undergarden", LibraryRooftopPosition, false),
            KnownLocation("library_pool_m5_5_1_a", "图书馆边水池", "undergarden", LibraryPoolPosition, false),
            KnownLocation("library_placeholder", "图书馆", "library", Vector3.zero, true),
            KnownLocation("library_5f_placeholder", "图书馆五楼", "library", new Vector3(0f, 1f, 0f), true),
            KnownLocation("library_rooftop_placeholder", "图书馆五楼露台", "library", new Vector3(0f, 1f, 0f), true),
            KnownLocation("library_fountain_placeholder", "图书馆露台/喷泉池", "library", new Vector3(0f, 1f, 0f), true),
            LibraryFloor1StoryLocations.ToDefinition(LibraryFloor1StoryLocations.M1_3_1),
            LibraryFloor1StoryLocations.ToDefinition(LibraryFloor1StoryLocations.M2_0_1),
            LibraryFloor1StoryLocations.ToDefinition(LibraryFloor1StoryLocations.M2_2_1),
            SeminarRoomLocation(),
            KnownLocation("computerroom_placeholder", "机房", "computerroom", ComputerRoomStoryPosition, true),
            KnownLocation("ta3_stair_placeholder", "TA3楼梯间", TeachingRoomASceneName, TeachingRoomAPosition, false),
            KnownLocation("dorm_placeholder", "宿舍区", "uppergarden", new Vector3(350.13f, 44.51f, -625.67f), true),
            KnownLocation("admin_placeholder", "竞赛剧情点", "undergarden", CompetitionStoryPosition, false),
            TxcAdminLocation(),
            AiOfficeLocation(),
            ClassroomLocation(),
            ClubClassroomLocation(),
            KnownLocation("college_lounge_placeholder", "书院休息室", "uppergarden", new Vector3(350.13f, 44.51f, -625.67f), true),
            KnownLocation("playground_placeholder", "操场", "undergarden", Vector3.zero, true),
            AuditoriumLocation(),
            KnownLocation("offcampus_placeholder", "校外剧情触发点", "undergarden", new Vector3(-1359.02002f, 16.1900005f, 1085.17004f), true),
            ConvenienceStoreLocation(),
            YifuConferenceFrontLocation(),
            KnownLocation("clocktower_placeholder", "薯塔/楼梯间", "undergarden", new Vector3(-1359.02002f, 16.1900005f, 1085.17004f), true),
            KnownLocation("m5_flower_bed", "花坛中心", "undergarden", FlowerBedCenterPosition, false),
            KnownLocation("m5_flagpole", "国旗旗杆下", "undergarden", FlagpolePosition, false),
            KnownLocation("m5_campus_gate", "校园大门", "undergarden", CampusGatePosition, false),
            KnownLocation("m5_final_placeholder", "终章剧情触发点", "undergarden", CampusGatePosition, false),
            KnownLocation("story_placeholder", "待确认剧情地点", "undergarden", new Vector3(-1359.02002f, 16.1900005f, 1085.17004f), true)
        };
        return locations;
    }

    static string DetermineNpcId(StoryDialogueDocument doc)
    {
        var counter = new Dictionary<string, int>();
        foreach (var page in doc.pages)
        {
            if (string.IsNullOrWhiteSpace(page.speakerName))
                continue;
            string speaker = page.speakerName.Trim();
            if (IsIgnoredSpeakerName(speaker))
                continue;
            counter.TryGetValue(speaker, out int count);
            counter[speaker] = count + 1;
        }

        if (counter.Count == 0)
            return "story_trigger";
        string name = counter.OrderByDescending(kv => kv.Value).First().Key;
        return "npc_" + SanitizeId(name);
    }

    static bool IsIgnoredSpeakerName(string speaker)
    {
        if (string.IsNullOrWhiteSpace(speaker))
            return true;
        if (speaker == "林渡" || speaker == "旁白" || speaker == "choices" || speaker == "倾向" || speaker == "结果")
            return true;
        if (speaker.Contains("系统") || speaker.StartsWith("【", StringComparison.Ordinal))
            return true;
        if (speaker.Contains("/") || speaker.Contains("，") || speaker.Contains("。") || speaker.Contains("（") || speaker.Contains("“"))
            return true;
        if (speaker.IndexOfAny(new[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' }) >= 0)
            return true;
        return speaker.Length > 8;
    }

    static List<StoryNpcDefinition> BuildNpcs(List<StoryDialogueDocument> documents, List<StoryLocationDefinition> locations)
    {
        var result = new Dictionary<string, StoryNpcDefinition>(StringComparer.OrdinalIgnoreCase);
        var storyTrigger = new StoryNpcDefinition
        {
            npcId = "story_trigger",
            displayName = "剧情触发点",
            prefabPath = StoryNpcFolder + "/剧情触发点.prefab",
            defaultSceneName = "undergarden",
            defaultPosition = new Vector3(-1359.02002f, 16.1900005f, 1085.17004f),
            needsPositionConfirmation = true
        };
        StoryNpcPrefabSyncMenu.AssignPrefabFromPath(storyTrigger);
        result["story_trigger"] = storyTrigger;

        foreach (var doc in documents)
        {
            if (result.ContainsKey(doc.npcId))
                continue;

            string displayName = ResolveNpcDisplayName(doc);
            var loc = locations.FirstOrDefault(l => l.locationId == doc.locationId);
            var npcDef = new StoryNpcDefinition
            {
                npcId = doc.npcId,
                displayName = displayName,
                prefabPath = StoryNpcFolder + "/" + SanitizeAssetName(displayName) + ".prefab",
                defaultSceneName = loc != null ? loc.sceneName : "undergarden",
                defaultPosition = loc != null ? loc.position : Vector3.zero,
                needsPositionConfirmation = doc.needsLocationConfirmation
            };
            StoryNpcPrefabSyncMenu.AssignPrefabFromPath(npcDef);
            result.Add(doc.npcId, npcDef);
        }

        return result.Values.OrderBy(n => n.displayName).ToList();
    }

    static string ResolveNpcDisplayName(StoryDialogueDocument doc)
    {
        if (doc.npcId == "story_trigger")
            return "剧情触发点";

        foreach (var page in doc.pages)
        {
            if (!string.IsNullOrWhiteSpace(page.speakerName) && ("npc_" + SanitizeId(page.speakerName)) == doc.npcId)
                return page.speakerName;
        }
        return doc.npcId;
    }

    static void CreateNpcPrefabs(StoryDialogueDatabase database)
    {
        foreach (var npc in database.npcs)
        {
            if (npc == null || string.IsNullOrWhiteSpace(npc.prefabPath))
                continue;
            if (File.Exists(npc.prefabPath))
                continue;

            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = npc.displayName;
            var storyNpc = go.AddComponent<StoryQuestNpc>();
            storyNpc.displayName = npc.displayName;
            storyNpc.hideAfterStoryCompleted = true;
            PrefabUtility.SaveAsPrefabAsset(go, npc.prefabPath);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    static void EnsureStoryRuntime(StoryDialogueDatabase database)
    {
        var runtime = UnityEngine.Object.FindObjectOfType<StoryQuestRuntime>(true);
        if (runtime == null)
        {
            var go = new GameObject("StoryQuestRuntime");
            runtime = go.AddComponent<StoryQuestRuntime>();
        }

        var so = new SerializedObject(runtime);
        so.FindProperty("database").objectReferenceValue = database;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(runtime);
    }

    static void ApplyQuestChainsToManager(StoryDialogueDatabase database)
    {
        var manager = UnityEngine.Object.FindObjectOfType<QuestSystemManager>(true);
        if (manager == null)
        {
            var go = new GameObject("QuestSystemManager");
            manager = go.AddComponent<QuestSystemManager>();
        }

        var so = new SerializedObject(manager);
        var chains = so.FindProperty("questChains");
        for (int i = chains.arraySize - 1; i >= 0; i--)
        {
            string questId = chains.GetArrayElementAtIndex(i).FindPropertyRelative("questId").stringValue;
            if (questId.StartsWith(StoryQuestPrefix, StringComparison.OrdinalIgnoreCase))
                chains.DeleteArrayElementAtIndex(i);
        }

        var rootDocsByChapter = database.documents
            .Where(d => d.isRootTask)
            .GroupBy(d => d.chapterId)
            .OrderBy(g => NaturalSortKey(g.Key));

        foreach (var chapter in rootDocsByChapter)
        {
            int chainIndex = chains.arraySize;
            chains.InsertArrayElementAtIndex(chainIndex);
            var chain = chains.GetArrayElementAtIndex(chainIndex);
            bool main = IsMainChapter(chapter.Key);
            chain.FindPropertyRelative("questId").stringValue = StoryQuestPrefix + chapter.Key.ToLowerInvariant();
            chain.FindPropertyRelative("questName").stringValue = main ? chapter.Key + " 主线剧情" : chapter.Key + " 支线/人物剧情";
            chain.FindPropertyRelative("category").enumValueIndex = main ? (int)QuestCategory.Main : (int)QuestCategory.Side;
            chain.FindPropertyRelative("shortDescription").stringValue = StoryQuestDescriptionTable.GetChapterShortZh(
                chapter.Key,
                main ? "推进林渡在未名之海中的主线旅程。" : "完成校园人物与选择分支，获得更多金币奖励。");
            chain.FindPropertyRelative("longDescription").stringValue = StoryQuestDescriptionTable.GetChapterLongZh(
                chapter.Key,
                "来自 content1/" + chapter.Key + " 的剧情任务链。每个节点通过对应 NPC 的分页对话完成。");
            chain.FindPropertyRelative("finalRewardGold").intValue = DetermineFinalRewardGold(chapter.Key);

            var subtasks = chain.FindPropertyRelative("subTasks");
            var docs = chapter.OrderBy(d => NaturalSortKey(d.documentId)).ToList();
            EnsureArraySize(subtasks, docs.Count);
            for (int i = 0; i < docs.Count; i++)
                SetupStorySubTask(subtasks.GetArrayElementAtIndex(i), docs[i]);
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(manager);
    }

    static void SetupStorySubTask(SerializedProperty subTask, StoryDialogueDocument doc)
    {
        subTask.FindPropertyRelative("subTaskId").stringValue = "story_" + SanitizeId(doc.documentId);
        subTask.FindPropertyRelative("subTaskName").stringValue = doc.title;
        string npcName = ResolveShortNpcName(doc);
        subTask.FindPropertyRelative("shortDescription").stringValue = StoryQuestDescriptionTable.GetSubTaskShortZh(
            doc.documentId,
            "与 " + npcName + " 对话，阅读剧情《" + doc.documentId + "》。");
        subTask.FindPropertyRelative("longDescription").stringValue = StoryQuestDescriptionTable.GetSubTaskLongZh(
            doc.documentId,
            "前往 " + doc.finishLocationName + "，与剧情 NPC 交互。对白按页推进，分支选项只会触发一次奖励。");
        subTask.FindPropertyRelative("triggerType").enumValueIndex = (int)QuestTriggerType.ManualEvent;
        subTask.FindPropertyRelative("triggerEventId").stringValue = doc.completeEventId;
        subTask.FindPropertyRelative("targetSceneName").stringValue = doc.targetSceneName;
        subTask.FindPropertyRelative("targetTransform").objectReferenceValue = null;
        subTask.FindPropertyRelative("fallbackTargetPosition").vector3Value = doc.fallbackTargetPosition;
        subTask.FindPropertyRelative("autoCompleteDistance").floatValue = 5f;
        subTask.FindPropertyRelative("targetCount").intValue = 1;
        subTask.FindPropertyRelative("rewardGold").intValue = doc.rewardGold;
        subTask.FindPropertyRelative("finishLocationName").stringValue = doc.finishLocationName;
    }

    static string ResolveShortNpcName(StoryDialogueDocument doc)
    {
        foreach (var page in doc.pages)
            if (!string.IsNullOrWhiteSpace(page.speakerName) && page.speakerName != "林渡")
                return page.speakerName;
        return "剧情触发点";
    }

    static void PlaceRootStoryNpcsInActiveScene(StoryDialogueDatabase database)
    {
        string activeSceneName = SceneManager.GetActiveScene().name;
        foreach (var doc in database.documents.Where(d => d.isRootTask))
        {
            if (!string.Equals(NormalizeSceneName(activeSceneName), NormalizeSceneName(doc.targetSceneName), StringComparison.OrdinalIgnoreCase))
                continue;
            if (UnityEngine.Object.FindObjectsOfType<StoryQuestNpc>(true).Any(n => n.documentId == doc.documentId))
                continue;

            var npc = database.FindNpc(doc.npcId);
            if (npc == null)
                continue;
            var prefab = StoryNpcPrefabResolver.Resolve(database, npc);
            if (prefab == null && !string.IsNullOrWhiteSpace(npc.prefabPath))
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(npc.prefabPath);
            GameObject go = prefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(prefab)
                : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "StoryNPC_" + npc.displayName + "_" + doc.documentId;
            go.transform.position = doc.fallbackTargetPosition + GetM0NpcOffset(doc.documentId);
            var storyNpc = go.GetComponent<StoryQuestNpc>();
            if (storyNpc == null)
                storyNpc = go.AddComponent<StoryQuestNpc>();
            storyNpc.documentId = doc.documentId;
            storyNpc.displayName = npc.displayName;
            EditorUtility.SetDirty(storyNpc);
        }
    }

    static Vector3 GetM0NpcOffset(string documentId)
    {
        if (documentId == "M0-2-1")
            return new Vector3(-2f, 0f, 0f);
        if (documentId == "M0-1-3" || documentId == "M0-3-1")
            return new Vector3(2f, 0f, 0f);
        return new Vector3(1.5f, 0f, 1.5f);
    }

    static StoryDialogueDatabase LoadOrCreateDatabase()
    {
        var db = AssetDatabase.LoadAssetAtPath<StoryDialogueDatabase>(DatabasePath);
        if (db != null)
            return db;

        db = ScriptableObject.CreateInstance<StoryDialogueDatabase>();
        AssetDatabase.CreateAsset(db, DatabasePath);
        return db;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    static void EnsureArraySize(SerializedProperty arr, int size)
    {
        while (arr.arraySize < size)
            arr.InsertArrayElementAtIndex(arr.arraySize);
        while (arr.arraySize > size)
            arr.DeleteArrayElementAtIndex(arr.arraySize - 1);
    }

    static void SplitSpeaker(string line, out string speaker, out string text)
    {
        speaker = "";
        text = line;
        int idx = line.IndexOf('：');
        if (idx < 0) idx = line.IndexOf(':');
        if (idx <= 0 || idx > 12)
            return;

        string candidate = line.Substring(0, idx).Trim();
        if (candidate.Contains(" ") || candidate.Contains("/") || candidate.Contains("，"))
            return;

        speaker = candidate;
        text = line.Substring(idx + 1).Trim();
    }

    static string NormalizeStoryText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        return text.Replace("百万大道", "龙翔大道").Trim();
    }

    static string NormalizeDocumentId(string rawId)
    {
        string id = rawId.Trim();
        int space = id.IndexOf(' ');
        if (space > 0)
            id = id.Substring(0, space);
        return id;
    }

    static string NormalizeSceneName(string sceneRef)
    {
        if (string.IsNullOrWhiteSpace(sceneRef))
            return "";
        string s = sceneRef.Trim().Replace('\\', '/');
        int slash = s.LastIndexOf('/');
        if (slash >= 0 && slash < s.Length - 1)
            s = s.Substring(slash + 1);
        if (s.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            s = s.Substring(0, s.Length - 6);
        return s.Trim();
    }

    static bool IsRootDocumentId(string id)
    {
        if (string.Equals(id, "M5-5-1-A", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(id, "M3-2-1R", StringComparison.OrdinalIgnoreCase))
            return true;
        return Regex.IsMatch(id, @"^M\d+-\d+-\d+$");
    }

    static bool IsBranchDocumentId(string id)
    {
        return !IsRootDocumentId(id);
    }

    static bool IsMainChapter(string chapter)
    {
        if (string.IsNullOrWhiteSpace(chapter))
            return false;
        switch (chapter.Trim().ToUpperInvariant())
        {
            case "M0":
            case "M1":
            case "M2":
            case "M3":
            case "M4":
            case "M5":
                return true;
            default:
                return false;
        }
    }

    static string BuildEventId(string documentId)
    {
        return "content1_story_complete_" + SanitizeId(documentId);
    }

    static int DetermineFinalRewardGold(string chapterId)
    {
        switch (chapterId)
        {
            case "M1": return 160;
            case "M2": return 420;
            case "M3": return 360;
            case "M4": return 520;
            case "M5": return 260;
            default: return IsMainChapter(chapterId) ? 180 : 360;
        }
    }

    static string SanitizeId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unknown";
        return Regex.Replace(value.Trim().ToLowerInvariant(), @"[^\p{L}\p{Nd}]+", "_").Trim('_');
    }

    static string SanitizeAssetName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "StoryNpc";
        string invalid = new string(Path.GetInvalidFileNameChars());
        string pattern = "[" + Regex.Escape(invalid) + "]";
        return Regex.Replace(value.Trim(), pattern, "_");
    }

    static string NaturalSortKey(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        return Regex.Replace(value, @"\d+", m => m.Value.PadLeft(8, '0')).ToLowerInvariant();
    }
}
#endif
