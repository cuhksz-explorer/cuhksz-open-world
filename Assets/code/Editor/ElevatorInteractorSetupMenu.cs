#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 电梯一键配置：一栋电梯一个 ElevatorSystem（全楼层表）+ 每层一个 ElevatorInteractor（呼叫点）。
/// </summary>
public static class ElevatorInteractorSetupMenu
{
    const string MenuCreate = "Tools/电梯/创建整栋电梯（全楼层表+各层呼叫点）";
    const string MenuAddCall = "Tools/电梯/为选中物体添加单层呼叫点";

    [MenuItem(MenuCreate, true)]
    static bool ValidateCreate() => Selection.activeGameObject != null;

    [MenuItem(MenuCreate, false, 200)]
    static void CreateFullElevator()
    {
        var go = Selection.activeGameObject;
        if (go == null)
            return;

        int floorCount = EditorInputDialog.Show("楼层数量", "该电梯共有几层？（将创建对应呼叫点）", "5");
        if (floorCount <= 0)
            return;

        var system = go.GetComponent<ElevatorSystem>();
        if (system == null)
            system = Undo.AddComponent<ElevatorSystem>(go);

        var floorsRoot = EnsureChild(go.transform, "ElevatorFloors");
        if (system.floors == null)
            system.floors = new List<ElevatorFloorDestination>();

        for (int i = 1; i <= floorCount; i++)
        {
            var floorT = EnsureFloorInSystem(system, floorsRoot, i, go.transform.position + Vector3.up * (i - 1) * 3f);
            EnsureCallPanel(go.transform, system, i, floorT);
        }

        EditorUtility.SetDirty(system);
        EditorUtility.SetDirty(go);
        Debug.Log($"[Elevator] {go.name}：ElevatorSystem {floorCount} 层 + 各层 CallPanel。任意层均可传送到全部楼层。");
    }

    [MenuItem(MenuAddCall, true)]
    static bool ValidateAddCall() => Selection.activeGameObject != null;

    [MenuItem(MenuAddCall, false, 201)]
    static void AddCallPanelToSelection()
    {
        var go = Selection.activeGameObject;
        var system = go.GetComponentInParent<ElevatorSystem>();
        if (system == null)
        {
            EditorUtility.DisplayDialog("电梯", "请先在父级创建「整栋电梯」，或选中 ElevatorSystem 下的物体。", "确定");
            return;
        }

        int floor = EditorInputDialog.Show("楼层号", "该呼叫点位于第几层？", "1");
        if (floor <= 0)
            return;

        EnsureCallPanel(system.transform, system, floor, go.transform);
        Debug.Log($"[Elevator] {go.name}：已添加 {floor}F 呼叫点，共用 {system.name} 全楼层表。");
    }

    static Transform EnsureFloorInSystem(ElevatorSystem system, Transform floorsRoot, int floorNum, Vector3 defaultPos)
    {
        var existing = FindFloor(system, floorNum);
        if (existing != null && existing.spawnPoint != null)
            return existing.spawnPoint;

        var floorGo = EnsureChild(floorsRoot, $"Floor_{floorNum}");
        if (Vector3.Distance(floorGo.position, floorsRoot.position) < 0.01f)
            floorGo.position = defaultPos;

        if (existing == null)
        {
            system.floors.Add(new ElevatorFloorDestination
            {
                floorNumber = floorNum,
                spawnPoint = floorGo
            });
        }
        else
        {
            existing.spawnPoint = floorGo;
        }

        return floorGo;
    }

    static void EnsureCallPanel(Transform elevatorRoot, ElevatorSystem system, int floorNum, Transform atTransform)
    {
        string panelName = $"CallPanel_Floor{floorNum}";
        var existing = elevatorRoot.Find(panelName);
        GameObject panelGo;
        if (existing != null)
            panelGo = existing.gameObject;
        else
        {
            panelGo = new GameObject(panelName);
            Undo.RegisterCreatedObjectUndo(panelGo, "Create CallPanel");
            panelGo.transform.SetParent(elevatorRoot, false);
        }

        if (atTransform != null)
            panelGo.transform.position = atTransform.position;

        var interactor = panelGo.GetComponent<ElevatorInteractor>();
        if (interactor == null)
            interactor = Undo.AddComponent<ElevatorInteractor>(panelGo);

        interactor.elevatorSystem = system;
        interactor.currentFloorNumber = floorNum;
        interactor.triggerPoint = panelGo.transform;
        interactor.interactDistance = 10f;

        EditorUtility.SetDirty(interactor);
    }

    static ElevatorFloorDestination FindFloor(ElevatorSystem e, int n)
    {
        if (e.floors == null) return null;
        for (int i = 0; i < e.floors.Count; i++)
        {
            if (e.floors[i] != null && e.floors[i].floorNumber == n)
                return e.floors[i];
        }
        return null;
    }

    static Transform EnsureChild(Transform parent, string childName)
    {
        var t = parent.Find(childName);
        if (t != null)
            return t;
        var go = new GameObject(childName);
        Undo.RegisterCreatedObjectUndo(go, "Create " + childName);
        go.transform.SetParent(parent, false);
        return go.transform;
    }
}

class EditorInputDialog : EditorWindow
{
    string _message;
    string _input = "1";
    int _result;

    public static int Show(string title, string message, string defaultValue)
    {
        var w = CreateInstance<EditorInputDialog>();
        w.titleContent = new GUIContent(title);
        w._message = message;
        w._input = defaultValue;
        w._result = 0;
        w.minSize = new Vector2(320, 120);
        w.ShowModalUtility();
        return w._result;
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField(_message);
        _input = EditorGUILayout.TextField("数值", _input);
        GUILayout.FlexibleSpace();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("确定"))
        {
            if (int.TryParse(_input, out int n) && n > 0)
                _result = n;
            Close();
        }
        if (GUILayout.Button("取消"))
            Close();
        EditorGUILayout.EndHorizontal();
    }
}
#endif
