#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 批量为 Assets/scene 下游戏场景配置 BGM、环境音、传送音、玩家脚步音。
/// 以 uppergarden 为参考模板。
/// </summary>
public static class SceneAudioBatchSetup
{
    const string MenuPath = "Tools/场景配置/一键配置所有场景音效（uppergarden 标准）";
    const string TemplateScenePath = "Assets/scene/uppergarden.unity";
    const string SceneFolder = "Assets/scene";

    static readonly HashSet<string> SkipScenes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    struct SceneAudioProfile
    {
        public string BgmPath;
        public string AmbientPath;
        public float AmbientVolume;
        public bool Outdoor;
    }

    static readonly Dictionary<string, SceneAudioProfile> Profiles = new Dictionary<string, SceneAudioProfile>(StringComparer.OrdinalIgnoreCase)
    {
        ["uppergarden"] = Profile("BGM/BGM_uppergarden.mp3", "AMB/AMB_birds_day.mp3", 0.28f, true),
        ["middlegarden"] = Profile("BGM/BGM_middlegarden.mp3", "AMB/AMB_birds_day.mp3", 0.28f, true),
        ["undergarden"] = Profile("BGM/BGM_undergarden_day.mp3", "AMB/AMB_wind.mp3", 0.32f, true),
        ["minival"] = Profile("BGM/BGM_minival.mp3", "AMB/AMB_birds_day.mp3", 0.25f, true),
        ["lingcollegea"] = Profile("BGM/BGM_lingcollege.mp3", "AMB/AMB_birds_day.mp3", 0.22f, true),
        ["lingcollegeb"] = Profile("BGM/BGM_lingcollege.mp3", "AMB/AMB_birds_day.mp3", 0.22f, true),
        ["lingcollegec"] = Profile("BGM/BGM_lingcollege.mp3", "AMB/AMB_birds_day.mp3", 0.22f, true),
        ["dlilgentiac"] = Profile("BGM/BGM_diligentia.mp3", "AMB/AMB_birds_day.mp3", 0.22f, true),
        ["dorm"] = Profile("BGM/BGM_dorm.mp3", "AMB/AMB_dorm_night.mp3", 0.2f, false),
        ["library"] = Profile("BGM/BGM_library.mp3", "AMB/AMB_library_hvac.mp3", 0.18f, false),
        ["libraryfloor1"] = Profile("BGM/BGM_library.mp3", "AMB/AMB_library_hvac.mp3", 0.18f, false),
        ["libraryfloor2"] = Profile("BGM/BGM_library.mp3", "AMB/AMB_library_hvac.mp3", 0.18f, false),
        ["libraryfloor4"] = Profile("BGM/BGM_library.mp3", "AMB/AMB_library_hvac.mp3", 0.18f, false),
        ["classroom1"] = Profile("BGM/BGM_classroom.mp3", "AMB/AMB_indoor_corridor.mp3", 0.15f, false),
        ["teachingrooma"] = Profile("BGM/BGM_teachingroom.mp3", "AMB/AMB_indoor_corridor.mp3", 0.15f, false),
        ["computerroom"] = Profile("BGM/BGM_computerroom.mp3", "AMB/AMB_library_hvac.mp3", 0.15f, false),
        ["learning room"] = Profile("BGM/BGM_learning_room.mp3", "AMB/AMB_indoor_corridor.mp3", 0.15f, false),
        ["discussion room (td一楼）"] = Profile("BGM/BGM_discussion_room.mp3", "AMB/AMB_indoor_corridor.mp3", 0.15f, false),
        ["lecture hall(tb)"] = Profile("BGM/BGM_lecture_hall.mp3", "AMB/AMB_indoor_corridor.mp3", 0.2f, false),
        ["corridor"] = Profile("BGM/BGM_classroom.mp3", "AMB/AMB_indoor_corridor.mp3", 0.18f, false),
        ["cec"] = Profile("BGM/BGM_cec.mp3", "AMB/AMB_indoor_corridor.mp3", 0.18f, false),
        ["eighta"] = Profile("BGM/BGM_eight_building.mp3", "AMB/AMB_indoor_corridor.mp3", 0.18f, false),
        ["eigthb"] = Profile("BGM/BGM_eight_building.mp3", "AMB/AMB_indoor_corridor.mp3", 0.18f, false),
        ["shawa"] = Profile("BGM/BGM_shaw.mp3", "AMB/AMB_indoor_corridor.mp3", 0.18f, false),
        ["musec"] = Profile("BGM/BGM_muse.mp3", "AMB/AMB_indoor_corridor.mp3", 0.2f, false),
        ["lingdrom"] = Profile("BGM/BGM_lingdrom.mp3", "AMB/AMB_wind.mp3", 0.25f, true),
        ["bus"] = Profile("BGM/BGM_bus_station.mp3", "AMB/AMB_traffic_distant.mp3", 0.2f, false),
        ["loadingscene"] = Profile("BGM/BGM_Loadingscene.mp3", "BGM/BGM_Loadingscene.mp3", 0.35f, false),
    };

    static SceneAudioProfile Profile(string bgm, string amb, float ambVol, bool outdoor) =>
        new SceneAudioProfile { BgmPath = bgm, AmbientPath = amb, AmbientVolume = ambVol, Outdoor = outdoor };

    static SceneAudioProfile DefaultOutdoor => Profile("BGM/BGM_undergarden_day.mp3", "AMB/AMB_birds_day.mp3", 0.25f, true);
    static SceneAudioProfile DefaultIndoor => Profile("BGM/BGM_classroom.mp3", "AMB/AMB_indoor_corridor.mp3", 0.16f, false);

    /// <summary>Batch mode entry: Tuanjie.exe -batchmode -quit -projectPath ... -executeMethod SceneAudioBatchSetup.RunBatchSilent</summary>
    public static void RunBatchSilent()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[SceneAudioBatchSetup] Exit Play Mode before running batch audio setup.");
            EditorApplication.Exit(1);
            return;
        }

        RunBatch();
        AssetDatabase.SaveAssets();
        EditorApplication.Exit(0);
    }

    [MenuItem(MenuPath, false, 5)]
    public static void RunFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("场景音效", "请先退出 Play Mode。", "确定");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "配置所有场景音效",
                "将为 Assets/scene 下游戏场景批量配置：\n\n" +
                "· MapSystem 传送音效\n" +
                "· 玩家 PlayerStepSound + BackgroundMusic\n" +
                "· SceneAmbientAudio 环境音\n" +
                "· GameAudioManager（若缺失）\n\n" +
                "将用 Assets/Music 中最新音频覆盖场景内已有 BGM/环境音/脚步/传送配置。是否继续？",
                "继续",
                "取消"))
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        RunBatch();
    }

    public static void RunBatch()
    {
        var clips = LoadClipLibrary();
        string[] scenePaths = CollectScenePaths();
        var originalSetup = EditorSceneManager.GetSceneManagerSetup();

        int processed = 0;
        int saved = 0;
        int teleportFixed = 0;
        int stepFixed = 0;
        int ambientAdded = 0;
        int bgmFixed = 0;

        try
        {
            for (int i = 0; i < scenePaths.Length; i++)
            {
                string path = scenePaths[i];
                EditorUtility.DisplayProgressBar("场景音效", path, (float)i / Math.Max(1, scenePaths.Length));

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                if (!scene.IsValid())
                    continue;

                var profile = ResolveProfile(path);
                var result = ConfigureScene(scene, clips, profile);
                processed++;
                teleportFixed += result.TeleportFixed;
                stepFixed += result.StepFixed;
                ambientAdded += result.AmbientConfigured;
                bgmFixed += result.BgmFixed;

                if (scene.isDirty)
                {
                    EditorSceneManager.SaveScene(scene);
                    saved++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (originalSetup != null && originalSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
        }

        EditorUtility.DisplayDialog(
            "场景音效配置完成",
            $"处理场景: {processed}\n保存: {saved}\n传送音: {teleportFixed}\n脚步音: {stepFixed}\n环境音: {ambientAdded}\nBGM: {bgmFixed}",
            "确定");
    }

    struct ConfigureResult
    {
        public int TeleportFixed;
        public int StepFixed;
        public int AmbientConfigured;
        public int BgmFixed;
    }

    class ClipLibrary
    {
        public AudioClip Teleport;
        public AudioClip Walk;
        public AudioClip BgmDefault;
        public Dictionary<string, AudioClip> ByPath = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
    }

    static ClipLibrary LoadClipLibrary()
    {
        var lib = new ClipLibrary();
        lib.Teleport = LoadClip("科幻传送.mp3") ?? LoadClip("SFX/System/SFX_System_teleport_scifi.mp3");
        lib.Walk = LoadClip("水泥地走路1.mp3") ?? LoadClip("SFX/Player/SFX_Player_walk_concrete_01.mp3");
        lib.BgmDefault = LoadClip("1(3).mp3") ?? LoadClip("BGM/BGM_uppergarden.mp3");

        foreach (var kv in Profiles)
        {
            if (!string.IsNullOrEmpty(kv.Value.BgmPath))
                lib.ByPath[kv.Value.BgmPath] = LoadClip(kv.Value.BgmPath);
            if (!string.IsNullOrEmpty(kv.Value.AmbientPath))
                lib.ByPath[kv.Value.AmbientPath] = LoadClip(kv.Value.AmbientPath);
        }

        return lib;
    }

    static AudioClip LoadClip(string relativePath)
    {
        string full = $"Assets/Music/{relativePath}";
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(full);
        if (clip == null)
        {
            string ogg = Path.ChangeExtension(full, ".ogg");
            clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ogg);
        }
        return clip;
    }

    static SceneAudioProfile ResolveProfile(string scenePath)
    {
        string stem = Path.GetFileNameWithoutExtension(scenePath).ToLowerInvariant();
        if (Profiles.TryGetValue(stem, out var p))
            return p;
        if (stem.Contains("garden") || stem.Contains("lingdrom") || stem.Contains("minival") || stem.Contains("bus"))
            return DefaultOutdoor;
        return DefaultIndoor;
    }

    static ConfigureResult ConfigureScene(Scene scene, ClipLibrary clips, SceneAudioProfile profile)
    {
        var result = new ConfigureResult();
        if (!scene.IsValid())
            return result;

        EnsureGameAudioManager();

        foreach (var map in UnityEngine.Object.FindObjectsOfType<MapSystem>(true))
        {
            if (clips.Teleport != null)
            {
                Undo.RecordObject(map, "Bind teleport sound");
                map.teleportSound = clips.Teleport;
                EditorUtility.SetDirty(map);
                result.TeleportFixed++;
            }
        }

        var player = UnityEngine.Object.FindObjectOfType<PlayerMovement>(true);
        if (player != null)
        {
            var go = player.gameObject;
            var step = go.GetComponent<PlayerStepSound>();
            if (step == null)
                step = Undo.AddComponent<PlayerStepSound>(go);

            bool stepDirty = false;
            if (clips.Walk != null && step.walkClip != clips.Walk) { step.walkClip = clips.Walk; stepDirty = true; }
            if (stepDirty)
            {
                EditorUtility.SetDirty(step);
                result.StepFixed++;
            }

            var bgmClip = clips.ByPath.TryGetValue(profile.BgmPath, out var b) ? b : clips.BgmDefault;
            var bgm = go.GetComponentInChildren<BackgroundMusic>(true);
            if (bgm == null)
            {
                var bgmGo = new GameObject("BackgroundMusic");
                Undo.RegisterCreatedObjectUndo(bgmGo, "Create BGM");
                bgmGo.transform.SetParent(go.transform, false);
                bgm = Undo.AddComponent<BackgroundMusic>(bgmGo);
                bgm.dontDestroyOnLoad = false;
            }

            if (bgmClip != null)
            {
                bgm.bgmClip = bgmClip;
                bgm.playOnStart = true;
                bgm.dontDestroyOnLoad = false;
                EditorUtility.SetDirty(bgm);
                result.BgmFixed++;
            }
        }

        var ambClip = clips.ByPath.TryGetValue(profile.AmbientPath, out var a) ? a : null;
        if (ambClip != null)
        {
            const string ambName = "SceneAmbientAudio";
            var ambGo = GameObject.Find(ambName);
            if (ambGo == null)
            {
                ambGo = new GameObject(ambName);
                Undo.RegisterCreatedObjectUndo(ambGo, "Create ambient audio");
            }

            var ambient = ambGo.GetComponent<SceneAmbientAudio>();
            if (ambient == null)
                ambient = Undo.AddComponent<SceneAmbientAudio>(ambGo);
            if (ambient.GetComponent<AudioSource>() == null)
                Undo.AddComponent<AudioSource>(ambGo);

            var so = new SerializedObject(ambient);
            var clipProp = so.FindProperty("clip");
            if (clipProp.objectReferenceValue != ambClip)
            {
                clipProp.objectReferenceValue = ambClip;
                so.FindProperty("volume").floatValue = profile.AmbientVolume;
                so.FindProperty("playOnAwake").boolValue = true;
                so.FindProperty("delayOneFrame").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(ambient);
                result.AmbientConfigured++;
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        return result;
    }

    static void EnsureGameAudioManager()
    {
        if (UnityEngine.Object.FindObjectOfType<GameAudioManager>(true) != null)
            return;

        var go = GameObject.Find("GameAudioManager");
        if (go == null)
        {
            go = new GameObject("GameAudioManager");
            Undo.RegisterCreatedObjectUndo(go, "Create GameAudioManager");
        }

        if (go.GetComponent<AudioSource>() == null)
            Undo.AddComponent<AudioSource>(go);
        if (go.GetComponent<GameAudioManager>() == null)
            Undo.AddComponent<GameAudioManager>(go);
    }

    static string[] CollectScenePaths()
    {
        var list = new List<string>();
        if (!Directory.Exists(SceneFolder))
            return list.ToArray();

        foreach (string path in Directory.GetFiles(SceneFolder, "*.unity", SearchOption.TopDirectoryOnly))
        {
            string norm = path.Replace('\\', '/');
            if (SkipScenes.Contains(norm) || norm.Contains("/Backups/"))
                continue;
            list.Add(norm);
        }

        foreach (string path in Directory.GetFiles(SceneFolder, "*.scene", SearchOption.TopDirectoryOnly))
        {
            string norm = path.Replace('\\', '/');
            if (SkipScenes.Contains(norm) || norm.Contains("/Backups/"))
                continue;
            list.Add(norm);
        }

        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list.ToArray();
    }
}
#endif
