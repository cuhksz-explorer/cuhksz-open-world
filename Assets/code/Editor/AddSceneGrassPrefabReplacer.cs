#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 将 Add scene 下 GLB 导入的子预制体按名称替换为 Plants and Foliage 源预制体。
/// </summary>
public static class AddSceneGrassPrefabReplacer
{
    const string MenuReplacePath = "Tools/场景配置/修复 Add scene 植物（替换为 Plants and Foliage 预制体）";
    const string MenuMaterialPath = "Tools/场景配置/修复 Add scene 植物白色材质（仅同步材质）";
    const string UndergardenScenePath = "Assets/scene/undergarden.unity";
    const string AddSceneRootName = "Add scene";

    static readonly string[] FoliageSearchFolders =
    {
        "Assets/code/Plants and Foliage",
        "Assets/code/Plants and Foliage/Ground Foliage/Plants and Foliage",
        "Assets/code/Plants and Foliage/Plants and Foliage",
        "Assets/草坪/Plants and Foliage/Ground Foliage/Plants and Foliage",
    };

    static readonly Dictionary<string, string> SceneNameAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "tree1", "Tree1" },
            { "big_bush", "Bush1" },
            { "grass1", "GrassBundle1" },
            { "grass2", "GrassBundle2" },
            { "grass", "GrassBundle1" },
            { "groundplant", "Ground Plant" },
            { "ground_plant", "Ground Plant" },
        };

    struct FoliageReplacement
    {
        public Transform Source;
        public GameObject Prefab;
    }

    [MenuItem(MenuReplacePath, false, 2)]
    public static void ReplaceFoliageInActiveScene()
    {
        if (!EnsureEditMode())
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
        {
            EditorUtility.DisplayDialog("修复 Add scene 植物", "请先打开包含 Add scene 的场景。", "确定");
            return;
        }

        RunReplaceDeferred(scene, promptSave: true);
    }

    [MenuItem(MenuReplacePath + " (打开 undergarden 并执行)", false, 3)]
    public static void ReplaceFoliageInUndergarden()
    {
        if (!EnsureEditMode())
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Scene scene = EditorSceneManager.OpenScene(UndergardenScenePath, OpenSceneMode.Single);
        RunReplaceDeferred(scene, promptSave: true);
    }

    [MenuItem(MenuMaterialPath, false, 4)]
    public static void FixWhiteMaterialsInActiveScene()
    {
        if (!EnsureEditMode())
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
        {
            EditorUtility.DisplayDialog("修复 Add scene 植物", "请先打开包含 Add scene 的场景。", "确定");
            return;
        }

        RunMaterialFixDeferred(scene, promptSave: true);
    }

    [MenuItem(MenuMaterialPath + " (打开 undergarden 并执行)", false, 5)]
    public static void FixWhiteMaterialsInUndergarden()
    {
        if (!EnsureEditMode())
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Scene scene = EditorSceneManager.OpenScene(UndergardenScenePath, OpenSceneMode.Single);
        RunMaterialFixDeferred(scene, promptSave: true);
    }

    public static void BatchReplaceUndergardenAndSave()
    {
        Scene scene = EditorSceneManager.OpenScene(UndergardenScenePath, OpenSceneMode.Single);
        ReplaceReport report = RunReplace(scene, promptSave: false);
        if (report.Replaced > 0)
        {
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        Debug.Log($"[AddSceneFoliage] Batch 替换：{report.Replaced}/{report.Found}");
        EditorApplication.Exit(report.Replaced > 0 ? 0 : 1);
    }

    static void RunReplaceDeferred(Scene scene, bool promptSave)
    {
        EditorApplication.delayCall += () =>
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;
            RunReplace(scene, promptSave);
        };
    }

    static void RunMaterialFixDeferred(Scene scene, bool promptSave)
    {
        EditorApplication.delayCall += () =>
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;
            RunMaterialFix(scene, promptSave);
        };
    }

    static bool EnsureEditMode()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
            return true;

        EditorUtility.DisplayDialog("修复 Add scene 植物", "请先退出 Play Mode。", "确定");
        return false;
    }

    static void EnsureAddSceneEditable(GameObject addSceneRoot)
    {
        if (!PrefabUtility.IsPartOfAnyPrefab(addSceneRoot))
            return;

        PrefabUtility.UnpackPrefabInstance(
            addSceneRoot,
            PrefabUnpackMode.Completely,
            InteractionMode.AutomatedAction);
    }

    public static ReplaceReport RunReplace(Scene scene, bool promptSave)
    {
        var report = new ReplaceReport();
        Dictionary<string, GameObject> catalog = BuildPrefabCatalog();
        if (catalog.Count == 0)
        {
            if (promptSave)
            {
                EditorUtility.DisplayDialog(
                    "修复 Add scene 植物",
                    "未在 Plants and Foliage 目录中找到预制体（已搜索 Assets/code 与 Assets/草坪）。",
                    "确定");
            }

            return report;
        }

        Transform addSceneRoot = FindBestAddSceneRoot(scene, catalog);
        if (addSceneRoot == null)
        {
            if (promptSave)
                EditorUtility.DisplayDialog("修复 Add scene 植物", $"未找到「{AddSceneRootName}」。", "确定");
            return report;
        }

        EnsureAddSceneEditable(addSceneRoot.gameObject);

        var pending = CollectFoliageNodes(addSceneRoot, catalog, ref report);
        if (pending.Count == 0)
        {
            if (promptSave)
            {
                string debug = BuildCollectDebug(addSceneRoot, catalog);
                EditorUtility.DisplayDialog(
                    "修复 Add scene 植物",
                    $"未找到需要替换的植物。\n匹配：{report.Found}\n已为正确预制体：{report.AlreadyCorrect}\n" +
                    $"Add scene 子物体数：{addSceneRoot.childCount}\n\n{debug}",
                    "确定");
            }

            return report;
        }

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("修复 Add scene 植物");

        try
        {
            for (int i = 0; i < pending.Count; i++)
            {
                if (EditorUtility.DisplayCancelableProgressBar(
                        "修复 Add scene 植物",
                        $"替换 {i + 1}/{pending.Count}：{pending[i].Source.name}",
                        (float)i / pending.Count))
                {
                    report.Cancelled = true;
                    break;
                }

                if (ReplaceOne(pending[i]))
                    report.Replaced++;
                else
                    report.Skipped++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Undo.CollapseUndoOperations(undoGroup);
        }

        if (report.Replaced > 0)
            EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log(
            $"[AddSceneFoliage] 替换 {report.Replaced}，跳过 {report.Skipped}，已正确 {report.AlreadyCorrect}，匹配 {report.Found}。");

        if (promptSave)
        {
            EditorUtility.DisplayDialog(
                "修复 Add scene 植物",
                report.Cancelled
                    ? $"已替换 {report.Replaced} 处后取消。"
                    : $"完成。\n替换：{report.Replaced}\n跳过：{report.Skipped}\n已正确：{report.AlreadyCorrect}",
                "确定");
        }

        return report;
    }

    static string BuildCollectDebug(Transform addSceneRoot, Dictionary<string, GameObject> catalog)
    {
        var sb = new StringBuilder();
        int sample = Mathf.Min(8, addSceneRoot.childCount);
        sb.AppendLine("子物体名称示例：");
        for (int i = 0; i < sample; i++)
        {
            Transform c = addSceneRoot.GetChild(i);
            bool hasPrefab = ResolvePrefabFromCatalog(c.name, catalog) != null;
            bool missing = IsMissingPrefabInstance(c.gameObject);
            sb.AppendLine($"  · {c.name} → {(hasPrefab ? "可匹配" : "无匹配")}{(missing ? "（预制体丢失）" : "")}");
        }

        int nameMatchInHierarchy = 0;
        foreach (Transform t in addSceneRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t == addSceneRoot)
                continue;
            if (ResolvePrefabFromCatalog(t.name, catalog) != null)
                nameMatchInHierarchy++;
        }

        sb.AppendLine($"层级中名称可匹配节点：{nameMatchInHierarchy}");
        return sb.ToString();
    }

    public static MaterialFixReport RunMaterialFix(Scene scene, bool promptSave)
    {
        var report = new MaterialFixReport();
        Dictionary<string, GameObject> catalog = BuildPrefabCatalog();

        Transform addSceneRoot = FindBestAddSceneRoot(scene, catalog);
        if (addSceneRoot == null)
        {
            if (promptSave)
                EditorUtility.DisplayDialog("修复 Add scene 植物", $"未找到「{AddSceneRootName}」。", "确定");
            return report;
        }

        EnsureAddSceneEditable(addSceneRoot.gameObject);

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("修复 Add scene 植物材质");

        try
        {
            foreach (MeshRenderer renderer in addSceneRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer == null)
                    continue;

                GameObject sourcePrefab = ResolvePrefabForRenderer(renderer, catalog);
                if (sourcePrefab == null)
                    continue;

                report.Found++;
                MeshRenderer sourceRenderer = sourcePrefab.GetComponent<MeshRenderer>();
                if (sourceRenderer == null || sourceRenderer.sharedMaterials == null ||
                    sourceRenderer.sharedMaterials.Length == 0)
                {
                    report.Skipped++;
                    continue;
                }

                Undo.RecordObject(renderer, "Fix foliage material");
                renderer.sharedMaterials = sourceRenderer.sharedMaterials;
                EditorUtility.SetDirty(renderer);
                report.Fixed++;
            }
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }

        if (report.Fixed > 0)
            EditorSceneManager.MarkSceneDirty(scene);

        if (promptSave)
        {
            EditorUtility.DisplayDialog(
                "修复 Add scene 植物",
                $"材质同步：修复 {report.Fixed}，匹配 {report.Found}，跳过 {report.Skipped}。",
                "确定");
        }

        return report;
    }

    static GameObject ResolvePrefabForRenderer(MeshRenderer renderer, Dictionary<string, GameObject> catalog)
    {
        Transform walk = renderer.transform;
        while (walk != null)
        {
            GameObject prefab = ResolvePrefabFromCatalog(walk.name, catalog);
            if (prefab != null)
                return prefab;
            if (string.Equals(walk.name, AddSceneRootName, StringComparison.Ordinal))
                break;
            walk = walk.parent;
        }

        return null;
    }

    static Dictionary<string, GameObject> BuildPrefabCatalog()
    {
        var catalog = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        string[] guids = AssetDatabase.FindAssets("t:Prefab", FoliageSearchFolders);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                continue;

            if (!catalog.ContainsKey(prefab.name))
                catalog[prefab.name] = prefab;
        }

        return catalog;
    }

    static List<FoliageReplacement> CollectFoliageNodes(
        Transform addSceneRoot,
        Dictionary<string, GameObject> catalog,
        ref ReplaceReport report)
    {
        var list = new List<FoliageReplacement>();
        var seenRoots = new HashSet<int>();

        foreach (Transform t in addSceneRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t == addSceneRoot)
                continue;

            Transform foliageRoot = FindFoliageInstanceRoot(t, addSceneRoot, catalog);
            if (foliageRoot == null)
                continue;

            int rootId = foliageRoot.gameObject.GetInstanceID();
            if (!seenRoots.Add(rootId))
                continue;

            GameObject targetPrefab = ResolvePrefabFromCatalog(foliageRoot.name, catalog);
            if (targetPrefab == null)
                continue;

            report.Found++;

            if (!ShouldReplaceInstance(foliageRoot.gameObject, targetPrefab))
            {
                report.AlreadyCorrect++;
                continue;
            }

            list.Add(new FoliageReplacement { Source = foliageRoot, Prefab = targetPrefab });
        }

        return list;
    }

    static Transform FindFoliageInstanceRoot(
        Transform t,
        Transform addSceneRoot,
        Dictionary<string, GameObject> catalog)
    {
        for (Transform walk = t; walk != null && walk != addSceneRoot; walk = walk.parent)
        {
            if (ResolvePrefabFromCatalog(walk.name, catalog) != null)
                return walk;
        }

        return null;
    }

    static bool ShouldReplaceInstance(GameObject instance, GameObject targetPrefab)
    {
        if (instance == null || targetPrefab == null)
            return false;

        if (IsMissingPrefabInstance(instance))
            return true;

        if (!PrefabUtility.IsPartOfPrefabInstance(instance))
            return true;

        if (IsFromPlantsAndFoliagePrefab(instance, targetPrefab))
            return false;

        string targetPath = AssetDatabase.GetAssetPath(targetPrefab);
        string instancePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance);

        if (!string.IsNullOrEmpty(targetPath) && !string.IsNullOrEmpty(instancePath))
        {
            if (string.Equals(instancePath, targetPath, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!instancePath.Replace('\\', '/').Contains("Plants and Foliage", StringComparison.OrdinalIgnoreCase))
                return true;

            if (instancePath.Contains("Add scene.glb", StringComparison.OrdinalIgnoreCase))
                return true;

            return AssetDatabase.LoadAssetAtPath<GameObject>(instancePath) == null;
        }

        // 团结/部分环境下 GetAssetPath 可能为空，已确认非源预制体则仍替换
        return true;
    }

    static bool IsFromPlantsAndFoliagePrefab(GameObject instance, GameObject targetPrefab)
    {
        GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
        if (source == null)
            return false;

        if (source == targetPrefab)
            return true;

        string sourcePath = AssetDatabase.GetAssetPath(source);
        if (string.IsNullOrEmpty(sourcePath))
            return false;

        return sourcePath.Replace('\\', '/').Contains("Plants and Foliage", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetBaseName(source.name), GetBaseName(targetPrefab.name), StringComparison.OrdinalIgnoreCase);
    }

    static bool IsMissingPrefabInstance(GameObject go)
    {
        if (go == null)
            return false;

        if (!PrefabUtility.IsPartOfPrefabInstance(go))
            return false;

        try
        {
            return PrefabUtility.IsPrefabAssetMissing(go);
        }
        catch
        {
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            return string.IsNullOrEmpty(path) ||
                   AssetDatabase.LoadAssetAtPath<GameObject>(path) == null;
        }
    }

    static GameObject ResolvePrefabFromCatalog(string sceneObjectName, Dictionary<string, GameObject> catalog)
    {
        if (catalog == null || catalog.Count == 0)
            catalog = BuildPrefabCatalog();

        string baseName = GetBaseName(sceneObjectName);
        if (string.IsNullOrEmpty(baseName))
            return null;

        if (catalog.TryGetValue(baseName, out GameObject direct))
            return direct;

        if (SceneNameAliases.TryGetValue(baseName, out string alias) && catalog.TryGetValue(alias, out GameObject aliased))
            return aliased;

        foreach (KeyValuePair<string, GameObject> entry in catalog)
        {
            if (!baseName.StartsWith(entry.Key, StringComparison.OrdinalIgnoreCase))
                continue;

            if (baseName.Length == entry.Key.Length)
                return entry.Value;

            char next = baseName[entry.Key.Length];
            if (next == ' ' || next == '(')
                return entry.Value;
        }

        return null;
    }

    static string GetBaseName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return objectName;

        int idx = objectName.IndexOf(" (", StringComparison.Ordinal);
        return idx > 0 ? objectName.Substring(0, idx) : objectName;
    }

    static bool IsUnderTransform(Transform t, Transform ancestor)
    {
        Transform p = t;
        while (p != null)
        {
            if (p == ancestor)
                return true;
            p = p.parent;
        }

        return false;
    }

    static bool ReplaceOne(FoliageReplacement item)
    {
        Transform oldTransform = item.Source;
        if (oldTransform == null || item.Prefab == null)
            return false;

        Vector3 worldPos = oldTransform.position;
        Quaternion worldRot = oldTransform.rotation;
        Vector3 lossy = oldTransform.lossyScale;
        float uniform = Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y), Mathf.Abs(lossy.z));
        if (uniform < 1e-5f)
            uniform = 1f;

        Transform parent = oldTransform.parent;
        int siblingIndex = oldTransform.GetSiblingIndex();
        string objectName = oldTransform.name;

        GameObject instance = PrefabUtility.InstantiatePrefab(item.Prefab, parent) as GameObject;
        if (instance == null)
            return false;

        Undo.RegisterCreatedObjectUndo(instance, "Replace foliage");

        Transform instanceTransform = instance.transform;
        instance.name = objectName;
        instanceTransform.SetSiblingIndex(siblingIndex);
        instanceTransform.SetPositionAndRotation(worldPos, worldRot);
        ApplyUniformWorldScale(instanceTransform, uniform);

        Undo.DestroyObjectImmediate(oldTransform.gameObject);
        return true;
    }

    static void ApplyUniformWorldScale(Transform target, float uniformWorldScale)
    {
        Transform parent = target.parent;
        target.SetParent(null, true);

        Vector3 local = target.localScale;
        float current = Mathf.Max(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));
        target.localScale = current > 1e-5f
            ? local * (uniformWorldScale / current)
            : Vector3.one * uniformWorldScale;

        target.SetParent(parent, true);
    }

    static Transform FindBestAddSceneRoot(Scene scene, Dictionary<string, GameObject> catalog)
    {
        Transform best = null;
        int bestScore = -1;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            CollectAddSceneCandidates(root.transform, catalog, ref best, ref bestScore);
        }

        return best;
    }

    static void CollectAddSceneCandidates(
        Transform searchRoot,
        Dictionary<string, GameObject> catalog,
        ref Transform best,
        ref int bestScore)
    {
        if (string.Equals(searchRoot.name, AddSceneRootName, StringComparison.Ordinal))
        {
            int score = CountCatalogMatchesUnder(searchRoot, catalog);
            if (score > bestScore)
            {
                bestScore = score;
                best = searchRoot;
            }
        }

        for (int i = 0; i < searchRoot.childCount; i++)
            CollectAddSceneCandidates(searchRoot.GetChild(i), catalog, ref best, ref bestScore);
    }

    static int CountCatalogMatchesUnder(Transform root, Dictionary<string, GameObject> catalog)
    {
        int count = 0;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == root)
                continue;
            if (ResolvePrefabFromCatalog(t.name, catalog) != null)
                count++;
        }

        return count;
    }

    static Transform FindByNameDepthFirst(Transform parent, string objectName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (string.Equals(child.name, objectName, StringComparison.Ordinal))
                return child;

            Transform found = FindByNameDepthFirst(child, objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    public struct ReplaceReport
    {
        public int Found;
        public int Replaced;
        public int Skipped;
        public int AlreadyCorrect;
        public bool Cancelled;
    }

    public struct MaterialFixReport
    {
        public int Found;
        public int Fixed;
        public int Skipped;
    }
}
#endif
