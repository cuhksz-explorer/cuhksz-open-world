using UnityEngine;

/// <summary>
/// 玩家走在液态水面上时，向 WaterRipples 发射踩踏波纹（不依赖 CharacterController 的 Trigger 回调）。
/// </summary>
[DisallowMultipleComponent]
public class PlayerWaterRippleEmitter : MonoBehaviour
{
    [Tooltip("波纹强度系数，越大水花越明显")]
    public float rippleStrength = 0.1f;

    [Tooltip("单次波纹速度上限")]
    public float maxRippleVelocity = 1.1f;

    [Tooltip("低于此水平速度不生成波纹")]
    public float minMoveSpeed = 0.12f;

    [Tooltip("两次发射波纹的最小间隔（秒）")]
    public float emitInterval = 0.07f;

    [Tooltip("脚底探测球半径")]
    public float probeRadius = 0.55f;

    [Tooltip("相对角色原点的脚底探测偏移")]
    public Vector3 feetOffset = new Vector3(0f, 0.05f, 0f);

    WaterRipples _activeWater;
    Vector3 _lastPosition;
    float _nextEmitTime;
    CharacterController _controller;

    void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _lastPosition = transform.position;
    }

    void FixedUpdate()
    {
        Vector3 feet = GetFeetPosition();
        if (!TryGetWaterUnderFeet(feet, out var water))
        {
            _activeWater = null;
            _lastPosition = transform.position;
            return;
        }

        _activeWater = water;

        float speed = (transform.position - _lastPosition).magnitude / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        _lastPosition = transform.position;

        if (speed < minMoveSpeed || Time.time < _nextEmitTime)
            return;

        float velocity = Mathf.Min(speed * rippleStrength, maxRippleVelocity);
        water.CreateRippleByPosition(feet, velocity);
        _nextEmitTime = Time.time + emitInterval;
    }

    Vector3 GetFeetPosition()
    {
        if (_controller != null)
            return transform.position + Vector3.up * (_controller.center.y - _controller.height * 0.5f + 0.15f) + feetOffset;

        return transform.position + feetOffset;
    }

    bool TryGetWaterUnderFeet(Vector3 feet, out WaterRipples water)
    {
        water = null;
        var hits = Physics.OverlapSphere(feet, probeRadius, ~0, QueryTriggerInteraction.Collide);
        float bestDist = float.MaxValue;

        foreach (var col in hits)
        {
            if (col == null)
                continue;

            var ripples = col.GetComponent<WaterRipples>();
            if (ripples == null)
                ripples = col.GetComponentInParent<WaterRipples>();
            if (ripples == null || !ripples.enabled || !ripples.gameObject.activeInHierarchy)
                continue;

            float d = Mathf.Abs(feet.y - col.bounds.max.y);
            if (d < bestDist)
            {
                bestDist = d;
                water = ripples;
            }
        }

        return water != null;
    }
}
