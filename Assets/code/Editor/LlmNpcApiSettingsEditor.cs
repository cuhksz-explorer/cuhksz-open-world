#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 编辑千问 API 配置（Key 仅存本地 ScriptableObject，勿提交版本库）。
/// </summary>
public static class LlmNpcApiSettingsEditor
{
    const string MenuPath = "Tools/Quest System/配置千问 API（LLM NPC）";

    [MenuItem(MenuPath, false, 48)]
    static void OpenSettings()
    {
        var settings = LlmNpcApiSettings.LoadOrCreateDefault();
        Selection.activeObject = settings;
        EditorGUIUtility.PingObject(settings);
        EditorUtility.DisplayDialog(
            "千问 API 配置",
            "已在 Project 中选中 LlmNpcApiSettings。\n\n" +
            "• apiBase: https://dashscope.aliyuncs.com/compatible-mode\n" +
            "• model: qwen-plus\n" +
            "• 请在 Inspector 填写 apiKey（sk-...）\n\n" +
            "请勿将含 Key 的 .asset 提交到 Git。",
            "确定");
    }
}
#endif
