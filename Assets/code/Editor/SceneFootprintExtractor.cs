#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 从场景 Renderer 占用栅格提取俯视图外轮廓（不规则、可凹）。
/// </summary>
public static class SceneFootprintExtractor
{
    public readonly struct FootprintResult
    {
        public readonly List<Vector2> PolygonWorldXZ;
        public readonly float MinY;
        public readonly float MaxY;
        public readonly float CellSize;
        public readonly Vector2 GridOriginXZ;
        public readonly Bounds WorldBounds;

        public FootprintResult(
            List<Vector2> polygonWorldXZ,
            float minY,
            float maxY,
            float cellSize,
            Vector2 gridOriginXZ,
            Bounds worldBounds)
        {
            PolygonWorldXZ = polygonWorldXZ;
            MinY = minY;
            MaxY = maxY;
            CellSize = cellSize;
            GridOriginXZ = gridOriginXZ;
            WorldBounds = worldBounds;
        }
    }

    public static FootprintResult Extract(
        IEnumerable<Renderer> renderers,
        float cellSize,
        float paddingMeters,
        int maxGridDimension = 2048)
    {
        if (renderers == null)
            throw new System.ArgumentNullException(nameof(renderers));

        var rendererList = new List<Renderer>();
        foreach (var r in renderers)
        {
            if (r != null && r.enabled)
                rendererList.Add(r);
        }

        if (rendererList.Count == 0)
            throw new System.InvalidOperationException("没有可用的 Renderer。");

        Bounds bounds = rendererList[0].bounds;
        for (int i = 1; i < rendererList.Count; i++)
            bounds.Encapsulate(rendererList[i].bounds);

        float minY = bounds.min.y;
        float maxY = bounds.max.y;
        float pad = Mathf.Max(0f, paddingMeters);
        Vector2 origin = new Vector2(bounds.min.x, bounds.min.z);
        float spanX = bounds.size.x;
        float spanZ = bounds.size.z;

        float cell = Mathf.Max(0.05f, cellSize);
        int w = Mathf.CeilToInt(spanX / cell);
        int h = Mathf.CeilToInt(spanZ / cell);
        if (w < 2 || h < 2)
            throw new System.InvalidOperationException("场景平面范围过小。");

        if (w > maxGridDimension || h > maxGridDimension)
        {
            float scale = Mathf.Max(w / (float)maxGridDimension, h / (float)maxGridDimension);
            cell *= scale;
            w = Mathf.CeilToInt(spanX / cell);
            h = Mathf.CeilToInt(spanZ / cell);
        }

        var grid = new bool[w, h];
        for (int i = 0; i < rendererList.Count; i++)
            RasterizeBounds(rendererList[i].bounds, origin, cell, w, h, grid);

        int dilateSteps = Mathf.CeilToInt(pad / cell);
        if (dilateSteps > 0)
            grid = Dilate(grid, w, h, dilateSteps);

        KeepLargestComponent(grid, w, h);
        FillInteriorHoles(grid, w, h);

        List<Vector2> polygon = TraceOuterContour(grid, w, h, origin, cell);
        if (polygon.Count < 3)
            throw new System.InvalidOperationException("无法提取有效外轮廓。");

        polygon = SimplifyPolygon(polygon, cell * 0.35f);
        EnsureCounterClockwise(polygon);

        return new FootprintResult(polygon, minY, maxY, cell, origin, bounds);
    }

    static void RasterizeBounds(Bounds b, Vector2 origin, float cell, int w, int h, bool[,] grid)
    {
        int i0 = Mathf.Clamp(Mathf.FloorToInt((b.min.x - origin.x) / cell), 0, w - 1);
        int i1 = Mathf.Clamp(Mathf.FloorToInt((b.max.x - origin.x) / cell), 0, w - 1);
        int j0 = Mathf.Clamp(Mathf.FloorToInt((b.min.z - origin.y) / cell), 0, h - 1);
        int j1 = Mathf.Clamp(Mathf.FloorToInt((b.max.z - origin.y) / cell), 0, h - 1);

        for (int i = i0; i <= i1; i++)
        for (int j = j0; j <= j1; j++)
            grid[i, j] = true;
    }

    static bool[,] Dilate(bool[,] src, int w, int h, int iterations)
    {
        var current = src;
        for (int step = 0; step < iterations; step++)
        {
            var next = new bool[w, h];
            for (int i = 0; i < w; i++)
            {
                for (int j = 0; j < h; j++)
                {
                    if (current[i, j])
                    {
                        next[i, j] = true;
                        continue;
                    }

                    for (int di = -1; di <= 1; di++)
                    for (int dj = -1; dj <= 1; dj++)
                    {
                        if (di == 0 && dj == 0)
                            continue;
                        int ni = i + di;
                        int nj = j + dj;
                        if (ni < 0 || nj < 0 || ni >= w || nj >= h)
                            continue;
                        if (current[ni, nj])
                        {
                            next[i, j] = true;
                            break;
                        }
                    }
                }
            }

            current = next;
        }

        return current;
    }

    static void KeepLargestComponent(bool[,] grid, int w, int h)
    {
        var visited = new bool[w, h];
        int bestArea = 0;
        int bestStartI = -1;
        int bestStartJ = -1;

        for (int i = 0; i < w; i++)
        {
            for (int j = 0; j < h; j++)
            {
                if (!grid[i, j] || visited[i, j])
                    continue;

                int area = FloodFillCount(grid, visited, w, h, i, j, out int startI, out int startJ);
                if (area > bestArea)
                {
                    bestArea = area;
                    bestStartI = startI;
                    bestStartJ = startJ;
                }
            }
        }

        if (bestArea <= 0)
            return;

        var keep = new bool[w, h];
        var keepVisited = new bool[w, h];
        FloodFillMark(grid, keep, keepVisited, w, h, bestStartI, bestStartJ);

        for (int i = 0; i < w; i++)
        for (int j = 0; j < h; j++)
            grid[i, j] = keep[i, j];
    }

    static int FloodFillCount(bool[,] grid, bool[,] visited, int w, int h, int si, int sj, out int startI, out int startJ)
    {
        startI = si;
        startJ = sj;
        int count = 0;
        var stack = new Stack<Vector2Int>();
        stack.Push(new Vector2Int(si, sj));
        visited[si, sj] = true;

        while (stack.Count > 0)
        {
            var p = stack.Pop();
            count++;
            for (int di = -1; di <= 1; di++)
            for (int dj = -1; dj <= 1; dj++)
            {
                if (di == 0 && dj == 0)
                    continue;
                int ni = p.x + di;
                int nj = p.y + dj;
                if (ni < 0 || nj < 0 || ni >= w || nj >= h)
                    continue;
                if (!grid[ni, nj] || visited[ni, nj])
                    continue;
                visited[ni, nj] = true;
                stack.Push(new Vector2Int(ni, nj));
            }
        }

        return count;
    }

    static void FloodFillMark(bool[,] src, bool[,] dst, bool[,] visited, int w, int h, int si, int sj)
    {
        var stack = new Stack<Vector2Int>();
        stack.Push(new Vector2Int(si, sj));
        visited[si, sj] = true;
        dst[si, sj] = true;

        while (stack.Count > 0)
        {
            var p = stack.Pop();
            for (int di = -1; di <= 1; di++)
            for (int dj = -1; dj <= 1; dj++)
            {
                if (di == 0 && dj == 0)
                    continue;
                int ni = p.x + di;
                int nj = p.y + dj;
                if (ni < 0 || nj < 0 || ni >= w || nj >= h)
                    continue;
                if (!src[ni, nj] || visited[ni, nj])
                    continue;
                visited[ni, nj] = true;
                dst[ni, nj] = true;
                stack.Push(new Vector2Int(ni, nj));
            }
        }
    }

    static void FillInteriorHoles(bool[,] grid, int w, int h)
    {
        var outside = new bool[w, h];
        var queue = new Queue<Vector2Int>();

        for (int i = 0; i < w; i++)
        {
            TryEnqueueOutside(grid, outside, queue, w, h, i, 0);
            TryEnqueueOutside(grid, outside, queue, w, h, i, h - 1);
        }

        for (int j = 0; j < h; j++)
        {
            TryEnqueueOutside(grid, outside, queue, w, h, 0, j);
            TryEnqueueOutside(grid, outside, queue, w, h, w - 1, j);
        }

        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            TryEnqueueOutside(grid, outside, queue, w, h, p.x + 1, p.y);
            TryEnqueueOutside(grid, outside, queue, w, h, p.x - 1, p.y);
            TryEnqueueOutside(grid, outside, queue, w, h, p.x, p.y + 1);
            TryEnqueueOutside(grid, outside, queue, w, h, p.x, p.y - 1);
        }

        for (int i = 0; i < w; i++)
        for (int j = 0; j < h; j++)
        {
            if (!grid[i, j] && !outside[i, j])
                grid[i, j] = true;
        }
    }

    static void TryEnqueueOutside(bool[,] grid, bool[,] outside, Queue<Vector2Int> queue, int w, int h, int i, int j)
    {
        if (i < 0 || j < 0 || i >= w || j >= h)
            return;
        if (grid[i, j] || outside[i, j])
            return;
        outside[i, j] = true;
        queue.Enqueue(new Vector2Int(i, j));
    }

    static List<Vector2> TraceOuterContour(bool[,] grid, int w, int h, Vector2 origin, float cell)
    {
        var edgeMap = new Dictionary<string, (Vector2Int a, Vector2Int b)>();
        for (int i = 0; i < w; i++)
        {
            for (int j = 0; j < h; j++)
            {
                if (!grid[i, j])
                    continue;

                bool rightEmpty = i == w - 1 || !grid[i + 1, j];
                bool leftEmpty = i == 0 || !grid[i - 1, j];
                bool upEmpty = j == h - 1 || !grid[i, j + 1];
                bool downEmpty = j == 0 || !grid[i, j - 1];

                float x0 = origin.x + i * cell;
                float x1 = origin.x + (i + 1) * cell;
                float z0 = origin.y + j * cell;
                float z1 = origin.y + (j + 1) * cell;

                if (rightEmpty)
                    RegisterEdge(edgeMap, PackPoint(x1, z0), PackPoint(x1, z1));
                if (leftEmpty)
                    RegisterEdge(edgeMap, PackPoint(x0, z1), PackPoint(x0, z0));
                if (upEmpty)
                    RegisterEdge(edgeMap, PackPoint(x1, z1), PackPoint(x0, z1));
                if (downEmpty)
                    RegisterEdge(edgeMap, PackPoint(x0, z0), PackPoint(x1, z0));
            }
        }

        if (edgeMap.Count == 0)
            return new List<Vector2>();

        var adjacency = new Dictionary<Vector2Int, List<Vector2Int>>();
        foreach (var edge in edgeMap.Values)
        {
            AddAdj(adjacency, edge.a, edge.b);
            AddAdj(adjacency, edge.b, edge.a);
        }

        var start = FindStartCorner(adjacency);
        return WalkLoop(start, adjacency);
    }

    static Vector2Int PackPoint(float x, float z)
    {
        return new Vector2Int(Mathf.RoundToInt(x * 1000f), Mathf.RoundToInt(z * 1000f));
    }

    static Vector2 UnpackPoint(Vector2Int packed)
    {
        return new Vector2(packed.x / 1000f, packed.y / 1000f);
    }

    static void RegisterEdge(Dictionary<string, (Vector2Int a, Vector2Int b)> edgeMap, Vector2Int a, Vector2Int b)
    {
        if (a == b)
            return;
        edgeMap[MakeEdgeKey(a, b)] = (a, b);
    }

    static string MakeEdgeKey(Vector2Int a, Vector2Int b)
    {
        if (a.x < b.x || (a.x == b.x && a.y <= b.y))
            return $"{a.x}_{a.y}_{b.x}_{b.y}";
        return $"{b.x}_{b.y}_{a.x}_{a.y}";
    }

    static void AddAdj(Dictionary<Vector2Int, List<Vector2Int>> adj, Vector2Int a, Vector2Int b)
    {
        if (!adj.TryGetValue(a, out var list))
        {
            list = new List<Vector2Int>(2);
            adj[a] = list;
        }

        if (!list.Contains(b))
            list.Add(b);
    }

    static Vector2Int FindStartCorner(Dictionary<Vector2Int, List<Vector2Int>> adjacency)
    {
        Vector2Int best = default;
        bool has = false;
        foreach (var kv in adjacency)
        {
            if (!has || kv.Key.x < best.x || (kv.Key.x == best.x && kv.Key.y < best.y))
            {
                best = kv.Key;
                has = true;
            }
        }

        return best;
    }

    static List<Vector2> WalkLoop(Vector2Int start, Dictionary<Vector2Int, List<Vector2Int>> adjacency)
    {
        var polygon = new List<Vector2>();
        Vector2Int current = start;
        Vector2Int previous = new Vector2Int(int.MinValue, int.MinValue);
        int guard = adjacency.Count + 8;

        do
        {
            polygon.Add(UnpackPoint(current));
            if (!adjacency.TryGetValue(current, out var neighbors) || neighbors.Count == 0)
                break;

            Vector2Int next = new Vector2Int(int.MinValue, int.MinValue);
            float bestDelta = float.NegativeInfinity;
            Vector2 cur = UnpackPoint(current);
            Vector2 prevDir = previous.x == int.MinValue
                ? Vector2.right
                : (cur - UnpackPoint(previous)).normalized;

            foreach (Vector2Int n in neighbors)
            {
                if (n == previous)
                    continue;
                Vector2 dir = (UnpackPoint(n) - cur).normalized;
                float angle = Mathf.Atan2(dir.y, dir.x);
                float prevAngle = Mathf.Atan2(prevDir.y, prevDir.x);
                float delta = angle - prevAngle;
                while (delta <= -Mathf.PI)
                    delta += Mathf.PI * 2f;
                while (delta > Mathf.PI)
                    delta -= Mathf.PI * 2f;
                if (delta > bestDelta)
                {
                    bestDelta = delta;
                    next = n;
                }
            }

            if (next.x == int.MinValue)
                break;

            previous = current;
            current = next;
            guard--;
        } while (current != start && guard > 0);

        return polygon;
    }

    static List<Vector2> SimplifyPolygon(List<Vector2> points, float epsilon)
    {
        if (points.Count <= 3)
            return points;

        bool[] keep = new bool[points.Count];
        for (int i = 0; i < keep.Length; i++)
            keep[i] = true;

        SimplifySection(points, 0, points.Count - 1, epsilon, keep);
        var result = new List<Vector2>();
        for (int i = 0; i < points.Count; i++)
        {
            if (keep[i])
                result.Add(points[i]);
        }

        return result;
    }

    static void SimplifySection(List<Vector2> points, int start, int end, float epsilon, bool[] keep)
    {
        if (end <= start + 1)
            return;

        float maxDist = 0f;
        int index = 0;
        Vector2 a = points[start];
        Vector2 b = points[end];
        for (int i = start + 1; i < end; i++)
        {
            float dist = DistancePointToSegment(points[i], a, b);
            if (dist > maxDist)
            {
                maxDist = dist;
                index = i;
            }
        }

        if (maxDist > epsilon)
        {
            SimplifySection(points, start, index, epsilon, keep);
            SimplifySection(points, index, end, epsilon, keep);
        }
        else
        {
            for (int i = start + 1; i < end; i++)
                keep[i] = false;
        }
    }

    static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude < 1e-8f ? 0f : Vector2.Dot(p - a, ab) / ab.sqrMagnitude;
        t = Mathf.Clamp01(t);
        Vector2 proj = a + ab * t;
        return Vector2.Distance(p, proj);
    }

    static void EnsureCounterClockwise(List<Vector2> polygon)
    {
        float signedArea = 0f;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[(i + 1) % polygon.Count];
            signedArea += a.x * b.y - b.x * a.y;
        }

        if (signedArea < 0f)
            polygon.Reverse();
    }
}
#endif
