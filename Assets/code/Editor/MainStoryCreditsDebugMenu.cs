#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class MainStoryCreditsDebugMenu
{
    const string MenuPath = "Tools/Game Credits/重置主线片尾播放记录（当前账号）";

    [MenuItem(MenuPath)]
    public static void ResetMainStoryCreditsFlag()
    {
        var sync = Object.FindObjectOfType<GameServerSync>(true);
        string user = sync != null ? sync.SavedUsername : "";
        if (string.IsNullOrWhiteSpace(user))
            user = "local";

        string key = "GameCredits_MainStoryRoll_v1_" + user.Trim();
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
        EditorUtility.DisplayDialog("主线片尾", "已重置账号「" + user + "」的主线片尾播放记录。", "确定");
    }
}
#endif
