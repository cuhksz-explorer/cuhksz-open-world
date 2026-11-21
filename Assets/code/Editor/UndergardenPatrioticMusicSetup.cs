#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 在 undergarden 指定坐标放置《我和我的祖国》近距离音乐触发器。
/// </summary>
public static class UndergardenPatrioticMusicSetup
{
    const string MenuPath = "Tools/场景配置/undergarden 添加我和我的祖国音乐点";
    const string ScenePath = "Assets/scene/undergarden.unity";
    const string ClipPath = "Assets/Music/我和我的祖国.mp3";
    const string ResourcesClipPath = "Assets/Resources/Music/我和我的祖国.mp3";
    const string TriggerObjectName = "PatrioticMusicTrigger_我和我的祖国";

    static readonly Vector3 DefaultPosition = new Vector3(-1360.78003f, 13.6599998f, 1100.43005f);

    [MenuItem(MenuPath, false, 12)]
    public static void SetupInUndergarden()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("音乐点", "请先退出 Play Mode。", "确定");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            EditorUtility.DisplayDialog("音乐点", "无法打开 undergarden 场景。", "确定");
            return;
        }

        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
        if (clip == null)
            clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ResourcesClipPath);
        if (clip == null)
        {
            EditorUtility.DisplayDialog(
                "音乐点",
                $"未找到音频：\n{ClipPath}\n或 {ResourcesClipPath}\n\n请将《我和我的祖国》mp3 放到上述任一路径。\n也可运行：py -3 Tools/download_wodezuoguo.py",
                "确定");
            return;
        }

        var existing = GameObject.Find(TriggerObjectName);
        GameObject go;
        if (existing != null)
        {
            go = existing;
            Undo.RecordObject(go, "更新爱国音乐触发器");
        }
        else
        {
            go = new GameObject(TriggerObjectName);
            Undo.RegisterCreatedObjectUndo(go, "创建爱国音乐触发器");
        }

        go.transform.position = DefaultPosition;

        var trigger = go.GetComponent<ProximityMusicTrigger>();
        if (trigger == null)
            trigger = Undo.AddComponent<ProximityMusicTrigger>(go);

        var source = go.GetComponent<AudioSource>();
        if (source == null)
            source = Undo.AddComponent<AudioSource>(go);

        SerializedObject so = new SerializedObject(trigger);
        so.FindProperty("triggerWorldPosition").vector3Value = DefaultPosition;
        so.FindProperty("triggerRadius").floatValue = 20f;
        so.FindProperty("musicClip").objectReferenceValue = clip;
        so.FindProperty("volume").floatValue = 0.22f;
        so.FindProperty("useHorizontalDistance").boolValue = true;
        so.FindProperty("loop").boolValue = true;
        so.FindProperty("fadeInDuration").floatValue = 2.5f;
        so.FindProperty("fadeOutDuration").floatValue = 2f;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);

        EditorUtility.DisplayDialog(
            "完成",
            $"已在 undergarden 放置音乐触发器。\n\n坐标：{DefaultPosition}\n半径：20m\n音量：0.22（较轻）\n\n请保存场景后进入游戏测试。",
            "确定");
    }

    [MenuItem("Tools/下载 我和我的祖国 音频", false, 13)]
    public static void DownloadAudioMenu()
    {
        string script = Path.Combine(Application.dataPath, "..", "Tools", "download_wodezuoguo.py");
        script = Path.GetFullPath(script);
        if (!File.Exists(script))
        {
            EditorUtility.DisplayDialog("下载", "未找到 Tools/download_wodezuoguo.py", "确定");
            return;
        }

        bool ok = EditorUtility.DisplayDialog(
            "下载音频",
            "将尝试用 yt-dlp 下载到 Assets/Music/我和我的祖国.mp3（需可访问 YouTube）。\n\n完成后请把文件复制到 Assets/Resources/Music/ 以便运行时自动加载。\n\n是否继续？",
            "下载",
            "取消");
        if (!ok)
            return;

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "py",
            Arguments = $"-3 \"{script}\"",
            WorkingDirectory = Path.GetDirectoryName(script),
            UseShellExecute = false,
            CreateNoWindow = false,
        };
        try
        {
            System.Diagnostics.Process.Start(psi);
            EditorUtility.DisplayDialog("下载", "已在终端启动下载脚本，完成后在 Unity 中 Refresh 并复制到 Resources/Music。", "确定");
        }
        catch (System.Exception ex)
        {
            EditorUtility.DisplayDialog("下载", "启动失败：" + ex.Message, "确定");
        }
    }
    [MenuItem("Tools/下载 游戏音效资源 (Kenney CC0)", false, 14)]
    public static void DownloadGameAudioMenu()
    {
        string script = Path.Combine(Application.dataPath, "..", "Tools", "download_game_audio.py");
        script = Path.GetFullPath(script);
        if (!File.Exists(script))
        {
            EditorUtility.DisplayDialog("下载", "未找到 Tools/download_game_audio.py", "确定");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "下载游戏音效",
                "将从 Kenney（CC0）下载脚步、UI、BGM、环境音等到 Assets/Music/。\n\n是否继续？",
                "下载",
                "取消"))
            return;

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "py",
            Arguments = $"-3 \"{script}\"",
            WorkingDirectory = Path.GetDirectoryName(script),
            UseShellExecute = false,
            CreateNoWindow = false,
        };
        try
        {
            System.Diagnostics.Process.Start(psi);
            EditorUtility.DisplayDialog("下载", "已在终端启动下载脚本。完成后在 Unity 中 Refresh，再运行：\nTools/场景配置/一键配置所有场景音效", "确定");
        }
        catch (System.Exception ex)
        {
            EditorUtility.DisplayDialog("下载", "启动失败：" + ex.Message, "确定");
        }
    }
}
#endif
