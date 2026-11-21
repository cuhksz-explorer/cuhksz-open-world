using System;
using UnityEngine;

/// <summary>
/// 客户端内置版本号（与 Player Settings → Version 一致）。
/// 格式 X.Y.Z，每段 0–9，满 9 进位：0.0.9 → 0.1.0。
/// </summary>
public static class ClientBuildVersion
{
    public const string DefaultVersion = "0.0.1";

    public static string LocalVersion => Normalize(Application.version);

    public static string Normalize(string raw)
    {
        string v = (raw ?? "").Trim();
        if (v.Length > 0 && (v[0] == 'v' || v[0] == 'V'))
            v = v.Substring(1).Trim();

        if (string.IsNullOrEmpty(v))
            return DefaultVersion;

        var parts = v.Split('.');
        int a = ParsePart(parts, 0);
        int b = ParsePart(parts, 1);
        int c = ParsePart(parts, 2);
        return $"{a}.{b}.{c}";
    }

    public static int Compare(string left, string right)
    {
        var l = ParseTriple(left);
        var r = ParseTriple(right);
        for (int i = 0; i < 3; i++)
        {
            int cmp = l[i].CompareTo(r[i]);
            if (cmp != 0)
                return cmp;
        }
        return 0;
    }

    public static bool Equals(string left, string right) => Compare(left, right) == 0;

    public static string Increment(string version)
    {
        var parts = ParseTriple(version);
        parts[2]++;
        if (parts[2] > 9)
        {
            parts[2] = 0;
            parts[1]++;
        }
        if (parts[1] > 9)
        {
            parts[1] = 0;
            parts[0]++;
        }
        return $"{parts[0]}.{parts[1]}.{parts[2]}";
    }

    static int[] ParseTriple(string version)
    {
        string v = Normalize(version);
        var segs = v.Split('.');
        return new[]
        {
            ParsePart(segs, 0),
            ParsePart(segs, 1),
            ParsePart(segs, 2),
        };
    }

    static int ParsePart(string[] parts, int index)
    {
        if (parts == null || index >= parts.Length)
            return 0;
        string seg = (parts[index] ?? "").Trim();
        if (string.IsNullOrEmpty(seg))
            return 0;
        if (!int.TryParse(seg, out int value) || value < 0)
            return 0;
        return Mathf.Min(value, 9);
    }
}
