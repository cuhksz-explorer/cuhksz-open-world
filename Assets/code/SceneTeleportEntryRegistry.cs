using System;
using UnityEngine;

/// <summary>
/// 跨场景传送入口表：不依赖 .unity 内坐标，可在 Inspector 中维护 SceneTeleportEntryRegistry.asset。
/// </summary>
[CreateAssetMenu(fileName = "SceneTeleportEntryRegistry", menuName = "Game/Scene Teleport Entry Registry")]
public class SceneTeleportEntryRegistry : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        [Tooltip("来源场景，留空表示任意来源")]
        public string sourceSceneName;

        public string targetSceneName;
        public Vector3 position;
        public Vector3 rotationEuler;
        public bool snapToGround;
    }

    public Entry[] entries = Array.Empty<Entry>();

    static SceneTeleportEntryRegistry _cached;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ClearCache() => _cached = null;

    public static SceneTeleportEntryRegistry Instance
    {
        get
        {
            if (_cached != null)
                return _cached;

            _cached = Resources.Load<SceneTeleportEntryRegistry>("SceneTeleportEntryRegistry");
            return _cached;
        }
    }

    public bool TryResolve(
        string sourceSceneName,
        string targetSceneName,
        ref Vector3 position,
        ref Vector3 rotationEuler,
        out bool snapToGround)
    {
        snapToGround = true;

        if (entries == null || entries.Length == 0 || string.IsNullOrWhiteSpace(targetSceneName))
            return false;

        string targetKey = SceneTeleportHelper.NormalizeSceneName(targetSceneName);
        string sourceKey = SceneTeleportHelper.NormalizeSceneName(sourceSceneName);

        Entry? best = null;
        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (!SceneTeleportHelper.SceneNameMatches(entry.targetSceneName, targetKey))
                continue;

            if (!string.IsNullOrWhiteSpace(entry.sourceSceneName) &&
                !SceneTeleportHelper.SceneNameMatches(entry.sourceSceneName, sourceKey))
                continue;

            if (best == null)
            {
                best = entry;
                continue;
            }

            bool currentHasSource = !string.IsNullOrWhiteSpace(entry.sourceSceneName);
            bool bestHasSource = !string.IsNullOrWhiteSpace(best.Value.sourceSceneName);
            if (currentHasSource && !bestHasSource)
                best = entry;
        }

        if (!best.HasValue)
            return false;

        position = best.Value.position;
        rotationEuler = best.Value.rotationEuler;
        snapToGround = best.Value.snapToGround;
        return true;
    }
}
