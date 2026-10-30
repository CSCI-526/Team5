# StartGame / EndGame 移植

UpgradeScene 的 Start Wave 按钮上方显示实时 `LEVEL n` 标签，参考旧 Team5 的白色粗体 36 样式（240×60），水平居中对齐按钮，距按钮顶部 12 UI 单位；根据按钮实际 Transform 换算位置，不移动按钮。标签由现有 LegacyUpgradeScene 创建，每帧只读取 BattleSceneFlow.NextBattleLevelNumber；流程在新一轮重置为 1，并从加载的 BattleSession.DisplayedLevelNumber 同步编号。目前仅有 Level 1，没有新增关卡推进规则。未移动仓库、编队或 Start Wave 按钮。离线编译通过（0 错误），报告 Logs/WarehouseChecks/UpgradeLevelCompile.log；未操控 Unity 界面验证。

场景从旧 Team5 的同名 Scene 直接复制，保留 Canvas、字体、标题、按钮、相机与原始 RectTransform 布局。新项目入口为 Assets/Rebuild/Scenes/StartGame.unity，也是 Build Settings 的首场景。

Start Game → Level1GridBattle（固定一名 Captain，跳过 UpgradeScene） → 战斗胜利或失败 → EndGame → Play Again → StartGame。新一轮重新加载战斗 Scene，生成全新的 Session 与成员状态。连续点击不会创建重复流程对象；结算通过下一帧异步切换场景，不在模拟回调中卸载场景。SquadBattlePrototype 仍作为独立调试场景保留。

LegacySceneMenu 保留原 StartGame / GoToStart 方法名，替换按钮事件接收者。BattleSceneFlow 在 Session 层观察当前 Simulation 的结算事件。Level1 无论从标题进入还是直接在 Editor Play，都会自动绑定唯一流程对象并在胜负后进入 EndGame；SquadBattlePrototype 仍作为独立调试场景停留。单独打开 EndGame 也可返回标题。

移除的旧依赖：OffenseData、ResetGame、FirebaseAnalyticsSender、AttemptAnalytics、AttemptResultsScreen。已接入 UpgradeScene 的无金币编队准备界面，并新增方块版旧 Level 1（见 Level1Migration.md）；未移植付费升级商店、金币、统计上传或运行时教程。输入模块与字体复用当前工程已安装的 Input System / TextMesh Pro 资源，无新包依赖。

本次不修改三个既有战斗场景、摄像机、Simulation 规则或 PC URP 设置。Speed Bar 与 Edit Formation 以用户当前 Scene 配置为准；测试不再约束旧导入坐标。

历史菜单移植验证（早于 Level1GridBattle）：EditMode 64/65 通过，唯一失败为已有摄像机取景检查；PlayMode 23/23 通过。曾人工点击 Start Game，经当时默认战斗 Victory 进入 EndGame，再点击 Play Again 返回标题，Console 0 警告、0 错误。历史报告位于 Logs/MenuMigrationEditModeResults.xml 与 Logs/MenuMigrationPlayModeResults.xml。新增 Level 1 仅执行了离线编译、静态场景检查及命令行模拟，未接管 Unity 界面验证，结果见 Level1Migration.md。
