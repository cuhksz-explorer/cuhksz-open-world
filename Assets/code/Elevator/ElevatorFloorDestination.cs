using System;
using UnityEngine;

/// <summary>
/// 电梯单层目的地：楼层号与世界坐标（可用 Transform 或手写 Vector3）。
/// </summary>
[Serializable]
public class ElevatorFloorDestination
{
    [Tooltip("楼层编号，例如 1、2、3…")]
    public int floorNumber = 1;

    [Tooltip("可选：场景中放置的空物体，传送时使用其 position/rotation")]
    public Transform spawnPoint;

    [Tooltip("无 spawnPoint 时使用的手动世界坐标")]
    public Vector3 worldPosition;

    [Tooltip("传送后玩家朝向（仅当使用 worldPosition 且无 spawnPoint 时生效）")]
    public float playerYawDegrees;

    public Vector3 ResolvePosition()
    {
        if (spawnPoint != null)
            return spawnPoint.position;
        return worldPosition;
    }

    public Quaternion ResolveRotation()
    {
        if (spawnPoint != null)
            return spawnPoint.rotation;
        return Quaternion.Euler(0f, playerYawDegrees, 0f);
    }
}
