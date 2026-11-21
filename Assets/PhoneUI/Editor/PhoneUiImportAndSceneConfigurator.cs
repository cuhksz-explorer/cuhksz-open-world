#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using PhoneUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PhoneUI.Editor
{
    /// <summary>
    /// 将队员提供的 Phone_UI_create.zip（PhoneUI 预制体）接入全部游戏场景，并禁用旧 closepanel / Phone Canvas。
    /// </summary>
    public static class PhoneUiImportAndSceneConfigurator
    {
        const string PhonePrefabPath = "Assets/PhoneUI/Prefabs/PhoneUI.prefab";
        const string SceneFolder = "Assets/scene";
        const string PhoneFrameSpritePath = "Assets/PhoneUI/CellPhone/UI/Phoneee.png";

        [MenuItem("PhoneUI/重新导入 PhoneUI 预制体", false, 0)]
        public static void ReimportPhoneUiPrefab()
        {
            if (!File.Exists(AbsolutePrefabPath))
            {
                EditorUtility.DisplayDialog("PhoneUI",
                    "磁盘上缺少预制体文件：\n" + AbsolutePrefabPath + "\n\n请确认已解压 Phone_UI_create.zip 到 Assets/PhoneUI。",
                    "确定");
                return;
            }

            AssetDatabase.ImportAsset(PhonePrefabPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            var prefab = ResolvePhoneUiPrefab(out var detail);
            EditorUtility.DisplayDialog("PhoneUI",
                prefab != null
                    ? "预制体已重新导入，AssetDatabase 可正常加载。\n\n" + detail
                    : "重新导入后仍无法加载预制体。\n\n" + detail,
                "确定");
        }

        [MenuItem("PhoneUI/修复预制体脚本 GUID（与 zip 一致）", false, 1)]
        public static void RepairPrefabScriptReferences()
        {
            RepairScriptMetaGuid("Assets/PhoneUI/Scripts/PhoneSessionController.cs", "903bd195e04a3814395e8dde424c4b89");
            RepairScriptMetaGuid("Assets/PhoneUI/Scripts/PhoneAppSlot.cs", "92dba220ce00843468ff383fceb1b3c7");
            RepairScriptMetaGuid("Assets/PhoneUI/Scripts/PhoneAppSlotVisual.cs", "66d8e4d4b18a5464bbfa6646d2002b1b");
            RepairScriptMetaGuid("Assets/PhoneUI/Scripts/PhoneAppGrid.cs", "9184dd2558a88264e965b7bd04644eb7");
            RepairScriptMetaGuid("Assets/PhoneUI/Scripts/PhonePresentationController.cs", "b8523306e5be19545b0a8d955f9a9bb9");
            RepairScriptMetaGuid("Assets/PhoneUI/Scripts/PhoneSettingsMigrator.cs", "e470a3ea3864de54c866e2ac1f232b63");
            RepairScriptMetaGuid("Assets/PhoneUI/Scripts/PhonePlayerRigSetup.cs", "35d8bf30c5bc056409c850d5bb3d2863");

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            ReimportPhoneUiPrefab();
        }

        static void RepairScriptMetaGuid(string assetPath, string guid)
        {
            var metaPath = assetPath + ".meta";
            if (!File.Exists(metaPath))
                return;
            var text = File.ReadAllText(metaPath);
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"^guid: .+$",
                "guid: " + guid,
                System.Text.RegularExpressions.RegexOptions.Multiline);
            File.WriteAllText(metaPath, text);
        }

        static string AbsolutePrefabPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "PhoneUI", "Prefabs", "PhoneUI.prefab"));

        /// <summary>团结/Unity 有时因 .meta 或脚本 GUID 未对齐导致 LoadAssetAtPath 返回 null，此处多重兜底。</summary>
        static GameObject ResolvePhoneUiPrefab(out string detail)
        {
            detail = "";

            if (!File.Exists(AbsolutePrefabPath))
            {
                detail = "文件不存在: " + AbsolutePrefabPath;
                return null;
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PhonePrefabPath);
            if (prefab != null)
            {
                detail = "已通过路径加载: " + PhonePrefabPath;
                return prefab;
            }

            AssetDatabase.ImportAsset(PhonePrefabPath, ImportAssetOptions.ForceUpdate);
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PhonePrefabPath);
            if (prefab != null)
            {
                detail = "强制 Import 后加载成功。";
                return prefab;
            }

            var guids = AssetDatabase.FindAssets("PhoneUI t:Prefab", new[] { "Assets/PhoneUI" });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("/PhoneUI.prefab", StringComparison.OrdinalIgnoreCase) &&
                    !path.EndsWith("\\PhoneUI.prefab", StringComparison.OrdinalIgnoreCase))
                    continue;

                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    detail = "通过 FindAssets 加载: " + path;
                    return prefab;
                }
            }

            var main = AssetDatabase.LoadMainAssetAtPath(PhonePrefabPath) as GameObject;
            if (main != null)
            {
                detail = "通过 LoadMainAssetAtPath 加载。";
                return main;
            }

            detail =
                "磁盘上有文件，但 AssetDatabase 无法识别为预制体。\n" +
                "请先执行菜单「PhoneUI → 修复预制体脚本 GUID（与 zip 一致）」或「重新导入 PhoneUI 预制体」。\n" +
                "若仍失败：在 Project 窗口选中 Assets/PhoneUI/Prefabs/PhoneUI.prefab，右键 Reimport。";
            return null;
        }

        [MenuItem("PhoneUI/修复预制体贴图引用", false, 2)]
        public static void RepairPrefabSprites()
        {
            var prefab = ResolvePhoneUiPrefab(out var detail);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("PhoneUI", "未找到预制体: " + PhonePrefabPath + "\n\n" + detail, "确定");
                return;
            }

            int fixedCount = 0;
            var images = prefab.GetComponentsInChildren<Image>(true);
            foreach (var image in images)
            {
                if (image.sprite != null)
                    continue;
                var path = GuessSpritePath(image.gameObject.name);
                if (string.IsNullOrEmpty(path))
                    continue;
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (tex != null)
                        sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                }
                if (sprite == null)
                    continue;
                image.sprite = sprite;
                fixedCount++;
            }

            var frame = AssetDatabase.LoadAssetAtPath<Sprite>(PhoneFrameSpritePath);
            if (frame != null)
            {
                foreach (var tr in prefab.GetComponentsInChildren<Transform>(true))
                {
                    if (!string.Equals(tr.name, "PhoneFrame", StringComparison.Ordinal))
                        continue;
                    var img = tr.GetComponent<Image>();
                    if (img != null && img.sprite == null)
                    {
                        img.sprite = frame;
                        fixedCount++;
                    }
                }
            }

            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("PhoneUI", $"已尝试修复预制体 Image 引用：{fixedCount} 处。", "确定");
        }

        static string GuessSpritePath(string objectName)
        {
            if (string.Equals(objectName, "PhoneFrame", StringComparison.Ordinal))
                return PhoneFrameSpritePath;
            return null;
        }

        [MenuItem("PhoneUI/全部场景替换为新设置 UI", false, 10)]
        public static void ReplacePhoneUiInAllScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("PhoneUI", "请先退出 Play Mode。", "确定");
                return;
            }

            var prefab = ResolvePhoneUiPrefab(out var resolveDetail);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("PhoneUI", "未找到: " + PhonePrefabPath + "\n\n" + resolveDetail, "确定");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "全部场景替换 PhoneUI",
                    "将遍历 Assets/scene 下所有 .unity，为每个场景：\n" +
                    "1) 实例化 PhoneUI.prefab\n" +
                    "2) 绑定 PlayerMovement\n" +
                    "3) 禁用旧 Phone / closepanel\n" +
                    "4) 重定向 OpenPhone / ToggleQualityPanel 按钮\n\n" +
                    "建议先备份或提交 Git。是否继续？",
                    "继续", "取消"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scenePaths = new List<string>();
            scenePaths.AddRange(Directory.GetFiles(SceneFolder, "*.unity", SearchOption.TopDirectoryOnly));
            scenePaths.AddRange(Directory.GetFiles(SceneFolder, "*.scene", SearchOption.TopDirectoryOnly));
            int ok = 0, skip = 0, fail = 0;
            var errors = new List<string>();

            foreach (var path in scenePaths)
            {
                var normalized = path.Replace('\\', '/');
                try
                {
                    var scene = EditorSceneManager.OpenScene(normalized, OpenSceneMode.Single);
                    if (ApplyToScene(scene, prefab))
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                        ok++;
                    }
                    else
                    {
                        skip++;
                    }
                }
                catch (Exception ex)
                {
                    fail++;
                    errors.Add(normalized + ": " + ex.Message);
                    Debug.LogException(ex);
                }
            }

            AssetDatabase.SaveAssets();
            var msg = $"完成。成功 {ok}，跳过 {skip}，失败 {fail}。";
            if (errors.Count > 0)
                msg += "\n\n" + string.Join("\n", errors);
            EditorUtility.DisplayDialog("PhoneUI 场景替换", msg, "确定");
        }

        [MenuItem("PhoneUI/当前场景替换为新设置 UI", false, 11)]
        public static void ReplacePhoneUiInActiveScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("PhoneUI", "请先退出 Play Mode。", "确定");
                return;
            }

            var prefab = ResolvePhoneUiPrefab(out var resolveDetail);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("PhoneUI", "未找到: " + PhonePrefabPath + "\n\n" + resolveDetail, "确定");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("PhoneUI", "请先打开要处理的游戏场景。", "确定");
                return;
            }

            if (ApplyToScene(scene, prefab))
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorUtility.DisplayDialog("PhoneUI", "当前场景已替换为新 PhoneUI。", "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("PhoneUI", "当前场景已存在 PhoneSessionController，未重复添加。", "确定");
            }
        }

        static bool ApplyToScene(Scene scene, GameObject prefab)
        {
            if (FindInScene<PhoneSessionController>(scene) != null)
                return false;

            var player = FindInScene<PlayerMovement>(scene);
            var oldManagers = FindAllInScene<PhoneUIManager>(scene);
            var closePanels = FindAllByName(scene, "closepanel");

            Transform uiParent = null;
            if (oldManagers.Count > 0 && oldManagers[0] != null)
                uiParent = oldManagers[0].transform.parent;
            if (uiParent == null)
            {
                var canvas = FindInScene<Canvas>(scene);
                if (canvas != null)
                    uiParent = canvas.transform;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            if (instance == null)
            {
                // 兜底：直接从 prefab 内容克隆（预制体未进 AssetDatabase 时）
                var contents = PrefabUtility.LoadPrefabContents(PhonePrefabPath);
                try
                {
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(contents, scene);
                    if (instance == null)
                        instance = UnityEngine.Object.Instantiate(contents);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            if (instance == null)
                throw new InvalidOperationException("实例化 PhoneUI.prefab 失败。请先执行 PhoneUI → 重新导入 PhoneUI 预制体。");

            instance.name = "PhoneUI";
            if (uiParent != null)
                instance.transform.SetParent(uiParent, false);

            var session = instance.GetComponentInChildren<PhoneSessionController>(true);
            var manager = instance.GetComponentInChildren<PhoneUIManager>(true);
            if (session == null)
                throw new InvalidOperationException("预制体缺少 PhoneSessionController。");

            if (player != null)
            {
                session.playerMovement = player;
                if (manager != null)
                    manager.playerMovement = player;
            }

            WirePhonePresentation(session, instance, player);

            var logout = instance.GetComponentInChildren<LogoutToLoadingSceneButton>(true);
            if (logout != null)
            {
                var server = FindInScene<GameServerSync>(scene);
                if (server != null)
                {
                    var so = new SerializedObject(logout);
                    var prop = so.FindProperty("serverSync");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = server;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }

            foreach (var old in oldManagers)
            {
                if (old == null) continue;
                RetargetPhoneButtons(scene, old, manager);
                old.gameObject.SetActive(false);
            }

            foreach (var panel in closePanels)
            {
                if (panel == null) continue;
                RetargetQualityButtons(scene, panel.GetComponent<QualityController>(), manager);
                panel.SetActive(false);
            }

            EnsureEventSystem(scene);
            return true;
        }

        static void RetargetPhoneButtons(Scene scene, PhoneUIManager oldManager, PhoneUIManager newManager)
        {
            if (oldManager == null || newManager == null)
                return;

            foreach (var button in FindAllInScene<Button>(scene))
            {
                if (button == null) continue;
                RetargetListener(button, oldManager, newManager, "OpenPhone", nameof(PhoneUIManager.OpenPhone));
                RetargetListener(button, oldManager, newManager, "ClosePhone", nameof(PhoneUIManager.ClosePhone));
            }
        }

        static void RetargetQualityButtons(Scene scene, QualityController oldController, PhoneUIManager newManager)
        {
            if (oldController == null || newManager == null)
                return;

            foreach (var button in FindAllInScene<Button>(scene))
            {
                if (button == null) continue;
                RetargetListener(button, oldController, newManager, "ToggleQualityPanel", nameof(PhoneUIManager.OpenPhoneToSettings));
            }
        }

        static void RetargetListener(Button button, UnityEngine.Object oldTarget, PhoneUIManager newManager, string oldMethod, string newMethod)
        {
            int count = button.onClick.GetPersistentEventCount();
            for (int i = count - 1; i >= 0; i--)
            {
                if (button.onClick.GetPersistentTarget(i) != oldTarget)
                    continue;
                if (button.onClick.GetPersistentMethodName(i) != oldMethod)
                    continue;

                Undo.RecordObject(button, "Retarget UI Button");
                UnityEditor.Events.UnityEventTools.RemovePersistentListener(button.onClick, i);
                if (newMethod == nameof(PhoneUIManager.OpenPhone))
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, newManager.OpenPhone);
                else if (newMethod == nameof(PhoneUIManager.ClosePhone))
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, newManager.ClosePhone);
                else
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, newManager.OpenPhoneToSettings);
                EditorUtility.SetDirty(button);
            }
        }

        static void WirePhonePresentation(PhoneSessionController session, GameObject phoneRoot, PlayerMovement player)
        {
            if (session == null || phoneRoot == null)
                return;

            var presentation = phoneRoot.GetComponentInChildren<PhonePresentationController>(true);
            if (presentation == null)
                return;

            session.presentation = presentation;
            session.launcherOnly = false;

            if (player != null)
            {
                presentation.playerTransform = player.transform;
                presentation.animator = player.GetComponentInChildren<Animator>(true);

                var switcher = player.GetComponentInChildren<CameraSwitcher>(true);
                if (switcher == null)
                    switcher = UnityEngine.Object.FindObjectOfType<CameraSwitcher>(true);
                if (switcher != null)
                {
                    presentation.cameraSwitcher = switcher;
                    presentation.thirdPersonCamera = switcher.thirdPersonCamera;
                }
            }

            if (player != null && player.GetComponent<PhonePlayerRigSetup>() == null)
                player.gameObject.AddComponent<PhonePlayerRigSetup>();

            EditorUtility.SetDirty(session);
            EditorUtility.SetDirty(presentation);
        }

        static void EnsureEventSystem(Scene scene)
        {
            if (FindInScene<EventSystem>(scene) != null)
                return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            SceneManager.MoveGameObjectToScene(go, scene);
        }

        static T FindInScene<T>(Scene scene) where T : Component
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var c = roots[i].GetComponentInChildren<T>(true);
                if (c != null)
                    return c;
            }
            return null;
        }

        static List<T> FindAllInScene<T>(Scene scene) where T : Component
        {
            var list = new List<T>();
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                list.AddRange(roots[i].GetComponentsInChildren<T>(true));
            return list;
        }

        static List<GameObject> FindAllByName(Scene scene, string objectName)
        {
            var list = new List<GameObject>();
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var transforms = roots[i].GetComponentsInChildren<Transform>(true);
                for (int t = 0; t < transforms.Length; t++)
                {
                    if (string.Equals(transforms[t].name, objectName, StringComparison.Ordinal))
                        list.Add(transforms[t].gameObject);
                }
            }
            return list;
        }
    }
}
#endif
