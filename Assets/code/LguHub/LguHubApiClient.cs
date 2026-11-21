using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>GameServer 换票 + LGUHUB Unity API 请求。</summary>
public class LguHubApiClient : MonoBehaviour
{
    public static LguHubApiClient Instance { get; private set; }

    public string UnityToken { get; private set; }
    public string LguHubBaseUrl { get; private set; }
    public string Username { get; private set; }
    public bool HasSession => !string.IsNullOrEmpty(UnityToken);

    public event Action<string> OnError;

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

    public void ClearSession()
    {
        UnityToken = "";
        LguHubBaseUrl = LguHubConfig.ResolveLguHubBaseUrl();
        Username = "";
    }

    public IEnumerator CoEnsureSession(Action<bool> onDone)
    {
        ClearSession();
        var sync = GameServerSync.Instance;
        if (sync == null || !sync.IsLoggedIn)
        {
            ReportError("请先登录游戏账号");
            onDone?.Invoke(false);
            yield break;
        }

        var url = $"{sync.BaseUrlTrimmed}/api/lguhub/session?token={UnityWebRequest.EscapeURL(sync.AuthTokenForExtensions)}";
        using (var req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                ReportError($"无法获取 LGUHUB 会话: {req.error} {req.downloadHandler?.text}");
                onDone?.Invoke(false);
                yield break;
            }

            var wrap = JsonUtility.FromJson<LguHubSessionDto>(req.downloadHandler.text);
            if (wrap == null || string.IsNullOrEmpty(wrap.unity_token))
            {
                ReportError("LGUHUB 会话响应无效");
                onDone?.Invoke(false);
                yield break;
            }

            UnityToken = wrap.unity_token;
            LguHubBaseUrl = string.IsNullOrEmpty(wrap.lguhub_base_url)
                ? LguHubConfig.ResolveLguHubBaseUrl()
                : wrap.lguhub_base_url.TrimEnd('/');
            Username = wrap.username ?? sync.SavedUsername;
            onDone?.Invoke(true);
        }
    }

    IEnumerator CoGet(string path, Action<string> onJson, Action<string> onErr = null)
    {
        using (var req = UnityWebRequest.Get(LguHubBaseUrl + path))
        {
            req.SetRequestHeader("Authorization", "Bearer " + UnityToken);
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                var err = req.downloadHandler?.text ?? req.error;
                onErr?.Invoke(err);
                ReportError(err);
                yield break;
            }
            onJson?.Invoke(req.downloadHandler.text);
        }
    }

    IEnumerator CoPostJson(string path, string json, Action<string> onJson)
    {
        using (var req = new UnityWebRequest(LguHubBaseUrl + path, "POST"))
        {
            var bytes = Encoding.UTF8.GetBytes(json ?? "{}");
            req.uploadHandler = new UploadHandlerRaw(bytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + UnityToken);
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                ReportError(req.downloadHandler?.text ?? req.error);
                yield break;
            }
            onJson?.Invoke(req.downloadHandler.text);
        }
    }

    void ReportError(string msg)
    {
        if (!string.IsNullOrEmpty(msg))
            OnError?.Invoke(msg);
    }

    public IEnumerator CoFetchMe(Action<LguHubMeDto> onDone)
    {
        yield return CoGet("/api/unity/me", json => onDone?.Invoke(JsonUtility.FromJson<LguHubMeDto>(json)));
    }

    public IEnumerator CoFetchPoints(Action<LguHubPointsDto> onDone)
    {
        yield return CoGet("/api/unity/points/summary", json => onDone?.Invoke(JsonUtility.FromJson<LguHubPointsDto>(json)));
    }

    public IEnumerator CoCheckin(Action<LguHubCheckinDto> onDone)
    {
        yield return CoPostJson("/api/unity/checkin", "{}", json => onDone?.Invoke(JsonUtility.FromJson<LguHubCheckinDto>(json)));
    }

    public IEnumerator CoFetchCommunity(string query, Action<LguHubCommunityListDto> onDone)
    {
        var path = "/api/unity/community/batches" + (string.IsNullOrEmpty(query) ? "" : "?" + query);
        yield return CoGet(path, json => onDone?.Invoke(JsonUtility.FromJson<LguHubCommunityListDto>(json)));
    }

    public IEnumerator CoFetchCourses(string q, Action<LguHubCoursesDto> onDone)
    {
        var path = "/api/unity/courses" + (string.IsNullOrEmpty(q) ? "" : "?q=" + UnityWebRequest.EscapeURL(q));
        yield return CoGet(path, json => onDone?.Invoke(JsonUtility.FromJson<LguHubCoursesDto>(json)));
    }

    public IEnumerator CoFetchQuestions(string filter, string course, Action<LguHubQuestionsDto> onDone)
    {
        var sb = new StringBuilder("/api/unity/questions?");
        sb.Append("filter=").Append(UnityWebRequest.EscapeURL(filter ?? "all"));
        if (!string.IsNullOrEmpty(course))
            sb.Append("&course=").Append(UnityWebRequest.EscapeURL(course));
        yield return CoGet(sb.ToString(), json => onDone?.Invoke(JsonUtility.FromJson<LguHubQuestionsDto>(json)));
    }

    public IEnumerator CoFetchQuestionDetail(int qid, Action<LguHubQuestionDetailDto> onDone)
    {
        yield return CoGet("/api/unity/questions/" + qid, json => onDone?.Invoke(JsonUtility.FromJson<LguHubQuestionDetailDto>(json)));
    }

    public IEnumerator CoRate(int batchId, float score, Action<LguHubRateDto> onDone)
    {
        var body = $"{{\"batch_id\":{batchId},\"score\":{score.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}";
        yield return CoPostJson("/api/unity/rate", body, json => onDone?.Invoke(JsonUtility.FromJson<LguHubRateDto>(json)));
    }

    public IEnumerator CoComment(int batchId, string content, Action<LguHubSimpleOkDto> onDone)
    {
        var esc = EscapeJson(content);
        yield return CoPostJson("/api/unity/comment", $"{{\"batch_id\":{batchId},\"content\":\"{esc}\"}}",
            json => onDone?.Invoke(JsonUtility.FromJson<LguHubSimpleOkDto>(json)));
    }

    public IEnumerator CoFavorite(int batchId, Action<LguHubFavoriteDto> onDone)
    {
        yield return CoPostJson("/api/unity/favorite", $"{{\"batch_id\":{batchId}}}",
            json => onDone?.Invoke(JsonUtility.FromJson<LguHubFavoriteDto>(json)));
    }

    public IEnumerator CoCreateQuestion(string title, string content, string courseTag, bool bounty, string bountyText, Action<LguHubSimpleOkDto> onDone)
    {
        var body =
            $"{{\"title\":\"{EscapeJson(title)}\",\"content\":\"{EscapeJson(content)}\",\"course_tag\":\"{EscapeJson(courseTag)}\",\"has_bounty\":{(bounty ? "true" : "false")},\"bounty_text\":\"{EscapeJson(bountyText)}\"}}";
        yield return CoPostJson("/api/unity/questions", body, json => onDone?.Invoke(JsonUtility.FromJson<LguHubSimpleOkDto>(json)));
    }

    public IEnumerator CoCreateAnswer(int questionId, string content, Action<LguHubSimpleOkDto> onDone)
    {
        var body = $"{{\"question_id\":{questionId},\"content\":\"{EscapeJson(content)}\"}}";
        yield return CoPostJson("/api/unity/answers", body, json => onDone?.Invoke(JsonUtility.FromJson<LguHubSimpleOkDto>(json)));
    }

    public IEnumerator CoUpdateProfile(string bio, string nickname, Action<LguHubSimpleOkDto> onDone)
    {
        var body = $"{{\"bio\":\"{EscapeJson(bio)}\",\"nickname\":\"{EscapeJson(nickname)}\"}}";
        yield return CoPostJson("/api/unity/profile", body, json => onDone?.Invoke(JsonUtility.FromJson<LguHubSimpleOkDto>(json)));
    }

    static string EscapeJson(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
    }
}
