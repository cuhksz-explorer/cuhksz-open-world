#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 修复 undergarden 场景：重新导入校园 FBX、清理失效 MeshCollider / 丢失脚本，避免 Broken PPtr 与 prefabInstance 空引用。
/// </summary>
public static class UndergardenSceneRepairMenu
{
    const string MenuPath = "Tools/场景配置/undergarden 修复场景（模型引用+碰撞体）";
    const string ScenePath = "Assets/scene/undergarden.unity";
    const string CampusFbxPath = "Assets/pictures/Scenes/down.fbx";
    const string CampusFbxGuid = "e11e17c455d4e1e488139f047c137e3b";

    [MenuItem(MenuPath, false, 1)]
    public static void RepairFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("undergarden 修复", "请先退出 Play Mode。", "确定");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        try
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var report = RepairLoadedScene(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                "undergarden 修复",
                report.ToString(),
                "确定");
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("undergarden 修复失败", ex.Message, "确定");
        }
    }

    /// <summary>BatchMode: -executeMethod UndergardenSceneRepairMenu.RepairAndSave</summary>
    public static void RepairAndSave()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        RepairLoadedScene(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[undergarden] RepairAndSave 完成。");
    }

    public static RepairReport RepairLoadedScene(Scene scene)
    {
        var report = new RepairReport();

        AssetDatabase.ImportAsset(CampusFbxPath, ImportAssetOptions.ForceUpdate);
        report.ReimportedCampusFbx = true;

        foreach (var root in scene.GetRootGameObjects())
            RepairHierarchy(root.transform, report);

        int removedScripts = 0;
        foreach (var go in GetAllSceneObjects(scene))
            removedScripts += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        report.RemovedMissingScripts = removedScripts;

        var foliageReport = AddSceneGrassPrefabReplacer.RunReplace(scene, promptSave: false);
        report.FoliageReplaced = foliageReport.Replaced;
        report.FoliageAlreadyCorrect = foliageReport.AlreadyCorrect;

        var materialReport = AddSceneGrassPrefabReplacer.RunMaterialFix(scene, promptSave: false);
        report.FoliageMaterialsFixed = materialReport.Fixed;

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[undergarden] {report}");
        return report;
    }

    static GameObject[] GetAllSceneObjects(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var list = new System.Collections.Generic.List<GameObject>();
        for (int i = 0; i < roots.Length; i++)
        {
            foreach (var t in roots[i].GetComponentsInChildren<Transform>(true))
            {
                if (t != null)
                    list.Add(t.gameObject);
            }
        }

        return list.ToArray();
    }

    static void RepairHierarchy(Transform root, RepairReport report)
    {
        var colliders = root.GetComponentsInChildren<MeshCollider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            var collider = colliders[i];
            if (collider == null)
                continue;

            if (!IsCampusFbxCollider(collider) && collider.sharedMesh != null)
                continue;

            if (collider.sharedMesh == null)
            {
                Undo.DestroyObjectImmediate(collider);
                report.RemovedInvalidMeshColliders++;
            }
        }

        var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer == null)
                continue;

            var meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh == null &&
                IsCampusFbxObject(renderer.gameObject))
                report.MissingCampusMeshes++;
        }

        var prefabRoots = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < prefabRoots.Length; i++)
        {
            var go = prefabRoots[i].gameObject;
            if (!PrefabUtility.IsPartOfPrefabInstance(go))
                continue;

            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            if (string.IsNullOrEmpty(path))
            {
                report.MissingPrefabAssets++;
                continue;
            }

            if (PrefabUtility.IsPrefabAssetMissing(go))
                report.MissingPrefabAssets++;
        }
    }

    static bool IsCampusFbxCollider(MeshCollider collider)
    {
        var mesh = collider.sharedMesh;
        if (mesh == null)
            return IsCampusFbxObject(collider.gameObject);

        string path = AssetDatabase.GetAssetPath(mesh);
        if (string.IsNullOrEmpty(path))
            return IsCampusFbxObject(collider.gameObject);

        return path.Replace('\\', '/').EndsWith("down.fbx", System.StringComparison.OrdinalIgnoreCase);
    }

    static bool IsCampusFbxObject(GameObject go)
    {
        var source = PrefabUtility.GetCorrespondingObjectFromSource(go);
        if (source == null)
            return false;

        string path = AssetDatabase.GetAssetPath(source);
        return !string.IsNullOrEmpty(path) &&
               path.Replace('\\', '/').EndsWith("down.fbx", System.StringComparison.OrdinalIgnoreCase);
    }

    public struct RepairReport
    {
        public bool ReimportedCampusFbx;
        public int RemovedInvalidMeshColliders;
        public int MissingCampusMeshes;
        public int MissingPrefabAssets;
        public int RemovedMissingScripts;
        public int FoliageReplaced;
        public int FoliageAlreadyCorrect;
        public int FoliageMaterialsFixed;

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("修复完成。");
            if (ReimportedCampusFbx)
                sb.AppendLine("· 已强制重新导入 down.fbx（下园校园模型）");
            sb.AppendLine($"· 删除失效 MeshCollider：{RemovedInvalidMeshColliders}");
            sb.AppendLine($"· 清理 Missing Script：{RemovedMissingScripts}");
            if (FoliageReplaced > 0 || FoliageAlreadyCorrect > 0)
                sb.AppendLine($"· Add scene 植物：替换 {FoliageReplaced}，已正确 {FoliageAlreadyCorrect}");
            if (FoliageMaterialsFixed > 0)
                sb.AppendLine($"· Add scene 植物材质同步：{FoliageMaterialsFixed}");
            if (MissingCampusMeshes > 0)
                sb.AppendLine($"· 校园模型网格仍缺失的 Renderer：{MissingCampusMeshes}（若>0，请确认 down.fbx 与导出源一致）");
            if (MissingPrefabAssets > 0)
                sb.AppendLine($"· 预制体资源丢失的实例：{MissingPrefabAssets}");
            sb.AppendLine();
            sb.AppendLine("说明：下园场景引用的是 Assets/pictures/Scenes/down.fbx，不是 Up.fbx。");
            sb.AppendLine("若模型仍不对，请用最新源文件覆盖 down.fbx 后再次执行本菜单。");
            return sb.ToString();
        }
    }
}
#endif
