# Level1 平台搭乘模式

`Level1GridBattle` 的 BattlePresentation 对象启用了独立的 `SquadPlatformView` 组件。Scene 1 没有该组件，继续使用原连续坐标自由小队系统。

## 模型与使用

- `SquadPlatform3x3.prefab` 为蓝白配色实体平台，配金属边框、拼接台面、角钉，以及对应三条铁轨的 12 个轮子。材质文件保留原文件名和 GUID，避免破坏引用。
- 车体尺寸仍为 X=3、Y=0.5、Z=3；厚度等于一个 BattleCell。车体相对道路抬高 0.32，台面相对逻辑锚点的高度为 0.82。轮子半径 0.18，接触高度为道路上方 0.012，与铁轨贴图相同。轮组直立时标称总高度为 0.808；旋转轮子的 Renderer 包围盒会有额外余量，因此尺寸检查单独验证车体与轮子半径。
- 轮子位于三条轨道的每根钢轨上（每条轨道前后两组，共 12 轮），人物统一抬高到 0.82 的台面高度。轮子按实际移动距离转动，轮组随行进方向转向；暂停时不转动。平台本体保持固定朝向。
- 打开 `Assets/Rebuild/Scenes/Level1GridBattle.unity` 并 Play 即可查看。正式场景仍保留原来的单 Captain 初始配置，测试中的双人配置没有保存进场景。
- 平台保持固定世界朝向，在道路转角处平移改变行进方向，不旋转扩大占地。
- 如需该场景恢复原表现，进入 Play 前禁用 BattlePresentation 上的 SquadPlatformView 组件。模型和台面偏移均不启用；不影响 Scene 1。

## 与原移动系统的关系

本模式使用 Level1 已有的固定 3×3 编队与三格宽路线检查，并非新增物理载具或寻路实体。逻辑位置、MemberId、SlotId、HP、攻击和移动速度仍由原 BattleSimulation 控制。平台没有 Collider/Rigidbody、独立血量或攻击。

所有搭乘者的渲染位置共用同一个插值锚点，显示为“平台位置 + 台面高度 + 相对站位”。交换站位仍使用原逻辑判定，但人物视觉在台面内平滑走向目标位置，不再瞬移显示；普通暂停时换位表现也暂停。教程确认换位后，会只用不受暂停影响的表现时间完成台面内移动，并等待实际显示位置到达目标，再显示下一条结果提示；平台和战斗时间仍然暂停。跳过教程不会留下持续运行的暂停覆盖状态。FW/Haon 动画驱动已接入搭乘状态：乘车位移不触发 Run，仅相对台面换位触发。

修改主要位于 SquadPlatformView、UnitView、BattlePresentation，以及 FWUnitVisual / HaonUnitAnimation 的平台分支。无平台组件时，原移动与动画路径保持不变。Camera、地图、轨道和正式初始编队配置不变。

## 验证

Unity 6000.3.22f1 编译通过。车体厚度仍为 0.5，车体包围盒检查为 3×0.5×3，轮子半径为 0.18，物理碰撞体为 0。

- 平台 Play Mode 专项通过：临时双人同步移动、台面高度、暂停停止、换位不瞬移与台面边界、Scene 1 无平台且仍为自由移动。结果 `Logs/SquadPlatform/PlatformOnly.xml`。
- 原 FW/人物动画与破城车回归 12 项通过，结果 `Logs/SquadPlatform/PlayMode.xml`；该轮中平台测试最初因测试配置尚未替换固定编队失败，修正测试夹具后已单独通过。
- 教程换位回归：按实际第 1→2→3 步进入编队编辑、确认换位，检查结果提示出现前人物已到达目标、平台保持原位且模拟时间未前进。记录为 `Logs/SquadPlatform/TutorialFix.xml`。
- 实际渲染预览 `Logs/SquadPlatform/Platform.png`。没有自动操作桌面 GUI，未执行 Git Commit/Push。

生成工具为 `SquadPlatformBuilder`；工具拒绝覆盖已存在的 Prefab，避免丢失手工调整。首次场景备份保存在 `Logs/SquadPlatform/Level1BeforePlatform.unity`。

蓝白轮组更新工具：Team5 → Rebuild → Level 1 → Style platform blue-white and add rail wheels。旧 Prefab、材质和场景备份在 `Logs/SquadPlatform/BeforeBlueWhite`。更新回归结果在 `Logs/SquadPlatform/BlueWhiteTests.xml`（原有回归 12 项通过）及 `Logs/SquadPlatform/BlueWhitePlatform.xml`（平台专项），包括轮数、接轨高度、滚动/暂停、人物台面高度、原换位与教程时序、Scene 1 模式与 FW/破城车功能。

## Level1 Catgirl 外观

Level1GridBattle 已替换为 Catgirl 2.5D 外观。默认 Prefab 为保留的白发混合版本；当前正式 Captain（MemberId 110、SlotId 4）使用金发 CatgirlCaptain。原方块 Unit Prefab 保留但该场景不再使用。未改变初始编队、属性、平台、地图或移动系统。人物搭乘时 Idle，在运行中的台面换位时 Run，换位完成返回 Idle；保留教程暂停期间完成显示换位后才出现结果提示的流程。

修改工具为 Level1CatgirlBuilder，场景备份和校验记录位于 `Logs/Level1Catgirl`。Unity 编译、实际渲染、原人物/破城车 12 项回归以及 Catgirl 平台/教程 2 项专项测试已通过。渲染中的双人编队仅为测试数据，正式场景仍为原单 Captain。

## Level2 平台搭乘

Level2GridBattle 已实例化同一个蓝白轮式平台 Prefab，并启用 SquadPlatformView。车体厚 0.5、抬高 0.32、台面高度 0.82，12 个轮子对应三条轨道；保留原 Catgirl 职业外观表、固定路线、初始编队、战斗属性和关卡设置。平台与单位同步，搭乘 Idle、换位 Run，暂停停止。专用工具 Level2RailPlatformBuilder 复用通用 RailwayPathBuilder.BuildScene，仅在 Level2 增加装饰和平台引用。实际运行预览 `Logs/Level2RailPlatform/PlayMode.png`，回归 16/16 通过。
