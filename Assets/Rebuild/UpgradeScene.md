# UpgradeScene 仓库与六人编队

流程：StartGame → UpgradeScene → Start Wave → SquadBattlePrototype → EndGame。

左侧仓库为两行六列，共 12 格。现有九名成员来自 PrototypeSquad.asset，其余三格显示 EMPTY。每格保留原职业介绍面板，以 60% 比例显示职业图标、说明和真实基础战斗数值。已上阵成员变暗并显示 IN SQUAD，同一成员不能重复出现。

右侧保留 3×3 空间槽位，但初始为空、上阵上限为六人。顶部 x/6 由 SquadPreparation.Changed 实时驱动；空队时 Start Wave 禁用。

- 仓库拖入空格：加入成员，达到六人时拒绝第七人。
- 仓库拖到已有成员的格子：替换该成员，被替换者回到可用状态。
- 编队内拖动或点击两格：移动或交换，成员身份不改变。
- 编队成员拖到编队之外：返回仓库，计数和按钮同步更新。

SquadPreparation 使用独立成员 ID 记录九个槽位的占用，0 表示空位。CaptureDeployment 提交只读快照，只包含 1..6 名已选成员，保留职业、HP 定义及出生时间。BattleSceneFlow 在场景加载前捕获快照，BattleSession 在生成模拟前接收；仓库中未选成员不生成。直接打开战斗场景调试时，默认使用初始配置中的前六名启用成员。共享初始配置与 Unity Scene 不在运行中修改。

金币、付费卡牌、旧升级树仍不启用。本次未修改地图、摄像机、战斗 UI 布局或 Simulation 的伤害、移动、选敌、治疗规则。

验证（本次不接管电脑）：全部 61 个 Rebuild C# 源文件（含测试）离线编译通过，0 错误；14 个 CS0649 警告均为 Unity Inspector 序列化字段。10 项实际仓库模型命令行测试通过，结果位于 Logs/WarehouseChecks/Rules.log。Unity EditMode / PlayMode 测试已更新，但本次未运行；界面显示、拖放手感及跨场景运行由用户在 Unity 中验证。
