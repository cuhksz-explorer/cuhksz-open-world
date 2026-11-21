# CarShop Scene Setup Guide

本文档用于在校园场景中接入 Carsystem 的车店交互与选车 UI。

## 1. 为什么不能挂 InteractProximityTarget

`Assets/code/InteractProximityTarget.cs` 是 `[Serializable]` 数据类，不是 `MonoBehaviour` 组件。

正确方式：
- 在玩家的 `InteractWithMouseAndMovementLock` 组件里配置 `proximityTargets` 数组
- 不要在 NPC 或其他物体上执行 `Add Component -> InteractProximityTarget`

## 2. 推荐对象结构

- `Player`
  - `PlayerMovement`
  - 原有对话交互组件（保留）
  - 新增一份车店专用 `InteractWithMouseAndMovementLock`
- `VehicleDealerNPC`（车店 NPC）
  - `InteractableNPC`
  - （可选）`SphereCollider` / `BoxCollider` 作为交互范围参考
- `VehicleShopCanvas`
  - `VehicleShopUI`
  - 左侧列表 `Scroll View/Viewport/Content`
  - 右侧详情 `DetailImage/DetailNameText/DetailDescText/DetailSpeedingText`
  - `SelectButton`、`CloseButton`

## 3. Player 上车店专用 InteractWithMouseAndMovementLock 配置

- `targetCanvas` -> `VehicleShopCanvas` 的 `Canvas`
- `activateKey` -> `F`
- `playerMovement` -> 玩家 `PlayerMovement`
- `playerModelTransform` -> 玩家根 `Transform`
- `proximityTargets` -> `Size = 1`
  - `trigger` -> `VehicleDealerNPC`（或其触发点空物体）
  - `displayName` -> `车辆商店`
  - `interactionType` -> `选车`

注意：
- 该组件的 `targetCanvas` 只有一个，请与原 NPC 对话用组件分开。

## 4. VehicleShopUI 字段对应

在 `VehicleShopUI` 中绑定：

- `Vehicle Card Prefab` -> `Assets/Carsystem/Prefabs/VehicleCard.prefab`
- `Vehicle List Content` -> `LeftPanel/Scroll View/Viewport/Content`
- `Detail Image` -> `RightPanel/DetailImage`
- `Detail Name Text` -> `RightPanel/DetailNameText`
- `Detail Desc Text` -> `RightPanel/DetailDescText`
- `Detail Speed Text` -> `RightPanel/DetailSpeedingText`
- `Select Button` -> `RightPanel/SelectButton`
- `Close Button` -> `PanelShopMainPanel/CloseButton`
- `Spawn Message Text` -> `SpawnMessageText`（可选）

文本类型要求：
- `DetailNameText` / `DetailDescText` / `DetailSpeedText` / `SpawnMessageText` 必须是 TextMeshPro 文本（`TMP_Text`）。

## 5. InteractableNPC 配置

在车店 NPC 上的 `InteractableNPC` 绑定：

- `vehicleShopUI` -> `VehicleShopCanvas` 根对象
- `spawnPoint` -> 场景 `CarSpawnPoint`（或你的 `RespawnPoint`）
- `availableVehicles` -> 你创建的 `Racing/Vehicle Data` 资产列表

运行时行为：
- `OnVehicleSelected` 会实例化车辆并自动补齐 `InteractableVehicle`、`Carbilili`（若缺失）
- 重复选车时会销毁上一辆已生成车辆

## 6. 车辆预制体最低要求

每辆车 prefab 最低需要：
- `Rigidbody`
- `Carbilili`
- `InteractableVehicle`
- 子相机（含 `Camera` + `CarCamera`）
- 4 个 `WheelCollider` + 对应轮子视觉模型（用于物理正确运行）

若缺少 `WheelCollider`，车辆可生成但无法正常物理驾驶。

## 7. 验收步骤

1. 进入场景，靠近车店 NPC，出现 `车辆商店 / [F] 选车` 提示
2. 按 `F` 打开 `VehicleShopCanvas`
3. 点击左侧卡片，右侧详情更新（图/名/描述/速度）
4. 点 `确认` 生成车辆并关闭 UI
5. 走近车辆按 `F` 上车，`WASD` 驾驶，按 `F` 下车
