# White Catgirl 2.5D Unit Visual

`WhiteCatgirlUnitVisual.prefab` 是从用户已保存的 `WhiteCatgirlAssembly/WhiteCatgirl_EditableAssembly.prefab` 制作的独立动画模型。
原始静态 Prefab 保留不变，生成时的快照和哈希保存在 `Logs/WhiteCatgirlUnit/`。

## 查看

打开 `Assets/Rebuild/Scenes/WhiteCatgirlVisualTest.unity`，点击 Play。测试单位沿现有路线移动，表现由现有 `UnitView` / `FWUnitVisual` 驱动。
也可以选中新 Prefab 中的 `White Catgirl Rig`，在 Animation 窗口预览 `WhiteCatgirlIdle` / `WhiteCatgirlRun`。
控制器通过布尔参数 `IsMoving` 切换 Idle 和 Run，切换时间 0.12 秒。

## 绑定

- 上半身以用户保存的部件位置、旋转、大小、图层顺序为绑定姿势。Idle 的第 0 帧保留该姿势。
- 使用真正 Unity 2D Animation 的 `SpriteSkin`、网格、绑定矩阵和骨骼权重。
- 两只连续袖子使用肩部和肘部的渐变权重，不拆成悬空的上下臂，不露手。
- 下半身复用之前 Catgirl 的 Cute Girl 原生腿部网格、权重、骨骼以及 Idle／Run 曲线；根据新角色的腰部和靴子高度适配。
- 靴子使用新角色腿部图像的下段，绑定到原来的脚部轨迹；完整静态腿部图像不会重复显示。
- 左右原生腿分别按保存的腿部图像校准腿根、倾角和长度；腿端伸入靴筒，靴子跟随重新烘焙的脚踝轨迹。腿根在短裤下保留遮盖，避免胯部接缝。
- 靴口另按原生腿的实际可见脚踝修正横向偏移，避免把网格边界误当成贴图的脚踝中心。
- 尾巴只随骨盆整体移动，没有独立摆尾轨道。
- 仅正面，左右移动时整体镜像；沿用 30 度显示倾角和现有世界 Z 排序材质。
- 禁用 Root Motion；模型不新增碰撞器，不决定单位逻辑坐标、碰撞半径或编队间距。

该模型和测试场景独立新增，未替换正式关卡的 Unit Prefab，也未改变 Camera、地图或 BattleSimulation。
如需继续调整静态美术，原始拼装 Prefab 仍可编辑，但不会自动覆盖已经生成的骨骼模型；应在新模型中调整对应绑定姿势和动画。

## 已验证

Unity 6000.3.22f1 已正常编译。D3D11 Play Mode 渲染测试检查：上半身 Idle 首帧各部件位置、旋转及蒙皮顶点与保存的拼装姿势一致（顶点容差小于半个源图像素）；Cute Girl 原生腿部网格在 Run 中实际变形；八个跑步阶段短裤、双腿和靴子保持连接；左右镜像、停止切回 Idle、暂停冻结，单位根位置不受动画影响。最终结果见下列测试记录。
测试记录为 `Logs/WhiteCatgirlUnit/PlayMode.xml`，Idle、Run 和腿部连接检查截图在同一目录。
尚未进行 Web 构建验证，也未替换正式关卡中的模型。
