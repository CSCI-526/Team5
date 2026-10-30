# Grid Battle Prototype：战斗逻辑 + Minimal Presentation

本目录使用 `Team5.Rebuild` 下的独立命名空间与程序集，不依赖其他 Team5 项目的代码或资产。

当前正式流程的战斗入口为 `Assets/Rebuild/Scenes/Level1GridBattle.unity`，菜单 **Team5 > Rebuild > Open Level 1 Grid Battle**。这是旧 Level 1 的方块版，沿用现有 UI；Start Game 直接进入此关，固定一名 Captain，跳过 Upgrade 仓库，说明与验证见 [Level1Migration.md](Level1Migration.md)。

独立调试原型入口：`Assets/Rebuild/Scenes/SquadBattlePrototype.unity`，菜单 **Team5 > Rebuild > Open Squad Battle Prototype**。原型地图与用户摄像机保留；规则与历史验证见 [SquadBattlePrototype.md](SquadBattlePrototype.md)。下文原始两个原型的默认波次与塔防胜负语义继续保留。

战斗 HUD 已按旧 Team5 截图迁入Canvas、字体、速度滑条和编队面板；金币系统已删除，速度与编队入口向左上移动并等比缩小至 75%，替代默认显示的 IMGUI 调试窗口；复用来源与接口适配见 [LegacyHud.md](LegacyHud.md)。

单位外观已从 Capsule 更新为方形模型：战场与 Edit Formation 共用 `LegacyUnitLooks / SoldierLook` 的颜色、边框与职业标记（Attack 菱形、Tank 装甲方块、Heal 十字），材质使用现有 URP/Unlit 保持颜色不受光照改变。模型最大宽度 0.88，仍在一个 Small Cell 内；模拟、地图与摄像机不变。

新增双层网格测试场景为 `GridBattleMacroPrototype.unity`：36×24 Small Cell、3×3 Macro 分组、九格塔与九人编队。原场景保留。运行方式、查询 API 和限制见 [MacroGridPrototype.md](MacroGridPrototype.md)。

## 运行

1. 打开 `Assets/Rebuild/Scenes/GridBattlePrototype.unity`，或使用菜单 **Team5 > Rebuild > Open Grid Battle Prototype**。
2. 点击 Play，在 **Game View** 直接查看地图、三座塔和自动生成的士兵；无需打开 Gizmos。现有 Universal 3D Renderer 使用标准 URP/Lit 材质及一盏简单方向光。
3. 选中 `BattleSession`：Inspector 实时显示结果、生命数、生成/存活/死亡/到达数量、每个单位的 HP/位置/进度，以及每座塔的冷却和目标。Console 记录主要战斗事件。
4. 红色格子为 Spawn，青色为 Goal，棕色为 Road，蓝紫色为 TowerPlatform，绿色为 Ground，灰色为 Obstacle。蓝色 Cylinder 为塔，黄色 Capsule 为士兵。Gizmos 仍可选开启，显示路线顺序、三维射程和 HP 调试球。
5. `BattleSession` Inspector 可校验场景、在 Play 中重新开始战斗。重新开始只重置普通 C# 运行时状态。
6. `BattleDebugGizmos` 仅影响调试图形；`Minimal Presentation (Runtime Unit Views)` 上的 `BattlePresentation` 仅管理士兵模型。禁用或移除它们均不影响战斗。静态模型保存在原格子/塔的子物体 `Cell Mesh / Tower Mesh` 中，可在 Scene 编辑，不在 Play 中重建。

默认场景有 12×8 的基础格子、4 个堆叠格子、18 个有序路线格子、三座塔及一个备用平台。默认波次 12 个单位，HP 65，速度 1.4，间隔 0.65 秒，基地生命 3；塔射程 2.4，伤害 18，攻击间隔 0.8 秒。参数是测试值，不是数值平衡方案。

战斗网格外围另有三圈 `Decorative Border Tiles`：共 156 个 1×0.5×1 的深绿色 Cube，范围 X=-3…14、Z=-3…10。它们保存在 Scene 中独立于 BattleGrid 的同级节点下，没有 BattleCell、Collider 或运行时脚本，位于 Ignore Raycast 层，不参与战斗占位、路线或部署支撑判断，Play 不重新生成。

## 职责与依赖

```text
World ───────────> Simulation（仅使用定义和状态类型）
Session ─────────> World + Simulation
DebugPresentation > Session + World + Simulation
Presentation ────> Session + Simulation（只读状态与事件）
Editor / Tests ──> 对应运行时程序集
Simulation ──────> System + UnityEngine 数学值类型
```

- `BattleCell / CellKind`：Scene 中一个 GameObject 对应一个格子，保存类型；Transform 为位置唯一来源。格子物理尺寸为 1×0.5×1，Transform 的世界缩放与旋转保持单位值。
- `BattleGrid`：从 Scene 子物体重建临时 `Vector3Int(x, layer, z)` 查询索引，校验占位冲突、对齐、旋转和缩放。索引不序列化，不保存可独立编辑的地图。
- `SceneRoute`：只保存有序 `BattleCell` 引用；校验 Spawn/Road/Goal 类型、正交相邻、等高、无重复、无遮挡、属于正确网格。
- `BattleTower`：Scene 中真实的塔实体，Transform 为攻击位置来源，序列化范围、伤害、间隔、目标规则；绑定该实体对应的运行时 `TowerState` 供 Inspector 观察。
- `BattleDefinition / UnitDefinition / TowerDefinition`：从 Scene 读取的不可变开战快照，不是地图资产或第二套可编辑坐标。构造时复制输入集合并校验数值。
- `BattleSimulation`：普通 C# 类，负责生成、移动、选敌、冷却、伤害、死亡、到达及胜败。没有 GameObject、MonoBehaviour、Renderer、Animator、Physics、UI 或场景查找依赖。
- `UnitState / TowerState`：独立运行时状态，对外只读；视图不拥有修改 HP、位置或目标的权限。
- `BattleSession`：唯一 Unity 生命周期桥接组件，校验并读取 Scene 引用、创建模拟、在 FixedUpdate 推进固定步长、将原 Scene 塔的 Transform 送入模拟。不开辟逻辑塔 GameObject，不生成静态地图，不改写场景配置。
- `BattleDebugGizmos` 与 Editor Inspector：可删除的调试观察者，读取状态，没有战斗职责。
- `BattlePresentation`：绑定当前模拟及 `SimulationCreated`，根据生成、移动、死亡/到达事件管理运行时 `UnitView`，重启/禁用时解绑并清理，重新启用时从存活状态补建 View。
- `UnitView`：读取 `UnitState.Position` 的前后采样；LateUpdate 只对模型做固定步之间的线性显示插值，最多落后一个固定步，不推演移动或写回规则状态。Prefab 根节点为脚底，Capsule 子物体提供视觉高度。
- `MinimalPresentationSetup`：仅 Editor 的静态模型创作工具，向已有场景实体添加 Primitive Mesh 子物体并保存，保持原格子和塔的 Transform、引用和配置。所有视图均没有 Collider，材质使用共享资产，不逐帧创建材质实例。
- `PrototypeSceneBuilder`：仅 Editor 的一次性初始场景创作工具；已有场景不会被覆盖，也不会在 Play 中运行。正式编辑直接修改 Scene，不存在 Stage 同步步骤。

## 坐标与编辑

- 原生 Unity XYZ：X/Z 为水平坐标，Y 为高度；格子中心 Y = layer × 0.5，顶面 Y = 中心 Y + 0.25。
- 单位 Position 为脚底支撑点；塔 Transform 也是塔底支撑点。调试图形在显示时添加高度，不改动战斗位置。
- 路线只允许正交相邻的同层格子，不支持斜向、楼梯、斜坡、A*。格子上方紧邻一层存在地块时不可通行。
- 开战时从 `BattleCell.TopCenter` 读取路线快照。在 Edit Mode 改动后，下一次开战直接采用新位置；需要保持合法路线与占位。Play 中不动态编辑地形。
- 塔的初始位置从同一个 `BattleTower.transform` 读取；战斗中每步继续同步该 Transform，不存在独立视觉塔或坐标转换。
- 塔可在无遮挡的 TowerPlatform 顶面范围内自由移动，不吸附到中心；离开平台或悬空会报告配置错误。
- 静态格子的规则组件不依赖 Renderer 或 Collider。已有 `Cell Mesh` 子物体为 1×0.5×1 的 Cube，原格子 Transform 仍保持单位缩放；移动原格子时模型自动随父物体移动。模型和材质可独立编辑，删除模型也不改变占位。
- 场景已加入 Build Settings 首位，保留原 SampleScene。

## 模拟语义

模拟由调用者按固定步长推进（场景使用 Unity FixedUpdate，默认 0.02 秒）；`Step` 不是任意大时间跨度的离线快进接口。测试快进通过循环固定小步长完成。

每步顺序：按时间生成 → 沿完整折线路径连续移动并结算到达 → 各塔依顺序选敌、瞬时命中、扣血和结算死亡 → 结算胜败。同一步到达终点的单位先结算到达，不再受塔攻击。

射程使用完整三维世界距离。可选择最接近终点（累计路线距离最多）或距离最近；平局按先生成单位优先。塔初始冷却为零；无目标时维持就绪，有攻击才开始冷却，每步最多攻击一次。不同塔不会重复攻击已经死亡的单位。

HP 最低为零，DamageEvent.Amount 为实际扣除值（不包含溢出伤害）。死亡和到达是互斥的终态，各触发一次。达到基地生命损失上限立即失败，并停止后续模拟；此时可能保留尚未结算的存活单位以供结果观察。所有预定单位完成生成且没有存活单位、基地仍有生命时胜利。结束事件只触发一次。

原始配置与运行时分离。重启创建新模拟，不重建格子或塔；不修改共享资产。终结单位保留在状态集合中用于调试和结算，不再移动、受击或参与选敌。

## Minimal Presentation 与后续接入约定

第二阶段仅增加最小 Primitive Mesh 观察者，没有正式美术、动画、弹丸、特效或 UI。Scene 中静态模型可保存、重开和编辑；运行时只实例化士兵 Capsule Prefab。无需任何 Stage/Scene 同步或运行时地图生成。

新增静态地块/塔时，可手动复制其 Mesh 子物体；**Team5 > Rebuild > Apply Minimal Presentation** 也可为现有实体补齐简单模型，该菜单只在 Edit Mode 执行。修改 CellKind 后可为其 Mesh 手动指定对应颜色材质，或使用该菜单重新按类型分配材质。菜单不生成 BattleCell、BattleTower 或路线，不调用战斗规则。

未来视图通过 `BattleSession.SimulationCreated` 取得每次新模拟，绑定 `UnitSpawned / UnitMoved / UnitDamaged / UnitDied / UnitReachedGoal / TowerAttacked / BattleEnded`，并读取只读状态。晚绑定时先遍历当前 `Units / Towers` 重建显示；重启、禁用或销毁视图时取消旧模拟的订阅。

事件是值快照，包含实体 ID、世界位置及对应的伤害、HP 或结果信息。可通过 ID 维护 View 映射，用相邻固定步的状态做显示插值。地形模型放置在现有格子上；塔外观附加到现有塔实体；单位 Sprite/Spine、朝向、动作、生命条、选中信息面板读取状态即可。

`TowerAttacked` 负责触发未来攻击/弹丸表现；`UnitDamaged`（携带 `DamageEvent`）表示模拟已经结算的伤害。ProjectileView 的到达、动画或特效回调不扣血，不修改目标，不影响死亡时机。技能若在后续实现，规则进入 Simulation，视图只消费其结果。

## 验证与当前边界

Unity Test Runner 中运行 `Team5.Rebuild.EditModeTests` 与 `Team5.Rebuild.PlayModeTests`，也可依次使用 **Team5 > Rebuild > Run Prototype EditMode Tests / Run Prototype PlayMode Tests**。报告保存在项目 `Logs/MinimalPresentationEditModeResults.xml` 与 `Logs/MinimalPresentationPlayModeResults.xml`；详细覆盖与结果见 `Validation.md`。

本阶段不含正式地形/角色美术、动画、特效、HUD、技能、编队、楼梯、寻路或专门渲染系统。测试并存数量用于逻辑正确性验证，不代表已完成性能基准。单位 View 当前直接实例化和销毁，没有对象池。当前移动遍历所有单位、每座就绪塔遍历存活单位；需要更大规模时可按实际测量再引入活跃列表或空间索引。

下一阶段应根据原型验证反馈另行确定；正式美术、技能、多路线及复杂 UI 均未开始。

完整场景流程入口：Assets/Rebuild/Scenes/StartGame.unity（菜单 Team5 > Rebuild > Open Start Game）。Start Game 直接进入 Level1GridBattle（单 Captain，跳过 UpgradeScene），结算进入 EndGame，Play Again 返回标题；适配说明见 [MenuScenes.md](MenuScenes.md)。

运行时单位点选：左键选择单位后，左半屏显示实时详情，右半屏中心临时缩放聚焦；Esc、X 或场景空白恢复原视角。说明见 [UnitInspection.md](UnitInspection.md)。
