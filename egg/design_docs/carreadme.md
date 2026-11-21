# Carsystem 车辆接入与配置说明

本文档用于说明当前项目中 `Assets/Carsystem` 目录下的主用赛车系统（下文称“系统2”）如何接入新车辆、配置车店老板 NPC、创建 `VehicleData` 资产，以及如何理解系统1与系统2的刹车/物理差异。

当前项目内存在两套载具系统：

- 系统2：`Carsystem` 主线，基于 `Rigidbody + WheelCollider + Carbilili`
- 系统1：旧车系统，基于 `VehicleController + CharacterController`

当前校园主线、NPC 选车生成、后续竞速/漂移扩展，优先沿系统2继续做。系统1主要作为参考和差异说明使用。

## 1. 目录与核心脚本

### 系统2（当前主用）

- `Assets/Carsystem/CarScripts/Carbili/Carbilili.cs`
  - 车辆驱动、转向、刹车、速度显示
- `Assets/Carsystem/CarScripts/InteractableVehicle.cs`
  - 靠近上车、下车、相机切换、驾驶 HUD 接入
- `Assets/Carsystem/CarScripts/Carbili/CarCamera.cs`
  - 跟车相机
- `Assets/Carsystem/CarScripts/CarSystemVehicleNetworkProxy.cs`
  - 系统2联机驾驶权与状态同步
- `Assets/Carsystem/NpcScripts/InteractableNPC.cs`
  - 车辆商店打开、车辆生成、联机请求生成
- `Assets/Carsystem/NpcScripts/VehicleDealerNPC.cs`
  - 车店老板入口、靠近提示、按键打开商店
- `Assets/Carsystem/NpcScripts/VehicleShopUI.cs`
  - 商店列表、详情、确认生成
- `Assets/Carsystem/CarScripts/VehicleData.cs`
  - 车辆商品数据资产

### 系统1（旧系统参考）

- `Assets/code/VehicleController.cs`
  - 旧车辆驾驶逻辑、刹车、相机、救援
- `Assets/code/VehicleHUD.cs`
  - 左下速度、右下教程、靠近提示 HUD
- `Assets/code/Multiplayer/VehicleNetworkProxy.cs`
  - 旧系统联机驾驶权与同步

## 2. 新车辆模型导入与预制体制作

## 2.1 导入前提

新车模型接入系统2前，建议先满足以下条件：

1. 车身模型与轮子模型尽量分离。
2. 至少能明确出四个代表性轮位：`前左 / 前右 / 后左 / 后右`。
3. 车辆根节点、视觉模型节点、轮子碰撞器节点不要混在一起。

推荐层级如下：

```text
Car_YourVehicle
├─ VisualRoot
│  └─ 车身模型、轮子视觉模型、装饰模型
├─ WheelCollider_FL
├─ WheelCollider_FR
├─ WheelCollider_RL
├─ WheelCollider_RR
└─ CarCameraRoot
   └─ Camera + CarCamera
```

其中：

- `Car_YourVehicle`：逻辑与物理根节点
- `VisualRoot`：只负责显示模型
- `WheelCollider_*`：只负责物理
- `CarCameraRoot`：只负责驾驶视角

## 2.2 为什么要单独分离车轮

系统2的 `Carbilili` 明确按四轮车写死：

- `wheelColliders[0] = 前左`
- `wheelColliders[1] = 前右`
- `wheelColliders[2] = 后左`
- `wheelColliders[3] = 后右`

并且 `wheelModels` 也只会按这四个轮位去跟随。

因此：

- 普通四轮车：直接绑定四个真实轮子即可
- 坦克/履带/多轮车：不要强行让所有可见轮都跟随这四个碰撞器

对于坦克、履带车、卡车多轮车型，推荐做法：

1. 用 4 个 `WheelCollider` 作为“代表性受力点”
2. 视觉模型整体放在 `VisualRoot`
3. `wheelModels` 可以留空，或只绑定 4 个简化视觉轮节点

否则常见现象是：

- 模型和物理不重合
- 复制来的轮子模型被 `wheelModels` 强行拉飞
- 车身对齐了，轮子视觉反而不对

## 2.3 创建 WheelCollider

在新车预制体根节点下创建 4 个空物体：

- `WheelCollider_FL`
- `WheelCollider_FR`
- `WheelCollider_RL`
- `WheelCollider_RR`

每个空物体挂 `WheelCollider`。

推荐原则：

1. `WheelCollider` 位置放在真实接地点附近
2. 左右要对称
3. 前轮略靠前，后轮略靠后
4. `radius` 与实际轮胎尺寸接近

如果模型不是标准轿车：

- 只要这四个碰撞点能稳定支撑整车即可
- 不要求和所有视觉轮子一一对应

## 2.4 视觉模型与物理不重合时怎么处理

不要优先整体移动 `Car_YourVehicle` 根节点去追模型。

推荐做法：

1. 保持根节点作为物理中心
2. 单独调整 `VisualRoot.localPosition / localRotation / localScale`
3. 单独摆 4 个 `WheelCollider`

也就是说：

- 根节点负责物理
- `VisualRoot` 负责显示对位
- `WheelCollider` 负责受力

## 2.5 常见模型问题排查

### `Missing Mesh / Missing Material`

说明原始模型资源或材质引用断了，不是脚本问题。  
需要重新导入原始模型，或者重做视觉层级。

### 生成后瞬间消失

通常是生成时被 `InteractableNPC.ConfigureSpawnedVehicle()` 判定为无效后立刻销毁。最常见原因：

- 缺少 `Carbilili`
- 缺少 `Rigidbody`
- `wheelColliders.Length < 4`
- 联机模式下缺少 `NetworkIdentity`

### 轮子模型乱飞

通常是 `wheelModels` 绑定了不适合当前四轮布局的视觉物体。  
优先检查：

- `wheelModels[0..3]` 是否真的对应四个代表轮位
- 多轮/履带车是否应该暂时留空 `wheelModels`

## 3. 各脚本挂载与 Inspector 配置

## 3.1 `Carbilili`

挂载位置：

- 挂在车辆根节点 `Car_YourVehicle`

必填字段：

- `wheelColliders`
  - 长度至少 4
  - 顺序必须是 `FL / FR / RL / RR`
- `motorTorqueMax`
- `steerAngleMax`
- `wheelRadius`

建议字段：

- `wheelModels`
  - 普通四轮车建议填 4 个
  - 坦克/履带车可留空，或只填 4 个简化轮
- `brakeForce`
- `forwardBrakeDeceleration`
- `coastStopDeceleration`
- `reverseTorqueScale`

说明：

- `Start()` 时会尝试自动修复 `wheelColliders`
- 但仍建议手动绑定正确顺序
- 若 `wheelColliders` 不足 4 个，车辆虽然可能被实例化，但会被生成流程判定为无效

## 3.2 `InteractableVehicle`

挂载位置：

- 挂在车辆根节点 `Car_YourVehicle`

主要字段：

- `carController` -> 根节点上的 `Carbilili`
- `carCamera` -> 子节点上的 `CarCamera`
- `carRigidbody` -> 根节点上的 `Rigidbody`
- `vehicleDisplayName` -> 靠近时显示的车辆名
- `interactKey` -> 默认 `F`
- `exitKey` -> 默认 `Escape`
- `interactDistance`
- `distanceBuffer`
- `interactPriority`

当前主线说明：

- 靠近提示优先走共享交互提示
- 驾驶 HUD 主要接系统1的 `VehicleHUD`
- `vehicleHud` 字段可以不手动依赖；当前主线不是靠 Carsystem 自带 HUD 来显示

## 3.3 `CarCamera`

挂载位置：

- 挂在子节点 `CarCameraRoot`
- 同物体上再挂一个 `Camera`

主要字段：

- `target`
  - 可为空，运行时会自动取父级
- `offset`
- `positionSmoothSpeed`
- `rotationSmoothSpeed`
- `cam`
  - 可留空，运行时自动取同物体上的 `Camera`
- `baseFOV`
- `maxFOV`
- `speedForMaxFOV`

可选字段：

- `blurMaterial`
- `motionBlurStartSpeed`
- `motionBlurMaxSpeed`
- `maxBlurIntensity`

## 3.4 `CarSystemVehicleNetworkProxy`

挂载位置：

- 挂在车辆根节点 `Car_YourVehicle`

前提组件：

- `NetworkIdentity`
- `NetworkTransformReliable`

说明：

- 该脚本自带 `RequireComponent`
- 如果是联机车辆，根节点上必须存在这两个 Mirror 组件
- 它负责：
  - 当前驾驶者占用状态
  - 驾驶 authority 分配
  - 客户端到服务端的车辆状态同步

## 3.5 系统2车辆预制体最低要求

一辆可正常被车店生成的系统2车辆，根节点至少应包含：

- `Rigidbody`
- `Carbilili`
- `InteractableVehicle`
- `CarSystemVehicleNetworkProxy`（联机推荐）
- `NetworkIdentity`（联机需要）
- `NetworkTransformReliable`（联机需要）

子节点至少应有：

- 4 个 `WheelCollider`
- 1 个 `Camera + CarCamera`

如果缺少以下任意项，容易出现“生成后立刻消失”：

- `Carbilili`
- `Rigidbody`
- 4 个 `WheelCollider`

## 4. 车店老板 NPC 创建与脚本参数配置

## 4.1 场景里需要哪些对象

一个完整的车店流程，至少需要：

- `VehicleDealerNPC`
- `VehicleShopCanvas`
- `CarSpawnPoint`
- 场景共享提示 `InteractPrompt_Shared`

如果不想手动一个个搭，也可以使用编辑器工具：

- `Tools/赛车/系统二替换系统一/一键配置当前场景`
- `Tools/赛车/系统二替换系统一/批量配置 Assets/scene 全部场景`

对应工具脚本：

- `Assets/Carsystem/Editor/CarSystemReplacementOneClickSetup.cs`

## 4.2 `VehicleDealerNPC` 配置

挂在“车店老板”入口对象上。

推荐字段：

- `disableSelfControl = true`
- `interactableNpc` -> 同物体或父物体上的 `InteractableNPC`
- `dealerDisplayName` -> 例如 `车店老板`
- `interactionTypeName` -> 例如 `选车`
- `unifiedInteractKey = F`
- `interactPriority = 45`
- `useSharedPrompt = true`
- `autoFindSharedPromptIfUnassigned = true`
- `interactDistance = 3.5`

共享提示引用建议：

- `sharedPromptPanel` -> `InteractPrompt_Shared`
- `sharedPromptNameText` -> `InteractNameText`
- `sharedPromptTypeText` -> `InteractTypeText`

说明：

- `disableSelfControl = true` 时，走统一 F 交互主线
- 不建议再走旧版 `E + 独立 Canvas` 逻辑

## 4.3 `InteractableNPC` 配置

挂在车店老板物体上，或者挂在它的父节点上。

必填字段：

- `vehicleShopUI` -> 场景中的 `VehicleShopCanvas`
- `spawnPoint` -> 场景中的 `CarSpawnPoint`
- `availableVehicles` -> `VehicleData` 资产数组

说明：

- `OpenShop()` 会打开商店
- `OnVehicleSelected()` 会根据 `availableVehicles` 查索引并生成车辆
- 重复选车时，旧车会被替换

自动查找逻辑：

- 如果 `vehicleShopUI` 为空，会尝试按对象名查找 `VehicleShopCanvas`
- 如果 `spawnPoint` 为空，会尝试按对象名查找 `CarSpawnPoint`

## 4.4 `VehicleShopUI` 配置

挂在 `VehicleShopCanvas` 下。

常用字段：

- `vehicleCardPrefab`
- `vehicleListContent`
- `detailImage`
- `detailNameText`
- `detailDescText`
- `detailSpeedText`
- `selectButton`
- `closeButton`
- `spawnMessageText`

推荐绑定：

- `Vehicle Card Prefab` -> `Assets/Carsystem/Prefabs/VehicleCard.prefab`
- `Vehicle List Content` -> `Scroll View/Viewport/Content`
- `Detail Image` -> 右侧详情图
- `Detail Name Text` -> 右侧名称文本
- `Detail Desc Text` -> 右侧描述文本
- `Detail Speed Text` -> 右侧速度文本
- `Select Button` -> 确认生成按钮
- `Close Button` -> 关闭按钮

文本类型要求：

- `detailNameText`
- `detailDescText`
- `detailSpeedText`
- `spawnMessageText`

都建议用 TextMeshPro 文本。

## 4.5 重要说明：商店列表真正以谁为准

运行时真正覆盖商店列表的是：

- `InteractableNPC.availableVehicles`

不是只改：

- `VehicleShopUI.vehicles`

原因是 `InteractableNPC.BindShop()` 会在运行时执行：

1. `shop.SetNPC(this)`
2. 把 `availableVehicles` 同步给 `shop.vehicles`

因此如果你只在 `VehicleShopUI` 里加了一个车，但没把同一个 `VehicleData` 加进 `InteractableNPC.availableVehicles`，运行时这个车会被覆盖掉，看不到。

## 5. `VehicleData` 资产制作与接入

## 5.1 创建 `VehicleData`

在 Project 视图中右键：

- `Create -> Racing -> Vehicle Data`

对应脚本：

- `Assets/Carsystem/CarScripts/VehicleData.cs`

字段说明：

- `vehicleName`
  - 商店名称
- `description`
  - 商店描述文字
- `thumbnail`
  - 车辆缩略图，必须是 `Sprite`
- `vehiclePrefab`
  - 车辆预制体
- `displayMaxSpeed`
  - 仅用于商店展示
- `accelerationRating`
  - 仅用于商店展示
- `handlingRating`
  - 仅用于商店展示

## 5.2 制作车辆预制体后如何接入商品

流程建议：

1. 先做好车辆 prefab
2. 创建对应的 `VehicleData`
3. 填好缩略图、名字、描述、预制体
4. 把该 `VehicleData` 拖进车店老板物体上的 `InteractableNPC.availableVehicles`

数据流如下：

```text
VehicleData.asset
-> InteractableNPC.availableVehicles
-> InteractableNPC.BindShop()
-> VehicleShopUI.vehicles
-> 商店列表显示
-> 选择后回调 InteractableNPC.OnVehicleSelected()
```

## 5.3 只改 ShopUI 不生效的原因

如果你遇到这种情况：

- `VehicleShopUI.vehicles` 里已经手动加了新车
- 但运行后商店里看不到

请优先检查：

- `InteractableNPC.availableVehicles` 是否也加了同一个 `VehicleData`

因为运行时 `BindShop()` 会重新覆盖 `VehicleShopUI.vehicles`。

## 6. 刹车与物理系统说明

## 6.1 两套系统的本质区别

### 系统1：`VehicleController`

特点：

- 基于 `CharacterController`
- 用 `_currentSpeed` 直接控制目标速度
- 刹车和减速更偏“游戏化”
- 手感容易做得很干脆

典型字段：

- `acceleration`
- `brakeForce`
- `forwardBrakeDeceleration`
- `coastStopDeceleration`
- `reverseAcceleration`

配套脚本：

- `VehicleHUD`
- `VehicleNetworkProxy`

适用：

- 校园内轻量驾驶
- 快速上手的老系统参考

### 系统2：`Carbilili`

特点：

- 基于 `Rigidbody + WheelCollider`
- 前轮转向，后轮扭矩驱动
- 刹车通过 `brakeTorque` 施加到后轮
- 更接近真实轮胎受力

典型字段：

- `motorTorqueMax`
- `steerAngleMax`
- `brakeForce`
- `forwardBrakeDeceleration`
- `coastStopDeceleration`
- `reverseTorqueScale`
- `wheelRadius`

适用：

- 车店生成流程
- 未来竞速系统
- 漂移、抓地力变化、物理扩展

## 6.2 为什么系统1更容易“快速刹停”

因为系统1本质上不是依赖真实轮胎扭矩，而是直接对 `_currentSpeed` 做加减和向 0 靠拢：

- 前进中按 `S`，直接减 `_currentSpeed`
- 松开油门，直接 `MoveTowards(..., 0)`

所以只要参数足够大，体感就会很利落。

## 6.3 为什么系统2刹车更容易显得慢

系统2虽然字段名看起来类似，但底层逻辑不同：

- 不是直接改车速
- 是把减速度换算成 `brakeTorque`
- 实际效果会受以下因素影响：
  - `Rigidbody.mass`
  - `wheelRadius`
  - `WheelCollider` 配置
  - 地面接触情况
  - 车型是否标准四轮

因此系统2刹车体感更依赖实际物理配置，而不是只调一个参数就能完全变成系统1手感。

## 6.4 当前项目为什么主用系统2

当前项目建议主用系统2，原因如下：

1. 已经接入车店老板 NPC -> 选车 -> 指定生成点的完整流程
2. 已经有系统2对应的联机代理 `CarSystemVehicleNetworkProxy`
3. 后续如果要做竞速地图、漂移系统、抓地力变化，系统2更适合作为底层继续扩展

系统1保留为参考用途：

- 参考它的 HUD 组织方式
- 参考它的快速刹车手感
- 参考它的旧场景兼容方式

但不建议：

- 在同一辆车上同时混挂系统1和系统2脚本

## 6.5 当前推荐策略

如果是校园主线接车：

- 用系统2做车辆接入
- UI 和流程沿当前主线走

如果想要系统1那种干脆刹车体感：

- 不要只看参数
- 要认识到系统1和系统2底层机制不同
- 后续应在系统2基础上继续做更“街机化”的刹车与转向增强

## 7. 系统1脚本与配置参考

系统1主要参考以下内容：

- `Assets/code/VehicleController.cs`
  - 旧车辆运动、刹车、相机、交互、救援
- `Assets/code/VehicleHUD.cs`
  - 左下速度、右下教程、靠近提示
- `Assets/code/Multiplayer/VehicleNetworkProxy.cs`
  - 旧系统联机驾驶权与状态同步

理解系统1时，重点看这些块：

1. 速度与刹车参数
2. CharacterController 移动方式
3. 共享交互提示接入
4. HUD 构建方式
5. 旧联机 authority 逻辑

用途建议：

- 作为旧系统参考
- 作为手感对照
- 作为 UI/HUD 组织参考

不建议再作为新车主线系统继续扩展。

## 8. 配套工具与参考文档

### 手工搭建参考

- `Assets/Carsystem/CarShopSetupGuide.md`
  - 更偏场景手工接线
  - 包含 `VehicleShopUI` 常见控件绑定方法

### 回归清单

- `Assets/Carsystem/赛车系统替换回归清单.md`
  - 更偏单机/联机验收
  - 适合改完场景或新接车辆后回归测试

### 一键迁移工具

- `Assets/Carsystem/Editor/CarSystemReplacementOneClickSetup.cs`

可用菜单：

- `Tools/赛车/系统二替换系统一/一键配置当前场景`
- `Tools/赛车/系统二替换系统一/批量配置 Assets/scene 全部场景`

该工具会：

1. 创建/复用 `VehicleDealerNPC`
2. 创建/复用 `VehicleShopCanvas`
3. 创建/复用 `Carcanvas`
4. 创建/复用 `CarSpawnPoint`
5. 自动绑定 `InteractableNPC / VehicleDealerNPC / VehicleShopUI`
6. 批量处理 `Assets/Carsystem/CarPrefabs` 下的车辆预制体联机组件
7. 停用旧系统 `VehicleController` 车辆

## 9. 最终验收建议

接入一辆新车后，至少检查以下项目：

1. `VehicleData` 是否能在商店中显示
2. 选择后车辆是否在 `CarSpawnPoint` 正常生成
3. 靠近车辆时是否有 `[F] 上车`
4. 上车后是否能切到车载相机
5. 左下速度与右下教程是否正常显示
6. `W / S / A / D / Esc` 是否正常
7. 再次选车时旧车是否被替换
8. 联机时是否具备：
   - `NetworkIdentity`
   - `NetworkTransformReliable`
   - `CarSystemVehicleNetworkProxy`

如果上述流程都通过，说明该车辆已经基本接入当前 Carsystem 主线。
