#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 由俯视图不规则多边形生成地板与外墙网格。
/// </summary>
public static class IrregularShellMeshBuilder
{
    public static Mesh BuildFloorMesh(string meshName, List<Vector2> polygonXZ, float topY, float thickness)
    {
        float bottomY = topY - Mathf.Max(0.02f, thickness);
        var outer = OffsetPolygon(polygonXZ, 0f);
        return BuildHorizontalSlab(meshName, outer, bottomY, topY);
    }

    public static Mesh BuildWallMesh(string meshName, List<Vector2> polygonXZ, float minY, float maxY, float thickness)
    {
        var inner = OffsetPolygon(polygonXZ, 0f);
        var outer = OffsetPolygon(polygonXZ, Mathf.Max(0.02f, thickness));
        return BuildVerticalStrip(meshName, inner, outer, minY, maxY);
    }

    public static Mesh BuildCeilingMesh(string meshName, List<Vector2> polygonXZ, float bottomY, float thickness)
    {
        float topY = bottomY + Mathf.Max(0.02f, thickness);
        var outer = OffsetPolygon(polygonXZ, 0f);
        return BuildHorizontalSlab(meshName, outer, bottomY, topY);
    }

    static Mesh BuildHorizontalSlab(string meshName, List<Vector2> polygon, float y0, float y1)
    {
        var topTris = TriangulatePolygon(polygon);
        int n = polygon.Count;
        var verts = new List<Vector3>(n * 2);
        var tris = new List<int>(topTris.Count * 2 + n * 6);

        for (int i = 0; i < n; i++)
            verts.Add(new Vector3(polygon[i].x, y1, polygon[i].y));
        int bottomStart = verts.Count;
        for (int i = 0; i < n; i++)
            verts.Add(new Vector3(polygon[i].x, y0, polygon[i].y));

        foreach (int t in topTris)
            tris.Add(t);

        for (int i = 0; i < topTris.Count; i += 3)
        {
            tris.Add(bottomStart + topTris[i + 2]);
            tris.Add(bottomStart + topTris[i + 1]);
            tris.Add(bottomStart + topTris[i]);
        }

        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            int t0 = i;
            int t1 = next;
            int b0 = bottomStart + i;
            int b1 = bottomStart + next;
            AddQuad(tris, t0, t1, b1, b0);
        }

        var mesh = new Mesh { name = meshName };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    static Mesh BuildVerticalStrip(string meshName, List<Vector2> inner, List<Vector2> outer, float y0, float y1)
    {
        int n = inner.Count;
        if (n < 3 || outer.Count != n)
            return new Mesh { name = meshName };

        var verts = new List<Vector3>(n * 8);
        var tris = new List<int>(n * 12);

        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            Vector2 in0 = inner[i];
            Vector2 in1 = inner[next];
            Vector2 out0 = outer[i];
            Vector2 out1 = outer[next];

            int b = verts.Count;
            verts.Add(new Vector3(in0.x, y0, in0.y));
            verts.Add(new Vector3(in1.x, y0, in1.y));
            verts.Add(new Vector3(out1.x, y0, out1.y));
            verts.Add(new Vector3(out0.x, y0, out0.y));
            verts.Add(new Vector3(in0.x, y1, in0.y));
            verts.Add(new Vector3(in1.x, y1, in1.y));
            verts.Add(new Vector3(out1.x, y1, out1.y));
            verts.Add(new Vector3(out0.x, y1, out0.y));

            AddQuad(tris, b + 0, b + 1, b + 5, b + 4);
            AddQuad(tris, b + 1, b + 2, b + 6, b + 5);
            AddQuad(tris, b + 2, b + 3, b + 7, b + 6);
            AddQuad(tris, b + 3, b + 0, b + 4, b + 7);
            AddQuad(tris, b + 4, b + 5, b + 6, b + 7);
            AddQuad(tris, b + 0, b + 3, b + 2, b + 1);
        }

        var mesh = new Mesh { name = meshName };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    static void AddQuad(List<int> tris, int a, int b, int c, int d)
    {
        tris.Add(a);
        tris.Add(b);
        tris.Add(c);
        tris.Add(a);
        tris.Add(c);
        tris.Add(d);
    }

    public static List<Vector2> OffsetPolygon(List<Vector2> polygon, float distance)
    {
        int n = polygon.Count;
        var result = new List<Vector2>(n);
        if (n < 3)
            return result;

        for (int i = 0; i < n; i++)
        {
            Vector2 prev = polygon[(i - 1 + n) % n];
            Vector2 curr = polygon[i];
            Vector2 next = polygon[(i + 1) % n];

            Vector2 d0 = (curr - prev).normalized;
            Vector2 d1 = (next - curr).normalized;
            Vector2 n0 = new Vector2(-d0.y, d0.x);
            Vector2 n1 = new Vector2(-d1.y, d1.x);
            Vector2 normal = (n0 + n1).normalized;

            float dot = Vector2.Dot(d0, d1);
            float sinHalf = Mathf.Sqrt(Mathf.Max(1e-6f, (1f + dot) * 0.5f));
            float miter = distance / sinHalf;
            float cross = d0.x * d1.y - d0.y * d1.x;
            if (cross < 0f)
                miter = -miter;

            result.Add(curr + normal * miter);
        }

        return result;
    }

    static List<int> TriangulatePolygon(List<Vector2> polygon)
    {
        int n = polygon.Count;
        var indices = new List<int>();
        if (n < 3)
            return indices;

        var verts = new List<Vector2>(polygon);
        var remaining = new List<int>();
        for (int i = 0; i < n; i++)
            remaining.Add(i);

        int guard = n * n;
        while (remaining.Count > 3 && guard-- > 0)
        {
            bool clipped = false;
            for (int i = 0; i < remaining.Count; i++)
            {
                int i0 = remaining[(i - 1 + remaining.Count) % remaining.Count];
                int i1 = remaining[i];
                int i2 = remaining[(i + 1) % remaining.Count];

                if (!IsEar(verts, remaining, i0, i1, i2))
                    continue;

                indices.Add(i0);
                indices.Add(i1);
                indices.Add(i2);
                remaining.RemoveAt(i);
                clipped = true;
                break;
            }

            if (!clipped)
                break;
        }

        if (remaining.Count == 3)
        {
            indices.Add(remaining[0]);
            indices.Add(remaining[1]);
            indices.Add(remaining[2]);
        }

        return indices;
    }

    static bool IsEar(List<Vector2> verts, List<int> remaining, int i0, int i1, int i2)
    {
        Vector2 a = verts[i0];
        Vector2 b = verts[i1];
        Vector2 c = verts[i2];
        if (Cross(b - a, c - b) <= 1e-8f)
            return false;

        for (int i = 0; i < remaining.Count; i++)
        {
            int idx = remaining[i];
            if (idx == i0 || idx == i1 || idx == i2)
                continue;
            if (PointInTriangle(verts[idx], a, b, c))
                return false;
        }

        return true;
    }

    static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

    static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float c1 = Cross(b - a, p - a);
        float c2 = Cross(c - b, p - b);
        float c3 = Cross(a - c, p - c);
        bool hasNeg = c1 < 0f || c2 < 0f || c3 < 0f;
        bool hasPos = c1 > 0f || c2 > 0f || c3 > 0f;
        return !(hasNeg && hasPos);
    }
}
#endif
