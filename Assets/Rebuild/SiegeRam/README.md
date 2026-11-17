# Scene 1 破城车召唤与路线规划

## 使用

第一格右上角显示库存方形角标（默认 `x1`）。在 Scene 1 的 BattlePresentation 对象上找到 `Siege Ram Fleet`，修改 **Initial Quantity** 可设置每场战斗的初始数量（最小 0，重新 Play 生效）。召唤成功立即扣除 1；被占用等失败情况不扣除；Esc 取消未出发车辆会退回数量。库存归零时图标变暗且按钮无法点击，新一场战斗重置为配置数量。

打开 `Assets/Rebuild/Scenes/1.unity` 并 Play。左侧原三个编队按钮下方，新增三个同列正方形格子，边长是原按钮的 **1.5 倍**。第一格显示用户原图的精确水平镜像，后两格为空且不可点击。

1. 先把原有小队移出红色起点区域。只要任何存活士兵的逻辑碰撞圆仍碰到红色 Spawn 区域，或者已有破城车仍占着那里，召唤就会被拒绝，并显示提示。
2. 点击第一格，在路线首个 Spawn 格中心生成破城车。游戏进入暂停规划状态，只有起点地块、新车、规划路线和提示保持明亮。其他场景对象与 UI 变暗，UI 点击和原小队的点击移动/检查被阻止；视图若处于人物检查状态，会先退出检查并恢复镜头。
3. 从车体或车体所在位置按住左键拖出路线。第一段连接拖拽方向上相邻的 3×3 区域中心（区域从 Cell 0..2 开始，中心为 1、4、7…；当前位置偏离中心时允许第一段斜向对齐），随后每步固定 3 格、水平/竖直移动和 90° 转弯。起点以外不能起笔；路线可以穿过木门，但不能跨越石墙、塔、障碍、空格或不等高地面。实际移动碰到未摧毁的木门后停下撞击：默认每次 1 点伤害、间隔 1 战斗秒，门被摧毁后沿原路线继续。可在 Siege Ram Fleet 的 Gate Damage / Gate Attack Interval 调整；暂停不攻击，倍速同步。
4. 松开确认有效路线。画面和交互恢复，车辆沿独立路线移动，走过的路线消失。到达终点后车辆停留在那里。
5. 无效或未到达首个 3×3 中心的路线会留在规划模式，重新从车体起笔即可。Esc 取消首次召唤、移除尚未出发的新车并恢复起点。
6. 非规划状态下，左键点击地图中的破城车（移动或停下时均可），暂停并从当前位置重新画路线。确认后替换原路线，不消耗库存；Esc 取消重画时保留该车、原路线、攻击冷却和库存。点击车体优先于小队的选中或地面移动操作，UI 点击及其他规划状态不会触发。

原先手动暂停的状态会被保留：若召唤前已经暂停，确认路线后仍暂停，点击 Resume 才继续。车辆按战斗实际流逝时间更新，因此暂停、1×/2×/3× 速度和原战斗时间倍率都会生效。规划时使用 Time.timeScale=0 和模拟暂停双重停止，结束/取消/禁用/重启时恢复原值。

## 本次范围

车辆是独立移动载具，不占用士兵的 MemberId、SlotId 或 3×3 编队，不重写原小队目的地。仅增加木门接触攻击，不添加车辆血量、石墙伤害、费用或胜负判定。当前底盘逻辑半径 0.42 格、移动速度 0.48 世界单位/战斗秒，视觉宽度约 2.2 格。逻辑底盘与绘制尺寸独立。

第一辆车离开起点后可以再次召唤；已有车在新规划期间也暂停并变暗。静态地图检查使用现有 BattleGrid 和模拟塔/墙占格，已摧毁墙可通行。路线不会自动寻路；玩家需要手动画出绕行路径。

原 Scene 1 的四种职业模型、地图、Camera、路线、编队和战斗配置保留。唯一场景结构变更是在 BattlePresentation 所在对象添加 `SiegeRamFleet` 组件。修改前的完整场景为 `Logs/SiegeRam/Scene1BeforeSiegeRam.unity`。

## 资源与实现

- `SiegeRamIconOriginal.png`：原始图片，不修改像素；UI Image 的 X 缩放为 -1，实现精确镜像。
- `SiegeRamTransparent.png`：内置 imagegen 去背景的世界精灵素材，生成源另有保留。AI 编辑可能造成小细节差异；地图中的 SpriteRenderer 执行水平翻转，默认朝右。
- `ArtProvenance.json`：工具与完整提示词记录；没有外部付费 API、Spine 或新依赖。
- `SiegeRam2D.prefab`：静态相机朝向的透明 2D 车体，沿用现有世界 Z 材质/排序；没有旧方块模型、刚体或动画 Root Motion。世界贴图启用 mipmaps 和三线性过滤。
- `Assets/Rebuild/Scripts/Simulation/SiegeRamMotion.cs`：独立位置、正交路线验证、路段推进和动态阻挡停止。
- `Assets/Rebuild/Scripts/Presentation/Minimal/SiegeRamFleet.cs`：三个按钮、起点占用检查、实例管理、暂停规划、变暗与恢复、指针路径处理。
- `Assets/Rebuild/Scripts/Presentation/Minimal/SiegeRamView.cs`：朝向、镜像、排序及剩余路线。
- `Assets/Rebuild/Editor/SiegeRamBuilder.cs`：一次性生成 Prefab/导入设置并绑定 Scene 1，拒绝覆盖已存在资源。

Sprite 使用 SpriteRenderer.color 变暗，避免属性块破坏现有 GPU SpriteSkin。其他 Mesh/LineRenderer 保存并恢复原属性块；UI 保存并恢复 CanvasGroup 状态。恢复后不会改变四个职业的发色。

## 验证

以下结果来自数量角标加入之前的版本。数量和木门功能已补充库存扣除、归零禁用、失败不扣除、取消退回、路线跨木门、接触伤害、暂停不攻击、石墙免疫、木门死亡事件与恢复通行的测试断言；由于当前 Unity Editor 占用项目，尚未重跑编译与这些断言。

此前 Unity 6000.3.22f1 编译通过。

- Edit Mode **2/2 通过**：直角/3 格路线、依次移动、拒绝斜线/短路线/跨障碍路线、移动中遇阻停止。
- Play Mode **3/3 通过**：起点占用拒绝、三个按钮尺寸和镜像、通过 EventSystem 指针事件点击按钮、同一生产屏幕坐标入口拖拽 3 格加 90° 转弯、规划暂停/变暗/交互阻断、松手独立移动、原小队目的地和 Id/Slot 保留、已走路线清除、无效路线、取消移除及重复召唤、原暂停状态和发色恢复，以及原 Scene 1 单位移动回归。

测试没有自动操控桌面 GUI；屏幕指针入口与实际 Mouse 更新共用 `ProcessPointer`。无界面测试进程无法可靠处理硬件合成鼠标队列，因此按钮使用 EventSystem 的 PointerDown/Up/Click，路线使用相同屏幕坐标处理入口。实体鼠标/键盘的人工操作未执行。

结果：`Logs/SiegeRamEditMode.xml`、`Logs/SiegeRamPlayMode.xml`。预览：`Logs/SiegeRam/NormalSlots.png`、`Planning.png`、`AfterMove.png`。UI 预览通过测试中的临时相机模式渲染，渲染后恢复；不修改游戏 Camera 配置。

`Logs/SiegeRam/DeliveryChecks.json` 记录了场景序列化检查：所有原场景记录保留，已有记录只增加组件挂接，新增记录为该组件和引用包装。未执行 Git Commit / Push / Pull / Merge。

## Level3 木门引导

`Level3GridBattle` 使用独立的新增红色起点（中心 `(7, 10)`）。小队沿原路线前进，所有存活成员在木门前停止至少 0.4 战斗秒后，显示左侧三个支援格和简短提示。普通暂停、检查人物、石墙阻挡或单纯靠近木门不会解锁。

点击破城车图标后从新增起点召唤，沿用 Scene 1 的库存、暂停规划、起点保护、三格路线、木门接触伤害与点击车辆重新规划。新增起点以外的 Spawn 不影响该车的占用检查，也不会在规划时保持明亮。木门摧毁后，小队自动继续原有路线。重新开始战斗时，支援入口重新锁定，库存重置。

配置位于 Level3 的 `Minimal Presentation (Runtime Unit Views)` → `Siege Ram Fleet`：`Support Spawn Center` 指定起点中心格，`Unlock After Wooden Gate Stop` 启用挡门引导。两项均保留默认值时，Scene 1 行为不变。

本次 Level3 接入已在 Unity 6000.3.22f1 实际编译并通过 **3/3 Play Mode 测试**：新增 Level3 完整挡停→提示→独立起点召唤→规划穿门→接触破门→小队恢复→重开重置流程，以及 Scene 1 原有两项破城车回归。Level3 测试仅在内存中使用单个 Captain 并关闭敌塔射程，以隔离破门机制，不保存这些测试配置；没有验证关卡整体难度和平衡。提示与规划阶段已实际渲染检查，未操控桌面 GUI。结果及预览位于 `Logs/Level3SiegeRam/FinalPlayMode.xml`、`GateGuide.png`、`Planning.png`、`GateDestroyed.png`。原场景记录检查证明，已有对象仅新增组件挂接，玩家编辑的地图、木门、路线与战斗参数保留。

木门伤害规则：只有 `TryRamWoodenGate` 通过距离、暂停状态和木门类型检查后的破城车接触可以扣血。普通和爆破类士兵均不会选木门为攻击目标；伤害结算也拒绝所有非破城车来源。石墙、普通敌塔和 Minion 的伤害规则保持原样。

木门仅受破城车伤害的规则已验证：Unity 6000.3.22f1 编译正常，Edit Mode 34/34、Play Mode 3/3 通过。覆盖普通/爆破士兵免伤、破城车扣血和清除阻挡、Minion 与石墙回归、Level3 挡门引导及 Scene1 破城车流程。结果：`Logs/GateRamOnly/EditMode.xml` 与 `Logs/GateRamOnly/PlayMode.xml`。

### 挡门提示的帧率修复

原版本错误地在没有 FixedUpdate 的画面帧（模拟时间差为 0）清空挡停计时，导致正常高帧率下无法累计到 0.4 战斗秒。现在无模拟推进的帧保留计时，实际暂停、重新移动、离开木门或战斗结束才重置。回归场景改为六种职业的六人小队，并在每次战斗推进之间插入两帧纯画面更新；旧版本已在此测试中复现不解锁（`Logs/GateGuideCadence/BeforeFix.xml`）。

修复后 Unity 编译与 Play Mode 3/3 通过（`Logs/GateGuideCadence/AfterFix.xml`），六人编队在含空白模拟帧的条件下正常解锁按钮和提示，并完成召唤、破门及恢复移动；Scene1 两项原有回归也通过。实际渲染已确认提示和按钮可见。

挡门提示显示期间，破城车图标以约 1 秒为周期平滑明暗闪烁，透明度保持在 35%～100%，数量角标保持清晰。闪烁使用真实时间，暂停不会停止；成功召唤并进入规划后立即停止，库存归零仍使用原有变暗效果。未成功的召唤不会关闭提示和闪烁，Scene1 无挡门提示时不会闪烁。

Level3 每次确认有效破城车路线、退出规划模式后，立即显示底部提示：`If your path doesn't reach the wooden gate, click the siege ram to draw a new one.` 提示不暂停或阻止交互，8 秒后自动隐藏；提前点击车体重新规划时立即隐藏，不重新触发召唤图标闪烁。取消规划或提交无效路线不显示这条提示。

Level3 破城车路线仅允许向右（世界 +X，对应当前镜头的屏幕右侧）水平延伸，不能向左或上下移动，也不能拐弯。鼠标上下偏移会投影到车体所在的水平路线；向起点拖回可缩短或清除当前草稿。首次对齐 3×3 区域中心及后续三格步长继续生效，重新规划也遵循同一方向限制。路线确认时再次校验所有路段必须向右。Scene1 仍支持原有四向直线和 90° 转弯，地图和起点不变。
