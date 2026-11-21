# 血量 / 死亡 / 复活 脚本包（副本）

**位置**：工程根目录下 `ExportedScripts/HealthSystemBundle/`（**故意放在 `Assets` 外**，避免与 `Assets/code` 里同名 `.cs` 同时被 Unity 编译导致类重复定义）。

本目录为**打包备份**：与 `Assets/code` 根目录下的同名脚本内容一致，便于拷贝到其他工程。**请勿**把本目录移入 `Assets` 作为第二套源码；正常使用仍只依赖 `Assets/code` 里的原脚本。

## 包含文件

| 文件 | 说明 |
|------|------|
| `PlayerMovement.cs` | 血量、坠落伤害、`TakeDamage`、死亡传送、`DeathReviveUI` 复活流程、体力等（与移动/相机同文件） |
| `DeathReviveUI.cs` | 运行时生成全屏死亡面板（倒计时、复活按钮），`BlocksGameplayInput` 用于屏蔽操作 |
| `GameplayUiHotkeyGuard.cs` | 统一判断「是否应屏蔽游戏热键/移动」；含死亡面板、联机 UI、商城/对话等 |
| `HP.cs` | `HealthBarUI`：绑定 `PlayerMovement.OnHealthChanged` |
| `Strength.cs` | `StaminaBarUI`：绑定 `PlayerMovement.OnStaminaChanged`（与血量条配套 UI） |
| `Multiplayer/CoopSessionState.cs` | 联机访客跳过复活扣金币（`PlayerMovement.ApplyReviveGoldCost` 使用） |

## 合并到其他 Unity 项目时

1. 将需要的 `.cs` 复制到目标工程的脚本目录（注意避免与现有类名冲突）。
2. **外部依赖**（需目标工程已有或一并移植）：
   - `GameQuestSystem.QuestSystemManager`：复活扣金币 `TryConsumeGold` / `AddGold`。
   - `DormMall`：`PlayerMovement` 中商店相关逻辑；`GameplayUiHotkeyGuard` 中商城/背包面板检测。
   - `Multiplayer`：`GameplayUiHotkeyGuard` 中联机面板、`CoopSessionState`。
   - 交互/对话 UI 类型：`InteractWithMouseAndMovementLock`、`CanvasInteractionUIManager`、`NpcDialogueUIManager` 等（仅 `GameplayUiHotkeyGuard` 用）。
3. 场景中：`PlayerMovement` 挂在带 `CharacterController` 的玩家上；血条/体力条 UI 分别挂 `HealthBarUI` / `StaminaBarUI` 并引用同一 `PlayerMovement`。
4. `DeathReviveUI` 使用资源路径加载字体：`Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF`（需存在于 Resources 或调整代码）。

若目标工程没有任务金币系统，可在 `PlayerMovement.ApplyReviveGoldCost` 中改为自己的经济接口或置空逻辑。
