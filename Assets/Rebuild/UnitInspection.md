# 运行时单位点选与详情

小队聚焦现以完整 3×3 槽位的中心（UnitState.AnchorPosition，对齐 UnitView 的插值位置）为中心，固定覆盖 3×3 水平范围；空位和成员死亡不会使焦点偏向剩余成员。选中小队时，存活 Captain 单位的 Body 与 Outline 呈现金色明暗脉冲，保留白色王冠，地块不变色。使用 Renderer 的 MaterialPropertyBlock，不改共享材质或 Simulation，不添加 Bloom、灯光或后处理。应用、取消、改选单位、重新开战或禁用表现层时恢复原属性块；无存活 Captain 时不高光。脉冲使用非缩放时间，暂停时仍可见。离线编译报告：Logs/WarehouseChecks/CaptainGlowCompile.log。PlayMode 测试覆盖稀疏编队中心、单位金光恢复和地块颜色不变；未操控 Unity 界面或执行 PlayMode 测试。

生命值下方显示三个竖排正方形小队按钮。当前仅小队 1 可选，图标优先使用 Captain，没有 Captain 时使用首位成员；另外两个保持无文字、无图标且不可点击。点击小队 1 后，镜头按完整 3×3 编队中心和范围在右半屏居中并留出边距，左侧复用 LegacyFormationEditor 的 3×3 编队草稿。编辑期间暂停；应用提交槽位排列并恢复进入前的暂停状态，然后退出聚焦。Esc、X、再次选择小队或切换到成员会取消未提交草稿并恢复原暂停状态。原单成员聚焦和 Captain 技能入口保持可用。没有修改 Scene、相机资产、Speed Bar 的位置或 Simulation 规则。

小队选择的离线 C# 编译通过（0 错误），15 项现有命令行规则检查通过。新增 PlayMode 测试覆盖三按钮、空位无文字、队长图标、整个小队的相机取景、左侧换位、暂停、应用/取消、成员状态保持和禁用表现层后的恢复；未运行这些 PlayMode 测试或操控 Unity 界面。报告：Logs/WarehouseChecks/SquadSelectionCompile.log、SquadSelectionRules.log。

所有职业的详情下方均显示三个 96×96 正方形技能按钮。Captain 的第一个为 Edit Formation，复用 LegacyBattleHud / LegacyFormationEditor 的暂停、编队草稿与提交逻辑；其余职业的三个按钮及 Captain 的第二、第三个按钮可点击，但只提示尚未配置。单位死亡或到达终点后按钮禁用。运行时隐藏原 HUD 的 Edit Formation 入口，禁用 HUD 时恢复其原始 activeSelf；未改 Scene 中的位置或大小。编队编辑窗口在详情面板上方显示，应用后恢复原暂停状态，保留聚焦慢速。

技能框变更已通过离线 C# 编译（0 错误）。PlayMode 测试覆盖各职业的三个方框、预留技能反馈、Captain 打开编辑器、暂停/恢复；未操控 Unity 界面或执行这些 PlayMode 测试。报告：Logs/WarehouseChecks/AllUnitSkillsCompile.log。

BattlePresentation 在运行时接入可选的 BattleUnitInspection，不修改 Scene、相机资产或 Simulation。左键点击存活单位的可见 Mesh 边界进行选择，无需 Collider / Physics；UI 区域的点击不会穿透到单位。

点选后，左半屏显示详情，单位在右半屏中心平滑聚焦。Perspective 通过临时移动相机拉近，Orthographic 通过临时缩小 orthographicSize；保留当前投影类型、FOV 和观察角度。焦点跟随 UnitView 插值位置。换选其他单位不会覆盖最初保存的视角。

详情只读取 UnitState / UnitDefinition：职业、当前/最大 HP、攻击伤害、实际伤害减免比例、基础移速、攻击范围、攻击间隔与冷却、治疗量与八邻位规则、成员 ID、编队位置及存活状态。非攻击职业不显示虚假的攻击冷却与射程。字体和职业图标沿用当前 UI。

点击 X、按 Esc 或点击场景空白退出，平滑恢复点选前视角。禁用表现层、重开战斗或销毁组件时立即恢复。选中单位死亡或到达终点后保留最后焦点并显示实际状态，直到关闭、重新选择或战斗场景切换。

聚焦期间 BattleSession 固定以基础速度的 0.1 倍推进战斗，覆盖 Speed Bar 的 1/2/3 倍选择。滑条暂时禁用并保留原档位，退出详情后恢复；换选成员持续保持慢速，暂停仍优先。禁用表现层或重开战斗会解除慢速。全局 Time.timeScale 不变，相机过渡与 UI 保持正常速度。

基础流速现为原来的 0.8 倍，聚焦有效流速因此为原来的 0.08 倍，仍不受滑条影响。此次变更的离线编译及 14 项命令行检查通过，报告位于 Logs/WarehouseChecks/BaseTimeCompile.log、BaseTimeRules.log；未运行 Unity 界面验证。

验证：全部 Rebuild C# 源码与测试离线编译通过，0 错误；14 个 CS0649 为 Unity 序列化字段警告。14 项命令行检查通过，含三个速度档位下聚焦推进量、暂停与退出后的速度恢复，以及已有详情、仓库和移速检查。报告：Logs/InspectionSlowMotionBackup/Compile.log、Rules.log。新 PlayMode / EditMode 测试已编写，本次不运行或操控 Unity 界面。

手动检查：Play 后点击单位，检查左侧数据和右侧聚焦；点击另一单位；等待其受伤，检查 HP；按 Esc / X 退出并检查视角恢复。确认原摄像机参数、Speed Bar 与 Edit Formation 位置保持不变。
