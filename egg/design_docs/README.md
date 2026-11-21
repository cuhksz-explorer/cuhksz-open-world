# Assets/code 项目说明（总 README）

本文档**整合**工程内以下文档的要点，并列出 `Assets/code` 下**每个 C# 文件**的职责说明：

| 文档 | 内容 |
|------|------|
| [`1.md`](1.md) | 《未名之海》任务手册与剧情设计：主线/支线结构、全局变量建议、成就与 JSON 模板、叙事原则等（**策划向**，与当前代码非一一对应）。 |
| [`联机与服务器配置指南.md`](联机与服务器配置指南.md) | 终端 HTTP 与 Mirror TCP 双端口、部署、`GameServerSync`、Loading 与联机面板、防火墙与 API 自检等（**运维/联机必读**）。 |
| [`DormMall/搭建步骤.md`](DormMall/搭建步骤.md) | 寝室商城 UI 与 `ProductDatabase` 资源搭建步骤。 |
| 下文「脚本目录」 | 对**每个脚本文件**的用途说明（随代码迭代可继续增补）。 |

---

## 一、策划文档摘要（摘自 `1.md`）

- **定位**：开放世界叙事 RPG / 校园探索；约 100h 体量设计（主线约 30h + 支线约 70h），现实主义 + 都市传说风格。
- **建议全局量**：GPA、Stress、Energy、Money、各类声望与信任、Ethic、Mystery_ZeroClass 等；高压力可触发 Debuff 规则。
- **主线结构示例**：序章 M0 → 各章 M1～M5（绩点/情感/关系网/机会猎场/第零课真相）与终章；含 Boss 与多结局设计思路。
- **支线池**：薯塔回声、地下实验室、食堂线、情感线、都市传说巴士、宿舍协议、社团选举、打工、家庭线、隐藏平行课表等；另有 X1～X10 扩展支线设计。
- **成就与任务模板**：文档内提供成就列表示例与 **JSON 任务录入模板**，便于向任务系统迁移。
- **实装优先级建议**：文档建议先 M0–M1 + 食堂线，再关系冲突，再神秘线与结局闭环。

> 上述内容为**设计目标**；具体已实现功能以本文「脚本目录」与 Unity 场景为准。

---

## 二、技术架构摘要（摘自《联机与服务器配置指南》）

| 用途 | 协议 | 默认端口 | 说明 |
|------|------|----------|------|
| 终端服务器（HTTP API） | HTTP | **8765** | `GameServer/backend/server.py`：账号、存档、好友、聊天、联机信令、主机地址登记/解析、公开房间号等。 |
| Mirror 联机 | **TCP** | **7777** | 当主机的玩家机器监听；与 HTTP 端口**不要混用**。 |

- 所有客户端须将 **`GameServerSync` 的 Base URL**（或代码中的 `GameServerEndpoint` 默认值 / PlayerPrefs 覆盖）指向**同一逻辑**上的终端服务器。
- **冷启动**：`GET /api/connection_check` 与 `GameServerSync.loadingSceneName` 等行为见联机指南 §2.5、§3。
- **详细步骤、反向代理 `X-Forwarded-For`、防火墙、公开房间流程**见 [`联机与服务器配置指南.md`](联机与服务器配置指南.md)。

---

## 三、寝室商城资源搭建摘要（摘自 `DormMall/搭建步骤.md`）

1. 复制 `DormMall` 到 `Assets/` 下；**Create → DormMall → Product Database** 生成商品库。  
2. 商品图放在 `Resources/DormMall/...`，设为 Sprite；菜单 **GameObject → DormMall → 创建寝室商城 UI** 生成 `DormMallCanvas`。  
3. 绑定 `DormMallUI` 的 Database、初始金币等；详见 [`DormMall/搭建步骤.md`](DormMall/搭建步骤.md)。

---

## 四、操作键位速查

**说明**：多数键可在对应组件 **Inspector** 中修改；下表为代码中的常见默认值。

| 功能 | 默认按键 | 主要脚本 |
|------|-----------|----------|
| 移动 | `W` `A` `S` `D` / 轴 | `PlayerMovement` |
| 第一人称视角 | 鼠标（锁定时） | `FirstPersonCamera`（`CC1P.cs`） |
| 光标锁定切换 | `Esc` | `FirstPersonCamera` |
| 奔跑 / 跳跃 / 蹲下 | `Shift` / `Space` / `Ctrl` | `PlayerMovement` |
| 大地图（可暂停时间） | `M` | `PlayerMovement` + `MapSystem` |
| 第一/第三人称切换 | `V` | `CameraSwitcher`（`C1C3transfer.cs`） |
| 靠近交互 | `F` | `CanvasInteractionUIManager`、`InteractWithMouseAndMovementLock`、商城 NPC 等 |
| 载具上下车 | `F` / `Esc` | `VehicleController` |
| 游戏服登录面板显隐 | `P` | `GameServerLoginPanelToggle` |
| 寝室商城 | `T` | `DormMallHotkeyTController` |
| 联机房间号面板 | **`K`** | `MultiplayerEntryToggle` / `MultiplayerRoomCodePanelController` |
| 好友/聊天侧栏 | **`C`** | `GameServerSync` 轮询 → `MultiplayerFriendChatSidebarController` |
| 背包 | `B` | `InventoryUI` |
| 任务详情 | `J` / `Esc` | `QuestSystemUI` |

`GameServerSync` 在服务器出生点同步完成前可通过 `BlocksInputForServerSpawn` 屏蔽大量输入；任务教程内事件检测见 `QuestSystemManager`。

---

## 五、脚本与代码文件用途（按目录）

### 根目录 `Assets/code/`

| 文件 | 类 / 主要符号 | 用途 |
|------|----------------|------|
| `PlayerMovement.cs` | `PlayerMovement` | 角色移动、跳跃、蹲、跑、体力、生命、第一人称鼠标、飞行、`M` 开大地图等。 |
| `CC1P.cs` | `FirstPersonCamera` | 第一人称相机与 `Esc` 光标锁定，与其它系统协调。 |
| `CC3P.cs` | `WideViewCamera` | 第三人称俯视跟随，可选右键拖转。 |
| `C1C3transfer.cs` | `CameraSwitcher` | 第一/第三人称切换（默认 `V`），同步 `AudioListener` 等。 |
| `camera.cs` | `CameraFollow` | 第三人称类平滑跟随与瞬移阈值。 |
| `CCHP.cs` | `HoverCamera` | 悬停俯视相机（小地图渲染用），跟随目标。 |
| `interaction system.cs` | `CanvasInteractionUIManager`、`InteractionEscGate` | 靠近提示、`F` 开 LLM 对话、`Esc` 关并锁移动；`InteractionEscGate` 为 Esc 互斥静态工具。 |
| `interaction mode2.cs` | `InteractWithMouseAndMovementLock` | 通用近距离交互：`F` 开指定 Canvas，`Esc` 关。 |
| `InteractProximityTarget.cs` | `InteractProximityTarget` | 交互点数据（触发、显示名、类型等）。 |
| `InteractPromptSharedUiLocator.cs` | `InteractPromptSharedUiLocator` | 按场景查找共用交互提示 UI。 |
| `InteractPromptSharedResolver.cs` | `InteractPromptSharedResolver`、`InteractSharedPromptFrame` | 运行时解析/绑定共用交互提示。 |
| `VehicleController.cs` | `VehicleController` | 载具驾驶、`F` 上下车、`Esc` 下车。 |
| `VehicleHUD.cs` | `VehicleHUD` | 载具 HUD 显示。 |
| `MAPSYSTEM.cs` | `MapSystem`、`MapLocation` | 大地图、地点、传送、与 `PlayerMovement` 联动。 |
| `MINIMAPcontrol.cs` | `MinimapWithRedDot` | 小地图 RawImage、玩家红点、任务方向/标记、点击开大地图。 |
| `MINIMAPposition.cs` | `MinimapUIAnchor` | 小地图 Rect 锚到左上角。 |
| `scenetransfer.cs` | `TeleportButton` | UI 切场景与可选加载视频。 |
| `Transfer.cs` | `TeleportController` | 将玩家传到场景内目标 Transform。 |
| `teleportmanager.cs` | `TeleportDataManager` | 单例：跨场景保存目标场景名与坐标。 |
| `TeleportSceneLoadOverlay.cs` | `TeleportSceneLoadOverlay` | DontDestroy 全屏加载视频层。 |
| `LoadingVideoRandomUtil.cs` | `LoadingVideoRandomUtil` | 从多段 `VideoClip` 随机选一条。 |
| `buscontrol.cs` | `SpawnMoveRotateDisappearSceneSwitch` | 按钮：动画后切场景（可选加载视频）。 |
| `GameServerSync.cs` | `GameServerSync` | 连接终端服务器：注册/登录、位置上报、存档恢复、健康检查、联机热键轮询等。 |
| `GameServerEndpoint.cs` | `GameServerEndpoint` | **集中配置**终端服务器 Base URL（默认常量、旧默认升级、PlayerPrefs 覆盖 API）。 |
| `GameServerAuthUI.cs` | `GameServerAuthUI` | 注册/登录表单与 `GameServerSync` 绑定。 |
| `GameServerLoginPanelToggle.cs` | `GameServerLoginPanelToggle` | `P` 切换登录面板。 |
| `MenuLoginPortalFlow.cs` | `MenuLoginPortalFlow` | 菜单登录流程：巡轨、点击进入世界、与 `GameServerSync` 协作。 |
| `LogoutToLoadingSceneButton.cs` | `LogoutToLoadingSceneButton` | 登出并加载指定场景。 |
| `ServerSpawnLoadingOverlay.cs` | `ServerSpawnLoadingOverlay` | 服务器拉档期间全屏加载层。 |
| `IntroDollyCamera.cs` | `IntroDollyCamera` | 菜单/开场巡轨镜头。 |
| `NewUserIntroCinematic.cs` | `NewUserIntroCinematic` | 新用户注册后开场演出。 |
| `PhoneUIManager.cs` | `PhoneUIManager` | 手机全屏 UI，打开时禁用 `PlayerMovement`。 |
| `WebViewUnityCef.cs` | `WebViewUnityCef` | Windows 下网页显示到 RawImage。 |
| `GameplayUiHotkeyGuard.cs` | `GameplayUiHotkeyGuard` | 焦点在输入框时屏蔽联机热键（避免英文输入 `C`/`K` 误触）。 |
| `NPC behavior.cs` | `NPCControl` | NavMeshAgent 游荡与靠近玩家。 |
| `NavMeshWallCarver.cs` | `NavMeshWallCarver` | 墙体 `NavMeshObstacle` 挖洞。 |
| `NpcDialogueInteractor.cs` | `NpcDialogueOption`、`NpcDialogueNode`、`NpcDialogueInteractor` | NPC 对话图与交互组件。 |
| `NpcDialogueUIManager.cs` | `NpcDialogueUIManager` | NPC 对话 UI 管理。 |
| `GameController.cs` | `GameController` | 游戏开始等占位入口。 |
| `NewBehaviourScript.cs` | `NewBehaviourScript` | Unity 默认空模板。 |
| `button.cs` | `button` | 空脚本模板。 |
| `HP.cs` | `HealthBarUI` | 血条 UI，订阅 `PlayerMovement.OnHealthChanged`。 |
| `Strength.cs` | `StaminaBarUI` | 体力条 UI。 |
| `Playersound.cs` | `PlayerStepSound` | 脚步与喘息音效。 |
| `BGM.cs` | `BackgroundMusic` | 全局循环 BGM，可选 DontDestroy。 |
| `SceneAmbientAudio.cs` | `SceneAmbientAudio` | 场景环境音淡入。 |
| `qualitycontrol.cs` | `QualityController` | 画质档位与 `PlayerPrefs`。 |
| `Qualitycontrolhide.cs` | `CurvePanelController` | 画质相关面板显隐。 |
| `time.cs` | `SunSimulation` | 昼夜与天空颜色仿真。 |
| `lightcontrol.cs` | `TimeBasedLightController` | 按时间控制灯光。 |
| `skycontrol.cs` | `MultiTimeSkyboxSwitcher` | 按时间切换 Skybox。 |
| `shadowchanger.cs` | `TimeBasedShadowController` | 按时间调整阴影。 |
| `cloudcontrol.cs` | `CloudScroller` | 云材质滚动。 |
| `VegetationDistanceOptimization.cs` | `VegetationDistanceCuller`、`VegetationCullSystem`、`VegetationCameraLayerCull` | 植被距离优化与分层剔除。 |
| `Telsys.cs` | `TeleportSystem` | 世界坐标传送与可选特效。 |
| `hidebutton.cs` | `UIToggleController` | 按钮切换某 UI 显隐。 |
| `wasd(2).cs` | `KeyInputController` | WASD/Shift 映射到 Animator。 |
| `aiming.cs` | `ExistingUIPanel_CrosshairClick`、`Example3DButton` | 屏幕准星与 3D/UGUI 交互。 |
| `Qcontrol.cs` | `Qcontrol` | 按情绪名触发 Animator Trigger（LLM/NPC 用）。 |
| `TargetGuideUI.cs` | `TargetGuideUI` | 任务目标世界引导图标与距离。 |
| `UiLifecycleSafe.cs` | `UiLifecycleSafe` | UI 生命周期安全工具（跨场景等）。 |

---

### `GameQuestSystem/`

| 文件 | 类 / 主要符号 | 用途 |
|------|----------------|------|
| `QuestSystemManager.cs` | `QuestSystemManager` | 任务链与金币、HTTP 存档同步、距离/购买/内置教程事件、联机 Relay 与快照等。 |
| `QuestSystemUI.cs` | `QuestSystemUI` | 任务栏 UI，`J`/`Esc` 等。 |
| `QuestModels.cs` | `QuestSubTask`、`QuestChainDefinition`、`QuestChainProgress`、`QuestSavePayload` | 任务数据结构。 |
| `LingCollegeBDormQuestPreset.cs` | `LingCollegeBDormQuestPreset` | 静态工厂：寝室商城教学任务链。 |
| `QuestSystemAutoUIBuilder.cs` | `QuestSystemAutoUIBuilder` | 任务 UI 层级自动生成辅助。 |

---

### `DormMall/`

| 文件 | 类 / 主要符号 | 用途 |
|------|----------------|------|
| `DormMallUI.cs` | `DormMallUI` | 商城主界面、分类、购买、与任务金币联动。 |
| `DormMallHotkeyTController.cs` | `DormMallHotkeyTController` | `T` 开关商城。 |
| `InventoryUI.cs` | `InventoryUI` | 背包 UI，`B` 开关。 |
| `InventoryData.cs` | `InventoryData` | 静态背包数据。 |
| `ProductDatabase.cs` | `ProductDatabase`、`ProductEntry` | ScriptableObject 商品库。 |
| `ProductItemUI.cs` | `ProductItemUI` | 单个商品格子 UI。 |
| `DormMallMerchantNpc.cs` | `DormMallMerchantNpc` | 靠近店主 `F` 打开商城。 |
| `PlacementModeController.cs` | `PlacementModeController` | 场景内放置家具：确认/取消/旋转/缩放等。 |
| `PlacementHelpPanel.cs` | `PlacementHelpPanel` | 放置模式帮助文案。 |
| `DormTutorialProximityHint.cs` | `DormTutorialProximityHint` | Trigger 进入显示教程提示。 |
| `DormMallRemoteCatalog.cs` | `DormMallRemoteCatalog` | 从 GameServer 拉取商城目录协程。 |
| `DormMallCatalogJson.cs` | `DormMallCatalogFileJson`、`DormMallCatalogProductJson` | 商城 JSON 结构。 |
| `Editor/DormMallEditor.cs` | `DormMallEditor` | 编辑器菜单：创建商城 UI、商品库等。 |
| `Editor/ProductDatabaseEditor.cs` | `ProductDatabaseEditor` | `ProductDatabase` 自定义 Inspector。 |

---

### `Multiplayer/`

| 文件 | 类 / 主要符号 | 用途 |
|------|----------------|------|
| `CoopSessionState.cs` | `CoopSessionState` | 静态：是否 Host/Client；客机是否阻止写 HTTP 存档。 |
| `LanCoopNetworkManager.cs` | `LanCoopNetworkManager` | Mirror `NetworkManager` + Telepathy 端口与人数刷新。 |
| `CoopNetworkPlayer.cs` | `CoopNetworkPlayer` | 联机玩家占位：跟随 `PlayerMovement`、动画与场景 SyncVar、远端可视模型。 |
| `CoopPlayerNameplate.cs` | `CoopPlayerNameplate` | 头顶世界空间姓名与用户 ID 标签。 |
| `CoopPlayerRoomStats.cs` | `CoopPlayerRoomStats` | SyncVar 同步房间在线人数等。 |
| `CoopQuestStateHub.cs` | `CoopQuestStateHub` | 主机：任务+金币快照同步给客机。 |
| `CoopQuestRelay.cs` | `CoopQuestRelay` | 客机：经 Command 在主机推进任务。 |
| `CoopRoomOnlineHud.cs` | `CoopRoomOnlineHud` | 联机时右上角在线人数 HUD。 |
| `MultiplayerApiClient.cs` | `MultiplayerApiClient` | 好友、聊天、联机邀请、房间号、分片、近距聊天等 REST 封装。 |
| `MultiplayerDtos.cs` | 多种 DTO 类 | HTTP JSON 数据结构（好友、信令、近距聊天、分片状态等）。 |
| `MultiplayerHubController.cs` | `MultiplayerHubController` | 联机总面板逻辑（邀请、主机、公开房间、Mirror 启动等）。 |
| `MultiplayerEntryToggle.cs` | `MultiplayerEntryToggle` | `K` 开关房间号面板或绑定按钮。 |
| `MultiplayerRoomCodePanelController.cs` | `MultiplayerRoomCodePanelController` | 房间号/公开房间等 UI 与信令。 |
| `MultiplayerFriendChatSidebarController.cs` | `MultiplayerFriendChatSidebarController` | 好友列表与聊天侧栏 UI 与轮询。 |
| `FriendChatSidebarUiBinder.cs` | `FriendChatSidebarUiBinder` | 侧栏场景 UI 引用绑定。 |
| `MultiplayerPanelHotkeyUtility.cs` | `MultiplayerPanelHotkeyUtility` | 静态：`K`/`C` 热键、自动挂载面板、光标策略。 |
| `MultiplayerServiceRoot.cs` | `MultiplayerServiceRoot` | 联机服务根物体 DontDestroy 辅助。 |
| `ProximityChatPanelController.cs` | `ProximityChatPanelController` | 同场景近距聊天面板与 HTTP 收发。 |
| `SceneShardCoordinator.cs` | `SceneShardCoordinator` | 分片 endpoint 与 GameServer 主机 IP 协调（多场景分服扩展）。 |
| `VehicleNetworkProxy.cs` | `VehicleNetworkProxy` | 载具相关网络同步占位/代理。 |
| `Editor/CoopPlayerPrefabCreator.cs` | `CoopPlayerPrefabCreator` | 菜单生成 `Resources/CoopPlayer.prefab`。 |
| `Editor/MultiplayerHubOneClickSetup.cs` | `MultiplayerHubOneClickSetup` | 一键配置联机服务物体与组件。 |
| `Editor/MultiplayerFriendChatSidebarController.Editor.cs` | `FriendChatSidebarPanelMenu` | 菜单生成好友聊天侧栏 UI。 |

---

### `LLMcode/`

| 文件 | 类 / 主要符号 | 用途 |
|------|----------------|------|
| `LLMChatClientLocalQwen(1).cs` | `LLMChatClient_LocalQwen`、`StreamingHandler` | 兼容 OpenAI 的 API 流式对话、记忆、情绪 JSON。 |
| `NPCPortraitCameraController.cs` | `NPCPortraitCameraController` | 对话头像 RenderTexture 相机。 |
| `NpcEmotionController.cs` | `NPCEmotionController` | NPC 表情/动画与对话联动。 |
| `LLMEmotionClassifier.cs` | `LLMEmotionClassifier` | 情绪分类辅助。 |

---

### `freshman/`

| 文件 | 类 / 主要符号 | 用途 |
|------|----------------|------|
| `Bliiboard.cs` | `Billboard` | 物体始终朝向相机。 |

---

### `Editor/`（根目录，仅编辑器）

| 文件 | 主要符号 | 用途 |
|------|-----------|------|
| `ApplyLingCollegeBDormQuestMenu.cs` | `ApplyLingCollegeBDormQuestMenu` | 菜单写入 LingCollegeB 寝室任务预设。 |
| `TargetGuideOneClickSetup.cs` | `TargetGuideOneClickSetup` | 任务引导一键场景配置。 |
| `TeleportLoadVideosAutoAssign.cs` | `TeleportLoadVideosAutoAssign` | 传送加载视频批量赋值。 |
| `DormMallCatalogExportToGameServer.cs` | `DormMallCatalogExportToGameServer` | 商城目录导出到 GameServer。 |
| `InteractionPromptOneClickSetup.cs` | `InteractionPromptOneClickSetup` | 交互提示一键配置。 |
| `NewUserIntroCinematicSetup.cs` | `NewUserIntroCinematicSetup` | 新用户开场镜头场景配置。 |
| `SceneSetupAuditWindow.cs` | `SceneSetupAuditWindow` | 场景检查 EditorWindow。 |
| `QuestNewUserOnboardingSetup.cs` | `QuestNewUserOnboardingSetup` | 新用户任务引导配置。 |
| `QuestSystemOneClickSetup.cs` | `QuestSystemOneClickSetup` | 任务系统一键配置。 |
| `GameServerAuthUICreator.cs` | `GameServerAuthUICreator` | 生成 GameServerSync + 登录面板。 |
| `AddMeshCollidersToScene.cs` | `AddMeshCollidersToScene` | 批量添加 MeshCollider。 |

---

### `GameServer/backend/`（Python，非 C#）

| 路径 | 用途 |
|------|------|
| `server.py` | FastAPI 终端服务器：账号、存档、好友、聊天、联机信令、商城目录等。 |
| `requirements.txt` | Python 依赖。 |
| `scripts/gen_dorm_mall_catalog_from_unity_asset.py` | 从 Unity 导出生成商城目录。 |
| `start_server.bat`、`run_server.cmd`、`stop_server.bat` | Windows 启停脚本。 |

---

## 五·二、椅子坐下与待机动画（队友模块 · 必读）

**现象**：能坐到椅子位置但没有坐下动作 → 通常是 **Mixamo FBX 未导入** 或 **未运行一键配置**。

### 资源位置

| 路径 | 内容 |
|------|------|
| `Assets/Animations/ChairSit/` | 坐下/站起 Mixamo FBX + `PlayerChairSit.controller` |
| `Assets/Resources/ChairSit/` | 运行时自动加载用的同名资源副本 |
| `Assets/Animations/AfkDance/` | 10 秒无操作待机动画 |

所需 FBX（Mixamo 下载 **In Place** 版本）：

| 用途 | 推荐文件名 |
|------|-----------|
| 站→坐 | `Sitting (2).fbx`（或 `Sitting (1).fbx`） |
| 坐着 idle | `Sitting Idle.fbx`（或 `Sitting Talking.fbx`） |
| 站起 | `Sit To Stand.fbx`（或 `Stand Up.fbx`） |

### 一键配置（Unity 编辑器）

1. 打开含玩家与椅子的场景（如 `undergarden`）
2. **停止 Play**
3. 菜单 **`Tools → 椅子交互 → 完整配置（动画 + 所有长椅）`**
4. **Ctrl+S** 保存场景
5. Play：靠近椅子按 **F** 坐下，再按 **F/Esc** 站起

其他菜单：

- `Tools → 椅子交互 → 集成到项目` — 动画 + 椅子 + 待机动画 + 新手教程
- `Tools → 待机动画 → 一键配置` — 仅 AFK 随机待机

### 核心脚本

| 文件 | 用途 |
|------|------|
| `InteractableChair.cs` | 椅子 F 交互、座位锚点 |
| `PlayerSitSequence.cs` | 切专用 Animator、播坐下/站起序列 |
| `PlayerAfkIdleDance.cs` | 10 秒无输入随机待机 |
| `ChairSitState.cs` | 坐下全局状态，屏蔽移动/热键 |
| `Editor/ChairSitOneClickWizard.cs` | 导入 FBX、生成 Controller、挂到玩家 |

### 排查

- Console 出现 `[PlayerSitSequence] Animator 无状态 SitDown` → 重新运行「完整配置」
- Hierarchy 玩家无 `Player Sit Sequence` → 运行完整配置（坐下时会自动 AddComponent，但需先配好动画）
- 椅子无 `[F] 坐下` 提示 → `Tools → 椅子交互 → 为场景中所有 bench 添加坐下交互`

---

## 六、依赖与备注

- **Mirror**：见 `Packages/manifest.json`（常用 OpenUPM：`com.mirrornetworking.mirror`）。  
- **同场景多第一人称相机**：避免 `PlayerMovement` 与 `FirstPersonCamera` 重复旋转（以实际挂载为准）。  
- **`Esc`** 被多系统共用；各脚本通过 `InteractionEscGate`、`PlacementModeController` 等做互斥。  
- 键位或流程变更后，请同步更新 **Inspector** 与本 README 对应行。

---

*文档合并自仓库内策划与配置说明，并覆盖 `Assets/code` 下主要 C# 文件；若新增脚本请在本表追加一行。*
