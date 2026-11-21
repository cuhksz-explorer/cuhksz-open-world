using System;

/// <summary>
/// 与 GameServer <c>/api/flag_auction/*</c> JSON 字段对齐（JsonUtility）。
/// </summary>
[Serializable]
public class FlagAuctionStateResponse
{
    public bool ok;
    public float server_time;
    public int auction_sec;
    public int customize_sec;
    public FlagAuctionSlotWire[] slots;
}

[Serializable]
public class FlagAuctionSlotWire
{
    public string slot_id;
    public string phase;
    public float phase_ends_at;
    public int bid_amount;
    public string high_bidder_nickname;
    public int customize_user_id;
    public string display_text;
    public float color_r;
    public float color_g;
    public float color_b;
    public float color_a;
}

[Serializable]
public class FlagAuctionBidWire
{
    public string token;
    public string slot_id;
    public int amount;
}

[Serializable]
public class FlagAuctionDisplayWire
{
    public string token;
    public string slot_id;
    public string text;
    public float color_r;
    public float color_g;
    public float color_b;
    public float color_a;
}

[Serializable]
public class FlagAuctionPlayerGoldWire
{
    public bool ok;
    public int dorm_gold;
}
