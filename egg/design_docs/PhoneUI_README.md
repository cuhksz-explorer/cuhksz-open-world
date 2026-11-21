# PhoneUI 配置与集成说明

本文档说明 `Assets/PhoneUI` 的目录结构、前置依赖、场景接线方式、可实现功能、相对旧版手机的改动，以及常见故障排查。整合依据：

- 工程计划：[`.cursor/plans/phoneui_键位集成_1fa90d88.plan.md`](../.cursor/plans/phoneui_键位集成_1fa90d88.plan.md)
- 参考场景：`Assets/scene/undergarden.unity`（`launcherOnly = 1` 的 Launcher 模式）
- 预制体：`Assets/PhoneUI/Prefabs/PhoneUI.prefab`（根物体 `PhoneUI` → 子物体 `PhoneLauncher`）

---

## 目录结构

| 路径 | 用途 |
|------|------|
| [`Scripts/`](Scripts/) | 会话、九宫格、路由、镜头呈现、资源加载 |
| [`ChangeBoard/`](ChangeBoard/) | 手机内**键位设置** UI（`PhoneKeyBindingPanel` / `PhoneKeyBindingUI`） |
| [`Prefabs/PhoneUI.prefab`](Prefabs/PhoneUI.prefab) | 可拖入场景的完整手机 Canvas 预制体 |
| [`Prefabs/PhoneProp.prefab`](Prefabs/PhoneProp.prefab) | 手持手机模型（右手骨骼挂载） |
| [`Resources/PhoneUI/`](Resources/PhoneUI/) | 运行时 `Resources.Load`（`PhoneProp`、`smartphone.png` 边框图） |
| [`Animation/`](Animation/) | `Phone_Open` / `Phone_Hold_Loop` / `Phone_Close` 与上半身 Mask |
| [`CellPhone/`](CellPhone/) | 手机 FBX 与九宫格图标素材 |
| [`Editor/`](Editor/) | 菜单：复制边框图、配置 Animator、选中场景 Phone |

键位子包细节见 [`ChangeBoard/README.md`](ChangeBoard/README.md)。

---

## 前置依赖

PhoneUI **不是**独立小游戏模块，依赖场景里已有的校园玩法与 UI 系统。缺任一项时，对应格子会打日志「未找到 XXX」或点击无反应。

### 1. 键位与输入层（必装，消除 CS0246）

| 路径 | 说明 |
|------|------|
| [`Assets/code/Controls/`](../code/Controls/) | `GameControlAction`、`ControlBindingProfile`、`ControlBindingStore`、`GameInput` |
| PlayerPrefs 键 | `GameControls.Profile_v2`（自动从 v1 迁移） |
| ChangeBoard | [`PhoneKeyBindingPanelView.cs`](ChangeBoard/PhoneKeyBindingPanelView.cs) 1280×720 双行改键 UI |

玩法热键（移动、互动、背包、联机 K/C/O、商城 T 等）已接入 `GameInput`。

### 2. 寝室商城（DormMall）

| 依赖 | 用途 |
|------|------|
| [`Assets/code/DormMall/`](../code/DormMall/) | 商城 UI、`ProductDatabase`、`InventoryUI` |
| `Resources/DormMall/ProductDatabase` | 商品数据 |
| `DormMallHotkeyTController` / `DormMallUI` | 手机 **Shop** 格：靠近商户按 **T** 开店，或 Router 查找 `DormMallUI` |

搭建步骤：[`Assets/code/DormMall/搭建步骤.md`](../code/DormMall/搭建步骤.md)。

手机 **Shop 格无全局热键**，仅通过手机九宫格或场景内商户触发。

### 3. 设置 / 画质页

| 组件 | 说明 |
|------|------|
| [`PhoneSettingsMigrator.cs`](Scripts/PhoneSettingsMigrator.cs) | 将旧 `closepanel` 下 `QualityController.qualityPanel` **迁入** `settingsPage`（仅 legacy 场景需要） |
| 九宫格 **Settings** | `PhoneAppId.Settings` → `OpenSettingsPage()`，显示画质子页 |
| 旧版 `closepanel` | 预制体/场景里可能仍存在；Bootstrap 会 **SetActive(false)** 隐藏同级旧子节点 |

新场景若手动画质页，请**关闭** `PhoneUIManager.autoMigrateSettings`。

### 4. 键位设置页（ChangeBoard，运行时自动创建）

| 步骤 | 说明 |
|------|------|
| 九宫格 **Custom1** | `appId = Custom1`，显示名「自定义按键」 |
| 入口 | `PhoneSessionController.OpenKeyBindingsPage()` / `PhoneAppRouter` |
| 页面 | `EnsureKeyBindingsPage()` 在 `phonePanel` 下创建 `KeyBindingsPage`，无需手改 Prefab |

详见 [`ChangeBoard/README.md`](ChangeBoard/README.md)。

### 5. 各 App 桥接（`PhoneAppRouter`）

| `PhoneAppId` | 桥接目标 | 行为摘要 |
|--------------|----------|----------|
| Map | `MapSystem` | 关手机 → `ShowMap()` |
| Quest | `QuestSystemUI` | 关手机 → `OpenFullPanelFromPhone()` |
| Shop | `DormMallHotkeyTController` / `DormMallUI` | 关手机 → 打开商城 |
| Inventory | `InventoryUI` | 关手机 → `OpenPanelExternal()` |
| Multiplayer | `MultiplayerRoomCodePanelController` | 关手机 → `OpenPanel()` |
| Settings | `PhoneSessionController` | 不关手机，切 `settingsPage` |
| Custom1 | `PhoneKeyBindingPanelView` | 1280×720 改键面板 |
| Custom2 | `PerformLogout` | 登出 |
| Account | — | 未接入 |

### 6. 镜头 / 动画（Presentation 模式，可选）

| 依赖 | 说明 |
|------|------|
| 角色 `Animator` + `wasd(1).controller` | 菜单 **PhoneUI → Setup Phone Animator** 添加 Phone Layer |
| `PhonePresentationController` | `launcherOnly = false` 时 F1 切第三人称正面镜头、播掏手机动画 |
| `PhonePlayerRigSetup` | 自动创建 `PhoneViewAnchor`、`PhoneProp`（`Resources/PhoneUI/PhoneProp`） |
| `CameraSwitcher` / `FirstPersonCamera` | 与 `PlayerMovement` 同角色 |

**undergarden 当前为 `launcherOnly = 1`**：只弹九宫格 UI，**不**切镜头、不播 Presentation 动画。

### 7. 全局热键互斥

| 脚本 | 说明 |
|------|------|
| [`GameplayUiHotkeyGuard.cs`](../code/GameplayUiHotkeyGuard.cs) | 手机打开、联机面板、商城/背包、对话等打开时屏蔽玩法热键 |
| `PhoneSessionController.BlocksGameplayInput` | 手机打开期间为 true |
| `GameServerSync.BlocksInputForServerSpawn` | 联机出生同步完成前 **禁止 F1 开手机** |

---

## 一、能实现的功能

### 已实现（开箱或按场景接线后）

| 功能 | 操作 / 入口 |
|------|-------------|
| 打开手机 | 默认 **F1**（`GameInput` / `TogglePhone`） |
| 九宫格 Launcher | 设置、地图、任务、商城、背包、联机、账号、两个自定义槽 |
| 画质设置 | **Settings** 格 → `settingsPage`（可配合 `PhoneSettingsMigrator`） |
| 跳转各系统 | `PhoneAppRouter` 关闭手机后打开对应全屏/面板 UI |
| 打开期间暂停移动 | `PlayerMovement.isMovementPaused = true`，解锁鼠标 |
| Canvas 置顶 | 打开手机时 `sortingOrder` 提升到 `phoneCanvasSortOrder`（默认 3000） |
| 键位面板 | `PhoneKeyBindingPanelView`：1280×720 双行改键、组合键、冲突检测 |
| 旧 Button 兼容 | [`PhoneUIManager.cs`](../code/PhoneUIManager.cs) 转发 `OpenPhone` / `ClosePhone` |

### 需额外接线或代码阶段后才完整

| 功能 | 状态 |
|------|------|
| Presentation 掏手机 + 切镜头 | 需 `launcherOnly = false` + Animator Layer + `presentation` 引用 |
| Account | 账户页（`PhoneAccountPanelView`） |

---

## 二、原有组件的更改与推荐 Hierarchy

### 目标结构（推荐）

```
玩家根 (含 PlayerMovement)
├── Phones                          ← 新建空物体，集中手机相关
│   ├── PhoneCanvas                 ← 主 UI：PhoneSessionController + Canvas（undergarden 上多为 Inactive）
│   │   └── phonePanel / PhoneMenuRoot
│   │       ├── PhoneShell          ← 九宫格外壳（F1 后显示）
│   │       ├── SettingPage         ← 画质（Settings 格）
│   │       └── KeyBindingsPage     ← 键位（Custom1，需新建）
│   ├── PhoneViewAnchor             ← PhonePlayerRigSetup 可自动创建
│   └── PhoneProp                   ← 手持模型，平时 Inactive
└── （相机、Animator 等同层或子级）

角色 Prefab / pack 下（需处理）
└── Phone                           ← 旧版手机 Canvas，应禁用或删除
```

### 与「pack 下原 Phone」的关系

- 场景中名为 **`pack`** 的物体多为**场景资源打包节点**（大量子物体），不一定是手机本身。
- **真正需要禁用的是角色身上的旧 `Phone` / `PhoneCanvas`**：在 `undergarden` 中，玩家下存在 **`Phones`**，其内常有：
  - **`PhoneCanvas`**：带 `PhoneSessionController` 的新 UI（应保持 **唯一有效实例**）；
  - **`Phone`**：旧 Canvas（场景中常为 **`m_IsActive: 0`**，且第二个 `PhoneSessionController` 为 **disabled**）。
- **操作 checklist**：
  1. 在**玩家模型下**保留或新建 **`Phones`**，将当前使用的 **`PhoneCanvas`（或 `PhoneLauncher`）** 放在其下。
  2. 将 **pack / 角色 Prefab 里自带的旧 `Phone` 物体** 设为 **Inactive**，或移除其上的 `PhoneSessionController` / `Canvas`，避免与新区冲突。
  3. 全场景只保留 **一个** 已配置完整的 `PhoneSessionController`（`phonePanel`、`phoneShell`、`settingsPage`、`appGrid` 均已赋值）。
  4. `PhoneUIManager` 上 **关闭** 自动项（undergarden 已符合）：
     - `autoBuildUi = false`
     - `autoAddPresentation = false`
     - `autoMigrateSettings = false`
     - `autoAddPlayerRigSetup = false`（若已手搭 Rig 可保持 false；需要自动挂 Prop 可改为 true）

### 使用预制体 `PhoneUI.prefab`

1. 将 [`Prefabs/PhoneUI.prefab`](Prefabs/PhoneUI.prefab) 拖入场景或挂到 `Phones` 下。
2. 根物体为 **`PhoneUI`**（Canvas），子物体 **`PhoneLauncher`** 上已有 `PhoneSessionController` + `PhoneUIManager`。
3. 在 Inspector 绑定：
   - `hudPanel` → 场景 HUD 根（可选）
   - `phonePanel` / `phoneShell` / `settingsPage` / `appGrid` → 预制体内部引用（预制体已配则检查即可）
   - `playerMovement` → 当前场景玩家
   - `presentation` → 同玩家或 Launcher 上的 `PhonePresentationController`（Presentation 模式才需要）
4. 若场景已有手搭 Canvas，**不要**再拖第二份预制体，以免出现两个 `PhoneSessionController.Instance`。

### 模式开关（Inspector）

| 字段 | 推荐（Launcher / undergarden） | Presentation 演出 |
|------|--------------------------------|-------------------|
| `launcherOnly` | **true** | **false**（菜单 **PhoneUI → Configure Active Scene Phone Presentation**） |
| `useManualLayout` | **true**（使用手摆 PhoneFrame） | 可按需 |
| `enableToggleHotkey` | true（F1） | 同左 |

### 九宫格默认含义（`PhoneUiBootstrap` / 预制体）

| 槽位 | `PhoneAppId` | 显示名（默认） |
|------|----------------|----------------|
| 0 | Settings | 设置 |
| 1 | Map | 地图 |
| 2 | Quest | 任务 |
| 3 | Shop | 商城 |
| 4 | Inventory | 背包 |
| 5 | Multiplayer | 联机 |
| 6 | Account | 账号 |
| 7 | Custom1 | 自定义 → **建议改为「键位」** |
| 8 | Custom2 | 自定义 |

各 `PhoneAppSlot` 需指定 `session`（或在 `phoneShell` 下能 `GetComponentInParent` 到 Session）。

### Editor 菜单（`PhoneUI/`）

| 菜单项 | 作用 |
|--------|------|
| Copy Smartphone To Resources | 复制边框图到 `Resources/PhoneUI/smartphone.png` |
| Setup Phone Animator | 创建 Clip、Mask、wasd Controller Phone Layer、PhoneProp |
| Configure Active Scene Phone Presentation | `launcherOnly=false` + 启用 Presentation / Rig |
| Select Phone In Active Scene | 选中 `PhoneLauncher` 或 `Phone` |

---

## 三、场景配置步骤（Checklist）

1. **确认依赖**：`Assets/code/Controls` 存在且无编译错误；DormMall / Map / Quest / 联机 UI 已在场景中。
2. **唯一 Session**：只启用一个配置完整的 `PhoneSessionController`。
3. **绑定玩家**：`playerMovement`、（可选）`presentation`。
4. **面板引用**：`hudPanel`、`phonePanel`、`phoneShell`、`settingsPage`、`appGrid` 非空；`IsConfigured` 为真。
5. **禁用旧 Phone**：角色/pack 下旧 `Phone` 物体 Inactive，避免重复 Canvas。
6. **画质**：Settings 格能打开 `SettingPage`；legacy 场景可一次性运行 `PhoneSettingsMigrator.Migrate()`。
7. **键位页**：新建 `KeyBindingsPage` + `PhoneKeyBindingPanel`；Custom1 接线 `ShowPanel` / `HidePanel`。
8. **Play 验收**：
   - F1 打开/关闭九宫格；
   - 各格能打开对应系统或设置页；
   - 进 Play **不会**再弹出 F3/F4 全屏设置（ChangeBoard 旧脚本已移除）；
   - 无重复 Instance 警告。

---

## 四、可能存在的问题与排查

### 1. 角色无法移动（与 PhoneUI 无关的情况最多）

| 现象 | 常见原因 | 处理 |
|------|----------|------|
| 全程不能走 | `GameServerSync.BlocksInputForServerSpawn` 一直为 true | 完成联机出生同步；或单机场景检查 `GameServerSync` |
| 能播动画但不能位移 | `PlayerMovement` 内 `isMovementPaused` 或提前 `return` | 查 Inspector 是否被其它脚本设为暂停 |
| 按 M 后卡住 | `MapSystem` 打开时 `Time.timeScale = 0` | 正常；用地图关闭逻辑恢复 `timeScale` |
| 仅打开手机时不能走 | **设计如此**：`OpenPhone()` 设 `isMovementPaused = true` | 关闭手机应 `RestoreInputBlock()`；若关手机仍不能走，查 `ClosePhone` 是否被调用、是否有第二个 Session 未恢复 |

> 禁用整个 `PhoneUI` 文件夹后仍不能移动 → 优先查 **`GameServerSync`**、**`timeScale`**、**`isMovementPaused`**，而非 Phone 脚本。

### 2. 视角 / 镜头异常

| 现象 | 原因 | 处理 |
|------|------|------|
| 开手机后鼠标乱飞 | 第一人称 `FirstPersonCamera` 与手机同时改光标 | 关手机后应恢复；检查 `presentation` 是否误开 |
| 关手机后角色「消失」 | `restoreFirstPersonViewOnClose = true` 切回 FP | `PhonePresentationController` 默认 **false**，保持第三人称可见 |
| 没有掏手机动画 | `launcherOnly = true` | 改为 false 并配置 Animator + Presentation |
| 镜头不回到跟随 | `CameraSwitcher` 未绑定 | `PhonePlayerRigSetup.EnsureRig()` 或 Inspector 手绑 |

### 3. 手机 UI 本身

| 现象 | 原因 | 处理 |
|------|------|------|
| F1 无反应 | `enableToggleHotkey` 关闭；或未 `IsConfigured` | 补全 `phoneShell` 等引用 |
| F1 无反应 | `CanOpenPhoneFromHotkey()` 被挡 | 联机同步、UI 焦点、`GameplayUiHotkeyGuard` |
| 点击格子无反应 | 目标系统不在场景 | 看 Console `[PhoneUI] 未找到 ...` |
| 两个手机 / 警告重复实例 | 场景里两个 Session | 禁用 pack/旧 `Phone` 上多余 Session |
| Canvas 不显示 | 父级 Inactive 或 `localScale = 0` | `EnsureCanvasRootsVisible()`；检查 `PhoneUI` 根 Scale |
| Settings 与旧 closepanel 重复 | 未迁移或未隐藏 legacy | 运行 Migrator 或手动隐藏 `closepanel` 子树 |

### 4. 键位系统

| 现象 | 原因 | 处理 |
|------|------|------|
| Custom1 点击空白 | 旧版无 `KeyBindingsPage` | 已用 `EnsureKeyBindingsPage()` 自动创建 |
| 改键保存了但游戏仍用旧键 | 个别脚本仍硬编码 | 检查是否遗漏 `GameInput` |

### 5. 商城 / 背包格子

| 现象 | 原因 |
|------|------|
| Shop 打不开 | 未靠近带 `DormMallHotkeyTController` 的商户，且无场景 `DormMallUI` |
| Inventory 打不开 | 无 `InventoryUI.Instance` |

---

## 五、核心脚本速查

| 脚本 | 职责 |
|------|------|
| `PhoneSessionController` | F1、面板显隐、移动/光标锁定、App 点击分发 |
| `PhoneAppRouter` | Map / Quest / Shop / Inventory / Multiplayer 桥接 |
| `PhoneAppSlot` / `PhoneAppGrid` | 九宫格槽位与点击 |
| `PhonePresentationController` | 第三人称镜头 + 手机动画（非 Launcher 模式） |
| `PhonePlayerRigSetup` | 场景加载后自动 Claim Session、挂 Anchor/Prop |
| `PhoneUIManager` | 兼容旧 Button；可选 AutoBuild / Migrator / Rig |
| `PhoneUiBootstrap` | 运行时生成 Shell + 九宫格 + SettingsPage（`buildOnAwake`） |
| `PhoneSettingsMigrator` | legacy 画质 UI 迁入 `settingsPage` |
| `PhoneKeyBindingPanelView` | Custom1 改键面板 |
| `PhoneUIManager`（`Assets/code`） | 场景 Button → Open/ClosePhone |

---

## 六、键位系统（已完成）

- `PhoneSessionController`：`keyBindingsPage`、`OpenKeyBindingsPage` / `CloseKeyBindingsPage`
- `GameInput` + `ControlBindingStore` Profile v2
- 玩法热键与新手教程检测已接 `GameInput`

---

## 七、相关文档

| 文档 | 内容 |
|------|------|
| [计划书：PhoneUI 键位集成](../.cursor/plans/phoneui_键位集成_1fa90d88.plan.md) | 架构、P1/P2、验收 |
| [ChangeBoard/README.md](ChangeBoard/README.md) | 键位页专用说明 |
| [Assets/code/README.md](../code/README.md) | 全项目脚本与默认热键表 |
| [DormMall/搭建步骤.md](../code/DormMall/搭建步骤.md) | 寝室商城资源 |

---

*文档版本：与当前工程 `undergarden` + `PhoneUI.prefab` + `Controls`/`ChangeBoard` 状态一致；场景名以你项目实际为准。*
