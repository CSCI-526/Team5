# Catgirl + Cute Girl 下半身混合版

## 查看

打开 `Assets/Rebuild/Scenes/CatgirlFrontTest.unity`，点击 Play。Attack 测试单位 MemberId **111**、SlotId **4** 默认使用此混合版。沿用原来的小队移动操作即可看到跑步。其他单位、正式 Scene 1、地图、Camera、战斗属性、碰撞和编队系统均未修改。

静止为 Idle，移动为 Run，停止切回 Idle，暂停冻结在 Idle。保持正面，整个角色一起左右镜像。真实位置由 BattleSimulation / UnitView 决定，Root Motion 关闭。

## 保留与替换

- 保留 Catgirl 的头部、头发、上衣、手臂、黑色下装和前后衣摆/裙摆。Sprite 资源引用与原 Catgirl 相同。
- 保留 Catgirl 原来的棕色、黄色扣带、绿色鞋底靴子贴图；靴子显示比例为原独立靴子 Sprite 的 85%，以适配 FW 腿长。
- 腿部采用 FW Cute Girl 原生 `F_BODY` 的下半身网格、原始绑定矩阵和顶点权重，保留其浅蓝色长袜外观。原网格同时包含上衣/短裤/鞋子，因此副本沿原始绑定姿态裁出腿部：Y 范围约 2.8–4.7。裁边新顶点的 UV 和权重由原三角形线性插值，原 FW 资源不变。
- 当前腿部截面横向收窄为原来的 **85%**。收窄以每条腿的髋—膝—小腿中心线为基准，不改变骨骼位置、腿长或两腿间距；裙摆和靴子不缩放。UV、权重、绑定矩阵及动画曲线保持原值。
- FW 原上身、短裤主体及鞋子不会显示。Catgirl 当前自行制作的两条连续腿在此副本中关闭，原资源仍保留。

## 动画

只有一个 Animator：`Catgirl Front Rig`。上身继承已调整的 Catgirl Idle/Run，头部和手臂的局部曲线保持原值；原 Catgirl 自制腿部曲线从混合版副本中移除。

FW 原生正面 `F_IDLE01` / `F_RUN01` 的躯干根骨与腿骨曲线复制到 `FW Lower Body/...`，保持相对运动和原始权重。以原 Catgirl 的 Idle 2 秒、Run 0.64 秒统一上下身周期，FW 曲线按归一化相位重定时；没有直接把 FW 的骨骼角度套到 Catgirl 自制腿骨上。

为避免上下身反向起伏，混合版仅将 `Skeleton/Pelvis` 的 Y 起伏对齐 FW 两个髋骨的平均高度变化。其余上身曲线与原 Catgirl 相同。靴子轨迹来自原 FW 足底网格顶点的实际蒙皮位置，按每秒约 60 次离线采样烘焙进同一套动画；运行时不增加第二个 Animator 或同步脚本。

FW 源人物朝左，因此 donor rig 局部 X 镜像以匹配 Catgirl 朝右的原画；外层整体镜像继续由现有 FWUnitVisual 控制。

## 文件

- `CatgirlFWLowerBody.prefab`：混合版 UnitView / 表现 Prefab。
- `FWNativeLowerBody.asset`：裁出的原生 FW 腿部 Sprite，沿用原始贴图、骨骼定义和权重。
- `FWNativeLowerBody_BeforeSlim.asset`：瘦腿前的网格备份。编辑 Prefab 的 `FW Lower Body/F_BODY`，将 Sprite 改为此资源可恢复原腿宽。
- `CatgirlOriginalBootL.asset`、`CatgirlOriginalBootR.asset`：使用原 Catgirl 靴子贴图的独立 Sprite 和新脚部绑定。
- `CatgirlFWLowerIdle.anim`、`CatgirlFWLowerRun.anim`：统一的上下身动画。
- `CatgirlFWLowerBody.controller`：Idle/Run 状态机，0.12 秒过渡。
- `Assets/Rebuild/Editor/CatgirlFWLowerBodyBuilder.cs`：混合版构建、裁边、脚部烘焙及腰部同步工具。构建菜单拒绝覆盖已经存在的混合 Prefab。
- `Assets/Rebuild/Tests/PlayMode/CatgirlFWLowerBodyTests.cs`：混合版图像和动画测试。

## 验证与限制

Unity 6000.3.22f1 编译通过；D3D11 Play Mode 共 **5/5 测试通过，0 跳过**，结果为 `Logs/CatgirlFWLowerResults.xml`。检查覆盖：

1. 混合版保留 Catgirl 头部、身体、裙摆和靴子贴图；没有叠加原 Catgirl 腿部。
2. FW 腿部曲线在相同归一化时间与原 FW 动画一致，实际蒙皮顶点发生运动。
3. 头部/手臂等上身曲线与原 Catgirl 保持一致，腰部高度差在跑步周期内保持稳定。
4. 八个 Run 相位的整角色渲染，以及腿部与靴子的隔离渲染连通性检查。
5. 测试场景只替换 MemberId 111，模拟移动驱动 Run，暂停回到 Idle；原 Catgirl 和原 FW 回归检查通过。

预览为 `Logs/CatgirlFWLower/HybridIdle.png`、`HybridRun0.png` 至 `HybridRun7.png`。原始场景测试配置备份为 `Logs/CatgirlFWLower/TestSceneBeforeHybrid.unity`；腰部裁边和同步前的资源备份也保存在该日志目录。

瘦腿后的专项复查为 `Logs/CatgirlSlimLegsResults.xml`：**2/2 通过，0 跳过**，覆盖混合角色渲染/蒙皮/腰脚连接及实际场景移动。`Logs/CatgirlFWLower/SlimLegVerification.json` 记录了网格 Y/Z、UV、权重、索引和绑定矩阵未变的检查结果。菜单 **Team5 > Rebuild > Catgirl Front > Set hybrid leg width to 85%** 可从原宽备份重新应用，重复执行不会累积变细。

这仍是混合美术原型：FW 的长袜与 Catgirl 的线条、肤色可能存在风格差异；没有制作新服装或攻击动作，也没有逐一测试所有墙体和透明物排列。沿用已有世界 Z 排序和材质。

## 回退与后续手动编辑

在测试场景的 BattlePresentation 中，将 **Test Unit Prefab** 改回上一级的 `CatgirlFrontUnitVisual.prefab`，可恢复完整 Catgirl 版本；关闭 **Use Test Unit Visual** 则恢复当前普通 FW 外观。

修改混合版头部/手臂时，打开此目录的 Prefab，在 Animation 窗口选择 `CatgirlFWLowerIdle` 或 `CatgirlFWLowerRun`。这些是独立副本，修改原 Catgirl 动画不会自动更新混合版。修改 FW donor 腿骨时保留骨名及层级；靴子的轨迹已烘焙，如果重做迈步动作需要同步重烘焙靴子。
