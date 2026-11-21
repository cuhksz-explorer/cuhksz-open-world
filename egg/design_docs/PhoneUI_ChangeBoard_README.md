# PhoneUI 键位设置（ChangeBoard）

手机九宫格 **自定义按键**（`PhoneAppId.Custom1`）打开 **1280×720** 双行键位面板。

## 文件

| 文件 | 说明 |
|------|------|
| [`PhoneKeyBindingPanelView.cs`](PhoneKeyBindingPanelView.cs) | 运行时 Ensure 全屏壳、缩放、返回 |
| [`PhoneKeyBindingUI.cs`](PhoneKeyBindingUI.cs) | 9+8 键位网格、点击改键、组合键、冲突提示 |
| [`PhoneKeyBindingPanel.cs`](PhoneKeyBindingPanel.cs) | 兼容 `ShowPanel()` / `HidePanel()` 转发 |

## 依赖

[`Assets/code/Controls/`](../../code/Controls/)：

- `GameControlAction` — 17 项可绑定操作
- `KeyChord` / `ControlBindingStore` — `PlayerPrefs` `GameControls.Profile_v2`（自动从 v1 迁移）
- `GameInput` — `GetKeyDown` / `GetKey` / `GetMovementAxis`
- `KeyBindingConflictChecker` — 重复组合键拒绝绑定

## 接入方式

无需在 Prefab 手摆 `KeyBindingsPage`：`PhoneSessionController.EnsureKeyBindingsPage()` 会在 `phonePanel` 下自动创建。

入口：`PhoneSessionController.OpenKeyBindingsPage()` 或九宫格 Custom1。

## 改键交互

1. 显示默认键位（与玩法一致：F 互动、B 背包、K/C/O/T 等）
2. **点击键帽** → 文字清空 → 按下新键（可 Ctrl/Shift/Alt 组合）
3. Esc 取消；冲突时底部提示占用者
4. **恢复默认** 写回 v2 配置

## 默认键位

| 行 | 操作 | 默认 |
|----|------|------|
| 1 | 前进/后退/左/右/奔跑/跳跃/蹲下/互动/手机 | W S A D Shift Space Ctrl F F1 |
| 2 | 地图/背包/任务/联机/视角/好友/竞拍/商城 | M B J K V C O T |

玩法脚本已改为读取 `GameInput`（见 `PlayerMovement`、`interaction system`、`InventoryUI` 等）。
