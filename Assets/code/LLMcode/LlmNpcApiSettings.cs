using UnityEngine;

/// <summary>
/// 千问 / DashScope 等 LLM NPC 的全局 API 与人格配置。建议将生成的 .asset 加入 .gitignore，勿提交 API Key。
/// </summary>
[CreateAssetMenu(fileName = "LlmNpcApiSettings", menuName = "LLM/Llm NPC API Settings")]
public class LlmNpcApiSettings : ScriptableObject
{
    public const string DefaultAssetPath = "Assets/Settings/LlmNpcApiSettings.asset";

    [Header("DashScope 兼容模式")]
    public LLMChatClient_LocalQwen.BackendType backend = LLMChatClient_LocalQwen.BackendType.OpenAICompatible;

    [Tooltip("不要带 /v1")]
    public string apiBase = "https://dashscope.aliyuncs.com/compatible-mode";

    public string model = "qwen-plus";

    public string apiKey = "";

    public int timeoutSeconds = 60;

    public bool verboseLog = true;

    public bool allowSelfSigned = false;

    [Header("人格（system）")]
    [TextArea(6, 24)]
    public string personaText =
        "You are a friendly wandering NPC in a Unity college-life game. " +
        "Always reply in concise Chinese unless the player uses English. " +
        "Return STRICT JSON only:\n" +
        "{\"reply\":\"<your reply>\",\"emotion\":\"<one of: idle, waving, thinking, hands_on_hip, clapping, thankful>\"}";

    public TextAsset personaAsset;

    public bool alwaysInjectPersona = true;

    public void ApplyTo(LLMChatClient_LocalQwen client)
    {
        if (client == null || this == null)
            return;

        client.backend = backend;
        client.apiBase = apiBase;
        client.model = model;
        client.apiKey = apiKey;
        client.timeoutSeconds = timeoutSeconds;
        client.verboseLog = verboseLog;
        client.allowSelfSigned = allowSelfSigned;
        client.personaText = personaText;
        client.personaAsset = personaAsset;
        client.alwaysInjectPersona = alwaysInjectPersona;
    }

#if UNITY_EDITOR
    public static LlmNpcApiSettings LoadOrCreateDefault()
    {
        var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<LlmNpcApiSettings>(DefaultAssetPath);
        if (existing != null)
            return existing;

        if (!System.IO.Directory.Exists("Assets/Settings"))
            System.IO.Directory.CreateDirectory("Assets/Settings");

        var asset = CreateInstance<LlmNpcApiSettings>();
        UnityEditor.AssetDatabase.CreateAsset(asset, DefaultAssetPath);
        UnityEditor.AssetDatabase.SaveAssets();
        return asset;
    }
#endif
}
