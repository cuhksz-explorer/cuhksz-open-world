/// <summary>
/// 登录界面展示用《用户协议》《隐私政策》（测试版，便于排查联机、存档与玩法问题）。
/// </summary>
public static class LguHubLegalDocuments
{
    public static string UserAgreementTitle =>
        Loc.Choose("用户协议（测试版）", "Terms of Service (Test)");

    public static string UserAgreementBody =>
        Loc.Choose(
            "欢迎使用 LGU EXPLORER 提供的《未名之海》校园探索体验（以下简称「本游戏」）。本协议为封闭测试用途，请在进入游戏前仔细阅读。\n\n" +
            "一、服务说明\n" +
            "1. 本游戏为 Unity 实机演示版本，包含书院大厅、寝室、任务日志、商城布置、场景传送、第一/第三人称视角切换等功能。\n" +
            "2. 账号登录、注册、存档同步、好友与联机房间依赖 GameServer 终端服务（HTTP 与 Mirror TCP），测试期间可能出现维护、断线或数据回滚。\n" +
            "3. 剧情任务（含主线与支线）、NPC 对话、寝室商城与放置模式可能随版本更新调整，不保证与策划文档完全一致。\n\n" +
            "二、账号与安全\n" +
            "1. 请使用本人测试账号，勿将密码告知他人或在公共设备勾选「记住密码」。\n" +
            "2. 发现异常登录、存档丢失或联机不同步，请通过测试渠道反馈并保留发生时间、场景名与操作步骤。\n" +
            "3. 禁止利用漏洞刷取游戏币、重复提交任务、篡改客户端请求或干扰他人联机。\n\n" +
            "三、测试行为规范\n" +
            "1. 您同意在测试期间配合收集：崩溃日志、帧率异常、任务卡住、传送点错误、相机无法转动、UI 重叠等问题描述。\n" +
            "2. 实验性按键（如登录面板显隐、联机面板、任务面板）可能变更，以当前版本说明为准。\n" +
            "3. 本游戏内容（校园地图、人物对话、支线事件）仅供体验与问题复现，不得用于商业转载。\n\n" +
            "四、免责声明\n" +
            "1. 测试版本可能存在存档损坏、场景加载失败、联机延迟等风险，运营方将尽力修复但不承担因测试数据丢失导致的间接损失。\n" +
            "2. 因网络、设备性能或第三方服务导致的无法进入游戏，请先检查终端服务器连接与防火墙设置。\n\n" +
            "五、协议变更\n" +
            "协议更新后将通过登录界面或版本公告提示；继续登录即视为接受修订内容。\n\n" +
            "（本协议最后更新：测试构建，用于问题检测与内测合规提示。）",
            "Welcome to LGU EXPLORER's Sea of the Unknown campus exploration experience (\"the Game\"). This agreement is for closed testing; please read before entering.\n\n" +
            "1. Service\n" +
            "- Unity demo build with academy hall, dorm, quest journal, mall placement, scene teleport, 1st/3rd person camera.\n" +
            "- Login, saves, friends, and co-op depend on GameServer (HTTP + Mirror TCP); maintenance, disconnects, or rollbacks may occur.\n" +
            "- Quests, NPC dialogue, dorm mall, and placement may change between builds.\n\n" +
            "2. Account & Security\n" +
            "- Use your own test account; do not share passwords or stay logged in on public devices.\n" +
            "- Report abnormal logins, lost saves, or desync with time, scene, and steps.\n" +
            "- No exploiting bugs, duplicate quest submissions, tampering with requests, or disrupting others' sessions.\n\n" +
            "3. Test Conduct\n" +
            "- You agree to help report crashes, FPS issues, stuck quests, bad teleports, camera/UI problems.\n" +
            "- Experimental hotkeys may change per build.\n" +
            "- Game content is for testing only; no commercial redistribution.\n\n" +
            "4. Disclaimer\n" +
            "- Test builds may corrupt saves, fail to load scenes, or lag in co-op; we will try to fix issues but are not liable for indirect loss of test data.\n" +
            "- Connection failures: check server reachability and firewall first.\n\n" +
            "5. Changes\n" +
            "- Updates will be announced on login or in patch notes; continuing to log in means you accept revisions.\n\n" +
            "(Last updated: test build for QA and compliance notice.)");

    public static string PrivacyPolicyTitle =>
        Loc.Choose("隐私政策（测试版）", "Privacy Policy (Test)");

    public static string PrivacyPolicyBody =>
        Loc.Choose(
            "LGU EXPLORER 重视测试参与者隐私。本政策说明《未名之海》演示版在问题检测阶段如何处理信息。\n\n" +
            "一、我们收集的信息\n" +
            "1. 账号信息：注册/登录使用的用户名、密码哈希（由服务器保存，客户端不明文存储密码）。\n" +
            "2. 游戏行为：场景进入记录、任务进度、寝室布置与商城购买等存档字段，用于复现 Bug 与平衡性验证。\n" +
            "3. 联机相关：房间号、主机地址登记、好友列表与聊天内容（仅用于联机功能，测试环境请勿发送真实隐私）。\n" +
            "4. 技术日志：客户端报错、连接失败原因、加载场景名称、可选的性能采样，用于定位「进不了游戏」「任务不触发」「相机失灵」等问题。\n\n" +
            "二、信息使用目的\n" +
            "1. 提供登录、存档读写、联机匹配与任务系统服务。\n" +
            "2. 统计崩溃与卡顿高发场景（如书院↔寝室传送、离开 UI 后视角异常）。\n" +
            "3. 在内测群组中跟进您主动提交的问题单，不用于广告投放。\n\n" +
            "三、存储与共享\n" +
            "1. 数据默认保存在项目配置的 GameServer 终端；请勿在公网未加密环境长期暴露测试服。\n" +
            "2. 除法律法规要求或修复安全漏洞外，不向无关第三方出售您的测试账号数据。\n" +
            "3. 开发、测试人员仅在排查问题时访问必要日志，并遵守最小权限原则。\n\n" +
            "四、您的权利\n" +
            "1. 可申请导出或删除测试账号（删除后存档不可恢复）。\n" +
            "2. 可拒绝提供非必要的联系方式；但缺少复现信息可能影响 Bug 处理进度。\n" +
            "3. 发现隐私疑问可通过测试反馈渠道联系运营方。\n\n" +
            "五、未成年人\n" +
            "若您未满 18 周岁，请在监护人同意下参与测试，并避免在聊天或角色名中填写真实姓名、学校、住址。\n\n" +
            "六、政策更新\n" +
            "隐私政策修订后将同步至登录界面；勾选同意并进入游戏即表示您已阅读更新内容。\n\n" +
            "（本政策最后更新：测试构建，与当前 GameServer、任务与寝室系统版本配套。）",
            "LGU EXPLORER respects tester privacy. This policy describes how the Sea of the Unknown demo handles data during QA.\n\n" +
            "1. Data We Collect\n" +
            "- Account: username and password hash on server (not stored in plain text on client).\n" +
            "- Gameplay: scene visits, quest progress, dorm layout, mall purchases for bug reproduction and balance checks.\n" +
            "- Multiplayer: room codes, host addresses, friends, chat (for co-op only; do not send real personal info in test chat).\n" +
            "- Technical logs: client errors, connection failures, scene names, optional performance samples.\n\n" +
            "2. How We Use It\n" +
            "- Login, saves, matchmaking, quests.\n" +
            "- Crash/hitch hotspots (e.g. academy↔dorm teleport, camera after closing UI).\n" +
            "- Follow up on bugs you report in the test group; not used for ads.\n\n" +
            "3. Storage & Sharing\n" +
            "- Data stays on the configured GameServer; do not expose test servers on the public internet unencrypted.\n" +
            "- We do not sell test account data except as required by law or to fix security issues.\n" +
            "- Dev/test staff access logs only when needed, with least privilege.\n\n" +
            "4. Your Rights\n" +
            "- Request export or deletion of test account (deletion is irreversible).\n" +
            "- You may withhold non-essential contact info; less detail may slow bug fixes.\n" +
            "- Privacy questions: use test feedback channels.\n\n" +
            "5. Minors\n" +
            "- Under 18: participate with guardian consent; avoid real names, schools, or addresses in chat or character names.\n\n" +
            "6. Updates\n" +
            "- Revisions appear on the login screen; agreeing and entering means you have read the update.\n\n" +
            "(Last updated: test build aligned with current GameServer, quest, and dorm systems.)");
}
