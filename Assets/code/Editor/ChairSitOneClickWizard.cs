#if UNITY_EDITOR
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 新手一键：导入 Mixamo FBX、生成坐下 Animator、挂到玩家。
/// </summary>
public static class ChairSitOneClickWizard
{
    const string ChairSitFolder = "Assets/Animations/ChairSit";
    const string SitControllerPath = ChairSitFolder + "/PlayerChairSit.controller";
    const string ClipSetResourcesFolder = "Assets/Resources/ChairSit";
    const string ClipSetAssetPath = ClipSetResourcesFolder + "/ChairSitClipSet.asset";
    const string ClipSetStatusFile = "chair_sit_binding_status.txt";
    const string TeammateZipPath = @"z:\Assembly-CSharp-Editor(1).zip";
    const string WeChatFbxFolder = @"z:\微信\xwechat_files\wxid_iex8clusq8ee32_2373\msg\file\2026-05";

    [MenuItem("Tools/Chair Sit/1 Full Setup (Anim + All Benches)", false, 0)]
    [MenuItem("Tools/椅子交互/完整配置（动画 + 所有长椅）", false, 0)]
    public static void RunFullSetupWithBenchesPublic()
    {
        if (!ChairSitEditorGuard.EnsureNotPlaying())
            return;
        RunFullSetupPublic();
        InteractableChairSetup.AddChairInteractionAllBenches();
    }

    [MenuItem("Tools/Chair Sit/2 Setup Anim Only", false, 1)]
    [MenuItem("Tools/椅子交互/新手一键：导入动画并配置坐下", false, 1)]
    public static void RunFullSetupPublic()
    {
        if (!ChairSitEditorGuard.EnsureNotPlaying())
            return;
        EnsureFbxFromDownloads();
        ConfigureAllFbxImporters();
        AssetDatabase.Refresh();

        AnimationClip sitDown = FindClipFromCandidates(ChairSitClipCatalog.SitDownCandidates);
        AnimationClip sitIdle = FindClipFromCandidates(ChairSitClipCatalog.SitIdleCandidates);
        AnimationClip standUp = FindClipFromCandidates(ChairSitClipCatalog.StandUpCandidates);

        var controller = CreateOrUpdateSitController(sitDown, sitIdle, standUp);
        var player = FindPlayerInOpenScenes();
        if (player == null)
        {
            EditorUtility.DisplayDialog(
                "椅子交互",
                "动画已导入，但未在场景中找到带 PlayerMovement 的玩家。\n请打开游戏场景后重试，或手动给玩家挂 PlayerSitSequence。",
                "确定");
            return;
        }

        var sitSeq = player.GetComponent<PlayerSitSequence>();
        if (sitSeq == null)
            sitSeq = Undo.AddComponent<PlayerSitSequence>(player.gameObject);

        sitSeq.sitDownClip = sitDown;
        sitSeq.sitIdleClip = sitIdle;
        sitSeq.standUpClip = standUp;
        sitSeq.sitAnimatorController = controller;
        EditorUtility.SetDirty(sitSeq);

        SyncResourcesChairSitFolder();
        BuildClipSetAndValidate(force: true);
        MarkScenesDirty();
        AssetDatabase.SaveAssets();

        string clipInfo =
            $"站→坐: {(sitDown != null ? sitDown.name : "未找到")}\n" +
            $"坐着: {(sitIdle != null ? sitIdle.name : "未找到")}\n" +
            $"站起: {(standUp != null ? standUp.name : "未找到")}";

        if (sitDown == null || sitIdle == null || standUp == null)
        {
            EditorUtility.DisplayDialog(
                "椅子交互 · 动画缺失",
                "坐下 Mixamo FBX 未全部导入，角色只会平移到椅子位置，没有坐下动作。\n\n" +
                "请将以下文件放入 Assets/Animations/ChairSit 后重试：\n" +
                "· Sitting (2).fbx（站→坐）\n· Sitting Idle.fbx（坐着）\n· Sit To Stand (1).fbx（站起）\n\n" +
                "然后再次运行本菜单。",
                "确定");
            return;
        }

        EditorUtility.DisplayDialog(
            "椅子交互",
            $"已配置玩家「{player.name}」。\n\n{clipInfo}\n\n下一步：\n" +
            "Tools → 椅子交互 → 为场景中所有 bench 添加坐下交互\n然后 Ctrl+S 保存场景。",
            "确定");

        Debug.Log("[椅子交互] 新手一键配置完成。\n" + clipInfo);
    }

    static void EnsureFbxFromDownloads()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Animations"))
            AssetDatabase.CreateFolder("Assets", "Animations");
        if (!AssetDatabase.IsValidFolder(ChairSitFolder))
            AssetDatabase.CreateFolder("Assets/Animations", "ChairSit");

        string downloads = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Downloads");
        foreach (string name in ChairSitClipCatalog.RequiredFbxFileNames)
        {
            string dst = Path.Combine(ChairSitFolder, name).Replace('\\', '/');
            if (File.Exists(dst))
                continue;

            string fromDownloads = Path.Combine(downloads, name);
            if (File.Exists(fromDownloads))
            {
                File.Copy(fromDownloads, dst, true);
                Debug.Log($"[椅子交互] 已从 Downloads 复制: {name}");
                continue;
            }

            string fromWeChat = Path.Combine(WeChatFbxFolder, name);
            if (File.Exists(fromWeChat))
            {
                File.Copy(fromWeChat, dst, true);
                Debug.Log($"[椅子交互] 已从微信文件目录复制: {name}");
                continue;
            }

            if (TryExtractFbxFromTeammateZip(name, dst))
                Debug.Log($"[椅子交互] 已从队友包 zip 解压: {name}");
            else
                Debug.LogWarning($"[椅子交互] 未找到 {name}（Downloads / 微信目录 / zip 均无此文件）");
        }

        SyncResourcesChairSitFolder();
    }

    static void CopyOptionalFbx(string fileName, string downloadsFolder)
    {
        string dst = Path.Combine(ChairSitFolder, fileName).Replace('\\', '/');
        if (File.Exists(dst))
            return;

        string fromWeChat = Path.Combine(WeChatFbxFolder, fileName);
        if (File.Exists(fromWeChat))
        {
            File.Copy(fromWeChat, dst, true);
            return;
        }

        string fromDownloads = Path.Combine(downloadsFolder, fileName);
        if (File.Exists(fromDownloads))
            File.Copy(fromDownloads, dst, true);
    }

    static bool TryExtractFbxFromTeammateZip(string fileName, string dst)
    {
        if (!File.Exists(TeammateZipPath))
            return false;

        try
        {
            using (var zip = ZipFile.OpenRead(TeammateZipPath))
            {
                string entryPath = "Assets/Animations/ChairSit/" + fileName;
                var entry = zip.GetEntry(entryPath);
                if (entry == null)
                    return false;

                Directory.CreateDirectory(Path.GetDirectoryName(dst) ?? ".");
                using (Stream stream = entry.Open())
                using (FileStream file = File.Create(dst))
                    stream.CopyTo(file);
                return true;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[椅子交互] 解压 zip 失败 ({fileName}): {ex.Message}");
            return false;
        }
    }

    static void SyncResourcesChairSitFolder()
    {
        const string resourcesFolder = "Assets/Resources/ChairSit";
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(resourcesFolder))
            AssetDatabase.CreateFolder("Assets/Resources", "ChairSit");

        string[] syncNames =
        {
            "Sitting (2).fbx",
            "Sitting Idle.fbx",
            "Sit To Stand.fbx",
            "Sit To Stand (1).fbx",
            "PlayerChairSit.controller"
        };

        foreach (string name in syncNames)
        {
            string src = Path.Combine(ChairSitFolder, name).Replace('\\', '/');
            string dst = Path.Combine(resourcesFolder, name).Replace('\\', '/');
            TrySyncOneResourceFile(src, dst, name);
        }

        AssetDatabase.Refresh();
    }

    static void TrySyncOneResourceFile(string src, string dst, string displayName)
    {
        if (!File.Exists(src))
            return;

        if (File.Exists(dst))
        {
            try
            {
                var srcInfo = new FileInfo(src);
                var dstInfo = new FileInfo(dst);
                if (srcInfo.Length == dstInfo.Length)
                    return;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[椅子交互] Resources 已存在 {displayName}，跳过覆盖（{ex.Message}）");
                return;
            }
        }

        try
        {
            if (File.Exists(dst))
            {
                if (!AssetDatabase.DeleteAsset(dst))
                {
                    Debug.LogWarning($"[椅子交互] 无法覆盖 Resources/{displayName}（文件可能被 Unity 占用），已跳过。");
                    return;
                }
            }

            if (!AssetDatabase.CopyAsset(src, dst))
                Debug.LogWarning($"[椅子交互] AssetDatabase 复制失败: {displayName}");
        }
        catch (System.UnauthorizedAccessException)
        {
            Debug.LogWarning($"[椅子交互] 无权限写入 Resources/{displayName}（可能被 Unity 导入占用），已跳过。运行时仍可使用 Animations/ChairSit 中的资源。");
        }
        catch (System.IO.IOException ex)
        {
            Debug.LogWarning($"[椅子交互] 跳过 Resources 同步 {displayName}: {ex.Message}");
        }
    }

    static void ConfigureAllFbxImporters()
    {
        ConfigureFbxImportersInFolder(ClipSetResourcesFolder);
    }

    static void ConfigureFbxImportersInFolder(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
            return;

        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { folder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                continue;

            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                continue;

            if (HasStalePlayerAvatarMapping(importer))
                ResetModelImporterRig(path);

            importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                continue;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            importer.autoGenerateAvatarMappingIfUnspecified = true;

            string fileName = Path.GetFileNameWithoutExtension(path);
            bool loop = fileName.IndexOf("Talking", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || string.Equals(fileName, "Sitting", System.StringComparison.OrdinalIgnoreCase);

            try
            {
                var clips = importer.clipAnimations;
                if (clips == null || clips.Length == 0)
                    clips = importer.defaultClipAnimations;
                if (clips != null && clips.Length > 0)
                {
                    clips[0].loopTime = loop;
                    clips[0].lockRootRotation = true;
                    clips[0].lockRootHeightY = true;
                    clips[0].lockRootPositionXZ = true;
                    clips[0].keepOriginalOrientation = true;
                    clips[0].keepOriginalPositionY = true;
                    clips[0].keepOriginalPositionXZ = true;
                    importer.clipAnimations = clips;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[椅子交互] 跳过 FBX 片段细调（{path}）: {ex.Message}");
            }

            try
            {
                importer.SaveAndReimport();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[椅子交互] FBX 导入失败（{path}）: {ex.Message}");
            }
        }
    }

    static bool HasStalePlayerAvatarMapping(ModelImporter importer)
    {
        if (importer == null)
            return false;

        var human = importer.humanDescription.human;
        if (human == null || human.Length == 0)
            return false;

        for (int i = 0; i < human.Length; i++)
        {
            if (human[i].humanName == "Hips"
                && !string.IsNullOrEmpty(human[i].boneName)
                && human[i].boneName.IndexOf("mixamorig", System.StringComparison.OrdinalIgnoreCase) < 0)
                return true;
        }

        return false;
    }

    static void ResetModelImporterRig(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
            return;

        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
        importer.sourceAvatar = null;
        importer.SaveAndReimport();
    }

    static AnimationClip FindClipFromCandidates(string[] candidates)
    {
        if (candidates == null || candidates.Length == 0)
            return null;

        foreach (string folder in new[] { ClipSetResourcesFolder, ChairSitFolder })
        {
            if (!AssetDatabase.IsValidFolder(folder))
                continue;

            string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { folder });
            var clips = new AnimationClip[guids.Length];
            for (int i = 0; i < guids.Length; i++)
                clips[i] = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guids[i]));

            var picked = ChairSitClipCatalog.PickClip(clips, candidates);
            if (picked != null)
                return picked;
        }

        for (int c = 0; c < candidates.Length; c++)
        {
            var legacy = FindClip(candidates[c]);
            if (legacy != null)
                return legacy;
        }

        return null;
    }

    static AnimationClip FindClip(string nameContains)
    {
        foreach (string folder in new[] { ClipSetResourcesFolder, ChairSitFolder })
        {
            if (!AssetDatabase.IsValidFolder(folder))
                continue;

            string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null)
                    continue;
                if (clip.name.IndexOf(nameContains, System.StringComparison.OrdinalIgnoreCase) >= 0
                    || Path.GetFileNameWithoutExtension(path).IndexOf(nameContains, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return clip;
            }
        }

        return null;
    }

    static AnimatorController CreateOrUpdateSitController(AnimationClip sitDown, AnimationClip sitIdle, AnimationClip standUp)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(SitControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(SitControllerPath);
        }

        var root = controller.layers[0].stateMachine;
        var existing = root.states.Select(s => s.state).ToArray();
        foreach (var st in existing)
            root.RemoveState(st);

        var stDown = sitDown != null ? root.AddState("SitDown", new Vector3(280, 0, 0)) : null;
        var stIdle = sitIdle != null ? root.AddState("SitIdle", new Vector3(520, 0, 0)) : null;
        var stUp = standUp != null ? root.AddState("StandUp", new Vector3(760, 0, 0)) : null;

        if (stDown != null)
        {
            stDown.motion = sitDown;
            root.defaultState = stDown;
        }
        else if (stIdle != null)
            root.defaultState = stIdle;

        if (stIdle != null)
        {
            stIdle.motion = sitIdle;
            if (stDown != null)
            {
                var t = stDown.AddTransition(stIdle);
                t.hasExitTime = true;
                t.exitTime = 0.92f;
                t.duration = 0.15f;
            }
        }

        if (stUp != null)
        {
            stUp.motion = standUp;
            if (stIdle != null)
            {
                var t = stIdle.AddTransition(stUp);
                t.hasExitTime = false;
                t.duration = 0.1f;
                t.AddCondition(AnimatorConditionMode.If, 0, "Stand");
            }
        }

        if (!controller.parameters.Any(p => p.name == "Stand"))
            controller.AddParameter("Stand", AnimatorControllerParameterType.Trigger);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    [MenuItem("Tools/Chair Sit/Rebuild Sit Controller", false, 5)]
    [MenuItem("Tools/椅子交互/修复坐下 Animator 引用", false, 5)]
    public static void RebuildSitControllerMenu()
    {
        if (!ChairSitEditorGuard.EnsureNotPlaying())
            return;
        if (RebuildSitControllerFromFolder())
            EditorUtility.DisplayDialog("椅子交互", "已重建 PlayerChairSit.controller 动画引用。", "确定");
        else
            EditorUtility.DisplayDialog("椅子交互", "未找到坐下 FBX，请先运行完整配置或确认 Assets/Animations/ChairSit 下有 Mixamo 文件。", "确定");
    }

    /// <summary>用当前 ChairSit 文件夹内的 FBX 重建 Controller（Editor API 写入正确 GUID）。</summary>
    public static bool RebuildSitControllerFromFolder()
    {
        if (!File.Exists(Path.Combine(ChairSitFolder, "Sitting (2).fbx"))
            && !File.Exists(Path.Combine(ChairSitFolder, "Sitting (1).fbx"))
            && !File.Exists(Path.Combine(ChairSitFolder, "Sitting.fbx")))
            return false;

        ConfigureAllFbxImporters();
        AssetDatabase.Refresh();

        AnimationClip sitDown = FindClipFromCandidates(ChairSitClipCatalog.SitDownCandidates);
        AnimationClip sitIdle = FindClipFromCandidates(ChairSitClipCatalog.SitIdleCandidates);
        AnimationClip standUp = FindClipFromCandidates(ChairSitClipCatalog.StandUpCandidates);
        if (sitDown == null && sitIdle == null && standUp == null)
            return false;

        CreateOrUpdateSitController(sitDown, sitIdle, standUp);
        SyncResourcesChairSitFolder();
        AssetDatabase.SaveAssets();
        return sitDown != null || sitIdle != null || standUp != null;
    }

    [MenuItem("Tools/椅子交互/重建 ClipSet 并验证绑定", false, 6)]
    public static void BuildClipSetAndValidateMenu()
    {
        if (!ChairSitEditorGuard.EnsureNotPlaying())
            return;
        BuildClipSetAndValidate(force: true);
    }

    public static void BuildClipSetAndValidate(bool force = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        EnsureFbxFromDownloads();
        ConfigureAllFbxImporters();
        AssetDatabase.Refresh();

        EnsureClipSetResourcesFolder();
        var set = LoadOrCreateClipSetAsset();
        if (set == null)
        {
            WriteClipSetStatus("FAILED: 无法创建 ChairSitClipSet.asset");
            return;
        }

        bool changed = AssignClipSetClips(set);
        if (changed || force)
        {
            EnsureClipSetScriptReference(set);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        if (!set.IsComplete)
            RebuildSitControllerFromFolder();
        else
            SyncResourcesChairSitFolder();

        var sb = new StringBuilder();
        sb.AppendLine("Chair Sit Binding Status");
        sb.AppendLine("Generated: " + System.DateTime.Now);
        sb.AppendLine("Asset: " + ClipSetAssetPath);
        AppendClipSetLine(sb, "SitDown", set.sitDownClip, "Sitting (2).fbx");
        AppendClipSetLine(sb, "SitIdle", set.sitIdleClip, "Sitting Idle.fbx");
        AppendClipSetLine(sb, "StandUp", set.standUpClip, "Sit To Stand (1).fbx");
        sb.AppendLine("Complete: " + set.IsComplete);

        WriteClipSetStatus(sb.ToString());
        if (set.IsComplete)
            Debug.Log("[椅子交互] ClipSet 绑定完成。\n" + sb);
        else
            Debug.LogWarning("[椅子交互] ClipSet 未完整绑定。\n" + sb);
    }

    static void EnsureClipSetResourcesFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(ClipSetResourcesFolder))
            AssetDatabase.CreateFolder("Assets/Resources", "ChairSit");
    }

    static ChairSitClipSet LoadOrCreateClipSetAsset()
    {
        var existing = AssetDatabase.LoadAssetAtPath<ChairSitClipSet>(ClipSetAssetPath);
        if (existing != null)
        {
            EnsureClipSetScriptReference(existing);
            return existing;
        }

        var created = ScriptableObject.CreateInstance<ChairSitClipSet>();
        AssetDatabase.CreateAsset(created, ClipSetAssetPath);
        EnsureClipSetScriptReference(created);
        return created;
    }

    static void EnsureClipSetScriptReference(ChairSitClipSet set)
    {
        if (set == null)
            return;

        var script = MonoScript.FromScriptableObject(set);
        if (script == null)
            return;

        var so = new SerializedObject(set);
        var scriptProp = so.FindProperty("m_Script");
        if (scriptProp != null && scriptProp.objectReferenceValue != script)
        {
            scriptProp.objectReferenceValue = script;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(set);
        }
    }

    static bool AssignClipSetClips(ChairSitClipSet set)
    {
        bool changed = false;
        changed |= AssignClipField(ref set.sitDownClip, LoadClipSetClipFromFbx("Sitting (2).fbx", ChairSitClipCatalog.SitDownCandidates));
        changed |= AssignClipField(ref set.sitIdleClip, LoadClipSetClipFromFbx("Sitting Idle.fbx", ChairSitClipCatalog.SitIdleCandidates));
        changed |= AssignClipField(ref set.standUpClip, LoadClipSetClipFromFbx("Sit To Stand (1).fbx", ChairSitClipCatalog.StandUpCandidates));
        return changed;
    }

    static bool AssignClipField(ref AnimationClip field, AnimationClip value)
    {
        if (field == value)
            return false;
        field = value;
        return true;
    }

    static AnimationClip LoadClipSetClipFromFbx(string fbxFileName, string[] candidates)
    {
        string path = ResolveChairSitFbxPath(fbxFileName, candidates);
        if (string.IsNullOrEmpty(path))
            return null;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        if (assets == null || assets.Length == 0)
            return null;

        var clips = new System.Collections.Generic.List<AnimationClip>();
        for (int i = 0; i < assets.Length; i++)
        {
            var clip = assets[i] as AnimationClip;
            if (clip == null || clip.name.StartsWith("__preview"))
                continue;
            clips.Add(clip);
        }

        if (clips.Count == 0)
            return null;

        var picked = ChairSitClipCatalog.PickClip(clips.ToArray(), candidates);
        if (picked != null)
            return picked;

        return clips[0];
    }

    static string ResolveChairSitFbxPath(string fbxFileName, string[] candidates)
    {
        string[] folders = { ClipSetResourcesFolder, ChairSitFolder };
        for (int f = 0; f < folders.Length; f++)
        {
            string path = folders[f] + "/" + fbxFileName;
            if (File.Exists(path))
                return path.Replace('\\', '/');
        }

        for (int f = 0; f < folders.Length; f++)
        {
            foreach (string candidate in candidates)
            {
                var found = FindClipSetFbxByNamePart(candidate, folders[f]);
                if (!string.IsNullOrEmpty(found))
                    return found;
            }
        }

        return null;
    }
    static string FindClipSetFbxByNamePart(string namePart, string folder = null)
    {
        folder = string.IsNullOrEmpty(folder) ? ChairSitFolder : folder;
        if (string.IsNullOrEmpty(namePart) || !AssetDatabase.IsValidFolder(folder))
            return null;

        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { folder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (path.IndexOf(namePart, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return path;
        }

        return null;
    }

    static void AppendClipSetLine(StringBuilder sb, string label, AnimationClip clip, string expectedFbx)
    {
        if (clip == null)
            sb.AppendLine(label + ": MISSING (expected " + expectedFbx + ")");
        else
            sb.AppendLine(label + ": OK '" + clip.name + "' length=" + clip.length.ToString("F2") + "s");
    }

    static void WriteClipSetStatus(string text)
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string path = Path.Combine(projectRoot ?? ".", ClipSetStatusFile);
        File.WriteAllText(path, text, Encoding.UTF8);
    }

    static GameObject FindPlayerInOpenScenes()
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
