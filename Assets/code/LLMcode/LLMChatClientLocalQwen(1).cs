using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.Networking;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System;

public class LLMChatClient_LocalQwen : MonoBehaviour
{
    [Header("UI（可手动拖拽，也会自动查找）")]
    public TMP_InputField inputField;
    public TMP_Text resultText;
    public Button sendButton;

    [Header("后端配置")]
    public BackendType backend = BackendType.OpenAICompatible; // vLLM / LM Studio / qwen.cpp / DashScope兼容
    [Tooltip("基础地址，不要带 /v1，示例： http://localhost:8000 / http://localhost:1234 / http://localhost:11434 / https://dashscope.aliyuncs.com/compatible-mode")]
    public string apiBase = "http://localhost:8000";
    public string model = "qwen2.5-7b-instruct";
    public string apiKey = "";          // DashScope 等需要填 sk-...
    public int timeoutSeconds = 60;
    public bool verboseLog = true;
    public bool allowSelfSigned = false;

    [Header("流式输出")]
    [Range(0.01f, 0.2f)] public float uiFlushInterval = 0.03f; // UI刷新节流

    [Header("system 人格（文本或 TextAsset）")]
    [TextArea(4, 20)] public string personaText;
    public TextAsset personaAsset;
    public bool alwaysInjectPersona = true;
    /*
     * 建议在 persona 中用英文告诉大模型：
     * - 它是 Unity 游戏中的 NPC
     * - 必须始终返回严格的 JSON：
     *   {
     *     "reply": "<NPC's English reply to the player>",
     *     "emotion": "<one of: idle, waving, thinking, hands_on_hip, clapping, thankful>"
     *   }
     */

    [Header("聊天记忆")]
    public bool rememberHistory = true;
    [Range(0, 10)] public int maxHistoryPairs = 6;
    private readonly List<Msg> history = new();

    [Header("NPC 情绪控制（可选）")]
    public NPCEmotionController npcEmotion;

    [Header("NPC 游走（可选）")]
    public RandomWalkNPC npcWalker;

    public void ApplyApiSettings(LlmNpcApiSettings settings) => settings?.ApplyTo(this);

    public void EnsureBoundUnderCanvas(Canvas canvas)
    {
        if (canvas == null) return;
        if (!inputField)
            inputField = canvas.GetComponentsInChildren<TMP_InputField>(true).FirstOrDefault();
        if (!sendButton)
            sendButton = canvas.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(b => b.GetComponentInParent<TMP_InputField>() == null);
        if (!resultText)
        {
            resultText = canvas.GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(t =>
                    t.GetComponentInParent<Button>() == null &&
                    !t.name.ToLower().Contains("placeholder"));
        }
    }

    // 一轮对话完成后抛出（用户输入，模型最终回复）
    public Action<string /*user*/, string /*assistant*/> OnTurnFinished;

    // 流式缓存
    private StringBuilder _accum = new StringBuilder();
    private StringBuilder _buffer = new StringBuilder();
    private float _nextFlushTime = 0f;

    void Awake()
    {
        BindUIByType();
    }

    void Start()
    {
        if (sendButton)
        {
            sendButton.onClick.RemoveAllListeners();
            sendButton.onClick.AddListener(OnSendClicked);
        }
    }

    void OnSendClicked()
    {
        Debug.Log("[Chat] OnSendClicked() fired");
        if (!inputField || !resultText) return;

        var q = inputField.text;
        if (string.IsNullOrWhiteSpace(q))
        {
            resultText.text = "Please enter your question…";
            return;
        }

        // 1）开始对话：挥手 waving
        if (npcEmotion != null)
        {
            npcEmotion.TriggerStartConversation();   // → waving
        }

        npcWalker?.StopWandering();

        resultText.text = "Sending…";

        // 只使用流式
        StartCoroutine(SendStream(q));
    }

    // ------------------ 流式（SSE/NDJSON） ------------------
    IEnumerator SendStream(string question)
    {
        _accum.Length = 0;
        _buffer.Length = 0;
        _nextFlushTime = 0f;

        // 2）请求开始：思考中 → Thinking
        if (npcEmotion != null)
        {
            npcEmotion.TriggerThinking();
        }

        var msgs = BuildMessages(question);
        string url, json;

        if (backend == BackendType.OpenAICompatible)
        {
            url = Trim(apiBase) + "/v1/chat/completions";
            var payload = new OpenAICompatPayload { model = model, stream = true, messages = msgs.ToArray() };
            json = JsonUtility.ToJson(payload);
        }
        else // Ollama
        {
            url = Trim(apiBase) + "/api/chat";
            var payload = new OllamaPayload { model = model, stream = true, messages = msgs.ToArray() };
            json = JsonUtility.ToJson(payload);
        }

        if (verboseLog) Debug.Log($"[LLM] STREAM POST {url}\n{json}");

        var dh = new StreamingHandler((bytes, len) =>
        {
            var chunk = Encoding.UTF8.GetString(bytes, 0, len);
            if (backend == BackendType.OpenAICompatible) HandleOpenAISSE(chunk);
            else HandleOllamaNDJson(chunk);
        });

        using (var req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = dh;
            req.timeout = Mathf.Max(1, timeoutSeconds);
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Accept", "text/event-stream, application/json");
            if (!string.IsNullOrEmpty(apiKey)) req.SetRequestHeader("Authorization", "Bearer " + apiKey);
            if (allowSelfSigned) req.certificateHandler = new PermissiveCert();

            var op = req.SendWebRequest();

            while (!op.isDone)
            {
                if (Time.unscaledTime >= _nextFlushTime && resultText)
                {
                    // 流式中先显示原始内容（可能是 JSON，结束后会再处理一次）
                    resultText.text = _accum.ToString();
                    _nextFlushTime = Time.unscaledTime + uiFlushInterval;
                }
                yield return null;
            }

            long status = req.responseCode;
            string raw = dh.GetAll();
            if (verboseLog) Debug.Log($"[LLM] Stream finished Status {status}");

            if (req.result == UnityWebRequest.Result.Success)
            {
                var rawFinal = _accum.ToString();
                var finalText = HandleModelTextAndEmotion(question, rawFinal);

                if (resultText) resultText.text = finalText;

                RecordHistory(question, finalText);
                OnTurnFinished?.Invoke(question, finalText);
            }
            else
            {
                UpdateUIFinal($"Error {status}: {req.error}\n{raw}");
                Debug.LogError($"[LLM] 流式失败 {status} - {req.error}");

                // 请求失败：愤怒/困惑 → HandsOnHip
                if (npcEmotion != null)
                {
                    npcEmotion.TriggerAngryOrConfused();
                }
            }
        }
    }

    // ------------------ LLM 文本 + 情绪处理 ------------------
    /// <summary>
    /// 处理模型返回的内容：
    /// 1）尝试解析为 {"reply":"...","emotion":"..."} JSON
    /// 2）或解析末尾标签 <emotion=xxx>
    /// 3）使用英文关键词做兜底推断
    /// 4）驱动 NPCEmotionController
    /// 返回最终要显示在 UI 的纯文本（仅 reply 部分）
    /// </summary>
    string HandleModelTextAndEmotion(string userQuestion, string modelContent)
    {
        if (string.IsNullOrEmpty(modelContent))
        {
            if (npcEmotion != null) npcEmotion.ResetToIdle();
            return "";
        }

        string finalText = modelContent;
        string emotionTag = null;
        string trimmed = modelContent.Trim();

        // 1）优先尝试 JSON：{"reply":"...","emotion":"..."}
        if (trimmed.StartsWith("{"))
        {
            try
            {
                var emoRes = JsonUtility.FromJson<LLMEmotionResponse>(trimmed);
                if (emoRes != null)
                {
                    if (!string.IsNullOrEmpty(emoRes.reply))
                        finalText = emoRes.reply;
                    if (!string.IsNullOrEmpty(emoRes.emotion))
                        emotionTag = emoRes.emotion;
                }
            }
            catch
            {
                // 解析失败则继续尝试标签模式
            }
        }

        // 2）尝试解析末尾标签：...<emotion=thinking>
        if (emotionTag == null)
        {
            int idx = trimmed.LastIndexOf("<emotion=", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                int close = trimmed.IndexOf('>', idx + 9);
                if (close > idx)
                {
                    emotionTag = trimmed.Substring(idx + 9, close - (idx + 9)).Trim();
                    finalText = trimmed.Substring(0, idx).TrimEnd();
                }
            }
        }

        // 3）没有明确标签时，做英文关键词推断（兜底）
        if (emotionTag == null)
        {
            bool Contains(string src, string kw) =>
                src.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0;

            // 感谢 → thankful
            if (Contains(trimmed, "thank you") ||
                Contains(trimmed, "thanks") ||
                Contains(trimmed, "appreciate") ||
                Contains(trimmed, "grateful"))
            {
                emotionTag = "thankful";
            }
            // 认可/夸奖玩家 → clapping
            else if (Contains(trimmed, "you are right") ||
                     Contains(trimmed, "you're right") ||
                     Contains(trimmed, "good point") ||
                     Contains(trimmed, "good job") ||
                     Contains(trimmed, "well done") ||
                     Contains(trimmed, "correct") ||
                     Contains(trimmed, "i agree"))
            {
                emotionTag = "clapping";
            }
            // 生气/不耐烦/想结束 → hands_on_hip
            else if (Contains(trimmed, "angry") ||
                     Contains(trimmed, "upset") ||
                     Contains(trimmed, "annoyed") ||
                     Contains(trimmed, "impatient") ||
                     Contains(trimmed, "i will stop here") ||
                     Contains(trimmed, "end this conversation") ||
                     Contains(trimmed, "i don't want to talk") ||
                     Contains(trimmed, "let's stop here"))
            {
                emotionTag = "hands_on_hip";
            }
        }

        // 4）驱动 NPC 动画
        if (npcEmotion != null)
        {
            if (!string.IsNullOrEmpty(emotionTag))
                npcEmotion.SetEmotionFromString(emotionTag);
            else
                npcEmotion.ResetToIdle();
        }

        return finalText;
    }

    // ---- 组装消息：system + 历史 + 当前 ----
    List<Msg> BuildMessages(string userQuestion)
    {
        var msgs = new List<Msg>();

        if (alwaysInjectPersona)
        {
            string sys = personaAsset ? personaAsset.text : personaText;
            if (!string.IsNullOrWhiteSpace(sys))
                msgs.Add(new Msg { role = "system", content = sys });
        }

        if (rememberHistory && history.Count > 0)
        {
            int take = Mathf.Min(history.Count, maxHistoryPairs * 2);
            msgs.AddRange(history.Skip(history.Count - take));
        }

        msgs.Add(new Msg { role = "user", content = userQuestion });
        return msgs;
    }

    void RecordHistory(string question, string reply)
    {
        if (!rememberHistory) return;

        history.Add(new Msg { role = "user", content = question });
        history.Add(new Msg { role = "assistant", content = reply });

        int max = maxHistoryPairs * 2;
        if (history.Count > max)
            history.RemoveRange(0, history.Count - max);
    }

    // ---- 解析流式 ----
    // OpenAI 兼容：SSE，每行可能是 "data: {...}" 或 "data: [DONE]"
    void HandleOpenAISSE(string chunk)
    {
        _buffer.Append(chunk);
        int nl;
        while ((nl = IndexOfNewline(_buffer)) >= 0)
        {
            var line = _buffer.ToString(0, nl).TrimEnd('\r');
            _buffer.Remove(0, nl + 1); // 去掉 '\n'

            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("data:")) continue;

            var payload = line.Substring(5).Trim();
            if (payload == "[DONE]") return;

            try
            {
                var ev = JsonUtility.FromJson<OpenAIStreamEvent>(payload);
                if (ev?.choices != null && ev.choices.Length > 0)
                {
                    var delta = ev.choices[0].delta;
                    if (delta != null && !string.IsNullOrEmpty(delta.content))
                        _accum.Append(delta.content);
                }
            }
            catch { /* 忽略异常行 */ }
        }
    }

    // Ollama：NDJSON，每行一个 JSON；done=true 结束
    void HandleOllamaNDJson(string chunk)
    {
        _buffer.Append(chunk);
        int nl;
        while ((nl = IndexOfNewline(_buffer)) >= 0)
        {
            var line = _buffer.ToString(0, nl).TrimEnd('\r');
            _buffer.Remove(0, nl + 1);

            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var ev = JsonUtility.FromJson<OllamaStreamChunk>(line);
                if (ev != null)
                {
                    if (ev.message != null && !string.IsNullOrEmpty(ev.message.content))
                        _accum.Append(ev.message.content);
                    if (ev.done) return;
                }
            }
            catch { /* 忽略异常行 */ }
        }
    }

    int IndexOfNewline(StringBuilder sb)
    {
        for (int i = 0; i < sb.Length; i++)
            if (sb[i] == '\n') return i;
        return -1;
    }

    void UpdateUIFinal(string text)
    {
        if (resultText) resultText.text = text;
    }

    // ---- 自动绑定 UI ----
    void BindUIByType()
    {
        var canvas = FindObjectOfType<Canvas>();
        if (!canvas) return;

        if (!inputField)
            inputField = canvas.GetComponentsInChildren<TMP_InputField>(true).FirstOrDefault();
        if (!sendButton)
            sendButton = canvas.GetComponentsInChildren<Button>(true).FirstOrDefault();
        if (!resultText)
        {
            var texts = canvas.GetComponentsInChildren<TMP_Text>(true);
            resultText = texts.FirstOrDefault(t =>
                t.GetComponentInParent<Button>() == null &&
                !t.name.ToLower().Contains("placeholder"));
        }
    }

    string Trim(string s) => string.IsNullOrEmpty(s) ? s : (s.EndsWith("/") ? s[..^1] : s);

    // ---- 数据结构 ----
    public enum BackendType { OpenAICompatible, Ollama }

    [System.Serializable]
    public class Msg
    {
        public string role;
        public string content;
    }

    [System.Serializable]
    public class OpenAICompatPayload
    {
        public string model;
        public bool stream;
        public Msg[] messages;
    }

    // OpenAI 流式事件
    [System.Serializable]
    public class OpenAIStreamEvent
    {
        public OpenAIStreamChoice[] choices;
    }

    [System.Serializable]
    public class OpenAIStreamChoice
    {
        public Delta delta;
        public string finish_reason;
    }

    [System.Serializable]
    public class Delta
    {
        public string content;
        public string role;
    }

    // Ollama payload/stream
    [System.Serializable]
    public class OllamaPayload
    {
        public string model;
        public bool stream;
        public Msg[] messages;
    }

    [System.Serializable]
    public class OllamaMessage
    {
        public string role;
        public string content;
    }

    [System.Serializable]
    public class OllamaStreamChunk
    {
        public OllamaMessage message;
        public bool done;
    }

    // 带情绪的 JSON 格式：{"reply":"...","emotion":"..."}
    [System.Serializable]
    public class LLMEmotionResponse
    {
        public string reply;
        public string emotion;
    }

    // 自签证书（仅调试）
    class PermissiveCert : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] d) => true;
    }
}

// —— 小型流式下载处理器：每到一块字节就回调 —— //
public class StreamingHandler : DownloadHandlerScript
{
    private readonly Action<byte[], int> _onBytes;
    private readonly StringBuilder _all = new StringBuilder(); // 可选：仅在错误时展示

    public StreamingHandler(Action<byte[], int> onBytes) : base()
    {
        _onBytes = onBytes;
    }

    protected override bool ReceiveData(byte[] data, int dataLength)
    {
        if (data == null || dataLength == 0) return false;

        _onBytes?.Invoke(data, dataLength);
        _all.Append(Encoding.UTF8.GetString(data, 0, dataLength));
        return true;
    }

    public string GetAll() => _all.ToString();
}

