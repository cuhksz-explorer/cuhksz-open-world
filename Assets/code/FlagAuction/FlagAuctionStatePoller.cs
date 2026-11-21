using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 轮询 GameServer 旗帜竞拍状态并广播到场景中的 <see cref="FlagAuctionSlotView"/>。
/// 首个旗帜注册时自动挂到 <see cref="GameServerSync"/> 或独立物体。
/// </summary>
[DisallowMultipleComponent]
public class FlagAuctionStatePoller : MonoBehaviour
{
    public static FlagAuctionStatePoller Instance { get; private set; }

    [Tooltip("轮询间隔（秒）")]
    [SerializeField] float pollIntervalSeconds = 2f;

    readonly List<FlagAuctionSlotView> _slots = new List<FlagAuctionSlotView>();
    Coroutine _loop;
    FlagAuctionStateResponse _last;

    public FlagAuctionStateResponse LastState => _last;
    public event Action<FlagAuctionStateResponse> OnStateUpdated;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static void EnsureExists()
    {
        if (Instance != null) return;
        if (GameServerSync.Instance != null)
        {
            var root = GameServerSync.Instance.gameObject;
            var p = root.GetComponent<FlagAuctionStatePoller>();
            if (p == null)
                p = root.AddComponent<FlagAuctionStatePoller>();
            return;
        }

        var go = new GameObject("FlagAuctionStatePoller");
        DontDestroyOnLoad(go);
        go.AddComponent<FlagAuctionStatePoller>();
    }

    public void Register(FlagAuctionSlotView v)
    {
        if (v == null) return;
        if (!_slots.Contains(v))
            _slots.Add(v);
        EnsureLoop();
    }

    public void Unregister(FlagAuctionSlotView v)
    {
        if (v == null) return;
        _slots.Remove(v);
        if (_slots.Count == 0 && _loop != null)
        {
            StopCoroutine(_loop);
            _loop = null;
        }
    }

    void EnsureLoop()
    {
        if (_loop != null || _slots.Count == 0) return;
        _loop = StartCoroutine(CoPollLoop());
    }

    IEnumerator CoPollLoop()
    {
        var wait = new WaitForSeconds(Mathf.Max(0.5f, pollIntervalSeconds));
        while (_slots.Count > 0)
        {
            var ids = new string[_slots.Count];
            for (int i = 0; i < _slots.Count; i++)
                ids[i] = _slots[i].SlotId;
            yield return FlagAuctionApi.CoFetchState(
                ids,
                r =>
                {
                    _last = r;
                    OnStateUpdated?.Invoke(r);
                    ApplyToViews(r);
                },
                _ => { });
            yield return wait;
        }

        _loop = null;
    }

    void ApplyToViews(FlagAuctionStateResponse resp)
    {
        if (resp?.slots == null) return;
        for (int i = 0; i < _slots.Count; i++)
        {
            var view = _slots[i];
            if (view == null) continue;
            FlagAuctionSlotWire w = null;
            string sid = view.SlotId;
            for (int j = 0; j < resp.slots.Length; j++)
            {
                if (resp.slots[j] != null &&
                    string.Equals(resp.slots[j].slot_id, sid, StringComparison.Ordinal))
                {
                    w = resp.slots[j];
                    break;
                }
            }

            view.ApplyWire(w);
        }
    }

    /// <summary>面板打开时立即拉取一次（不依赖轮询间隔）。</summary>
    public void RequestImmediateRefresh()
    {
        if (_slots.Count == 0) return;
        StartCoroutine(CoOnce());
    }

    IEnumerator CoOnce()
    {
        var ids = new string[_slots.Count];
        for (int i = 0; i < _slots.Count; i++)
            ids[i] = _slots[i].SlotId;
        yield return FlagAuctionApi.CoFetchState(
            ids,
            r =>
            {
                _last = r;
                OnStateUpdated?.Invoke(r);
                ApplyToViews(r);
            },
            _ => { });
    }
}
