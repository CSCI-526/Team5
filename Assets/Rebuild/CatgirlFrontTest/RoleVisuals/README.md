# Scene 1 四职业模型

四个独立 Prefab 复制自当前瘦腿版 `../FWLowerBody/CatgirlFWLowerBody.prefab`。白发原模型保持不变，`CatgirlFrontTest` 测试场景仍保留白发模型。

四职业外观的 Billboard Transform 已统一从 1 放大为 1.1（在当前大小基础上增加 10%）。只缩放人物视觉子层级，不修改逻辑碰撞半径、编队间距、角色根坐标、动画或破城车；原白发 Prefab 保持不变。此项已检查四个 Prefab 的变更范围，尚未在 Play Mode 验证。

| 职业 | Prefab | MemberId / SlotId | 图标主题色 |
|---|---|---|---|
| Attack | CatgirlAttack.prefab | 111 / 4 | 红色，#F2594C |
| Tank | CatgirlTank.prefab | 112 / 7 | 蓝灰色，#808FA6 |
| Heal | CatgirlHeal.prefab | 113 / 3 | 绿色，#4CC773 |
| Captain | CatgirlCaptain.prefab | 114 / 1 | 金棕色，#B88F47 |

颜色直接读取 `LegacyUnitLooks.For(role).Body`，与当前单位图标采用同一来源。表中的十六进制值由 Unity ColorUtility 输出。实际发丝还保留原贴图的阴影，像素不一定全部等于该纯色。

只有 Catgirl 自身的 `F_HAIR_FRONT`、`F_B_HAIR` 两个 SpriteRenderer 设置职业颜色。原耳朵、脸、肤色、眼睛、衣服、裙摆、蓝色长袜及棕色靴子均保留原色。没有使用旧 FW 棕发遮罩，也没有修改纹理、Shader 或原始模型。SpriteRenderer 的原生着色与 GPU SpriteSkin 共存，不添加材质属性块。

四个 Prefab 共用原混合模型的动画、网格和纹理，拥有各自的颜色配置。当前 85% 腿宽、原靴子、裙摆、正面模式及左右镜像全部沿用。

## 查看与手动修改

打开 `Assets/Rebuild/Scenes/1.unity` 并 Play，即可查看四个职业外观。仅该场景当前四个成员的表现覆盖配置更新；默认备用 Unit Prefab 及其他场景保持原设置。

要单独改某种发色，打开对应 Prefab，在 `Camera Facing Visual / Mirror Entire Character / Catgirl Front Rig` 下，修改自身 `F_HAIR_FRONT`、`F_B_HAIR` 的 SpriteRenderer Color。不要修改 `FW Lower Body` 中隐藏的同名 FW 头发层。修改动画时注意四种外观共用动画资源。

白发原模型仍在 `FWLowerBody/CatgirlFWLowerBody.prefab`。Scene 1 的 BattlePresentation 使用现有的单成员/附加成员外观覆盖字段配置这些 Prefab；关闭 **Use Test Unit Visual** 可恢复该场景原来的默认 FW 外观。修改前完整 Scene 1 文件保存在 `Logs/CatgirlRoles/Scene1BeforeRoleVisuals.unity`。

## 文件及验证

构建工具：`Assets/Rebuild/Editor/CatgirlRoleVisualBuilder.cs`。工具会拒绝覆盖已有四职业 Prefab，并检查原白发 Prefab 的 SHA256 不变、场景的成员/数值/地形/路线/Camera 签名不变。

本次没有改动 BattleSimulation、MemberId、SlotId、地图、Camera、编队间距或战斗属性。构建报告为 `Logs/CatgirlRoles/BuildReport.json`。

Unity 6000.3.22f1 编译通过；D3D11 Play Mode **4/4 通过，0 跳过**，结果为 `Logs/CatgirlRolesResults.xml`：

1. Scene 1 四职业前后发颜色与图标主题色一致，其他 Sprite 保持原色；实际隔离渲染验证四种发色；Idle/Run 保持正面。
2. Scene 1 连续移动播放 Run，暂停为 Idle，独立世界 Z 材质更新正常。
3. 原地/移动中交换编队位置不产生错误转向，调整仍播放移动动画。
4. 原白发混合版保留衣服、靴子和 FW 原生动作，蒙皮、腰部与靴口衔接回归通过。

预览：`Logs/CatgirlRoles/FourRoleModels.jpg`；单职业 Idle/Run 和隔离头发图在 `Logs/FWVisual/Catgirl<Role>*.png`。

## Summon 全职业 Catgirl

新增独立的 CatgirlSniper、CatgirlVanguard、CatgirlDemolisher、CatgirlFrostweaver、CatgirlWarcaller、CatgirlAegis Prefab；原 Attack、Tank、Heal、Captain 四个 Prefab 与白发原版没有被覆盖。

| 职业 | 发色（取自图标主题色） | Prefab |
|---|---|---|
| Attack | 红色 #F2594C | CatgirlAttack.prefab |
| Tank | 蓝灰 #808FA6 | CatgirlTank.prefab |
| Heal | 绿色 #4CC773 | CatgirlHeal.prefab |
| Captain | 金棕 #B88F47 | CatgirlCaptain.prefab |
| Sniper | 青色 #38C7E0 | CatgirlSniper.prefab |
| Vanguard | 蓝紫 #617AD1 | CatgirlVanguard.prefab |
| Demolisher | 橙色 #FA8C29 | CatgirlDemolisher.prefab |
| Frostweaver | 冰蓝 #61D6FF | CatgirlFrostweaver.prefab |
| Warcaller | 杏橙 #FFA84C | CatgirlWarcaller.prefab |
| Aegis | 紫色 #9E73FF | CatgirlAegis.prefab |

当前 Summon 池为 6 种常规职业和 3 种传奇职业，共 9 种；Captain 不在随机招募池，但保留对应外观，总计 10 种。只改变头发图层，不改变肤色、服装、武器、骨骼或动画；同职业的等级/稀有度共用外观，稀有度继续由原 UI 表示。

`CatgirlAllRoles.asset` 为职业→Prefab 外观表。BattlePresentation 的 Role Visuals 指向此表，动态招募产生任意 MemberId 后仍按 Role 正确选择。原单成员/额外成员测试覆盖优先级更高；未知的 Test 角色回退原 Unit Prefab。

已接入 Level1–Level7、TestLevel 与 Scene 1，共 9 个战斗场景。只改变表现配置，不修改 Summon 概率、价格、单位属性、技能、编队、地图或各场景原移动模式；卡牌图标和单位介绍 UI 保持原样。Level1 保持平台系统，Scene 1 保持自由小队系统。Haon/FW 等独立模型测试场景不应用该外观表。

生成工具：CatgirlSummonVisualBuilder。备份、颜色记录和验证结果在 `Logs/CatgirlAllRoles`。

验证：Unity 6000.3.22f1 编译通过。职业资源 Edit Mode 检查 1/1 通过；动态 MemberId 全职业配色与 Idle/Run、原平台/教程、FW 动画及破城车 Play Mode 检查 15/15 通过，0 跳过。九个场景原对象 ID 保留，配置差异仅为 Role Visuals 外观表引用。
