# 方块版 Level 1

教程第 3 步与第 4 步换位成功后的最后说明统一聚焦 Captain 与对应塔的世界位置中点，根据两者实际模型边界计算临时镜头距离（兼容 Perspective / Orthographic），同时容纳完整单位和塔并避开下方提示区域。未修改场景中保存的摄像机参数；第 2 步仍保持正常关卡视角。离线编译通过（0 错误），报告 `Logs/CompactLevel1Backup/TutorialPairFocusCompile.log`；未操控 Unity 界面验证。

教程第 4 步等待点击小队时，在 Team1 图标右侧显示白色填充、黑色实边的左指箭头。箭头用普通 UI Image 线条绘制，黑色底边与白色内线叠加；使用屏幕坐标转换对齐目标并保持在教程 Canvas 最上层，按非缩放时间在 0.8～1.15 倍间循环缩放，尖端保持指向图标，不拦截点击。打开编队后隐藏，错误操作需要重新打开时再次显示；讲解完成、Skip 或禁用后清理。离线编译通过（0 错误），报告 `Logs/CompactLevel1Backup/TutorialArrowDisplayCompile.log`；未操控 Unity 界面验证外观。

仅 Level1 的 levelTimeMultiplier 设为 2，其他场景默认 1。全局基础常数仍为 0.8，所以 Level1 的 1/2/3 档对应原始模拟时间的 1.6/3.2/4.8 倍；聚焦时固定为 Level1 新基础的 0.1 倍（原始时间 0.16 倍），不受滑条叠加影响。暂停与教程镜头/金光的非缩放动画不变。离线编译和 16 项检查通过，含关卡倍率 1/2、滑条三档、聚焦与暂停组合。报告 `Logs/CompactLevel1Backup/LevelSpeedCompile.log`、`LevelSpeedRules.log`，未操控 Unity 界面验证。

Level1 结算修复：过去只有从 StartGame 创建的 BattleSceneFlow 会订阅胜负事件，直接 Editor Play Level1 会判胜但不离开场景。现在通过运行时 sceneLoaded / 首场景初始化为真实 Level1 自动创建并绑定唯一流程对象，Captain 到达终点后下一帧异步进入 EndGame。原 SquadBattlePrototype 仍保持独立调试。新增直接 Level1 进入 EndGame 且只跳转一次的 PlayMode 回归测试；离线编译、保存场景检查和 16 项规则检查通过（含实际单 Captain 到终点仅结算一次），报告 `Logs/CompactLevel1Backup/SettlementCompile.log`、`SettlementRules.log`。未执行 Unity PlayMode 测试或操控界面验证跳转。

## 当前缩小版地图

按地图平面 X/Z 约缩短 50% 调整：39×27 取整为 20×14，共 280 个 Small Cell；每格仍为 1×0.5×1，未缩放世界或地块模型。地块坐标 X=9..28、Z=6..19，尽量保持原地图中心，摄像机所有保存参数和位置均未修改。外围三圈装饰共 240 格，仍不参与战斗逻辑。

当前路线为 (10,0.25,16) → (19,0.25,16) → (19,0.25,7)，共 19 个真实格子引用，三格宽并通过九槽位扫掠检查。只保留第一座塔，位置为 (22,0.25,16)，原 HP、伤害、射程和方向不变；原第二座塔在缩小后的对应位置 (19,0.25,7) 改为 3×3 终点，移除原右侧尾段。固定单 Captain、当前 UI 和每次触发教程保持不变。

教程根据一座塔自动改为五步，跳过第二塔说明。实际地图数据的离线模拟验证：Captain 承受一次伤害后换到安全行，可直接到达新终点，以 5.5 HP 胜利。16 项规则检查、离线编译和保存场景检查通过，未操控 Unity 界面验证。原图与教程备份、报告位于 `Logs/CompactLevel1Backup/`。此次编辑工具为 `Tools/Rebuild/compact_level1.py`，重复运行会拒绝再次缩小。下文原移植尺寸和双塔流程为历史基线。

## 适配后的 Level 1 教程

教程高亮使用对应物体自身的金色明暗脉冲：Captain 的身体与外框发金光但保留白色王冠，塔本体显示蓝色明暗脉冲，终点地块保持金色脉冲；引导小队按钮时，按钮底色自身脉冲。已移除原黄色世界方框和按钮外方框。阶段切换、Skip、禁用、重开会恢复原 Renderer 属性块或按钮颜色，不修改共享材质、地块类型或战斗状态。脉冲使用非缩放时间，暂停讲解时仍可见，不新增 Bloom 或后处理。离线编译通过（0 错误），报告 `Logs/Level1Migration/TutorialGlowCompile.log`；未操控 Unity 界面验证外观。

Level1GridBattle 的 BattleSession 对象上新增可禁用的 LevelOneTutorial。参考旧 HowToPlay 六步冻结画面教程，复用当前 UI 工厂、镜头和真实编队编辑器，不依赖旧项目脚本。

1. 自动前进的单 Captain：金色王冠、HP 6、50% 减伤、攻击 0.5。
2. 敌方蓝塔：固定方向和蓝色射线区域。
3. 等到 Captain 实际受伤后暂停，展示当前真实 HP，替换旧金币奖励说明。
4. 高亮生命值下方的小队按钮，要求玩家实际打开左侧 3×3 编队，将 Captain 移到安全行并应用；错误换位或取消不会自动跳过。
5. 接近第二塔射程之前提示上射方向，必要时要求改到安全列；若此前已处于安全列，解释后直接继续。
6. 距终点约六个世界单位时提示胜利条件，继续后恢复正常战斗。

讲解期间只暂停 Simulation，镜头动画使用非缩放时间。暂停/速度按钮暂时禁用，编队交互继续有效。编队编辑会保留教程暂停，安全换位完成后才释放；不直接写 HP、槽位、冷却、移动或胜负。讲解面板位于右下方，避开左侧编队的 Apply 按钮。Skip、禁用教程或重开战斗会清理讲解并恢复其拥有的暂停/镜头/输入控制。旧项目的金币、卡牌、Firebase 和教程倒带状态恢复未移植。

每次进入或重新开始 Level 1 都自动触发完整教程；完成和 Skip 只结束本次教程，不再读取或写入旧 PlayerPrefs 完成记录。旧记录即使存在也不影响触发。Play 中可在 LevelOneTutorial 组件右键菜单选择 **Replay Level 1 Tutorial**：重新开始当前 Level 1 并重播。关闭该组件即可关闭自动教程，不影响独立战斗规则。

验证：当前全部 Rebuild 源码和测试离线编译通过（0 错误）；16 项命令行检查通过，其中真实场景数据的单 Captain 引导操作依次经历真实受伤、避开横向射线、避开纵向射线，以 5.5 HP 胜利，未补血或生成其他单位。静态场景/资源引用与固定名单检查通过。新增 Skip / Replay / 禁用后的控制恢复 PlayMode 测试，无关菜单流程测试显式禁用教程组件，不再依赖完成记录。未执行 Unity 测试或操控界面验证。报告 `Logs/Level1Migration/TutorialCompile.log`、`TutorialRules.log`。

入口场景：`Assets/Rebuild/Scenes/Level1GridBattle.unity`，菜单 **Team5 > Rebuild > Open Level 1 Grid Battle**。
当前主流程为 StartGame → Level1GridBattle → EndGame，跳过 UpgradeScene。StartGame 仍为构建首场景，SquadBattlePrototype 保留作独立调试。

Level 1 现固定使用独立的 Level1Captain.asset，仅 Captain #110、中心槽位 4、立即生成。Scene 的 useSceneRosterOnly 开启，禁止 SetPreparedMembers 覆盖固定名单，流程也不注入仓库部署。Captain 的 HP 6、减伤 50%、攻击力 0.5 保持原值。PrototypeSquad 与 UpgradeScene 仓库仍保留，不影响调试原型。新增配置离线编译通过（0 错误，15 个序列化字段警告），16 项离线规则检查通过；报告 CaptainOnlyCompile.log / CaptainOnlyRules.log。菜单与场景测试已调整，未执行 Unity 测试或操控界面。

## 旧项目核对与尺度

只读参考旧 Team5 的 `Defense/Scenes/DefenseSandbox.unity`，其 BoardView.layout 指向 `Defense/Data/DefaultStage.asset`（原 Level 1），不是六塔练习关 Level1PracticeStage。
旧路线为 (-6,3) → (0,3) → (0,0) → (0,-3) → (6,-3)。导入时每个旧水平布局单位对应三个 Small Cell，并平移为非负网格坐标；导入完成后，只使用 Scene 中真实 BattleCell 的 X/Z Transform 和 SceneRoute 引用，不保留旧坐标系统的运行时依赖。

- 地图：39×27，共 1053 个 1×0.5×1 地块，全部直接保存在 Scene 中；外围三圈共 432 个不可交互装饰地块。
- 路线：55 个按顺序排列的 BattleCell 引用，三格宽，转弯处满足全部九个槽位的扫掠范围。
- 出生中心：(1,0.25,22)；转弯中心：(19,0.25,22)、(19,0.25,4)；终点：(37,0.25,4)。
- 两座塔分别位于 (22,0.25,22)、(19,0.25,1)，各占一个完整 3×3 Macro Cell，支撑格不与道路重叠。
- 旧塔向左 / 向上，原射程 3 / 5；为保留关卡比例，新场景对应 -X / +Z，射程 9 / 15，弹道宽 0.9。HP 20、伤害 1、攻击间隔 1 模拟秒沿用旧 DefaultTower。角色属性和基础战斗规则保持现有版本。
- 新场景相机沿用原型的观察角度、投影类型和 FOV，仅在新场景中重新取景以覆盖地图与装饰边界。原型场景和原型摄像机未修改。

## 沿用的系统

保留当前 UpgradeScene 的 2×6 仓库、初始空编队、最多六人部署和 Captain，作为独立准备界面；当前主流程跳过此界面，Level 1 固定单 Captain，不恢复旧 Claim Card、金币或教程系统。
战斗使用现有 BattleSession / BattleSimulation、Primitive UnitView、射程显示、血条、Pause / Resume、Speed Bar、单位详情、三个技能框、小队按钮与左侧编队编辑。Upgrade 与调试原型仍绑定 PrototypeSquad，固定 Level 1 使用自己的 Level1Captain 名单。
Play 中只生成单位 View 和调试表现，不重建静态地块或静态塔。

## 已执行验证

- 全部现有 Rebuild C# 源码和测试离线编译：0 错误（14 个现有序列化字段警告）。
- 对实际保存的新 Scene 解析验证：全部局部 fileID 和资源 GUID 可解析；1053 个独立 Small Cell 无冲突；55 个路线节点连续；九个槽位的转弯/路段扫掠均可通行；18 个塔支撑格合法且互不重叠。
- 新相机在 16:9 下能包围地图、塔和装饰边界；旧 Team5 参考文件与 SquadBattlePrototype 的 SHA256 保持不变。
- 将实际 Scene 导出的路线、可行走格与塔参数交给真实 BattleSimulation 运行。六人测试配置获得 Victory，塔累计受伤 12，死亡 2 人，首名到达后胜利；单 Captain 中路配置正确 Defeat；暂停、编队命令和只结算一次检查通过。
- 共 16 项离线命令行规则检查通过。报告：`Logs/Level1Migration/Compile.log`、`Rules.log`。场景检查命令：`Tools/Rebuild/validate_level1.py`。
- 新增 Unity EditMode 场景配置/重新打开检查；现有菜单流程测试跟随新的 BattleScene。尚未执行 Unity EditMode / PlayMode 测试，也未接管 Unity 界面验证画面、拖动和场景跳转。

`Tools/Rebuild/import_level1.py` 是此次使用的一次性离线场景作者工具，只读旧项目并且拒绝覆盖已有目标；运行时不引用它。之后直接在 Unity Scene 编辑地块、塔和路线。

每次触发的变更已通过离线编译（0 错误），报告 `Logs/Level1Migration/TutorialEveryRunCompile.log`；未操控 Unity 界面验证。
