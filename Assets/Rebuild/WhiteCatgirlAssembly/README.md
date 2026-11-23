# White Catgirl Editable Static Assembly

双击 `WhiteCatgirl_EditableAssembly.prefab` 进入 Prefab Mode，在 Hierarchy 中展开根对象即可选择各个 `F_*` 部件。

- 按 **F** 聚焦人物；Scene 视图切换 **2D** 更方便调整。
- 用 **W** 移动、**E** 旋转、**R** 缩放；也可以直接改 Inspector 中的 Local Position / Rotation / Scale。
- 部件前后遮盖通过 SpriteRenderer 的 **Order in Layer** 调整，数值越大越靠前。不要用 Z 位移调整这套静态部件的遮盖。
- 修改完在 Prefab Mode 保存。根节点原点位于脚底附近，所有部件都是独立子对象。

此 Prefab 只做静态拼装，不包含骨骼、SpriteSkin、Animator、碰撞器、UnitView 或战斗控制脚本；没有接入现有单位或场景。
初始拼装使用制作时 `Art/WhiteCatgirlFront/Parts/*.png` 的内容和原有位置。PNG 是独立副本，用户修改后的实际尺寸会保留，不强制拉伸。

请直接调整本 Prefab。生成器只允许首次创建，避免覆盖手动修改。
