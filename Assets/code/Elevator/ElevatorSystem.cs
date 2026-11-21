using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一栋电梯的「全楼层表」：所有楼层目的地只配置一次，各层的 <see cref="ElevatorInteractor"/> 共用。
/// 挂在电梯根物体（如 Elevator_A）上；每层门口再放一个 ElevatorInteractor 并引用本组件。
/// </summary>
public class ElevatorSystem : MonoBehaviour
{
    [Header("电梯")]
    public string elevatorName = "电梯";

    [Header("全部可抵达楼层（每层只配一次出生点）")]
    public List<ElevatorFloorDestination> floors = new List<ElevatorFloorDestination>();

    public string DisplayName => string.IsNullOrWhiteSpace(elevatorName) ? "电梯" : elevatorName;

    public ElevatorFloorDestination FindFloor(int floorNumber)
    {
        if (floors == null)
            return null;
        for (int i = 0; i < floors.Count; i++)
        {
            if (floors[i] != null && floors[i].floorNumber == floorNumber)
                return floors[i];
        }
        return null;
    }

    public void GetFloorRange(out int minFloor, out int maxFloor)
    {
        minFloor = int.MaxValue;
        maxFloor = int.MinValue;
        if (floors == null || floors.Count == 0)
        {
            minFloor = 1;
            maxFloor = 1;
            return;
        }
        for (int i = 0; i < floors.Count; i++)
        {
            if (floors[i] == null)
                continue;
            int f = floors[i].floorNumber;
            if (f < minFloor) minFloor = f;
            if (f > maxFloor) maxFloor = f;
        }
        if (minFloor == int.MaxValue)
        {
            minFloor = 1;
            maxFloor = 1;
        }
    }

    public bool HasAnyFloor()
    {
        return floors != null && floors.Count > 0;
    }

    void OnDrawGizmosSelected()
    {
        if (floors == null)
            return;
        for (int i = 0; i < floors.Count; i++)
        {
            var f = floors[i];
            if (f == null)
                continue;
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(f.ResolvePosition(), 0.35f);
        }
    }
}
