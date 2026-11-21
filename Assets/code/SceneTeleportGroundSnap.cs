using UnityEngine;

/// <summary>
/// 将传送落点 Y 对齐到脚下碰撞体，避免落在模型内部或地板下方。
/// </summary>
public static class SceneTeleportGroundSnap
{
    public static Vector3 SnapPosition(Vector3 position, float probeHeight = 80f, float feetOffset = 0.05f)
    {
        var origin = position + Vector3.up * probeHeight;
        if (Physics.Raycast(
                origin,
                Vector3.down,
                out RaycastHit hit,
                probeHeight * 2f,
                ~0,
                QueryTriggerInteraction.Ignore))
        {
            position.y = hit.point.y + feetOffset;
        }

        return position;
    }
}
