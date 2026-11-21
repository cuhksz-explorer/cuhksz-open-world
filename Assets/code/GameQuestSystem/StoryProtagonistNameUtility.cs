using System;

namespace GameQuestSystem
{
    /// <summary>
    /// 剧情文案中的默认主角名「林渡」在展示时替换为玩家注册昵称。
    /// </summary>
    public static class StoryProtagonistNameUtility
    {
        public const string DefaultProtagonistName = "林渡";

        const string MirrorProtagonistName = "镜像林渡";
        const string SmileProtagonistName = "林渡笑笑";

        public static string GetPlayerNickname()
        {
            string nick = GameServerSync.Instance != null ? GameServerSync.Instance.SavedNickname : "";
            if (string.IsNullOrWhiteSpace(nick))
                return DefaultProtagonistName;
            return nick.Trim();
        }

        public static string ResolveSpeakerName(string speaker)
        {
            if (string.IsNullOrWhiteSpace(speaker))
                return speaker;

            string trimmed = speaker.Trim();
            string nick = GetPlayerNickname();

            if (string.Equals(trimmed, DefaultProtagonistName, StringComparison.Ordinal))
                return nick;
            if (string.Equals(trimmed, SmileProtagonistName, StringComparison.Ordinal))
                return nick;
            if (string.Equals(trimmed, MirrorProtagonistName, StringComparison.Ordinal))
                return Loc.F("镜像{0}", "Mirror {0}", nick);

            return speaker;
        }

        public static string PersonalizeStoryText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            string nick = GetPlayerNickname();
            if (string.Equals(nick, DefaultProtagonistName, StringComparison.Ordinal))
                return text;

            text = text.Replace(MirrorProtagonistName, Loc.F("镜像{0}", "Mirror {0}", nick));
            text = text.Replace(SmileProtagonistName, Loc.F("{0}笑笑", "{0} (Smile)", nick));
            return text.Replace(DefaultProtagonistName, nick);
        }
    }
}
