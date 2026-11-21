#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// 根据轴对齐包围盒生成薄板网格（地板/墙面）。
/// </summary>
public static class BuildingShellMeshUtility
{
    public static Mesh CreateBoxMesh(string meshName, Vector3 center, Vector3 size)
    {
        Vector3 h = size * 0.5f;
        var vertices = new Vector3[8];
        vertices[0] = center + new Vector3(-h.x, -h.y, -h.z);
        vertices[1] = center + new Vector3(h.x, -h.y, -h.z);
        vertices[2] = center + new Vector3(h.x, -h.y, h.z);
        vertices[3] = center + new Vector3(-h.x, -h.y, h.z);
        vertices[4] = center + new Vector3(-h.x, h.y, -h.z);
        vertices[5] = center + new Vector3(h.x, h.y, -h.z);
        vertices[6] = center + new Vector3(h.x, h.y, h.z);
        vertices[7] = center + new Vector3(-h.x, h.y, h.z);

        int[] triangles =
        {
            0, 2, 1, 0, 3, 2,
            4, 5, 6, 4, 6, 7,
            0, 1, 5, 0, 5, 4,
            1, 2, 6, 1, 6, 5,
            2, 3, 7, 2, 7, 6,
            3, 0, 4, 3, 4, 7
        };

        var mesh = new Mesh { name = meshName };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }
}
#endif
