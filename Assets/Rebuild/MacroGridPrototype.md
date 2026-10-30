# Small Cell / Macro Cell 扩展原型

扩展场景：`Assets/Rebuild/Scenes/GridBattleMacroPrototype.unity`。通过 **Team5 > Rebuild > Open Macro Grid Prototype** 打开，或直接打开该文件。Game View 使用 16:9，点击 Play 生成一个九人小队。原 `GridBattlePrototype.unity` 保持原样；实施前快照位于 `Logs/MacroGridBackup/`。

## 场景尺度

- 36×24 = 864 个基础 Small Cell，另有 36 个高层地块。每个原 BattleCell 被细分为九个独立、保存在 Scene 中的 BattleCell。
- 原格坐标 `(x, layer, z)` 对应 Small Cell `(3x+dx, layer, 3z+dz)`，dx/dz = 0、1、2。高度层不乘三，仍为 0.5；每个地块 Mesh 仍为 1×0.5×1，规则对象和父物体缩放均为 1。
- 原三圈装饰边界同样细分，成为九个 Small Cell 宽（仍对应三圈 Macro），共 1404 个纯视觉 Cube。它们不属于 GameplayGrid，没有 BattleCell、Collider 或脚本。
- Macro Cell 没有 GameObject、序列化位置或地块副本；它是九个实际 Small Cell 的查询分组。

## 查询接口与占用

`BattleGrid.RebuildIndex(errors)` 后，可以调用：

```csharp
var macro = BattleGrid.ToMacroCell(smallCoordinate); // X/Z 向下除以 3，Y 保持高度层
var cells = grid.GetMacroCells(macro);              // 恰好九个槽位，缺格为 null
var states = grid.GetMacroOccupancy(macro, simulation);
```

九格按 `index = localZ * 3 + localX` 排列；Macro `(mx, layer, mz)` 的中心是 `(3mx+1, layer*0.5+0.25, 3mz+1)`。负坐标也使用向下取整分组。

`BattleSimulation.GetCellOccupancy(Vector3Int)` 返回只读 `TowerIds / UnitIds`，从塔的实际运行时位置和存活单位位置推导，不维护另一份地图。死亡和到达单位不再占格。`UnitState.OccupiedCell` 使用半开边界映射，避免 Unity 的偶数舍入在正好半格时把两个成员算到同一格。

选中 GameplayGrid，可在 Inspector 输入 Macro 坐标，查看九个真实地块引用和运行时占用；选中 BattleSession 可查看每名士兵的 SquadId、FormationSlot、HP、位置和占格。

## 塔的九格占地

扩展场景的三座 `BattleTower` 启用 `usesMacroFootprint`，`FootprintSize=3`，Transform 位于 Macro 中心的支撑面上。开战校验会逐格检查九个位置均存在、均为 TowerPlatform、顶面无遮挡且支撑高度一致，并拒绝塔间重叠与非 Macro 中心位置。

在 Edit Mode 移动原塔到其他合法 Macro 中心，下一次 Play 从该 Transform 重新读取并校验；没有 TowerSlot 或同步按钮。备用平台对应旧 `(1,5)`，新中心为 `(4,0.25,16)`。

攻击仍沿用现有选敌、冷却、瞬时扣血、DamageEvent 和死亡结算。射程设为 **4.5 世界单位**，而非原 2.4 的三倍 7.2；伤害 18、间隔 0.8 保持。原场景的塔默认仍是单格模式，以保留旧原型和回归测试。

## 九人队形与道路

BattleSession 的 `formationSize=3`、`unitCount=9`。九名单位在同一步生成，分别拥有自己的 HP、UnitLifeState、FormationSlot 和死亡事件。槽位 `(0..2,0..2)` 对应世界偏移 `(-1..1,0,-1..1)`。

采用固定世界轴对齐的方阵：成员沿相同 Scene 中心线、加各自固定偏移移动，转弯时不旋转阵型。成员死亡后该槽位空出，其他人不补位。当前位置由 Simulation 计算，现有 UnitView 继续只读并插值，不包含移动规则。

本阶段三格编队模式只支持一个小队、最多九名成员，避免把多队在转角处的拥堵避让混入本次范围；旧单格模式继续支持原多单位波次。

SceneRoute 仍只保存真实 BattleCell 引用，扩展路线包含 52 个相邻 Small Cell 中心节点。`requiredWidth=3` 时，校验每个中心周围完整 3×3 道路支撑区域，并要求所有中心节点相邻、等高。相邻节点周围的方形区域覆盖整个轴向移动扫过的区域，包含转角，不允许阵型边缘进入墙、塔位、缺格或被上层地块遮挡的区域。Spawn / Goal Macro 内的连续起终点格也允许作为中心线节点。

在 Edit Mode 改动道路的 Transform 或类型后，下一次开战重新校验并读取位置；不从其他坐标资产同步。无效布局会阻止开战，而非让显示模型自行绕障碍。Play 中动态修改地形、换阵和多队避让不在本阶段范围内。

## 摄像机

沿用源场景 Perspective Camera 的朝向和 FOV，使用所有静态 Mesh（包含装饰边界）的世界包围盒，按 16:9 的水平/垂直视锥分别计算最小距离并加留白。Y 高度来自这一几何计算，未直接乘三；裁剪距离也随包围盒调整。没有新增摄像机演出或运行时地图生成器。

## 验证入口

**Team5 > Rebuild > Run Prototype EditMode Tests / Run Prototype PlayMode Tests** 包含新旧原型的测试。新增覆盖：864 个基础格、每 Macro 九格、九格塔支撑与重叠、队形占格与转角扫掠、死亡留空、Scene Transform 改动及保存重开、16:9 Perspective 取景、静态对象不复制，以及禁用表现层后的战斗一致性。

`MacroPrototypeSceneBuilder` 仅为一次性 Editor 创作工具，已有扩展场景不会被覆盖，也不在 Play 中调用。此后直接编辑 Scene。

已完成 11 项 PlayMode 回归测试（包含两个扩展场景用例），报告为 `Logs/MacroGridPlayModeResults.xml`。曾发现扩展场景的 `BattlePresentation.unitPrefab` 为空，导致模拟在运行但没有士兵模型；现已重新绑定保存的 Capsule Prefab，并让创建工具在切换 Scene 后按资产路径重新加载和校验引用。使用用户调整后的摄像机实际运行结果为 Victory，击杀 2、到达 7，零警告和错误。修复只改变了表现层的 Prefab 引用，摄像机及其 Transform、URP 相机组件逐字节保持不变；记录为 `Logs/UnitPrefabRepairValidation.json`。
