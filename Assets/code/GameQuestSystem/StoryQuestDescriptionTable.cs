using System;
using System.Collections.Generic;

namespace GameQuestSystem
{
    /// <summary>剧情根任务（documentId）的简要/详细任务栏文案。由策划逐条撰写，非模板批量生成。</summary>
    public static class StoryQuestDescriptionTable
    {
        public readonly struct Entry
        {
            public readonly string ShortZh;
            public readonly string ShortEn;
            public readonly string LongZh;
            public readonly string LongEn;

            public Entry(string shortZh, string shortEn, string longZh, string longEn)
            {
                ShortZh = shortZh;
                ShortEn = shortEn;
                LongZh = longZh;
                LongEn = longEn;
            }

            public string Short => Loc.IsEnglish ? ShortEn : ShortZh;
            public string Long => Loc.IsEnglish ? LongEn : LongZh;
        }

        public readonly struct ChapterEntry
        {
            public readonly string ShortZh;
            public readonly string ShortEn;
            public readonly string LongZh;
            public readonly string LongEn;

            public ChapterEntry(string shortZh, string shortEn, string longZh, string longEn)
            {
                ShortZh = shortZh;
                ShortEn = shortEn;
                LongZh = longZh;
                LongEn = longEn;
            }

            public string Short => Loc.IsEnglish ? ShortEn : ShortZh;
            public string Long => Loc.IsEnglish ? LongEn : LongZh;
        }

        static readonly Dictionary<string, Entry> ByDocumentId =
            new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase)
            {
                // —— M0 序章 ——
                ["M0-1-1"] = E(
                    "在下园校门，听迎新学长讲完这座校园的第一课。",
                    "At the Lower Garden gate, hear the orientation senior's first lesson about this campus.",
                    "深圳八月的蝉鸣比想象中更尖锐——咸湿海风裹着柏油路被晒化的苦味，「未名之海」的第一印象便如此落笔。\n前往下园校门，与迎新学长对话，读完序章开篇。",
                    "August cicadas in Shenzhen cut sharper than expected—salt wind and sun-softened asphalt, your first impression of The Nameless Sea.\nGo to the Lower Garden gate and speak with the orientation senior to begin the prologue."),
                ["M0-1-3"] = E(
                    "沿上园的路去宿舍，把膝盖和行李都照顾好。",
                    "Take the Upper Garden road to the dorm—mind your knees and your luggage.",
                    "「挺有精神。上园的路可不好走，记得保护好膝盖。」\n穿过宿舍区，在夜色或日光里找到属于你的那张床，完成抵达。",
                    "\"You've still got spirit. The Upper Garden road is rough—watch your knees.\"\nCross the dorm district until your bed feels real."),
                ["M0-2-1"] = E(
                    "在图书馆公共阅读区，与沈青禾相遇。",
                    "Meet Shen Qinghe in the library's public reading area.",
                    "龙翔长廊的冷气与潮热交替，林渡攥着手机，像攥着一根快要断掉的线。\n前往下园图书馆阅读区，与沈青禾对话，揭开序章里第一处伏笔。",
                    "Between AC chill and humid heat on Longxiang Walk, you grip your phone like a fraying lifeline.\nGo to the library reading area in Lower Garden and speak with Shen Qinghe."),
                ["M0-3-1"] = E(
                    "凌晨的上园宿舍区，读完那一行系统提示。",
                    "In the Upper Garden dorms past midnight, read the line that changes everything.",
                    "零点二十四分，大部分窗户已经熄灯，只剩预习与跨时区网课漏出的冷白。\n在上园宿舍区触发剧情，读完屏幕顶端浮现的那一行字。",
                    "00:24. Most windows are dark; only revision lamps and cross-timezone lectures leak cold light.\nTrigger the story in Upper Garden and read the single line atop your screen."),

                // —— M1 第一章 ——
                ["M1-1-1"] = E(
                    "机房选课系统前，在倒计时里为学分搏一次。",
                    "At the computer-room enrollment terminal, fight for credits as the timer runs down.",
                    "九月初，学勤书院宿舍里四台笔记本风扇同时轰鸣——教务系统的倒计时跳成 00:00。\n前往机房选课系统，与沈青禾对话，亲历那场抢课风暴。",
                    "Early September: four laptop fans scream in Xueqin dorm as the enrollment timer hits 00:00.\nGo to the computer room and speak with Shen Qinghe to live through the registration storm."),
                ["M1-1-3"] = E(
                    "抢课落幕，听室友们在余震里各自崩溃或庆幸。",
                    "After enrollment ends, hear roommates collapse or exhale in the aftershock.",
                    "风扇终于平息。苏沐瘫在椅子里，赵梓璇疯狂加群，梁嘉一已经对照新课表下单教材。\n完成本节剧情，看这一夜的输赢如何落在每个人肩上。",
                    "Fans finally quiet. Sumu slumps, Zhao Zixuan floods group chats, Liang Jiayi orders textbooks from a fresh timetable.\nFinish this scene and watch how victory and defeat settle on each shoulder."),
                ["M1-2-1"] = E(
                    "研讨楼 402，在白炽灯的嗡鸣里交一份小组作业。",
                    "In Seminar Building 402, submit a group paper under humming fluorescent lights.",
                    "傍晚六点半，龙岗夜色吞没天边最后一抹余晖。桌上是论文草稿与空掉的能量饮料，白炽灯管发出极细的嗡鸣。\n前往研讨楼 402 讨论室，与赵梓璇对话，完成小组线开篇。",
                    "Six-thirty PM: Longgang night swallows the last glow. Drafts and empty energy drinks cover the table under humming fluorescents.\nGo to Seminar Room 402 and speak with Zhao Zixuan to open the group-project arc."),
                ["M1-2-3"] = E(
                    "走廊那头在欢呼，你们这一组却沉默着。",
                    "Cheers erupt down the hall while your group stays silent.",
                    "别的组在讨论去哪里庆祝满分，空气里只有你们这边压低的呼吸。\n完成本节剧情，感受分数之外更锋利的落差。",
                    "Other groups plan celebrations; only your side breathes lower.\nComplete this scene and feel the sharper gap beneath the score."),
                ["M1-3-1"] = E(
                    "凌晨一点十五分，旧图书馆的桌面白得晃眼。",
                    "At 1:15 a.m., the old library desk glare is almost blinding.",
                    "白炽灯把桌面照得过分干净，像一张等待签名的空白契约。\n前往图书馆一楼，与沈青禾对话，进入深夜自习线。",
                    "Fluorescent light bleaches the desk clean, like a contract waiting for a signature.\nGo to Library Floor 1 and speak with Shen Qinghe."),
                ["M1-3-3"] = E(
                    "凌晨四点，沿凤凰木小道走回宿舍。",
                    "At four in the morning, walk the flamboyant-tree path back to the dorm.",
                    "图书馆外的凤凰木在微风里摇，路灯把林渡的影子拉得很长，长得像是另一个人。\n沿图书馆外小道完成剧情，把这一夜折进记忆里。",
                    "Flamboyant trees sway; streetlamps stretch your shadow into someone else.\nFinish the scene along the path outside the library."),
                ["M1-4-1"] = E(
                    "浓雾校巴站，遇见均分幻影。",
                    "At the fog-choked bus stop, meet the GPA phantom.",
                    "凌晨四点，雾气浓得化不开。路灯晕成深海里垂死的鱼眼，均分像幽灵般等在回宿舍的下坡路上。\n前往校巴站一带，与均分幻影对话，面对那场关于「平均值」的噩梦。",
                    "Four a.m. Fog so thick streetlamps look like dying fish eyes in deep water.\nGo to the bus stop area and speak with the GPA phantom."),
                ["M1-4-3"] = E(
                    "幻象散去，宿舍门口的第一缕晨光。",
                    "As illusions fade, the first morning light at the dorm door.",
                    "当最后一丝幻象消失，林渡发现自己站在宿舍门口。阳光穿透晨雾，照在龙翔大道的石碑上。\n完成本节，从梦魇里走回可触摸的清晨。",
                    "When the last illusion vanishes, you stand at the dorm entrance. Sunlight pierces the mist onto the Longxiang stone.\nComplete this scene and step from nightmare into touchable morning."),
                ["M1-5-1"] = E(
                    "学勤书院宿舍，周砚带来第一章的收束。",
                    "In Xueqin dorm, Zhou Yan closes the first chapter.",
                    "上午十点，书院宿舍的空气仍留着昨夜未散的紧张。\n与周砚对话，读完 M1 最后一节，看关系与选择如何同时收紧。",
                    "Ten a.m. The dorm still holds yesterday's tension.\nSpeak with Zhou Yan to close Chapter M1 and feel choices tighten."),

                // —— M2 第二章 ——
                ["M2-0-1"] = E(
                    "图书馆电脑区深夜，只剩屏幕与沈青禾。",
                    "Late night at the library PC zone—only screens and Shen Qinghe remain.",
                    "二十三点二十分，电脑区旁早已无人，键盘声却还在黑暗里回响。\n前往图书馆一楼电脑区，与沈青禾对话，开启第二章序曲。",
                    "23:20. The PC area is empty except for keyboard echoes in the dark.\nGo to Library Floor 1 PC zone and speak with Shen Qinghe."),
                ["M2-1-1"] = E(
                    "暴雨屋檐下的周砚，像一把收不住的刃。",
                    "Zhou Yan under the eaves in a downpour—blade sheathed too late.",
                    "暴雨如注，泥土气潮闷。她刚经历一场商业博弈，虽胜犹疲——深灰西装挺括，却透着不合时宜的孤。\n与周砚对话，进入第二章核心冲突。",
                    "Rain hammers down. She has just won a business gamble, victory leaving her exhausted—charcoal suit straight, loneliness out of place.\nSpeak with Zhou Yan to enter the chapter's core conflict."),
                ["M2-1-3"] = E(
                    "问她：守规矩的高分生，还是搅局的野心家？",
                    "Ask her: the rule-bound top student, or the troublemaker who bends them?",
                    "「你觉得在这座学校里，是守规矩的高分学生更有前途，还是像我们这种搅局者走得更远？」\n完成本节对话，你的答案会成为之后每一次选择的背景音。",
                    "\"On this campus, who goes further—the obedient high scorer or troublemakers like us?\"\nYour answer will echo behind every choice that follows."),
                ["M2-2-1"] = E(
                    "去图书馆的路上，听周砚讲起她的家。",
                    "On the way to the library, Zhou Yan speaks of her family.",
                    "路程过半，她突然聊起家庭——语气平静，像在说别人的天气。\n在图书馆一楼完成对话，把信任与试探再走深一步。",
                    "Halfway there, she speaks of family—calm, as if describing someone else's weather.\nFinish the scene on Library Floor 1 and deepen trust and testing."),
                ["M2-3-1"] = E(
                    "图书馆四楼，女生宿舍七号楼门前的夜谈。",
                    "Library Floor 4—a night talk at Dorm Building 7's gate.",
                    "楼门前的风比图书馆更冷，路灯把两个人的影子叠在一起又分开。剧情落在女生宿舍七号楼门前。\n前往图书馆四楼，与周砚完成本节对话。",
                    "Wind at the dorm gate is colder than the library; two shadows overlap, then split. The scene unfolds at Dorm Building 7's entrance.\nGo to Library Floor 4 and complete this scene with Zhou Yan."),
                ["M2-3-3"] = E(
                    "第一个和她谈「逻辑纯粹性」的人。",
                    "The first person she discusses \"logical purity\" with.",
                    "「在这个学校里，你是第一个坐在我对面，跟我谈逻辑纯粹性的人。」\n读完本节，明白这句话既是赞许，也是枷锁。",
                    "\"In this school, you're the first to sit across from me and talk about logical purity.\"\nFinish this scene—praise and shackle in the same sentence."),
                ["M2-4-1"] = E(
                    "冬日中环，肉桂咖啡与蛋挞香气里的周砚。",
                    "Mid-winter Central: Zhou Yan amid cinnamon coffee and egg-tart scent.",
                    "冬日午后的阳光收起燥热，带着清透的冷意。中环街头挂满新年灯饰，空气里飘着肉桂咖啡与蛋挞的香气。\n与周砚对话，进入跨城线。",
                    "Winter afternoon light loses its heat, turning crisp. New Year lights line Central; cinnamon coffee and egg tarts scent the air.\nSpeak with Zhou Yan to open the cross-city arc."),
                ["M2-4-3"] = E(
                    "午夜前避开人群，登上那艘快艇。",
                    "Before midnight, slip past the crowd onto that speedboat.",
                    "两人奇迹般在午夜前避开人潮，登上林渡提前预约的快艇。\n完成本节，海面的风会吹散一部分答案，也会带来新的问题。",
                    "You somehow reach the booked speedboat before midnight, past the crowds.\nFinish the scene—sea wind scatters some answers and raises new ones."),
                ["M2-5-1"] = E(
                    "跨境大巴上，深港大桥的车灯像二进制。",
                    "On the cross-border bus, Shenzhen–HK Bridge lights like binary code.",
                    "中环的喧嚣在凌晨两点的巴士上远去。林渡靠在车窗边，深港大桥上绵延的车灯像一串永不熄灭的二进制。\n沿图书馆外小道完成剧情，从异城回到校园。",
                    "Central's noise fades on the 2 a.m. bus. You lean against the window; bridge tail-lights stream like endless binary.\nFinish the scene along the library path—return from elsewhere."),
                ["M2-5-2"] = E(
                    "餐厅露台，傍晚六点半的晚餐与试探。",
                    "Restaurant terrace: dinner and testing at six-thirty.",
                    "露台的灯刚亮起，城市在脚下铺开一层温柔却疏远的金色。\n与周砚完成本节，把第二章收在一句未说尽的话里。",
                    "Terrace lights ignite; the city spreads gentle, distant gold below.\nClose Chapter M2 with Zhou Yan—a sentence left unfinished."),

                // —— M3 第三章 ——
                ["M3-1-1"] = E(
                    "深夜宿舍：塑料袋声、短视频与外放争吵。",
                    "Late-night dorm: plastic bags, short videos, and arguments.",
                    "二十三点三十分。梁嘉一烦躁翻身，赵梓璇刷短视频，苏沐推门，塑料袋声很大。\n与梁嘉一对话，处理寝室里第一次真正的摩擦。",
                    "23:30. Liang Jiayi tosses, Zhao Zixuan scrolls, Sumu enters with a loud plastic bag.\nSpeak with Liang Jiayi and face the dorm's first real friction."),
                ["M3-2-1"] = E(
                    "社团教室深夜，林见鹿的消息很短。",
                    "Club classroom at night—a very short message from Lin Jianlu.",
                    "二十三点四十五分，社团预订的教室只剩消防指示灯的幽绿微光。\n与林见鹿对话，进入社团线。",
                    "23:45. Only emergency exit glow remains in the booked club room.\nSpeak with Lin Jianlu to open the club arc."),
                ["M3-2-1R"] = E(
                    "「研讨室。现在。」——林见鹿的深夜邀约。",
                    "\"Seminar room. Now.\"—Lin Jianlu's midnight summons.",
                    "社团教室的门合上，走廊只剩幽绿微光。手机震了一下：研讨室，现在。\n前往 TXC 行政楼研讨室，与林见鹿对话。",
                    "The club door shuts; the hall glows green. One message: seminar room, now.\nGo to the TXC seminar room and speak with Lin Jianlu."),
                ["M3-3-1"] = E(
                    "卡园热帖引战，寝室只剩显示器荧光。",
                    "An inflammatory Card Garden post; only monitor glow in the dorm.",
                    "深夜，一条标题刺眼的帖子被顶到首页——《扒一下某社团账目造假的内幕，顺便聊聊某些人的上位手段》。\n与赵承勋对话，决定你在舆论里站哪一边。",
                    "Past midnight, a provocative post hits the homepage—insinuations of forged accounts and ambition.\nSpeak with Zhao Chengxun and choose a side in the noise."),
                ["M3-4-1"] = E(
                    "三人同时求助：科研、实习与公益，你只能选一条路。",
                    "Three friends need help at once: research, internship, charity—you can only pick one.",
                    "二十三点，书院休息室窗外暴雨。林渡刚赶完 ddl，入睡前看见三条短信同时亮起——沈青禾的科研名额、周砚的实习、顾澄的公益。\n与沈青禾对话，做出本章最沉重的选择。",
                    "23:00, rain on the lounge windows. You finish a deadline and find three texts at once—Shen Qinghe's lab slot, Zhou Yan's internship, Gu Cheng's charity.\nSpeak with Shen Qinghe and make this chapter's heaviest choice."),
                ["M3-4-3"] = E(
                    "关键时刻的缺席，让信任慢慢变薄。",
                    "Absence at the crucial hour thins trust.",
                    "林渡没有到场，另外两个项目石沉大海。并非全是你的错，但「坐视不管」会在聊天框里留下长久的冷场。\n完成本节，承受关系里第一道无法轻易修补的裂缝。",
                    "You weren't there; two projects sank. Not entirely your fault—but standing by leaves a long cold silence in the chat.\nFinish this scene and bear the first crack that won't patch easily."),

                // —— M4 第四章 ——
                ["M4-1-1"] = E(
                    "行政楼研讨室，楚天谈那项不能失败的项目。",
                    "Admin building seminar room—Chu Tian on the project that cannot fail.",
                    "下午两点，304 研讨室。白板上的流程图密密麻麻，像一张正在收紧的网。\n与楚天对话，进入第四章主线。",
                    "14:00, Room 304. Flowcharts on the whiteboard tighten like a net.\nSpeak with Chu Tian to open Chapter M4."),
                ["M4-1-2"] = E(
                    "深夜，群里孙哲的长消息打破代码测试的寂静。",
                    "Past midnight, Sun Zhe's long group message breaks the silence of your code test.",
                    "第二天深夜，林渡还在跑最后一段测试，屏幕突然亮起——群里弹出孙哲的一条长消息。\n与楚天继续剧情，看团队裂痕如何公开化。",
                    "The next night you're running the last test when the screen flares—a long message from Sun Zhe in the group chat.\nContinue with Chu Tian as the team's fracture goes public."),
                ["M4-2-1"] = E(
                    "人工智能系办公楼，陈教授的门半掩。",
                    "AI department building—Professor Chen's door left ajar.",
                    "下午三点半，办公楼走廊的空调声像某种耐心的叹息。\n与陈教授对话，把学术线推向更尖锐的质问。",
                    "15:30. AC hum in the corridor like patient sighing.\nSpeak with Professor Chen and sharpen the academic thread."),
                ["M4-2-2"] = E(
                    "院办档案室门外，听见陆明与老师的争执。",
                    "Outside the archives office, overhear Lu Ming arguing with faculty.",
                    "门半掩着。林渡拿着申请表走近，先听见的是同门陆明的声音。\n与王老师对话，揭开档案室里藏着的分歧。",
                    "The door is half open. You approach with a form and hear Lu Ming first.\nSpeak with Teacher Wang and uncover the dispute in the archives."),
                ["M4-3-1"] = E(
                    "图书馆五楼露台，傍晚的风与楚天的野心。",
                    "Library Floor 5 terrace—evening wind and Chu Tian's ambition.",
                    "傍晚的露台把城市切成两层：脚下是喧闹，头顶是渐暗的天。\n前往图书馆五楼露台，与楚天对话，在风口里谈下一步赌注。",
                    "The terrace splits the city: noise below, darkening sky above.\nGo to Library Floor 5 terrace and speak with Chu Tian about the next wager in the wind."),
                ["M4-3-3"] = E(
                    "在意向书上签下你的名字。",
                    "Sign your name on the letter of intent.",
                    "笔尖落下的一刻，其它路径像被同时关上。\n完成本节，确认你已无法假装尚未选择。",
                    "The moment the pen lands, other paths seem to close together.\nFinish this scene—you can no longer pretend you haven't chosen."),
                ["M4-4-1"] = E(
                    "答辩刚结束，导师面谈通知已到。",
                    "Right after the defense, the advisor meeting notice arrives.",
                    "上午的答辩结束，手机震了一下——导师面谈。\n触发剧情，走向第四章最冷的会议室。",
                    "Defense done. One vibration—advisor meeting.\nTrigger the story toward the chapter's coldest room."),
                ["M4-4-3"] = E(
                    "苏然的来电：社会学系合作伙伴的深夜求助。",
                    "Su Ran calls: a late-night plea from a sociology collaborator.",
                    "傍晚刚想喘口气，手机疯狂震动。苏然平时只在公选课上有交集，此刻语气却急促。\n与苏然对话，把个人危机接进更大的风暴。",
                    "You finally exhale when the phone erupts. Su Ran, usually an elective-class acquaintance, sounds urgent.\nSpeak with Su Ran as a private crisis joins a larger storm."),
                ["M4-4-5"] = E(
                    "操场深夜，决赛灯灭之后。",
                    "School track, deep night—after the final lights go out.",
                    "决赛日的灯光熄灭。林渡坐在看台上，屏幕微光映着疲惫的脸——这一天，她赢得了某些东西，也永远地失去了另一些。\n前往操场，与沈青禾完成本节终幕。",
                    "Final-day lights die. You sit in the stands, phone glow on a tired face—something won, something lost forever.\nGo to the track and finish this chapter's closing scene with Shen Qinghe."),

                // —— M5 终章 ——
                ["M5-1-1"] = E(
                    "图书馆顶层露台，学位帽像断翅的鸟。",
                    "Library rooftop terrace—mortarboards like broken-winged birds.",
                    "毕业季的夕阳将龙翔大道染成干涸的血色。林渡站在图书馆最高层露台，听着楼下欢呼与快门——黑金学位服在草坪上奔跑，方帽被抛起，又如断翅的鸟。\n与镜像林渡对话，进入终章。",
                    "Graduation sunset stains Longxiang Avenue like dried blood. On the library's top terrace you hear cheers and shutters below—black-gold gowns run across the lawn, caps thrown skyward like broken-winged birds.\nSpeak with Mirror Lindu to enter the finale."),
                ["M5-2-1"] = E(
                    "便利店角落，冷掉的黑咖啡与周砚。",
                    "Corner convenience store—cold black coffee and Zhou Yan.",
                    "白炽灯嗡鸣刺耳。林渡坐在最暗的角落，咖啡面上映着血丝的眼睛。\n与周砚对话，在毕业前的最后一夜把话说清。",
                    "Harsh fluorescent hum. You sit in the darkest corner; coffee reflects bloodshot eyes.\nSpeak with Zhou Yan on the last clear night before graduation."),
                ["M5-3-1"] = E(
                    "行政楼顶层露台，风声像困兽低吼。",
                    "Admin building roof—wind like a caged beast.",
                    "雨停了，云仍低垂，灰得令人窒息。镜像林渡站在风口，像一面照见所有选择的镜子。\n完成本节，面对那个从未真正离开的你。",
                    "Rain stopped; clouds still low and suffocating. Mirror Lindu in the wind reflects every choice.\nFinish this scene and face the self that never left."),
                ["M5-4-1"] = E(
                    "第四声钟响，推开阁楼那道窄门。",
                    "At the fourth bell, push open the narrow attic door.",
                    "钟响不像金属，像深水里的铁门缓缓推开。林渡没有回头看不乱的行政楼，也没有看滚动的名单。\n与镜像对话，推开顶层通往阁楼的窄门，走向终局前的最后一跃。",
                    "The bell sounds less like metal than an iron door opening underwater. You don't look back at the chaos or the scrolling names.\nSpeak with the mirror, push open the narrow attic door, and take the final leap."),
                ["M5-5-1-A"] = E(
                    "图书馆边水池，黎明前最后的平静。",
                    "Library poolside—the last calm before dawn.",
                    "电子风暴在黎明前停息。预想中的清算没有到来，只剩庞大而肃穆的沉默。\n与周砚在图书馆边水池完成终章，为《未名之海》画上句点。",
                    "The electronic storm ceases before dawn. No violent reckoning—only vast, solemn silence.\nWith Zhou Yan by the library pool, close The Nameless Sea."),
            };

        static readonly Dictionary<string, ChapterEntry> ByChapterId =
            new Dictionary<string, ChapterEntry>(StringComparer.OrdinalIgnoreCase)
            {
                ["M0"] = C(
                    "踏入未名之海，从校门到宿舍的第一程。",
                    "Step into The Nameless Sea—from the gate to your first dorm night.",
                    "序章串联迎新、初遇与深夜系统提示。蝉鸣、龙翔长廊与零点二十四分的冷白屏幕，奠定整部作品的氛围与节奏。",
                    "The prologue links orientation, first encounters, and a midnight system message—cicadas, Longxiang Walk, and cold light at 00:24 set the tone."),
                ["M1"] = C(
                    "选课、小组与深夜，关系的第一道裂缝。",
                    "Enrollment, group work, and late nights—the first cracks in every bond.",
                    "第一章围绕教务倒计时、研讨楼 402 的评分与图书馆深夜展开。室友、朋友与「均分幻影」同时挤压一个人的选择。",
                    "Chapter M1 turns on enrollment timers, Seminar Room 402 scores, and library nights—the GPA phantom pressing every choice."),
                ["M2"] = C(
                    "周砚：规矩、野心，与跨城的信任试探。",
                    "Zhou Yan: rules, ambition, and trust tested across cities.",
                    "第二章以周砚为核心，从暴雨屋檐到中环快艇、跨境巴士，追问守规矩者与搅局者谁走得更远，也把情感推向更危险的平衡。",
                    "Centered on Zhou Yan—from rainy eaves to Central's harbor and cross-border buses—who goes further, the obedient or the disruptor?"),
                ["M3"] = C(
                    "寝室、社团与舆论：无法兼顾的求助。",
                    "Dorm, clubs, and public opinion—help you cannot give to everyone.",
                    "第三章把摩擦写进日常生活：深夜宿舍、社团教室与卡园引战帖，最终以三条同时亮起的求助短信，考验信任与缺席的代价。",
                    "Daily life turns sharp: late dorms, club rooms, forum storms, and three texts you cannot answer all at once."),
                ["M4"] = C(
                    "项目、档案与意向书：选择的重量落地。",
                    "Projects, archives, and letters of intent—choices finally land.",
                    "第四章收束团队线、学术线与个人前途。研讨室的白板、档案室半掩的门、意向书上的签名，把故事推向不可逆的节点。",
                    "Team arcs, academic pressure, and personal fate converge—whiteboards, half-open archive doors, signatures you cannot unsign."),
                ["M5"] = C(
                    "毕业、镜像与告别：未名之海的句点。",
                    "Graduation, mirrors, and farewell—the period at the end.",
                    "终章从图书馆顶层露台写到边水池的黎明。与镜像中的自己、与周砚，完成这场关于选择与身份的最后一次对视。",
                    "From the library rooftop to dawn by the pool—final confrontations with your mirror and with Zhou Yan."),
            };

        static Entry E(string shortZh, string shortEn, string longZh, string longEn) =>
            new Entry(shortZh, shortEn, longZh, longEn);

        static ChapterEntry C(string shortZh, string shortEn, string longZh, string longEn) =>
            new ChapterEntry(shortZh, shortEn, longZh, longEn);

        public static bool TryGetSubTask(string documentId, out Entry entry) =>
            ByDocumentId.TryGetValue(documentId ?? "", out entry);

        public static bool TryGetChapter(string chapterId, out ChapterEntry entry) =>
            ByChapterId.TryGetValue(chapterId ?? "", out entry);

        public static string GetSubTaskShort(string documentId, string fallback) =>
            TryGetSubTask(documentId, out var e) ? e.Short : fallback;

        public static string GetSubTaskLong(string documentId, string fallback) =>
            TryGetSubTask(documentId, out var e) ? e.Long : fallback;

        public static string GetSubTaskShortZh(string documentId, string fallback) =>
            TryGetSubTask(documentId, out var e) ? e.ShortZh : fallback;

        public static string GetSubTaskLongZh(string documentId, string fallback) =>
            TryGetSubTask(documentId, out var e) ? e.LongZh : fallback;

        public static string GetChapterShort(string chapterId, string fallback) =>
            TryGetChapter(chapterId, out var e) ? e.Short : fallback;

        public static string GetChapterLong(string chapterId, string fallback) =>
            TryGetChapter(chapterId, out var e) ? e.Long : fallback;

        public static string GetChapterShortZh(string chapterId, string fallback) =>
            TryGetChapter(chapterId, out var e) ? e.ShortZh : fallback;

        public static string GetChapterLongZh(string chapterId, string fallback) =>
            TryGetChapter(chapterId, out var e) ? e.LongZh : fallback;
    }
}
