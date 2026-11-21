using System;
using System.Collections;
using System.Text;
using Multiplayer;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 调用旗帜竞拍 REST；<see cref="FlagAuctionStateResponse"/> 可匿名拉取，出价/改字需登录。
/// </summary>
public static class FlagAuctionApi
{
    public static string ResolveBaseUrl()
    {
        if (GameServerSync.Instance != null)
            return GameServerSync.Instance.BaseUrlTrimmed;
        return GameServerEndpoint.ResolveBaseUrl(null);
    }

    public static IEnumerator CoFetchState(string[] slotIds, Action<FlagAuctionStateResponse> onOk, Action<string> onErr)
    {
        if (slotIds == null || slotIds.Length == 0)
        {
            onErr?.Invoke("无旗帜槽位");
            yield break;
        }

        var sb = new StringBuilder();
        for (int i = 0; i < slotIds.Length; i++)
        {
            string s = (slotIds[i] ?? "").Trim();
            if (string.IsNullOrEmpty(s)) continue;
            if (sb.Length > 0) sb.Append(',');
            sb.Append(UnityWebRequest.EscapeURL(s));
        }
        if (sb.Length == 0)
        {
            onErr?.Invoke("无有效 slot_id");
            yield break;
        }

        string url = $"{ResolveBaseUrl()}/api/flag_auction/state?slots={sb}";
        using (var req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error + " " + req.downloadHandler?.text);
                yield break;
            }

            try
            {
                var r = JsonUtility.FromJson<FlagAuctionStateResponse>(req.downloadHandler.text);
                onOk?.Invoke(r ?? new FlagAuctionStateResponse());
            }
            catch (Exception e)
            {
                onErr?.Invoke(e.Message);
            }
        }
    }

    public static IEnumerator CoBid(string token, string slotId, int amount, Action onOk, Action<string> onErr)
    {
        if (string.IsNullOrEmpty(token))
        {
            onErr?.Invoke("未登录");
            yield break;
        }

        string url = $"{ResolveBaseUrl()}/api/flag_auction/bid";
        string json = JsonUtility.ToJson(new FlagAuctionBidWire
        {
            token = token,
            slot_id = slotId,
            amount = amount
        });
        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error + " " + req.downloadHandler?.text);
                yield break;
            }
        }

        onOk?.Invoke();
    }

    public static IEnumerator CoSetDisplay(
        string token,
        string slotId,
        string text,
        Color color,
        Action onOk,
        Action<string> onErr)
    {
        if (string.IsNullOrEmpty(token))
        {
            onErr?.Invoke("未登录");
            yield break;
        }

        string url = $"{ResolveBaseUrl()}/api/flag_auction/display";
        string json = JsonUtility.ToJson(new FlagAuctionDisplayWire
        {
            token = token,
            slot_id = slotId,
            text = text ?? "",
            color_r = color.r,
            color_g = color.g,
            color_b = color.b,
            color_a = color.a
        });
        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error + " " + req.downloadHandler?.text);
                yield break;
            }
        }

        onOk?.Invoke();
    }

    public static IEnumerator CoFetchDormGold(Action<int> onOk, Action<string> onErr)
    {
        if (GameServerSync.Instance == null || !GameServerSync.Instance.IsLoggedIn)
        {
            onErr?.Invoke("未登录");
            yield break;
        }

        string token = GameServerSync.Instance.AuthTokenForExtensions;
        string url = $"{ResolveBaseUrl()}/api/player_state?token={UnityWebRequest.EscapeURL(token)}";
        using (var req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error);
                yield break;
            }

            try
            {
                var w = JsonUtility.FromJson<FlagAuctionPlayerGoldWire>(req.downloadHandler.text);
                if (w != null && w.ok)
                    onOk?.Invoke(Mathf.Max(0, w.dorm_gold));
                else
                    onErr?.Invoke("无效响应");
            }
            catch (Exception e)
            {
                onErr?.Invoke(e.Message);
            }
        }
    }

    public static IEnumerator CoFetchMyUserId(Action<int> onOk, Action<string> onErr)
    {
        if (GameServerSync.Instance == null || !GameServerSync.Instance.IsLoggedIn)
        {
            onErr?.Invoke("未登录");
            yield break;
        }

        string token = GameServerSync.Instance.AuthTokenForExtensions;
        string url = $"{ResolveBaseUrl()}/api/me?token={UnityWebRequest.EscapeURL(token)}";
        using (var req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error);
                yield break;
            }

            try
            {
                var w = JsonUtility.FromJson<MeResponseDto>(req.downloadHandler.text);
                if (w != null && w.user_id > 0)
                    onOk?.Invoke(w.user_id);
                else
                    onErr?.Invoke("无效响应");
            }
            catch (Exception e)
            {
                onErr?.Invoke(e.Message);
            }
        }
    }
}
