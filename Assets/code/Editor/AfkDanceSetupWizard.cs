#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
 
/// <summary>
/// 从 Downloads 导入待机动画 FBX，生成 PlayerAfkDance.controller，并挂到玩家。
/// </summary>
public static class AfkDanceSetupWizard
{
    const string AfkFolder = "Assets/Animations/AfkDance";
    const string ControllerPath = AfkFolder + "/PlayerAfkDance.controller";

    /// <summary>从 Downloads 复制并参与随机的待机动画（文件名需完全一致）。</summary>
    static readonly string[] s_fbxFileNames =
    {
        "Female Sitting Pose.fbx", 
        "Breakdance Freeze Var 3.fbx",
        "Silly Dancing.fbx",
        "House Dancing.fbx",
        "Hokey Pokey.fbx",
        "Hip Hop Dancing (2).fbx",
        "Flair (1).fbx",
    };

    const float MinClipLengthSeconds = 0.5f;

    [MenuItem("Tools/待机动画/一键配置（10秒无操作随机待机动画）", false, 10)]
    [MenuItem("Tools/Afk Dance/Setup Idle Dances", false, 10)]
    public static void RunSetup()
    {
        if (!ChairSitEditorGuard.EnsureNotPlaying())
            return;

        EnsureFbxFromDownloads();
        ConfigureFbxImporters();
        AssetDatabase.Refresh();

        var lines = new StringBuilder();
        bool anyMissing = false;
        bool anyTooShort = false;

        var playableClips = new List<AnimationClip>();
        var playableSources = new List<string>();

        for (int i = 0; i < s_fbxFileNames.Length; i++)
        {
            string fbx = s_fbxFileNames[i];
            AnimationClip clip = FindClipByExactFbxFile(fbx);
            lines.AppendLine(FormatClipLine(i, fbx, clip, ref anyMissing, ref anyTooShort));

            if (clip != null && clip.length >= MinClipLengthSeconds)
            {
                playableClips.Add(clip);
                playableSources.Add(fbx);
            }
        }

        if (HasDuplicateClips(playableClips.ToArray()))
            lines.AppendLine("\n警告：有重复 Clip，请检查 FBX 是否导入了相同片段。");

        int skipped = s_fbxFileNames.Length - playableClips.Count;
        if (skipped > 0)
            lines.AppendLine($"\n已跳过 {skipped} 个过短/缺失片段（时长 < {MinClipLengthSeconds:F1} 秒）。");

        var controller = CreateOrUpdateController(playableClips.ToArray());
        var stateNames = BuildStateNames(playableClips.Count);

        var player = FindPlayer();
        if (player == null)
        {
            EditorUtility.DisplayDialog("待机动画", "动画已导入，但未找到带 PlayerMovement 的玩家。", "确定");
            return;
        }

        var afk = player.GetComponent<PlayerAfkIdleDance>();
        if (afk == null)
            afk = Undo.AddComponent<PlayerAfkIdleDance>(player);

        afk.afkDanceController = controller;
        afk.idleSeconds = 10f;
        afk.danceStateNames = stateNames;
        afk.afkDanceClips = playableClips.ToArray();
        EditorUtility.SetDirty(afk);

        MarkScenesDirty();
        AssetDatabase.SaveAssets();

        float pct = playableClips.Count > 0 ? 100f / playableClips.Count : 0f;
        string footer = anyMissing
            ? "\n有文件未在 Downloads 找到，请把 FBX 放进下载文件夹后重试。"
            : playableClips.Count == 0
                ? "\n没有可播放片段。请确认 Mixamo 下载的是 Animation（In Place），不是 Pose。"
                : $"\n共 {playableClips.Count} 个可播放待机动画，随机各约 {pct:F0}%。请 Ctrl+S 保存场景后 Play 测试。";

        EditorUtility.DisplayDialog(
            "待机动画",
            $"已配置玩家「{player.name}」。\n\n" +
            "10 秒无键盘/鼠标操作后，随机等概率循环播放：\n" +
            lines + footer,
            "确定");

        Debug.Log("[待机动画] 配置完成\n" + lines);
    }

    static string[] BuildStateNames(int count)
    {
        var names = new string[count];
        for (int i = 0; i < count; i++)
            names[i] = "Dance" + i;
        return names;
    }

    static string FormatClipLine(int index, string fbxName, AnimationClip clip, ref bool anyMissing, ref bool anyTooShort)
    {
        if (clip == null)
        {
            anyMissing = true;
            return $"· [{index}] {fbxName}：未找到";
        }

        bool ok = clip.length >= MinClipLengthSeconds;
        if (!ok)
            anyTooShort = true;

        string flag = ok ? "✓" : "✗过短";
        return $"· [{index}] {flag} {fbxName}\n    Clip「{clip.name}」{clip.length:F2} 秒 (ID {clip.GetInstanceID()})";
    }

    static bool HasDuplicateClips(AnimationClip[] clips)
    {
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null)
                continue;
            for (int j = i + 1; j < clips.Length; j++)
            {
                if (clips[j] == null)
                    continue;
                if (clips[i] == clips[j] || clips[i].GetInstanceID() == clips[j].GetInstanceID())
                    return true;
            }
        }
        return false;
    }

    static void EnsureFbxFromDownloads()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Animations"))
            AssetDatabase.CreateFolder("Assets", "Animations");
        if (!AssetDatabase.IsValidFolder(AfkFolder))
            AssetDatabase.CreateFolder("Assets/Animations", "AfkDance");

        string downloads = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
            "Downloads");

        foreach (string name in s_fbxFileNames)
        {
            string src = Path.Combine(downloads, name);
            string dst = Path.Combine(AfkFolder, name).Replace('\\', '/');
            if (!File.Exists(src))
            {
                Debug.LogWarning($"[待机动画] Downloads 中未找到: {src}");
                continue;
            }
            File.Copy(src, dst, true);
            Debug.Log($"[待机动画] 已复制: {name}");
        }
    }

    static void ConfigureFbxImporters()
    {
        foreach (string fbxName in s_fbxFileNames)
        {
            string path = $"{AfkFolder}/{fbxName}";
            if (!File.Exists(path))
                continue;

            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                continue;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;

            try
            {
                ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;
                ModelImporterClipAnimation best = PickLongestClip(defaults);
                if (best == null)
                {
                    var existing = importer.clipAnimations;
                    if (existing != null && existing.Length > 0)
                        best = PickLongestClip(existing);
                }

                if (best != null)
                {
                    best.name = Path.GetFileNameWithoutExtension(fbxName);
                    best.loopTime = true;
                    best.lockRootRotation = true;
                    best.lockRootHeightY = true;
                    best.lockRootPositionXZ = true;
                    importer.clipAnimations = new[] { best };
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[待机动画] 跳过片段设置 {path}: {ex.Message}");
            }

            importer.SaveAndReimport();
        }
    }

    static ModelImporterClipAnimation PickLongestClip(ModelImporterClipAnimation[] clips)
    {
        if (clips == null || clips.Length == 0)
            return null;

        ModelImporterClipAnimation best = clips[0];
        int bestFrames = FrameCount(best);
        for (int i = 1; i < clips.Length; i++)
        {
            int frames = FrameCount(clips[i]);
            if (frames > bestFrames)
            {
                bestFrames = frames;
                best = clips[i];
            }
        }
        return best;
    }

    static int FrameCount(ModelImporterClipAnimation clip)
    {
        return Mathf.RoundToInt(clip.lastFrame - clip.firstFrame);
    }

    static AnimationClip FindClipByExactFbxFile(string fbxFileName)
    {
        return FindClipByAssetPath($"{AfkFolder}/{fbxFileName}");
    }

    static AnimationClip FindClipByAssetPath(string assetPath)
    {
        assetPath = assetPath?.Replace('\\', '/');
        if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath))
            return null;

        AnimationClip best = null;
        float bestLen = -1f;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        foreach (Object obj in assets)
        {
            var clip = obj as AnimationClip;
            if (clip == null)
                continue;
            if (clip.name.StartsWith("__preview"))
                continue;

            if (clip.length > bestLen)
            {
                bestLen = clip.length;
                best = clip;
            }
        }

        return best;
    }

    static AnimatorController CreateOrUpdateController(AnimationClip[] clips)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        var root = controller.layers[0].stateMachine;
        foreach (var child in root.states.Select(s => s.state).ToArray())
            root.RemoveState(child);

        AnimatorState first = null;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null)
                continue;
            var st = root.AddState("Dance" + i, new Vector3(200 + i * 180, 0, 0));
            st.motion = clips[i];
            if (first == null)
                first = st;
        }

        if (first != null)
            root.defaultState = first;

        EditorUtility.SetDirty(controller);
        return controller;
    }

    static GameObject FindPlayer()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;
            foreach (var root in scene.GetRootGameObjects())
            {
                var pm = root.GetComponentInChildren<PlayerMovement>(true);
                if (pm != null)
                    return pm.gameObject;
            }
        }
        return null;
    }

    static void MarkScenesDirty()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded)
                EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
#endif
