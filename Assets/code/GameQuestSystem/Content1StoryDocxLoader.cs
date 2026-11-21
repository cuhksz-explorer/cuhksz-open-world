using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEngine;

namespace GameQuestSystem
{
    /// <summary>
    /// 从 Assets/content1 下的 docx 解析剧情分页，并在数据库条目不完整时自动补全。
    /// </summary>
    public static class Content1StoryDocxLoader
    {
        const string PlaceholderMarker = "剧情文本尚未导入";
        static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        public static void EnsureHydrated(StoryDialogueDatabase database, StoryDialogueDocument doc)
        {
            if (database == null || doc == null || !NeedsHydration(doc))
                return;

            if (!TryHydrateDocument(doc))
                return;

            InferChoiceTargets(database.documents);
        }

        public static void HydrateDatabaseIfNeeded(StoryDialogueDatabase database)
        {
            if (database?.documents == null || database.documents.Count == 0)
                return;

            SyncDocumentCategories(database);

            bool changed = false;
            for (int i = 0; i < database.documents.Count; i++)
            {
                var doc = database.documents[i];
                if (doc == null || !NeedsHydration(doc))
                    continue;
                changed |= TryHydrateDocument(doc);
            }

            if (changed)
                InferChoiceTargets(database.documents);
        }

        public static bool NeedsHydration(StoryDialogueDocument doc)
        {
            if (doc == null || string.IsNullOrWhiteSpace(doc.sourceAssetPath))
                return false;
            if (!doc.sourceAssetPath.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
                return false;

            if (doc.pages == null || doc.pages.Count == 0)
                return true;
            if (doc.pages.Count == 1)
                return true;

            for (int i = 0; i < doc.pages.Count; i++)
            {
                var page = doc.pages[i];
                if (page != null && !string.IsNullOrEmpty(page.text) && page.text.Contains(PlaceholderMarker))
                    return true;
            }

            return false;
        }

        public static bool TryHydrateDocument(StoryDialogueDocument doc)
        {
            if (doc == null || string.IsNullOrWhiteSpace(doc.sourceAssetPath))
                return false;

            string fullPath = ResolveDocxFullPath(doc.sourceAssetPath);
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
                return false;

            var parsed = ParseDocx(fullPath, doc.sourceAssetPath.Replace('\\', '/'));
            if (parsed == null || parsed.pages == null || parsed.pages.Count == 0)
                return false;

            if (doc.pages != null && doc.pages.Count >= parsed.pages.Count && !ContainsPlaceholder(doc))
                return false;

            doc.pages = parsed.pages;
            doc.choices = parsed.choices ?? new List<StoryDialogueChoice>();
            if (string.IsNullOrWhiteSpace(doc.title))
                doc.title = parsed.documentId;
            if (string.IsNullOrWhiteSpace(doc.documentId))
                doc.documentId = parsed.documentId;
            if (string.IsNullOrWhiteSpace(doc.chapterId))
                doc.chapterId = parsed.chapterId;

            return true;
        }

        static bool ContainsPlaceholder(StoryDialogueDocument doc)
        {
            if (doc?.pages == null)
                return false;
            for (int i = 0; i < doc.pages.Count; i++)
            {
                var page = doc.pages[i];
                if (page != null && !string.IsNullOrEmpty(page.text) && page.text.Contains(PlaceholderMarker))
                    return true;
            }
            return false;
        }

        public static string ResolveDocxFullPath(string sourceAssetPath)
        {
            if (string.IsNullOrWhiteSpace(sourceAssetPath))
                return null;

            string normalized = sourceAssetPath.Replace('\\', '/').Trim();
            if (Path.IsPathRooted(normalized) && File.Exists(normalized))
                return normalized;

            string relative = normalized;
            if (relative.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                relative = relative.Substring("Assets/".Length);

            string fromDataPath = Path.Combine(Application.dataPath, relative);
            if (File.Exists(fromDataPath))
                return fromDataPath;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (!string.IsNullOrEmpty(projectRoot))
            {
                string fromProject = Path.Combine(projectRoot, normalized.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(fromProject))
                    return fromProject;
            }

            return null;
        }

        public static StoryDialogueDocument ParseDocx(string fullPath, string sourceAssetPath)
        {
            List<string> paragraphs;
            try
            {
                paragraphs = ExtractParagraphs(fullPath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Content1StoryDocxLoader] 解析失败: " + fullPath + "\n" + ex.Message);
                return null;
            }

            if (paragraphs.Count == 0)
                return null;

            string rawId = Path.GetFileNameWithoutExtension(fullPath);
            string documentId = NormalizeDocumentId(rawId);
            string chapter = documentId.Split('-')[0];
            var doc = new StoryDialogueDocument
            {
                documentId = documentId,
                title = documentId,
                sourceAssetPath = sourceAssetPath,
                chapterId = chapter,
                category = IsMainChapter(chapter) ? QuestCategory.Main : QuestCategory.Side,
                isBranchDocument = !IsRootDocumentId(documentId),
                isRootTask = IsRootDocumentId(documentId),
                completeEventId = BuildEventId(documentId),
                pages = new List<StoryDialoguePage>(),
                choices = new List<StoryDialogueChoice>()
            };

            ParsePagesAndChoices(paragraphs, doc);
            return doc;
        }

        public static void InferChoiceTargets(IList<StoryDialogueDocument> documents)
        {
            if (documents == null || documents.Count == 0)
                return;

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < documents.Count; i++)
            {
                if (documents[i] != null && !string.IsNullOrWhiteSpace(documents[i].documentId))
                    ids.Add(documents[i].documentId);
            }

            for (int i = 0; i < documents.Count; i++)
            {
                var doc = documents[i];
                if (doc?.choices == null || doc.choices.Count == 0)
                    continue;

                var targets = FindCandidateTargets(doc.documentId, doc.choices.Count, ids);
                for (int c = 0; c < doc.choices.Count && c < targets.Count; c++)
                    doc.choices[c].targetDocumentId = targets[c];
            }
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

                SplitSpeaker(line, out string speaker, out string text);
                doc.pages.Add(new StoryDialoguePage
                {
                    pageNumber = currentPage,
                    speakerName = speaker,
                    text = text
                });
                currentPage = 0;
            }
        }

        static List<string> FindCandidateTargets(string documentId, int count, HashSet<string> ids)
        {
            var result = new List<string>();
            AddManualChoiceTargets(documentId, count, ids, result);
            if (result.Count >= count)
                return result.Take(count).ToList();

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

        static bool IsRootDocumentId(string id)
        {
            if (string.Equals(id, "M5-5-1-A", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(id, "M3-2-1R", StringComparison.OrdinalIgnoreCase))
                return true;
            return Regex.IsMatch(id, @"^M\d+-\d+-\d+$");
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

        static string SanitizeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";
            return Regex.Replace(value.Trim().ToLowerInvariant(), @"[^\p{L}\p{Nd}]+", "_").Trim('_');
        }

        static string NaturalSortKey(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            return Regex.Replace(value, @"\d+", m => m.Value.PadLeft(8, '0'));
        }

        /// <summary>
        /// 扫描 Assets/content1 下全部 docx，补全/刷新数据库中的剧情分页。
        /// </summary>
        public static void SyncDatabaseFromContentFolder(StoryDialogueDatabase database)
        {
            if (database == null)
                return;

            string contentRoot = Path.Combine(Application.dataPath, "content1");
            if (!Directory.Exists(contentRoot))
            {
                HydrateDatabaseIfNeeded(database);
                return;
            }

            if (database.documents == null)
                database.documents = new List<StoryDialogueDocument>();

            var existingById = new Dictionary<string, StoryDialogueDocument>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < database.documents.Count; i++)
            {
                var doc = database.documents[i];
                if (doc != null && !string.IsNullOrWhiteSpace(doc.documentId))
                    existingById[doc.documentId] = doc;
            }

            var merged = new List<StoryDialogueDocument>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string file in Directory.GetFiles(contentRoot, "*.docx", SearchOption.AllDirectories))
            {
                string relative = file.Substring(contentRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace('\\', '/');
                string assetPath = "Assets/content1/" + relative;
                var parsed = ParseDocx(file, assetPath);
                if (parsed == null || string.IsNullOrWhiteSpace(parsed.documentId))
                    continue;

                if (existingById.TryGetValue(parsed.documentId, out var existing))
                {
                    MergeParsedIntoExisting(existing, parsed);
                    merged.Add(existing);
                }
                else
                {
                    ApplyMinimalRuntimeFields(parsed);
                    merged.Add(parsed);
                }

                seen.Add(parsed.documentId);
            }

            for (int i = 0; i < database.documents.Count; i++)
            {
                var doc = database.documents[i];
                if (doc == null || string.IsNullOrWhiteSpace(doc.documentId))
                    continue;
                if (seen.Contains(doc.documentId))
                    continue;
                if (!string.IsNullOrEmpty(doc.sourceAssetPath) &&
                    doc.sourceAssetPath.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
                    continue;
                merged.Add(doc);
                seen.Add(doc.documentId);
            }

            merged.Sort((a, b) => string.Compare(NaturalSortKey(a.documentId), NaturalSortKey(b.documentId), StringComparison.OrdinalIgnoreCase));
            database.documents = merged;
            InferChoiceTargets(database.documents);
        }

        static void MergeParsedIntoExisting(StoryDialogueDocument existing, StoryDialogueDocument parsed)
        {
            if (existing == null || parsed == null)
                return;

            if (NeedsHydration(existing) || parsed.pages.Count > (existing.pages?.Count ?? 0))
            {
                existing.pages = parsed.pages;
                existing.choices = parsed.choices ?? new List<StoryDialogueChoice>();
            }

            if (string.IsNullOrWhiteSpace(existing.sourceAssetPath))
                existing.sourceAssetPath = parsed.sourceAssetPath;
            if (string.IsNullOrWhiteSpace(existing.title))
                existing.title = parsed.title;
            if (string.IsNullOrWhiteSpace(existing.chapterId))
                existing.chapterId = parsed.chapterId;
            if (string.IsNullOrWhiteSpace(existing.completeEventId))
                existing.completeEventId = parsed.completeEventId;
        }

        static void SyncDocumentCategories(StoryDialogueDatabase database)
        {
            if (database?.documents == null)
                return;

            for (int i = 0; i < database.documents.Count; i++)
            {
                var doc = database.documents[i];
                if (doc == null || string.IsNullOrWhiteSpace(doc.chapterId))
                    continue;
                doc.category = IsMainChapter(doc.chapterId) ? QuestCategory.Main : QuestCategory.Side;
            }
        }

        static void ApplyMinimalRuntimeFields(StoryDialogueDocument doc)
        {
            if (doc == null)
                return;

            if (!string.IsNullOrWhiteSpace(doc.chapterId))
                doc.category = IsMainChapter(doc.chapterId) ? QuestCategory.Main : QuestCategory.Side;

            if (string.IsNullOrWhiteSpace(doc.completeEventId))
                doc.completeEventId = BuildEventId(doc.documentId);
            if (string.IsNullOrWhiteSpace(doc.npcId))
                doc.npcId = DetermineNpcIdFromPages(doc);
            if (string.IsNullOrWhiteSpace(doc.targetSceneName))
                doc.targetSceneName = GuessSceneName(doc.documentId);
            if (doc.fallbackTargetPosition == Vector3.zero)
                doc.fallbackTargetPosition = GuessFallbackPosition(doc.documentId);
            if (string.IsNullOrWhiteSpace(doc.finishLocationName))
                doc.finishLocationName = GuessFinishLocationName(doc.documentId);
            if (doc.rewardGold <= 0)
                doc.rewardGold = doc.category == QuestCategory.Main ? 25 : 65;
        }

        static string DetermineNpcIdFromPages(StoryDialogueDocument doc)
        {
            var counter = new Dictionary<string, int>(StringComparer.Ordinal);
            if (doc.pages != null)
            {
                for (int i = 0; i < doc.pages.Count; i++)
                {
                    var page = doc.pages[i];
                    if (page == null || string.IsNullOrWhiteSpace(page.speakerName))
                        continue;
                    string speaker = page.speakerName.Trim();
                    if (IsIgnoredSpeakerName(speaker))
                        continue;
                    counter.TryGetValue(speaker, out int count);
                    counter[speaker] = count + 1;
                }
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

        static string GuessSceneName(string documentId)
        {
            switch (documentId)
            {
                case "M0-1-1":
                case "M0-2-1":
                    return "undergarden";
                case "M1-1-1":
                    return "computerroom";
                case "M0-1-3":
                case "M0-3-1":
                    return "uppergarden";
                default:
                    return "";
            }
        }

        static Vector3 GuessFallbackPosition(string documentId)
        {
            switch (documentId)
            {
                case "M0-1-1":
                    return new Vector3(-1373f, 3.16f, 1329f);
                case "M0-1-3":
                case "M0-3-1":
                    return new Vector3(350.13f, 44.51f, -625.67f);
                case "M0-2-1":
                    return new Vector3(-1014.69f, 14.22f, 979.67f);
                case "M1-1-1":
                    return new Vector3(-3.91f, 0.95f, -23.67f);
                default:
                    return Vector3.zero;
            }
        }

        static string GuessFinishLocationName(string documentId)
        {
            switch (documentId)
            {
                case "M0-1-1":
                    return "校门";
                case "M0-1-3":
                case "M0-3-1":
                    return "上园宿舍区";
                case "M0-2-1":
                    return "图书馆公共阅读区";
                case "M1-1-1":
                    return "机房/选课系统";
                default:
                    return "";
            }
        }
    }
}
