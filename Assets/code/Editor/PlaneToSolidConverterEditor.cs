#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 将选中的平面（MeshFilter）转为同形状的薄实体：顶面、底面、侧壁，双面可见且不透明。
/// </summary>
public class PlaneToSolidConverterEditor : EditorWindow
{
    const string MenuConvert = "Tools/Mesh/平面转实体（选中物体）";
    const string MenuWindow = "Tools/Mesh/平面转实体...";
    const string MenuGameObject = "GameObject/3D Object/平面转薄实体";

    const string PrefsThickness = "PlaneToSolid.Thickness";
    const string PrefsBidirectional = "PlaneToSolid.Bidirectional";
    const string PrefsDirection = "PlaneToSolid.Direction";
    const string PrefsMakeOpaque = "PlaneToSolid.MakeOpaque";
    const string PrefsAddCollider = "PlaneToSolid.AddCollider";

    const float DefaultThickness = 0.02f;

    float _thickness = DefaultThickness;
    bool _bidirectional = true;
    PlaneToSolidMeshBuilder.ExtrudeDirectionMode _direction =
        PlaneToSolidMeshBuilder.ExtrudeDirectionMode.AutoFromNormals;
    bool _makeMaterialsOpaque = true;
    bool _addMeshCollider = true;

    [MenuItem(MenuConvert, false, 100)]
    static void ConvertSelectionMenu()
    {
        LoadPrefsStatic();
        int converted = ConvertSelection(
            EditorPrefs.GetFloat(PrefsThickness, DefaultThickness),
            EditorPrefs.GetBool(PrefsBidirectional, true),
            (PlaneToSolidMeshBuilder.ExtrudeDirectionMode)EditorPrefs.GetInt(
                PrefsDirection,
                (int)PlaneToSolidMeshBuilder.ExtrudeDirectionMode.AutoFromNormals),
            EditorPrefs.GetBool(PrefsMakeOpaque, true),
            EditorPrefs.GetBool(PrefsAddCollider, true));

        if (converted == 0)
        {
            EditorUtility.DisplayDialog(
                "平面转实体",
                "未转换任何物体。\n\n请选中带 MeshFilter 且含网格的物体（如 Plane、Quad 或自定义平面网格）。",
                "确定");
        }
        else
        {
            Debug.Log($"[平面转实体] 已转换 {converted} 个物体。");
        }
    }

    [MenuItem(MenuConvert, true)]
    static bool ConvertSelectionMenuValidate() => Selection.transforms.Length > 0;

    [MenuItem(MenuWindow, false, 101)]
    static void OpenWindow()
    {
        var win = GetWindow<PlaneToSolidConverterEditor>(false, "平面转实体", true);
        win.minSize = new Vector2(320f, 260f);
        win.LoadPrefs();
        win.Show();
    }

    [MenuItem(MenuGameObject, false, 12)]
    static void ConvertFromGameObjectMenu() => ConvertSelectionMenu();

    [MenuItem(MenuGameObject, true)]
    static bool ConvertFromGameObjectMenuValidate() => Selection.transforms.Length > 0;

    void OnGUI()
    {
        EditorGUILayout.LabelField("平面 → 薄实体", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "为选中物体生成封闭薄网格（顶面 + 底面 + 侧壁），双面可见。适合 Plane、Quad 或单面自定义平面。",
            MessageType.Info);

        _thickness = EditorGUILayout.FloatField("厚度（本地单位）", Mathf.Max(0.0001f, _thickness));
        _bidirectional = EditorGUILayout.Toggle("双向挤出（居中）", _bidirectional);
        if (!_bidirectional)
            EditorGUILayout.HelpBox("单向挤出：从原平面沿挤出方向的反侧拉出厚度。", MessageType.None);

        _direction = (PlaneToSolidMeshBuilder.ExtrudeDirectionMode)EditorGUILayout.EnumPopup("挤出方向", _direction);
        _makeMaterialsOpaque = EditorGUILayout.Toggle("材质改为不透明", _makeMaterialsOpaque);
        _addMeshCollider = EditorGUILayout.Toggle("添加/更新 MeshCollider", _addMeshCollider);

        EditorGUILayout.Space(8f);
        using (new EditorGUI.DisabledScope(Selection.transforms.Length == 0))
        {
            if (GUILayout.Button("转换选中物体", GUILayout.Height(32f)))
            {
                SavePrefs();
                int n = ConvertSelection(_thickness, _bidirectional, _direction, _makeMaterialsOpaque, _addMeshCollider);
                if (n > 0)
                    ShowNotification(new GUIContent($"已转换 {n} 个物体"));
                else
                    EditorUtility.DisplayDialog("平面转实体", "未找到可转换的 MeshFilter。", "确定");
            }
        }
    }

    void OnDisable() => SavePrefs();

    void LoadPrefs()
    {
        _thickness = EditorPrefs.GetFloat(PrefsThickness, DefaultThickness);
        _bidirectional = EditorPrefs.GetBool(PrefsBidirectional, true);
        _direction = (PlaneToSolidMeshBuilder.ExtrudeDirectionMode)EditorPrefs.GetInt(
            PrefsDirection,
            (int)PlaneToSolidMeshBuilder.ExtrudeDirectionMode.AutoFromNormals);
        _makeMaterialsOpaque = EditorPrefs.GetBool(PrefsMakeOpaque, true);
        _addMeshCollider = EditorPrefs.GetBool(PrefsAddCollider, true);
    }

    void SavePrefs()
    {
        EditorPrefs.SetFloat(PrefsThickness, _thickness);
        EditorPrefs.SetBool(PrefsBidirectional, _bidirectional);
        EditorPrefs.SetInt(PrefsDirection, (int)_direction);
        EditorPrefs.SetBool(PrefsMakeOpaque, _makeMaterialsOpaque);
        EditorPrefs.SetBool(PrefsAddCollider, _addMeshCollider);
    }

    static void LoadPrefsStatic()
    {
        // 仅用于菜单快捷转换读取 EditorPrefs
    }

    static int ConvertSelection(
        float thickness,
        bool bidirectional,
        PlaneToSolidMeshBuilder.ExtrudeDirectionMode direction,
        bool makeOpaque,
        bool addCollider)
    {
        var transforms = Selection.transforms;
        if (transforms == null || transforms.Length == 0)
            return 0;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        int converted = 0;

        foreach (var t in transforms)
        {
            if (t == null)
                continue;
            var filters = t.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in filters)
            {
                if (mf == null || mf.sharedMesh == null)
                    continue;
                if (TryConvertMeshFilter(mf, thickness, bidirectional, direction, makeOpaque, addCollider))
                    converted++;
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
        return converted;
    }

    static bool TryConvertMeshFilter(
        MeshFilter meshFilter,
        float thickness,
        bool bidirectional,
        PlaneToSolidMeshBuilder.ExtrudeDirectionMode direction,
        bool makeOpaque,
        bool addCollider)
    {
        Mesh source = meshFilter.sharedMesh;
        if (!PlaneToSolidMeshBuilder.TryBuildSolid(
                source,
                thickness,
                direction,
                bidirectional,
                out Mesh solid,
                out string error))
        {
            Debug.LogWarning($"[平面转实体] 跳过 {meshFilter.name}: {error}", meshFilter);
            return false;
        }

        GameObject go = meshFilter.gameObject;
        Undo.RecordObject(meshFilter, "Plane To Solid");

        string assetPath = AssetDatabase.GetAssetPath(source);
        string folder;
        string baseName = source.name;

        if (!string.IsNullOrEmpty(assetPath))
        {
            folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            baseName = Path.GetFileNameWithoutExtension(assetPath);
        }
        else
        {
            folder = "Assets/GeneratedMeshes/PlaneSolids";
            if (!AssetDatabase.IsValidFolder("Assets/GeneratedMeshes"))
                AssetDatabase.CreateFolder("Assets", "GeneratedMeshes");
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/GeneratedMeshes", "PlaneSolids");
        }

        string meshAssetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{baseName}_Solid.asset");
        AssetDatabase.CreateAsset(solid, meshAssetPath);
        AssetDatabase.SaveAssets();

        meshFilter.sharedMesh = solid;

        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            Undo.RecordObject(renderer, "Plane To Solid Materials");
            if (makeOpaque)
                MakeMaterialsOpaque(renderer.sharedMaterials);
        }

        if (addCollider)
        {
            var col = go.GetComponent<MeshCollider>();
            if (col == null)
                col = Undo.AddComponent<MeshCollider>(go);
            else
                Undo.RecordObject(col, "Plane To Solid Collider");
            col.sharedMesh = solid;
            col.convex = false;
        }

        EditorUtility.SetDirty(go);
        if (renderer != null)
            EditorUtility.SetDirty(renderer);

        Debug.Log($"[平面转实体] {go.name} → {meshAssetPath}，厚度 {thickness}", go);
        return true;
    }

    static void MakeMaterialsOpaque(Material[] materials)
    {
        if (materials == null)
            return;

        foreach (Material mat in materials)
        {
            if (mat == null)
                continue;

            if (mat.HasProperty("_Mode"))
            {
                Undo.RecordObject(mat, "Plane To Solid Opaque");
                mat.SetFloat("_Mode", 0f);
                mat.SetInt("_SrcBlend", (int)BlendMode.One);
                mat.SetInt("_DstBlend", (int)BlendMode.Zero);
                mat.SetInt("_ZWrite", 1);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = (int)RenderQueue.Geometry;
                EditorUtility.SetDirty(mat);
                continue;
            }

            if (mat.HasProperty("_Surface"))
            {
                Undo.RecordObject(mat, "Plane To Solid Opaque");
                mat.SetFloat("_Surface", 0f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_AlphaClip", 0f);
                mat.SetFloat("_SrcBlend", (float)BlendMode.One);
                mat.SetFloat("_DstBlend", (float)BlendMode.Zero);
                mat.SetFloat("_ZWrite", 1f);
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.EnableKeyword("_SURFACE_TYPE_OPAQUE");
                mat.renderQueue = (int)RenderQueue.Geometry;
                EditorUtility.SetDirty(mat);
            }
        }
    }
}
#endif
