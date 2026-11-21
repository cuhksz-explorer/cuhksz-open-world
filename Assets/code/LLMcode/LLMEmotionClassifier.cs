using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using System.Text;

public class LLMEmotionClassifier : MonoBehaviour
{
    [Header("复用聊天客户端配置（拖入 LLMChatClient_LocalQwen）")]
    public LLMChatClient_LocalQwen chat;

    [Header("分类提示词（system）")]
    [TextArea(6,16)]
    public string classifierSystemPrompt =
@"You are an emotion+action classifier for a game character.
Return ONLY compact JSON and nothing else.
Allowed moods: [""neutral"",""happy"",""angry""]
Allowed actions: [""idle"",""wave"",""arms_akimbo""]
Rules:
- If user is friendly or assistant's reply is positive -> happy + wave
- If user is offensive/complaining/strong negative tone -> angry + arms_akimbo
- Otherwise -> neutral + idle
- Output exactly: {""mood"":""..."",""action"":""...""}.";

    [System.Serializable] public class Msg { public string role; public string content; }
    [System.Serializable] public class OpenAICompatPayload { public string model; public bool stream; public Msg[] messages; }
    [System.Serializable] public class Choice { public Msg message; }
    [System.Serializable] public class OpenAICompatResponse { public Choice[] choices; }
    [System.Serializable] public class Decision { public string mood; public string action; }

    public System.Action<Decision> OnDecisionReady;

    void OnEnable()
    {
        if (chat != null) chat.OnTurnFinished += HandleTurn;
    }
    void OnDisable()
    {
        if (chat != null) chat.OnTurnFinished -= HandleTurn;
    }

    void HandleTurn(string user, string assistant)
    {
        StartCoroutine(Classify(user, assistant));
    }

    IEnumerator Classify(string user, string assistant)
    {
        string url, json;

        // 统一按 OpenAI 兼容格式发（Ollama 也接受 messages 结构）
        if (chat.backend == LLMChatClient_LocalQwen.BackendType.OpenAICompatible)
            url = TrimBase(chat.apiBase) + "/v1/chat/completions";
        else
            url = TrimBase(chat.apiBase) + "/api/chat";

        var msgs = new Msg[]
        {
            new Msg{ role="system", content=classifierSystemPrompt },
            new Msg{ role="user", content=$"player: {user}\nassistant: {assistant}" }
        };
        var payload = new OpenAICompatPayload { model = chat.model, stream = false, messages = msgs };
        json = JsonUtility.ToJson(payload);

        using (var req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = 20;
            req.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(chat.apiKey))
                req.SetRequestHeader("Authorization", "Bearer " + chat.apiKey);
            if (chat.allowSelfSigned) req.certificateHandler = new PermissiveCert();

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                string raw = req.downloadHandler.text;
                var decision = TryParseDecision(raw);
                if (decision == null) decision = new Decision{ mood="neutral", action="idle" };
                OnDecisionReady?.Invoke(decision);
            }
            else
            {
                OnDecisionReady?.Invoke(new Decision{ mood="neutral", action="idle" });
            }
        }
    }

    LLMEmotionClassifier.Decision TryParseDecision(string raw)
    {
        // 优先解析 choices[0].message.content 里的 JSON
        try{
            var r = JsonUtility.FromJson<OpenAICompatResponse>(raw);
            var content = r?.choices!=null && r.choices.Length>0 ? r.choices[0].message.content : null;
            if (!string.IsNullOrEmpty(content))
            {
                int l = content.IndexOf('{'); int rr = content.LastIndexOf('}');
                if (l>=0 && rr>l)
                {
                    var json = content.Substring(l, rr-l+1);
                    return JsonUtility.FromJson<Decision>(json);
                }
            }
        } catch {}

        // 兜底：原文直接就是 JSON
        try{ return JsonUtility.FromJson<Decision>(raw); } catch {}
        return null;
    }

    class PermissiveCert : CertificateHandler { protected override bool ValidateCertificate(byte[] d)=>true; }
    string TrimBase(string s){ if (string.IsNullOrEmpty(s)) return s; return s.EndsWith("/")? s.Substring(0,s.Length-1):s; }
}

