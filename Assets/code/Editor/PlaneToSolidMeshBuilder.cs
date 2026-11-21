#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 将单面平面网格挤出为带顶面、底面与侧壁的封闭薄实体网格。
/// </summary>
public static class PlaneToSolidMeshBuilder
{
    public enum ExtrudeDirectionMode
    {
        AutoFromNormals,
        LocalPositiveX,
        LocalNegativeX,
        LocalPositiveY,
        LocalNegativeY,
        LocalPositiveZ,
        LocalNegativeZ,
    }

    public static bool TryBuildSolid(
        Mesh source,
        float thickness,
        ExtrudeDirectionMode directionMode,
        bool bidirectional,
        out Mesh result,
        out string error)
    {
        result = null;
        error = null;

        if (source == null)
        {
            error = "源网格为空。";
            return false;
        }

        if (thickness <= 0f)
        {
            error = "厚度必须大于 0。";
            return false;
        }

        if (!TryExtractMeshTopology(source, out Vector3[] vertices, out int[] triangles, out Vector2[] uvs, out error))
            return false;

        Vector3[] sourceNormals = source.isReadable ? source.normals : null;
        if (!TryResolveExtrudeDirection(vertices, triangles, sourceNormals, directionMode, out Vector3 localDir))
        {
            error = "无法确定挤出方向。";
            return false;
        }

        localDir.Normalize();
        float half = bidirectional ? thickness * 0.5f : thickness;
        Vector3 frontOffset = bidirectional ? localDir * half : Vector3.zero;
        Vector3 backOffset = bidirectional ? -localDir * half : -localDir * thickness;

        int vertCount = vertices.Length;
        var outVerts = new List<Vector3>(vertCount * 2 + 8);
        var outUvs = new List<Vector2>(vertCount * 2 + 8);
        var outTris = new List<int>(triangles.Length * 2 + vertCount * 6);

        var frontVerts = new Vector3[vertCount];
        var backVerts = new Vector3[vertCount];
        for (int i = 0; i < vertCount; i++)
        {
            frontVerts[i] = vertices[i] + frontOffset;
            backVerts[i] = vertices[i] + backOffset;
        }

        Vector2[] sourceUvs = uvs != null && uvs.Length == vertCount ? uvs : null;

        int frontBase = 0;
        int backBase = vertCount;

        for (int i = 0; i < vertCount; i++)
        {
            outVerts.Add(frontVerts[i]);
            outUvs.Add(sourceUvs != null ? sourceUvs[i] : Vector2.zero);
        }

        for (int i = 0; i < vertCount; i++)
        {
            outVerts.Add(backVerts[i]);
            outUvs.Add(sourceUvs != null ? sourceUvs[i] : Vector2.zero);
        }

        // 顶面（原正面）
        for (int i = 0; i < triangles.Length; i += 3)
            AppendTriangle(outTris, frontBase + triangles[i], frontBase + triangles[i + 1], frontBase + triangles[i + 2]);

        // 底面（反向绕序）
        for (int i = 0; i < triangles.Length; i += 3)
            AppendTriangle(outTris, backBase + triangles[i + 2], backBase + triangles[i + 1], backBase + triangles[i]);

        // 侧壁：仅边界边
        var edgeUseCount = new Dictionary<long, int>();
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int a = triangles[i];
            int b = triangles[i + 1];
            int c = triangles[i + 2];
            AddUndirectedEdge(edgeUseCount, a, b);
            AddUndirectedEdge(edgeUseCount, b, c);
            AddUndirectedEdge(edgeUseCount, c, a);
        }

        foreach (var pair in edgeUseCount)
        {
            if (pair.Value != 1)
                continue;

            int a = (int)(pair.Key >> 32);
            int b = (int)(pair.Key & 0xFFFFFFFF);
            AppendSideQuad(outTris, frontBase, backBase, a, b);
        }

        result = new Mesh { name = source.name + "_Solid" };
        result.SetVertices(outVerts);
        result.SetUVs(0, outUvs);
        result.SetTriangles(outTris, 0);
        result.RecalculateBounds();
        result.RecalculateNormals();
        result.RecalculateTangents();
        return true;
    }

    static bool TryExtractMeshTopology(
        Mesh source,
        out Vector3[] vertices,
        out int[] triangles,
        out Vector2[] uvs,
        out string error)
    {
        vertices = null;
        triangles = null;
        uvs = null;
        error = null;

        if (source.isReadable)
        {
            vertices = source.vertices;
            triangles = source.triangles;
            uvs = source.uv;
        }
        else
        {
            using var meshDataArray = UnityEngine.Mesh.AcquireReadOnlyMeshData(source);
            if (meshDataArray.Length == 0)
            {
                error = "无法读取网格数据（MeshData 为空）。";
                return false;
            }

            var meshData = meshDataArray[0];
            int vertCount = meshData.vertexCount;
            if (vertCount < 3)
            {
                error = "网格顶点数量不足。";
                return false;
            }

            vertices = new Vector3[vertCount];
            var nativeVerts = new NativeArray<Vector3>(vertCount, Allocator.Temp);
            try
            {
                meshData.GetVertices(nativeVerts);
                nativeVerts.CopyTo(vertices);
            }
            finally
            {
                nativeVerts.Dispose();
            }

            var subMeshCount = meshData.subMeshCount;
            if (subMeshCount <= 0)
            {
                error = "网格没有子网格。";
                return false;
            }

            var triList = new List<int>();
            if (meshData.indexFormat == IndexFormat.UInt16)
            {
                var indexData = meshData.GetIndexData<ushort>();
                for (int s = 0; s < subMeshCount; s++)
                {
                    var subMesh = meshData.GetSubMesh(s);
                    int start = (int)subMesh.indexStart;
                    int count = (int)subMesh.indexCount;
                    for (int i = 0; i < count; i++)
                        triList.Add(indexData[start + i]);
                }
            }
            else
            {
                var indexData = meshData.GetIndexData<uint>();
                for (int s = 0; s < subMeshCount; s++)
                {
                    var subMesh = meshData.GetSubMesh(s);
                    int start = (int)subMesh.indexStart;
                    int count = (int)subMesh.indexCount;
                    for (int i = 0; i < count; i++)
                        triList.Add((int)indexData[start + i]);
                }
            }

            triangles = triList.ToArray();

            if (meshData.HasVertexAttribute(VertexAttribute.TexCoord0))
            {
                uvs = new Vector2[vertCount];
                var nativeUvs = new NativeArray<Vector2>(vertCount, Allocator.Temp);
                try
                {
                    meshData.GetUVs(0, nativeUvs);
                    nativeUvs.CopyTo(uvs);
                }
                finally
                {
                    nativeUvs.Dispose();
                }
            }
        }

        if (vertices == null || vertices.Length < 3 || triangles == null || triangles.Length < 3)
        {
            error = "网格顶点或三角形数量不足。";
            return false;
        }

        return true;
    }

    public static bool TryResolveExtrudeDirection(
        Vector3[] vertices,
        int[] triangles,
        Vector3[] meshNormals,
        ExtrudeDirectionMode mode,
        out Vector3 localDir)
    {
        localDir = Vector3.up;
        switch (mode)
        {
            case ExtrudeDirectionMode.LocalPositiveX:
                localDir = Vector3.right;
                return true;
            case ExtrudeDirectionMode.LocalNegativeX:
                localDir = Vector3.left;
                return true;
            case ExtrudeDirectionMode.LocalPositiveY:
                localDir = Vector3.up;
                return true;
            case ExtrudeDirectionMode.LocalNegativeY:
                localDir = Vector3.down;
                return true;
            case ExtrudeDirectionMode.LocalPositiveZ:
                localDir = Vector3.forward;
                return true;
            case ExtrudeDirectionMode.LocalNegativeZ:
                localDir = Vector3.back;
                return true;
        }

        if (meshNormals != null && meshNormals.Length == vertices.Length)
        {
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < meshNormals.Length; i++)
                sum += meshNormals[i];
            if (sum.sqrMagnitude > 1e-8f)
            {
                localDir = sum.normalized;
                return true;
            }
        }

        if (triangles.Length >= 3)
        {
            Vector3 v0 = vertices[triangles[0]];
            Vector3 v1 = vertices[triangles[1]];
            Vector3 v2 = vertices[triangles[2]];
            localDir = Vector3.Cross(v1 - v0, v2 - v0);
            if (localDir.sqrMagnitude > 1e-8f)
            {
                localDir.Normalize();
                return true;
            }
        }

        return false;
    }

    static void AppendSideQuad(List<int> tris, int frontBase, int backBase, int a, int b)
    {
        int fa = frontBase + a;
        int fb = frontBase + b;
        int bb = backBase + b;
        int ba = backBase + a;
        AppendTriangle(tris, fa, fb, bb);
        AppendTriangle(tris, fa, bb, ba);
    }

    static void AppendTriangle(List<int> tris, int i0, int i1, int i2)
    {
        tris.Add(i0);
        tris.Add(i1);
        tris.Add(i2);
    }

    static void AddUndirectedEdge(Dictionary<long, int> edgeUseCount, int a, int b)
    {
        if (a > b)
            (a, b) = (b, a);
        long key = ((long)a << 32) | (uint)b;
        if (edgeUseCount.TryGetValue(key, out int count))
            edgeUseCount[key] = count + 1;
        else
            edgeUseCount[key] = 1;
    }
}
#endif
