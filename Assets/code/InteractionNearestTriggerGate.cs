using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 同一帧同一按键只触发一条交互请求，避免多个系统同时响应 F。
/// 仲裁规则（修正「纯优先级」导致远处商店/对话抢走近处载具/公交站的问题）：
/// - 两候选距离差 &gt; <see cref="DistanceDominatesOverPriorityMeters"/> 米时：只比远近（更近者胜）；
/// - 距离差在该阈值内：仍先比绝对距离（更近者胜）；几乎同距时再按 <c>priority</c>，最后 <c>requesterId</c>。
/// </summary>
public static class InteractionNearestTriggerGate
{
    /// <summary>距离差超过该值（米）时完全按远近决胜，忽略优先级差距（避免 200 优先级在 8 米外仍压过 1 米处的车/站）。</summary>
    public const float DistanceDominatesOverPriorityMeters = 0.75f;

    struct Candidate
    {
        public int priority;
        public float distance;
        public int requesterId;
        public Action action;
    }

    [DefaultExecutionOrder(32000)]
    sealed class Runner : MonoBehaviour
    {
        void LateUpdate() => Flush(Time.frameCount);
    }

    static int s_frame = -1;
    static readonly Dictionary<KeyCode, Candidate> s_bestByKey = new Dictionary<KeyCode, Candidate>();
    static Runner s_runner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        EnsureRunner();
    }

    static void EnsureRunner()
    {
        if (s_runner != null)
            return;
        var go = new GameObject("[InteractionNearestTriggerGate]");
        UnityEngine.Object.DontDestroyOnLoad(go);
        s_runner = go.AddComponent<Runner>();
    }

    /// <summary>提交本帧交互候选。见类注释：远距离以距离为主，极近时再用 priority。</summary>
    public static void Submit(KeyCode key, float distance, int requesterId, Action action, int priority = 0)
    {
        if (action == null)
            return;
        EnsureRunner();

        int frame = Time.frameCount;
        if (s_frame != frame)
        {
            s_frame = frame;
            s_bestByKey.Clear();
        }

        distance = Mathf.Max(0f, distance);
        if (!s_bestByKey.TryGetValue(key, out var cur))
        {
            s_bestByKey[key] = new Candidate
            {
                priority = priority,
                distance = distance,
                requesterId = requesterId,
                action = action
            };
            return;
        }

        bool better = IsBetterCandidate(priority, distance, requesterId, cur.priority, cur.distance, cur.requesterId);
        if (better)
            s_bestByKey[key] = new Candidate
            {
                priority = priority,
                distance = distance,
                requesterId = requesterId,
                action = action
            };
    }

    /// <summary>新候选是否应替换当前最佳。</summary>
    internal static bool IsBetterCandidate(
        int newPriority, float newDistance, int newRequesterId,
        int curPriority, float curDistance, int curRequesterId)
    {
        float dGap = Mathf.Abs(newDistance - curDistance);
        if (dGap > DistanceDominatesOverPriorityMeters)
            return newDistance < curDistance - 1e-4f;

        // 距离差不大时仍以「谁更近」为主，再处理完全同距时的 priority
        if (newDistance < curDistance - 1e-4f)
            return true;
        if (curDistance < newDistance - 1e-4f)
            return false;

        if (newPriority != curPriority)
            return newPriority > curPriority;
        return newRequesterId < curRequesterId;
    }

    static void Flush(int frame)
    {
        if (s_frame != frame || s_bestByKey.Count == 0)
            return;

        foreach (var kv in s_bestByKey)
            kv.Value.action?.Invoke();

        s_bestByKey.Clear();
        s_frame = -1;
    }
}
