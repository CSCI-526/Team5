# 单场反向塔防玩法迁移

入口：`Assets/Rebuild/Scenes/SquadBattlePrototype.unity`，或 **Team5 > Rebuild > Open Squad Battle Prototype**。点击 Play，关闭 Gizmos 也能观察战斗。编辑器目前保留此场景供直接运行。

后续 HUD 更新：默认界面已替换为旧 Team5 样式，使用右侧速度滑条与 Edit Formation，编队面板通过 APPLY & RESUME 提交。下文调试窗口操作为迁移初版记录；最新 HUD 与验证说明见 [LegacyHud.md](LegacyHud.md)。

本轮只迁移战斗内运行规则。没有迁移旧地图、StageLayout、TowerSlot、XY 坐标系统、运行时静态地图生成、成长卡牌、关卡切换、教程、存档或网络统计。旧 Team5 未写入，没有执行 Git Commit/Push/Pull/Merge。

## 操作与配置

- 现有 Macro 地图、路线、装饰边界、静态 Mesh、全部 Transform 和用户摄像机直接沿用；单独场景保存战斗配置，原有 GridBattlePrototype / GridBattleMacroPrototype 场景逐字节保持不变。
- 默认九名成员用于空间与职业组合验证，并非强制的正式初始阵容。BattleSession 的 `members` 可配置 1..9 名成员，包含独立 memberId、slotId、职业、是否启用和生成时间。禁用成员即空位。
- 红色方块与菱形 = Attack，灰色方块与装甲标记 = Tank，绿色方块与白色十字 = Heal；颜色、边框和标记比例与 Edit Formation 共用同一外观定义。黄球 = 士兵弹丸，紫球 = 塔弹丸。死亡塔显示灰色，实体与占地仍保留，不把平台变成道路。
- 左上显示摘要，Pause/Resume 暂停或继续，Edit Formation 暂停并打开九槽位草稿。依次选择两格交换，Apply & Resume 原子提交；Cancel 丢弃草稿。Show Details 查看成员、塔 HP 等只读状态，Restart Battle 仅重建模拟。
- 所有计划成员生成完毕后才开放换位，避免待生成槽位的归属含混。支持延迟生成，用于检验失败条件；延迟成员加入当前移动中的编队锚点。
- 槽位编号 `slotId = localZ * 3 + localX`。世界 +Z 行显示在九宫格上方，没有旧 XY 映射。换位立即生效并重置视图插值，不模拟换位时互相穿行。
- 模拟暂停不修改全局 Time.timeScale。禁用表现组件不会重置或恢复用户已暂停的模拟。

## 核实的旧规则与明确适配

| 职业/参数 | 本轮基准 |
|---|---|
| Attack | HP 4、伤害 1、移动速度 0.5、射程 2.5、间隔 1 秒；边移动边攻击最近的存活塔，等距时沿用旧遍历最后候选的规则 |
| Tank | HP 8、减伤 20%，不攻击 |
| Heal | HP 3，每 2 秒治疗一次，每次 0.4 HP，不攻击，不治疗自己，不复活，不超最大 HP |
| 弹丸 | 速度 8，最多存续 5 秒；逻辑飞行与命中均由 Simulation 推进 |
| 测试塔 | HP 20、伤害 1、间隔 1 秒；保留现有地图的射程 4.5，未按地图倍数机械缩放；默认正方形（XZ 半边长 4.5），可在 BattleTower 配置四方向矩形与攻击宽度；Game View 使用半透明填充与边框显示相同的攻击区域，塔被摧毁后隐藏 |

数值依据旧项目 SquadRules、OffenseSoldier、OffenseWaveManager 与三个战斗场景的实际覆盖值。旧攻击射程字段默认 2，实际场景均为 2.5，本轮采用 2.5。

**用户明确选择：治疗保留八邻位覆盖效果，不保留旧绝对世界距离 1。**只按同队、存活、槽位行列距离各不超过 1 且非自身筛选，包含间距为 1 时世界距离为 √2 的斜角成员。旧死亡容差 0.001 不引入新模拟，HP 明确钳制至 0 并一次性判死，保持 Rebuild 生命契约。

## 状态与职责

- `MemberSpawnDefinition` 将稳定 MemberId、初始 SlotId 和生成计划分开。`UnitState` 保存成员 HP、冷却、进度与当前槽位；换位只改变映射和位置。成员 ID 不作为槽位索引。
- `BattleDefinition` 是从 Scene 捕获的不可变开战快照。可通行格集合也仅是从真实地块推导的只读快照，不是第二份可编辑地图。
- `FormationClearance` 枚举每名成员 1×1 占地沿各段路线的扫掠区域。SceneRoute 与反向模拟开战校验均检查九个槽位，包括空位；缺格、非法支撑和塔占地会拒绝开战。固定世界轴方阵，转角不旋转，也不切角。
- `BattleSimulation` 负责生成、移动、两侧选敌、冷却、弹丸、减伤、治疗、死亡、换位与结算。普通 C# 状态对外只读，无 MonoBehaviour / Renderer / Animator / Physics / UI 依赖。
- 塔追踪弹锁定 `(SquadId, SlotId)` 语义（本轮仅一队），每步读取该槽位当前存活成员；空位仍随队移动但不扣血。方向弹固定沿发射方向扫掠，只命中锁定槽位的合法占用者。士兵弹丸锁定塔身份。
- 实际伤害钳制至目标剩余 HP，触发伤害和死亡事件。金币系统已删除，攻击或摧毁塔不产生货币。
- `BattleCombatControls` 是可移除的调试显示和输入适配器，只提交暂停/换位命令，管理球体 View 与塔颜色。`UnitView` 只读位置插值。未来正式 UI/ProjectileView 可替换这些观察者，不改变伤害时机。

## 时序与结束条件

固定步顺序：生成到期成员 → 移动并优先结算到达 → 塔开火 → Attack 开火 → 弹丸命中/伤害/死亡 → 治疗 → 结算失败。新弹丸在发射所在固定步也推进一次。

任意一名成员到达立即锁定 Victory，再通知到达与结束观察者，并停止后续本步处理和未来生成；所有计划成员已生成且无人存活才 Defeat。当前无人存活但有人待生成时继续运行。终态只发生一次，已发射弹丸可以在发射者死亡后继续结算。摧毁所有塔不是胜利条件。

原始构造方式保持 DefendGoal 模式及原瞬时命中行为，原有波次原型继续可运行；成员计划构造方式明确启用 ReachGoal。没有将两种模式的阵营含义或胜负隐式混用。

本轮只支持一个最多九人的固定方阵、等高正交路线与静态地形，不增加多队避让、转阵动画或 A*。Step 仍是固定小步接口，不作为任意大步快进接口。运行中移动塔不自动重建地形合法性，Scene 布局应在 Edit Mode 编辑、下次开战重新校验。

## 验证

- 迁移前基线：EditMode 33 项，32 通过、1 失败；失败为用户调整镜头后现存的 `PerspectiveCameraIncludesAllTerrainAndBordersAt16By9`，未更改摄像机或放宽该断言。
- 迁移后 EditMode：51 项，50 通过，仍只有同一摄像机断言失败；新增 18 项全部通过。报告：`Logs/SquadBattleEditModeResults.xml`。
- PlayMode：14 项全部通过（含原有 11 项与新增 3 项）。报告：`Logs/SquadBattlePlayModeResults.xml`。
- 新增覆盖受伤后换位的 HP/冷却/身份不变、锁槽弹改命中新成员/空位、2 秒八邻位治疗、斜角治疗、禁止自疗/复活/超量、待生成成员不提前失败、首次到达只结算一次、拆塔/实际伤害钳制、方向弹扫掠、转角外侧阻挡、静态对象不复制及禁用全部表现后的精确状态一致。
- Game View 人工操作：受伤 Attack #101 与 Tank #102 交换后保持 HP/身份，继续运行约 51 秒到达 Victory，死亡 4、到达 1。默认阵容及当前地图仅供规则验证，不代表完成数值平衡。
- `Logs/CombatMigrationBackup/` 保存脚本备份、迁移前报告与原场景 SHA256。`SceneDelta.json` 证明独立场景仅改变三座塔配置、BattleSession 配置与组件列表，并增加操作组件；地形、路线、摄像机及 Transform 块一致。
