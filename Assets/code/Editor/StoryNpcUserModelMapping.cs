#if UNITY_EDITOR
using System.Collections.Generic;

/// <summary>用户提供的剧情角色 → GLB 资源路径（相对 Assets）。</summary>
public static class StoryNpcUserModelMapping
{
    public const string CharacterFolder = "Assets/Imports/Sketchfab/Characters";

    /// <summary>玩家模型 FBX（剧情中的林渡与主角一致）。</summary>
    public const string PlayerModelFbxPath = "Assets/character/character/自机角色女 裸模9.fbx";

    static readonly Dictionary<string, string> DisplayNameToGlb = new Dictionary<string, string>
    {
        { "张教授", CharacterFolder + "/npc_zhangjiaoshou.glb" },
        { "周砚", CharacterFolder + "/npc_zhouyan.glb" },
        { "沈青禾", CharacterFolder + "/npc_shenqinghe.glb" },
        { "林见鹿", CharacterFolder + "/npc_linjianlu.glb" },
        { "赵承勋", CharacterFolder + "/npc_zhaochengxun.glb" },
        { "顾澄", CharacterFolder + "/npc_gucheng.glb" },
        { "赵梓璇", CharacterFolder + "/npc_zhaozixuan.glb" },
        { "梁嘉一", CharacterFolder + "/npc_liangjiayi.glb" },
        { "楚天", CharacterFolder + "/npc_chutian.glb" },
        { "王老师", CharacterFolder + "/npc_wanglaoshi.glb" },
        { "苏然", CharacterFolder + "/npc_suran.glb" },
        { "苏沐", CharacterFolder + "/npc_sumu.glb" },
        { "宿管阿姨", CharacterFolder + "/npc_suguanayi.glb" },
        // 用户未单独提供：复用相近角色
        { "陈教授", CharacterFolder + "/npc_zhangjiaoshou.glb" },
        { "迎新学长", CharacterFolder + "/npc_zhaochengxun.glb" },
        { "夏林", CharacterFolder + "/npc_chutian.glb" },
    };

    static readonly Dictionary<string, string> AliasToCanonical = new Dictionary<string, string>
    {
        { "(第二天）周砚", "周砚" },
        { "（第二天）周砚", "周砚" },
        { "(第二天）沈青禾", "沈青禾" },
        { "（第二天）沈青禾", "沈青禾" },
        { "（若干天后）顾澄", "顾澄" },
    };

    public static bool TryResolveGlbPath(string displayName, out string glbPath)
    {
        glbPath = null;
        if (string.IsNullOrWhiteSpace(displayName))
            return false;

        if (AliasToCanonical.TryGetValue(displayName, out var canonical))
            displayName = canonical;

        if (UsesPlayerModel(displayName))
            return false;

        return DisplayNameToGlb.TryGetValue(displayName, out glbPath);
    }

    public static bool UsesPlayerModel(string displayName) =>
        displayName == "林渡" || displayName == "镜像林渡" || displayName == "镜像";

    public static bool ShouldUseGhostEffect(string displayName) =>
        displayName == "镜像林渡" || displayName == "镜像";
}
#endif
