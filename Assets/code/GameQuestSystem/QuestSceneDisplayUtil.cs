using UnityEngine;

namespace GameQuestSystem
{
    /// <summary>将任务 <see cref="QuestSubTask.targetSceneName"/> 等内部场景名格式化为玩家可读文案。</summary>
    public static class QuestSceneDisplayUtil
    {
        public static string FormatSceneDisplayName(string sceneRef)
        {
            string key = SceneTeleportHelper.NormalizeSceneName(sceneRef);
            if (string.IsNullOrEmpty(key))
                return Loc.Choose("未指定", "Unspecified");

            switch (key.ToLowerInvariant())
            {
                case "undergarden":
                    return Loc.Choose("下园", "Lower Garden");
                case "uppergarden":
                    return Loc.Choose("上园", "Upper Garden");
                case "middlegarden":
                    return Loc.Choose("中园", "Middle Garden");
                case "lingcollegea":
                    return Loc.Choose("令学院 A 区", "Ling College Block A");
                case "lingcollegeb":
                    return Loc.Choose("令学院 B 区", "Ling College Block B");
                case "lingcollegec":
                    return Loc.Choose("令学院 C 区", "Ling College Block C");
                case "libraryfloor1":
                    return Loc.Choose("图书馆一层", "Library Floor 1");
                case "libraryfloor2":
                    return Loc.Choose("图书馆二层", "Library Floor 2");
                case "libraryfloor4":
                    return Loc.Choose("图书馆四层", "Library Floor 4");
                case "learningroom":
                case "learning room":
                    return Loc.Choose("研讨室", "Seminar Room");
                case "teachingrooma":
                    return Loc.Choose("教学楼 A", "Teaching Building A");
                case "classroom1":
                    return Loc.Choose("教室", "Classroom");
                case "computerroom":
                    return Loc.Choose("机房", "Computer Room");
                case "dorm":
                    return Loc.Choose("寝室", "Dorm");
                case "shawa":
                    return Loc.Choose("邵逸夫楼", "Shaw Building");
                case "cec":
                    return Loc.Choose("CEC", "CEC");
                default:
                    return HumanizeSceneKey(key);
            }
        }

        public static string FormatQuestSceneLine(string sceneRef, bool inCurrentScene)
        {
            string display = FormatSceneDisplayName(sceneRef);
            if (string.IsNullOrEmpty(SceneTeleportHelper.NormalizeSceneName(sceneRef)))
                return Loc.Choose("任务场景：--", "Quest scene: --");

            if (inCurrentScene)
                return Loc.F("任务场景：{0}（当前）", "Quest scene: {0} (here)", display);

            return Loc.F("任务场景：{0}", "Quest scene: {0}", display);
        }

        static string HumanizeSceneKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return Loc.Choose("未知", "Unknown");

            string readable = key.Replace('_', ' ').Replace('-', ' ');
            if (!Loc.IsEnglish)
                return readable;

            if (readable.Length == 0)
                return readable;

            return char.ToUpperInvariant(readable[0]) + readable.Substring(1);
        }
    }
}
