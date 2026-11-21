#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(AirWall))]
public class AirWallEditor : UnityEditor.Editor
{
    const string MenuCreate = "GameObject/场景配置/创建空气墙";
    const string MenuCreateTools = "Tools/场景配置/创建空气墙";

    SerializedProperty _sizingMode;
    SerializedProperty _pointA;
    SerializedProperty _pointB;
    SerializedProperty _wallHeight;
    SerializedProperty _wallThickness;
    SerializedProperty _autoRebuildInEditor;
    SerializedProperty _gizmoColor;

    void OnEnable()
    {
        _sizingMode = serializedObject.FindProperty("sizingMode");
        _pointA = serializedObject.FindProperty("pointA");
        _pointB = serializedObject.FindProperty("pointB");
        _wallHeight = serializedObject.FindProperty("wallHeight");
        _wallThickness = serializedObject.FindProperty("wallThickness");
        _autoRebuildInEditor = serializedObject.FindProperty("autoRebuildInEditor");
        _gizmoColor = serializedObject.FindProperty("gizmoColor");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "空气墙使用实心 BoxCollider 阻挡玩家。\n" +
            "手动模式：用移动/旋转工具调整物体，并在下方修改碰撞体尺寸。\n" +
            "两点模式：拖入两个空物体作为墙的两端，自动计算长度与朝向。",
            MessageType.Info);

        EditorGUILayout.PropertyField(_sizingMode);
        EditorGUILayout.PropertyField(_autoRebuildInEditor);
        EditorGUILayout.PropertyField(_gizmoColor);

        var mode = (AirWall.SizingMode)_sizingMode.enumValueIndex;
        if (mode == AirWall.SizingMode.TwoPoint)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("两点模式", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_pointA);
            EditorGUILayout.PropertyField(_pointB);
            EditorGUILayout.PropertyField(_wallHeight);
            EditorGUILayout.PropertyField(_wallThickness);

            if (GUILayout.Button("立即重建墙体"))
            {
                foreach (var t in targets)
                {
                    if (t is AirWall wall)
                        wall.ApplySizing();
                }
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("创建两点标记（A / B）"))
                CreateTwoPointMarkers((AirWall)target);
        }
        else
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("手动尺寸（局部空间）", EditorStyles.boldLabel);

            var wall = (AirWall)target;
            var box = wall.GetComponent<BoxCollider>();
            if (box != null)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 center = EditorGUILayout.Vector3Field("Center", box.center);
                Vector3 size = EditorGUILayout.Vector3Field("Size", box.size);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(box, "调整空气墙尺寸");
                    box.center = center;
                    box.size = new Vector3(
                        Mathf.Max(0.05f, size.x),
                        Mathf.Max(0.05f, size.y),
                        Mathf.Max(0.05f, size.z));
                    EditorUtility.SetDirty(box);
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("快捷预设", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("标准门洞 (2×3×0.3)"))
                ApplyPreset(wall, new Vector3(0f, 1.5f, 0f), new Vector3(2f, 3f, 0.3f));
            if (GUILayout.Button("走廊隔断 (6×3×0.3)"))
                ApplyPreset(wall, new Vector3(0f, 1.5f, 0f), new Vector3(6f, 3f, 0.3f));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("房间墙 (10×3×0.3)"))
                ApplyPreset(wall, new Vector3(0f, 1.5f, 0f), new Vector3(10f, 3f, 0.3f));
            if (GUILayout.Button("全高厚墙 (4×5×0.5)"))
                ApplyPreset(wall, new Vector3(0f, 2.5f, 0f), new Vector3(4f, 5f, 0.5f));
            EditorGUILayout.EndHorizontal();
        }

        serializedObject.ApplyModifiedProperties();
    }

    static void ApplyPreset(AirWall wall, Vector3 center, Vector3 size)
    {
        if (wall == null)
            return;

        var box = wall.GetComponent<BoxCollider>();
        if (box == null)
            return;

        Undo.RecordObject(wall, "空气墙预设");
        Undo.RecordObject(box, "空气墙预设");
        wall.sizingMode = AirWall.SizingMode.Manual;
        box.center = center;
        box.size = size;
        EditorUtility.SetDirty(wall);
        EditorUtility.SetDirty(box);
    }

    static void CreateTwoPointMarkers(AirWall wall)
    {
        if (wall == null)
            return;

        Undo.RegisterCompleteObjectUndo(wall, "创建空气墙两点");

        Transform a = wall.pointA;
        Transform b = wall.pointB;

        if (a == null)
        {
            var goA = new GameObject("AirWall_PointA");
            Undo.RegisterCreatedObjectUndo(goA, "创建空气墙两点");
            goA.transform.SetParent(wall.transform.parent, true);
            goA.transform.position = wall.transform.position + wall.transform.right * -2f;
            a = goA.transform;
            wall.pointA = a;
        }

        if (b == null)
        {
            var goB = new GameObject("AirWall_PointB");
            Undo.RegisterCreatedObjectUndo(goB, "创建空气墙两点");
            goB.transform.SetParent(wall.transform.parent, true);
            goB.transform.position = wall.transform.position + wall.transform.right * 2f;
            b = goB.transform;
            wall.pointB = b;
        }

        wall.sizingMode = AirWall.SizingMode.TwoPoint;
        wall.ApplySizing();
        EditorUtility.SetDirty(wall);
        MarkSceneDirty();
    }

    [MenuItem(MenuCreate, false, 10)]
    [MenuItem(MenuCreateTools, false, 10)]
    static void CreateAirWall()
    {
        Vector3 spawnPos = GetSpawnPosition();

        var go = new GameObject("AirWall");
        Undo.RegisterCreatedObjectUndo(go, "创建空气墙");
        go.transform.position = spawnPos;
        go.transform.rotation = Quaternion.identity;

        var wall = Undo.AddComponent<AirWall>(go);
        var box = go.GetComponent<BoxCollider>();
        box.center = new Vector3(0f, 1.5f, 0f);
        box.size = new Vector3(4f, 3f, 0.3f);
        box.isTrigger = false;

        Selection.activeGameObject = go;
        MarkSceneDirty();
        Debug.Log("[AirWall] 已创建空气墙。用移动/旋转工具调整位置，在 Inspector 中修改 Size 即可改变大小。");
    }

    static Vector3 GetSpawnPosition()
    {
        if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
        {
            var cam = SceneView.lastActiveSceneView.camera;
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, 500f))
                return hit.point;
            return cam.transform.position + cam.transform.forward * 5f;
        }

        if (Selection.activeTransform != null)
            return Selection.activeTransform.position;

        return Vector3.zero;
    }

    static void MarkSceneDirty()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
    }
}
#endif
