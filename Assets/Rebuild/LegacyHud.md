# 战斗 HUD

Speed Bar 上方新增运行时 Pause / Resume 按钮，作为滑条的子对象跟随其现有位置和 75% 缩放，不修改 Scene 中的布局。按钮仅切换当前 Simulation.IsPaused，保留速度档位和单位聚焦慢速，不改全局 Time.timeScale。编队编辑期间按钮禁用，避免绕过编辑器暂停；应用编队后保留原先手动暂停状态。战斗结束后禁用，重开后重新绑定。离线 C# 编译通过（0 错误），报告 Logs/WarehouseChecks/PauseButtonCompile.log。已添加 PlayMode 测试覆盖暂停推进量、继续、速度保留、编辑器暂停与滑条布局；未操控 Unity 界面或执行该 PlayMode 测试。

Captain：仓库新增成员 110，HP 6、减伤 50%、攻击伤害 0.5；射程与间隔沿用 Attack（2.5 世界单位 / 1 模拟秒）。不含额外指挥技能。王冠使用共享线段定义，在仓库、编队、详情与单位顶部显示相同的金棕底色、白色标记。六人上限保持不变。离线规则报告：Logs/WarehouseChecks/CaptainRules.log（15 项通过）；未操控 Unity 界面验证外观。

入口为 `Assets/Rebuild/Scenes/SquadBattlePrototype.unity`。保留旧 Team5 的字体、按钮、整数速度滑条与编队编辑交互；原 IMGUI 调试面板在 HUD 启用时隐藏。

金币系统已移除：Simulation 不保存货币，攻击塔只产生伤害与死亡事件。金币显示组件、场景对象、数字跳动和闲置金币图标工厂已删除。关卡与次数标签的字体直接来自编队按钮，不再依赖金币文本。

速度滑条和 Edit Formation 在 Scene 中向左上移动，整个控件（包括文字与手柄）等比缩小到原来的 75%。Canvas 继续使用 500×500 参考分辨率与宽度匹配。编队编辑窗口本身保持原大小。

- Slider 与 Button 的持久 UnityEvent 继续调用 SetSpeedFromSlider 和 OpenSquadEditor。
- 速度为 1、2、3 倍，每次 FixedUpdate 推进对应数量的固定小步，不改全局 Time.timeScale。
- 新基础战斗流速为原来的 0.8 倍，三个档位对应原来的 0.8 / 1.6 / 2.4 倍；单位移动、冷却、治疗和弹丸统一使用该模拟时间，不再单独降低单位速度数值。
- 单位聚焦时固定为基础速度的 0.1 倍，滑条临时禁用，关闭详情后恢复原速度档位；暂停仍然优先。
- 爱心表示本关尝试次数，默认三次，每次 Defeat 只扣一次；开发用重启不重置次数。
- 编队拖动和点击只修改草稿，Apply 调用 Simulation 的换位命令，保留成员 ID、HP 和冷却。
- HUD、单位图标与血条只读取战斗状态。隐藏或删除表现层不改变战斗结果。

复用来源仍为旧 Team5 的 Stage1 Canvas、UiKit、SquadCellUI、UnitIconUI、UnitLooks、SoldierLook、BattleSquadEditor 与 HealthBar；所有适配位于新项目的 Team5.Rebuild 命名空间。旧项目只读，摄像机、地块、路线与塔的场景配置保持不变。
