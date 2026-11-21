using System;
using System.Collections.Generic;

/// <summary>
/// Runtime Chinese → English lookup for scene-serialized labels, interaction prompts, shop data, etc.
/// </summary>
public static class LocTerms
{
    static readonly Dictionary<string, string> Exact = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // Interaction actions
        ["对话"] = "Talk",
        ["交互"] = "Interact",
        ["进入"] = "Enter",
        ["前往"] = "Go to",
        ["离开"] = "Leave",
        ["继续"] = "Continue",
        ["购买食物"] = "Buy food",
        ["购买"] = "Buy",
        ["选车"] = "Select vehicle",
        ["上车"] = "Enter vehicle",
        ["乘坐电梯"] = "Take elevator",
        ["查看致谢"] = "View credits",
        ["制作人员鸣谢"] = "Credits",
        ["交互点"] = "Interaction point",
        ["公交站牌"] = "Bus stop sign",
        ["公交站"] = "Bus stop",
        ["查看"] = "View",

        // NPC roles / names
        ["服务员"] = "Attendant",
        ["NPC"] = "NPC",
        ["旁白"] = "Narrator",
        ["林渡"] = "Lin Du",
        ["迎新学长"] = "Orientation Senior",
        ["报到老师"] = "Registration Staff",
        ["宿管阿姨"] = "Dorm manager",
        ["沈青禾"] = "Shen Qinghe",
        ["周砚"] = "Zhou Yan",
        ["顾澄"] = "Gu Cheng",
        ["苏沐"] = "Su Mu",
        ["赵梓璇"] = "Zhao Zixuan",
        ["梁嘉一"] = "Liang Jiayi",
        ["林见鹿"] = "Lin Jianlu",
        ["夏林"] = "Xia Lin",
        ["张教授"] = "Professor Zhang",
        ["陈教授"] = "Professor Chen",
        ["王老师"] = "Teacher Wang",
        ["王教授"] = "Professor Wang",
        ["赵承勋"] = "Zhao Chengxun",
        ["苏然"] = "Su Ran",
        ["楚天"] = "Chu Tian",
        ["教授"] = "Professor",
        ["系统"] = "System",
        ["系统提示"] = "System notice",
        ["【系统提示"] = "[System notice]",
        ["【序章"] = "[Prologue]",
        ["【第一章"] = "[Chapter One]",
        ["均分幻影"] = "GPA phantom",
        ["镜像"] = "Mirror",
        ["镜像林渡"] = "Mirror Lin Du",
        ["(第二天)周砚"] = "Zhou Yan (Day 2)",
        ["(第二天)沈青禾"] = "Shen Qinghe (Day 2)",
        ["剧情触发点"] = "Story trigger",
        ["载具商人"] = "Vehicle dealer",
        ["长椅"] = "Bench",

        // Story choice labels
        ["礼貌回应"] = "Respond politely",
        ["冷淡回应"] = "Respond coolly",
        ["追求逻辑效率"] = "Pursue logical efficiency",
        ["寻找人脉捷径"] = "Seek connections",
        ["抬头发现风景"] = "Look up and take in the view",
        ["追求绩点安全性"] = "Prioritize GPA security",
        ["追求未知可能性"] = "Pursue the unknown",
        ["默许捷径"] = "Allow the shortcut",
        ["坚守原则"] = "Stick to your principles",
        ["选择求助"] = "Ask for help",
        ["选择硬抗"] = "Push through alone",
        ["我就是这套系统的胜者"] = "I am the winner of this system",
        ["我只为我自己的好奇心负责"] = "I answer only to my curiosity",
        ["我会打破这面镜子"] = "I will break this mirror",

        // Locations & buildings
        ["校门"] = "Campus gate",
        ["校园大门"] = "Campus main gate",
        ["图书馆"] = "Library",
        ["图书馆公共阅读区"] = "Library reading area",
        ["上园宿舍区"] = "Upper Garden dorm",
        ["宿舍区"] = "Dorm area",
        ["寝室"] = "Dorm",
        ["龙翔大道"] = "Longxiang Avenue",
        ["行政楼"] = "Administration building",
        ["体育馆"] = "Gymnasium",
        ["操场"] = "Playground",
        ["机房"] = "Computer lab",
        ["研讨室"] = "Seminar room",
        ["教室"] = "Classroom",
        ["便利店"] = "Convenience store",
        ["花坛中心"] = "Flower bed",
        ["国旗旗杆下"] = "Flagpole",
        ["目标地点"] = "Destination",
        ["前往厚含书院"] = "To Houhan College",
        ["厚含书院"] = "Houhan College",
        ["道扬书院B栋"] = "Daoyang College B",
        ["道扬书院"] = "Daoyang College",
        ["第八书院A栋"] = "Eighth College A",
        ["第八书院B栋"] = "Eighth College B",
        ["前往上园"] = "To Upper Garden",
        ["前往中园"] = "To Middle Garden",
        ["前往下园"] = "To Lower Garden",
        ["上园"] = "Upper Garden",
        ["前往寝室"] = "To dorm",
        ["前往TD"] = "To TD",
        ["下园"] = "Lower Garden",
        ["中园"] = "Middle Garden",
        ["逸夫书院"] = "Shaw College",
        ["前往逸夫书院"] = "To Shaw College",
        ["CEC教室"] = "CEC Classroom",
        ["前往CEC教室"] = "To CEC Classroom",
        ["TD教室"] = "TD Classroom",
        ["前往TD教室"] = "To TD Classroom",
        ["TC教室"] = "TC Classroom",
        ["前往TC教室"] = "To TC Classroom",
        ["图书馆一楼"] = "Library Floor 1",
        ["图书馆二楼"] = "Library Floor 2",
        ["前往图书馆一楼"] = "To Library Floor 1",
        ["前往图书馆二楼"] = "To Library Floor 2",
        ["电脑机房"] = "Computer Lab",
        ["前往电脑机房"] = "To Computer Lab",
        ["道扬A栋"] = "Daoyang Block A",
        ["道扬B栋"] = "Daoyang Block B",
        ["道扬C栋"] = "Daoyang Block C",
        ["思廷C栋"] = "Siting Block C",
        ["厚含A栋"] = "Houhan Block A",
        ["学勤C栋"] = "Xueqin Block C",
        ["前往道扬A栋"] = "To Daoyang Block A",
        ["前往道扬B栋"] = "To Daoyang Block B",
        ["前往道扬C栋"] = "To Daoyang Block C",
        ["前往思廷C栋"] = "To Siting Block C",
        ["前往厚含A栋"] = "To Houhan Block A",
        ["前往学勤C栋"] = "To Xueqin Block C",
        ["前往Teaching A"] = "To Teaching Block A",
        ["Teaching A"] = "Teaching Block A",
        ["未名之海"] = "The Nameless Sea",

        // Quest / story labels
        ["主线剧情"] = "Main story",
        ["支线剧情"] = "Side story",

        // Shop categories
        ["分类"] = "Category",
        ["墙面装饰"] = "Wall decor",
        ["照明氛围"] = "Lighting",
        ["绿植摆件"] = "Plants",
        ["生活用品"] = "Daily items",
        ["食物"] = "Food",
        ["食物商城"] = "Food shop",
        ["寝室商城"] = "Dorm shop",

        // Food effects
        ["牛奶"] = "Milk",
        ["面包"] = "Bread",
        ["红苹果"] = "Red apple",
        ["香蕉"] = "Banana",
        ["海报"] = "Poster",

        // Character registration
        ["本科新生"] = "Undergraduate freshman",
        ["研一新生"] = "Graduate year 1",
        ["博一新生"] = "PhD year 1",
        ["女"] = "F",
        ["A"] = "A",
        ["B"] = "B",

        // Death / revive
        ["你已阵亡"] = "You have fallen",
        ["复活"] = "Revive",
        ["未知原因"] = "Unknown cause",
        ["可以复活了"] = "You may revive now",

        // Flag auction
        ["自定义"] = "Customize",
        ["竞拍"] = "Auction",
        ["出价"] = "Place bid",
        ["出价金额"] = "Bid amount",
        ["应用旗帜"] = "Apply flag",

        // Phone / common UI
        ["设置"] = "Settings",
        ["地图"] = "Map",
        ["任务"] = "Quests",
        ["商城"] = "Shop",
        ["背包"] = "Inventory",
        ["联机"] = "Co-op",
        ["账户"] = "Account",
        ["自定义按键"] = "Key bindings",
        ["登出"] = "Log out",
        ["关闭"] = "Close",
        ["返回"] = "Back",
        ["公告"] = "Notice",
    };

    public static string Translate(string zh)
    {
        if (string.IsNullOrWhiteSpace(zh) || !LocalizationService.IsEnglish)
            return zh;

        zh = zh.Trim();
        if (Exact.TryGetValue(zh, out string en))
            return en;

        if (zh.StartsWith("前往", StringComparison.Ordinal) && zh.Length > 2)
        {
            string rest = zh.Substring(2);
            if (Exact.TryGetValue(rest, out string restEn))
                return "To " + restEn;
            return "To " + rest;
        }

        if (zh.StartsWith("死因：", StringComparison.Ordinal))
            return "Cause: " + Translate(zh.Substring(3));

        if (zh.StartsWith("回血+", StringComparison.Ordinal))
            return "HP +" + zh.Substring(3);
        if (zh.StartsWith("体力+", StringComparison.Ordinal))
            return "Stamina +" + zh.Substring(3);
        if (zh.StartsWith("移速+", StringComparison.Ordinal))
            return "Speed +" + zh.Substring(3);

        return zh;
    }
}
