using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GameQuestSystem
{
    [Serializable]
    class StoryEnglishPageDto
    {
        public int pageNumber;
        public string speakerName;
        public string text;
    }

    [Serializable]
    class StoryEnglishDocumentDto
    {
        public List<StoryEnglishPageDto> pages = new List<StoryEnglishPageDto>();
        public List<string> choices = new List<string>();
    }

    /// <summary>
    /// English overlay for content1 story dialogue (main M0/M1/M5 + side M2/M3/M4).
    /// </summary>
    public static class Content1StoryEnglishOverlay
    {
        static readonly string[] ResourcePaths =
        {
            "Localization/story_main_en",
            "Localization/story_side_en",
        };

        static Dictionary<string, StoryEnglishDocumentDto> _byDocumentId;
        static bool _loadAttempted;

        public static void InvalidateCache()
        {
            _byDocumentId = null;
            _loadAttempted = false;
        }

        public static bool TryGetPageText(string documentId, int pageNumber, out string text)
        {
            text = null;
            if (!LocalizationService.IsEnglish || string.IsNullOrWhiteSpace(documentId))
                return false;

            EnsureLoaded();
            StoryEnglishPageDto page;
            if (!TryGetPage(documentId, pageNumber, out page))
                return false;

            if (string.IsNullOrWhiteSpace(page.text))
                return false;

            text = StoryLocalizationUtil.NormalizeStoryLabel(page.text);
            return true;
        }

        public static bool TryGetPageSpeaker(string documentId, int pageNumber, string fallback, out string speaker)
        {
            speaker = fallback;
            if (!LocalizationService.IsEnglish || string.IsNullOrWhiteSpace(documentId))
                return false;

            EnsureLoaded();
            StoryEnglishPageDto page;
            if (!TryGetPage(documentId, pageNumber, out page))
                return false;

            if (string.IsNullOrWhiteSpace(page.speakerName))
                return false;

            speaker = StoryLocalizationUtil.NormalizeStoryLabel(page.speakerName);
            return true;
        }

        public static bool TryGetChoiceLabel(string documentId, int choiceIndex, string fallback, out string label)
        {
            label = fallback;
            if (!LocalizationService.IsEnglish || string.IsNullOrWhiteSpace(documentId))
                return false;

            EnsureLoaded();
            StoryEnglishDocumentDto doc;
            if (_byDocumentId == null || !_byDocumentId.TryGetValue(documentId, out doc))
                return false;
            if (doc.choices == null || choiceIndex < 0 || choiceIndex >= doc.choices.Count)
                return false;
            if (string.IsNullOrWhiteSpace(doc.choices[choiceIndex]))
                return false;

            label = StoryLocalizationUtil.NormalizeStoryLabel(doc.choices[choiceIndex]);
            return true;
        }

        static bool TryGetPage(string documentId, int pageNumber, out StoryEnglishPageDto page)
        {
            page = null;
            StoryEnglishDocumentDto doc;
            if (_byDocumentId == null || !_byDocumentId.TryGetValue(documentId, out doc))
                return false;
            if (doc.pages == null)
                return false;

            for (int i = 0; i < doc.pages.Count; i++)
            {
                var p = doc.pages[i];
                if (p != null && p.pageNumber == pageNumber)
                {
                    page = p;
                    return true;
                }
            }

            return false;
        }

        static void EnsureLoaded()
        {
            if (_loadAttempted)
                return;
            _loadAttempted = true;
            _byDocumentId = new Dictionary<string, StoryEnglishDocumentDto>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < ResourcePaths.Length; i++)
            {
                var asset = Resources.Load<TextAsset>(ResourcePaths[i]);
                if (asset != null && !string.IsNullOrWhiteSpace(asset.text))
                    ParseJson(asset.text);
            }

            if (_byDocumentId.Count == 0)
                TryLoadFromStreamingAssets();
        }

        static void TryLoadFromStreamingAssets()
        {
            string folder = Path.Combine(Application.streamingAssetsPath, "Localization");
            string[] files = { "story_main_en.json", "story_side_en.json" };
            for (int i = 0; i < files.Length; i++)
            {
                string path = Path.Combine(folder, files[i]);
                if (!File.Exists(path))
                    continue;
                try
                {
                    ParseJson(File.ReadAllText(path));
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Content1StoryEnglishOverlay] Failed to load " + files[i] + ": " + ex.Message);
                }
            }
        }

        static void ParseJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return;

            var root = MiniJson.Deserialize(json) as Dictionary<string, object>;
            if (root == null)
                return;

            foreach (var pair in root)
            {
                var docObj = pair.Value as Dictionary<string, object>;
                if (docObj == null)
                    continue;

                var dto = new StoryEnglishDocumentDto();
                object pagesObj;
                if (docObj.TryGetValue("pages", out pagesObj))
                {
                    var pagesList = pagesObj as List<object>;
                    if (pagesList != null)
                    {
                        for (int i = 0; i < pagesList.Count; i++)
                        {
                            var p = pagesList[i] as Dictionary<string, object>;
                            if (p == null)
                                continue;
                            dto.pages.Add(new StoryEnglishPageDto
                            {
                                pageNumber = Convert.ToInt32(GetDictValue(p, "pageNumber", 0)),
                                speakerName = GetDictValue(p, "speakerName", "") as string ?? "",
                                text = GetDictValue(p, "text", "") as string ?? ""
                            });
                        }
                    }
                }

                object choicesObj;
                if (docObj.TryGetValue("choices", out choicesObj))
                {
                    var choicesList = choicesObj as List<object>;
                    if (choicesList != null)
                    {
                        for (int i = 0; i < choicesList.Count; i++)
                            dto.choices.Add(choicesList[i] != null ? choicesList[i].ToString() : "");
                    }
                }

                _byDocumentId[pair.Key] = dto;
            }
        }

        static object GetDictValue(Dictionary<string, object> dict, string key, object fallback)
        {
            object value;
            return dict.TryGetValue(key, out value) ? value : fallback;
        }
    }

    static class MiniJson
    {
        public static object Deserialize(string json)
        {
            return new Parser(json).Parse();
        }

        sealed class Parser
        {
            readonly string _json;
            int _index;

            public Parser(string json)
            {
                _json = json;
                _index = 0;
            }

            public object Parse()
            {
                SkipWs();
                return ParseValue();
            }

            object ParseValue()
            {
                SkipWs();
                if (_index >= _json.Length)
                    return null;
                char c = _json[_index];
                if (c == '{')
                    return ParseObject();
                if (c == '[')
                    return ParseArray();
                if (c == '"')
                    return ParseString();
                if (c == 't' || c == 'f' || c == 'n')
                    return ParseLiteral();
                return ParseNumber();
            }

            Dictionary<string, object> ParseObject()
            {
                var dict = new Dictionary<string, object>();
                _index++;
                SkipWs();
                if (TryConsume('}'))
                    return dict;
                while (_index < _json.Length)
                {
                    SkipWs();
                    string key = ParseString();
                    SkipWs();
                    Consume(':');
                    dict[key] = ParseValue();
                    SkipWs();
                    if (TryConsume('}'))
                        break;
                    Consume(',');
                }
                return dict;
            }

            List<object> ParseArray()
            {
                var list = new List<object>();
                _index++;
                SkipWs();
                if (TryConsume(']'))
                    return list;
                while (_index < _json.Length)
                {
                    list.Add(ParseValue());
                    SkipWs();
                    if (TryConsume(']'))
                        break;
                    Consume(',');
                }
                return list;
            }

            string ParseString()
            {
                _index++;
                var sb = new System.Text.StringBuilder();
                while (_index < _json.Length)
                {
                    char c = _json[_index++];
                    if (c == '"')
                        break;
                    if (c == '\\' && _index < _json.Length)
                    {
                        char esc = _json[_index++];
                        switch (esc)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'u':
                                if (_index + 4 <= _json.Length)
                                {
                                    string hex = _json.Substring(_index, 4);
                                    sb.Append((char)Convert.ToInt32(hex, 16));
                                    _index += 4;
                                }
                                break;
                            default: sb.Append(esc); break;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                return sb.ToString();
            }

            object ParseNumber()
            {
                int start = _index;
                if (_json[_index] == '-')
                    _index++;
                while (_index < _json.Length && char.IsDigit(_json[_index]))
                    _index++;
                if (_index < _json.Length && _json[_index] == '.')
                {
                    _index++;
                    while (_index < _json.Length && char.IsDigit(_json[_index]))
                        _index++;
                }
                string num = _json.Substring(start, _index - start);
                if (num.Contains("."))
                    return double.Parse(num, System.Globalization.CultureInfo.InvariantCulture);
                return long.Parse(num, System.Globalization.CultureInfo.InvariantCulture);
            }

            object ParseLiteral()
            {
                if (Match("true")) return true;
                if (Match("false")) return false;
                if (Match("null")) return null;
                return null;
            }

            bool Match(string s)
            {
                if (!_json.Substring(_index).StartsWith(s))
                    return false;
                _index += s.Length;
                return true;
            }

            void SkipWs()
            {
                while (_index < _json.Length && char.IsWhiteSpace(_json[_index]))
                    _index++;
            }

            void Consume(char c)
            {
                SkipWs();
                if (_index >= _json.Length || _json[_index] != c)
                    throw new FormatException("Expected '" + c + "' at " + _index);
                _index++;
            }

            bool TryConsume(char c)
            {
                SkipWs();
                if (_index < _json.Length && _json[_index] == c)
                {
                    _index++;
                    return true;
                }
                return false;
            }
        }
    }
}
