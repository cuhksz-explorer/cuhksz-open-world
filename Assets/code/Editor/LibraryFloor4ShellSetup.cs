#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 图书馆四楼：按场景俯视图不规则外轮廓生成贴合边角的外壳（地板 + 外墙）。
/// </summary>
public static class LibraryFloor4ShellSetup
{
    public const string ScenePath = LibraryFloor4SceneSetup.ScenePath;
    public const string ModelAssetPath = LibraryFloor4DoubleSidedImport.ModelAssetPath;
    public const string ShellRootName = "图书馆四楼_外壳";
    public const string ShellFolder = "Assets/scene/图书馆四楼 Shell";
    public const string ShellMaterialPath = ShellFolder + "/LibraryFloor4Shell.mat";
    public const string FallbackWallMaterialPath = "Assets/scene/Materials/White plaster.mat";

    const string MenuBuild = "Tools/场景/libraryfloor4 - 生成不规则外壳（贴合轮廓）";
    const string MenuBuildOpen = "Tools/场景/libraryfloor4 - 打开场景并生成不规则外壳";

    public const float DefaultPadding = 0.12f;
    public const float DefaultThickness = 0.2f;
    public const float DefaultCellSize = 0.12f;

    [MenuItem(MenuBuild, false, 51)]
    public static void BuildShellInCurrentSceneFromMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("libraryfloor4 外壳", "请先退出 Play Mode。", "确定");
            return;
        }

        try
        {
            var result = BuildShellInActiveScene(
                DefaultPadding, DefaultThickness, DefaultCellSize, includeCeiling: false);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            EditorUtility.DisplayDialog(
                "libraryfloor4 外壳",
                $"已生成不规则外壳。\n轮廓顶点：{result.ContourVertexCount}\n高度：{result.Height:F2} m\n平面范围：{result.FootprintSpan}",
                "确定");
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("libraryfloor4 外壳", ex.Message, "确定");
        }
    }

    [MenuItem(MenuBuildOpen, false, 52)]
    public static void OpenSceneAndBuildShellFromMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("libraryfloor4 外壳", "请先退出 Play Mode。", "确定");
            return;
        }

        if (!File.Exists(ScenePath))
        {
            EditorUtility.DisplayDialog("libraryfloor4 外壳", $"场景不存在：{ScenePath}", "确定");
            return;
        }

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        BuildShellInCurrentSceneFromMenu();
    }

    /// <summary>BatchMode: -executeMethod LibraryFloor4ShellSetup.BuildAndSave</summary>
    public static void BuildAndSave()
    {
        if (!File.Exists(ScenePath))
            throw new FileNotFoundException($"场景不存在：{ScenePath}");

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        BuildShellInActiveScene(DefaultPadding, DefaultThickness, DefaultCellSize, includeCeiling: false);
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
    }

    public readonly struct ShellBuildResult
    {
        public readonly int ContourVertexCount;
        public readonly float Height;
        public readonly Vector2 FootprintSpan;

        public ShellBuildResult(int contourVertexCount, float height, Vector2 footprintSpan)
        {
            ContourVertexCount = contourVertexCount;
            Height = height;
            FootprintSpan = footprintSpan;
        }
    }

    public static ShellBuildResult BuildShellInActiveScene(
        float padding,
        float thickness,
        float cellSize,
        bool includeCeiling)
    {
        GameObject modelRoot = FindModelRoot();
        if (modelRoot == null)
            throw new System.InvalidOperationException("未找到「图书馆四楼」模型根节点，请先导入场景。");

        var renderers = CollectContentRenderers(modelRoot);
        if (renderers.Count == 0)
            throw new System.InvalidOperationException("模型下没有可用的 Renderer。");

        SceneFootprintExtractor.FootprintResult footprint = SceneFootprintExtractor.Extract(
            renderers, cellSize, padding);

        if (footprint.PolygonWorldXZ.Count < 3)
            throw new System.InvalidOperationException("外轮廓顶点不足。");

        float t = Mathf.Max(0.02f, thickness);
        float yPad = Mathf.Max(0.02f, padding * 0.5f);
        float floorTopY = footprint.MinY;
        float wallBottomY = footprint.MinY;
        float wallTopY = footprint.MaxY + yPad;

        Transform shellParent = EnsureShellRoot(modelRoot.transform);
        RemoveOldShellChildren(shellParent);
        EnsureShellFolder();

        Material shellMat = GetOrCreateShellMaterial();
        LibraryFloor4DoubleSidedImport.MigrateMaterial(shellMat);

        Undo.RegisterFullObjectHierarchyUndo(shellParent.gameObject, "Library Floor4 Irregular Shell");

        Mesh floorMesh = IrregularShellMeshBuilder.BuildFloorMesh("Shell_Floor_Irregular", footprint.PolygonWorldXZ, floorTopY, t);
        Mesh wallMesh = IrregularShellMeshBuilder.BuildWallMesh("Shell_Walls_Irregular", footprint.PolygonWorldXZ, wallBottomY, wallTopY, t);
        CreateShellObject(shellParent, "Shell_Floor", floorMesh, shellMat);
        CreateShellObject(shellParent, "Shell_Walls", wallMesh, shellMat);

        if (includeCeiling)
        {
            Mesh ceilingMesh = IrregularShellMeshBuilder.BuildCeilingMesh(
                "Shell_Ceiling_Irregular", footprint.PolygonWorldXZ, wallTopY, t);
            CreateShellObject(shellParent, "Shell_Ceiling", ceilingMesh, shellMat);
        }

        Vector2 min2 = footprint.PolygonWorldXZ[0];
        Vector2 max2 = footprint.PolygonWorldXZ[0];
        foreach (Vector2 p in footprint.PolygonWorldXZ)
        {
            min2 = Vector2.Min(min2, p);
            max2 = Vector2.Max(max2, p);
        }

        Debug.Log(
            $"[libraryfloor4 外壳] 不规则轮廓 {footprint.PolygonWorldXZ.Count} 顶点，" +
            $"栅格 {footprint.CellSize:F2}m，墙高 {wallTopY - wallBottomY:F2}m。");

        return new ShellBuildResult(
            footprint.PolygonWorldXZ.Count,
            wallTopY - wallBottomY,
            max2 - min2);
    }

    static void CreateShellObject(Transform parent, string name, Mesh mesh, Material mat)
    {
        string meshPath = $"{ShellFolder}/Meshes/{name}.asset";
        string dir = Path.GetDirectoryName(meshPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, meshPath);
        }
        else
        {
            existing.Clear();
            existing.vertices = mesh.vertices;
            existing.triangles = mesh.triangles;
            existing.RecalculateNormals();
            existing.RecalculateBounds();
            existing.RecalculateTangents();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            mesh = existing;
        }

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create Irregular Shell");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        go.isStatic = true;

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;

        var mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
        mc.convex = false;
    }

    static List<Renderer> CollectContentRenderers(GameObject modelRoot)
    {
        var list = new List<Renderer>();
        foreach (var renderer in modelRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || !renderer.enabled || IsShellRenderer(renderer))
                continue;
            list.Add(renderer);
        }

        return list;
    }

    static Transform EnsureShellRoot(Transform modelRoot)
    {
        Transform existing = modelRoot.Find(ShellRootName);
        if (existing != null)
            return existing;

        var shellGo = new GameObject(ShellRootName);
        Undo.RegisterCreatedObjectUndo(shellGo, "Create Shell Root");
        shellGo.transform.SetParent(modelRoot, false);
        shellGo.transform.localPosition = Vector3.zero;
        shellGo.transform.localRotation = Quaternion.identity;
        shellGo.transform.localScale = Vector3.one;
        return shellGo.transform;
    }

    static void RemoveOldShellChildren(Transform shellParent)
    {
        for (int i = shellParent.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(shellParent.GetChild(i).gameObject);
    }

    static GameObject FindModelRoot()
    {
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (IsModelRoot(root))
                return root;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var candidate = renderer.transform.root.gameObject;
                if (IsModelRoot(candidate))
                    return candidate;
            }
        }

        return null;
    }

    static bool IsModelRoot(GameObject go)
    {
        if (go == null)
            return false;

        if (go.name.Contains("图书馆四楼") || go.name.Contains("图书馆4"))
            return true;

        if (PrefabUtility.IsPartOfPrefabInstance(go))
        {
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            if (path == ModelAssetPath && PrefabUtility.GetNearestPrefabInstanceRoot(go) == go)
                return true;
        }

        return false;
    }

    static bool IsShellRenderer(Renderer renderer)
    {
        Transform t = renderer.transform;
        while (t != null)
        {
            if (t.name == ShellRootName || t.name.StartsWith("Shell_"))
                return true;
            t = t.parent;
        }

        return false;
    }

    static void EnsureShellFolder()
    {
        if (!AssetDatabase.IsValidFolder(ShellFolder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/scene"))
                return;
            AssetDatabase.CreateFolder("Assets/scene", "图书馆四楼 Shell");
        }
    }

    static Material GetOrCreateShellMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ShellMaterialPath);
        if (mat != null)
            return mat;

        var fallback = AssetDatabase.LoadAssetAtPath<Material>(FallbackWallMaterialPath);
        if (fallback != null)
        {
            mat = new Material(fallback) { name = "LibraryFloor4Shell" };
        }
        else
        {
            var shader = Shader.Find("Custom/LibraryDoubleSided") ?? Shader.Find("Standard");
            mat = new Material(shader) { name = "LibraryFloor4Shell" };
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", new Color(0.92f, 0.9f, 0.86f));
        }

        EnsureShellFolder();
        AssetDatabase.CreateAsset(mat, ShellMaterialPath);
        AssetDatabase.SaveAssets();
        return mat;
    }
}
#endif
