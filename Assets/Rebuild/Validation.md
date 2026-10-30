# Minimal Presentation 验证记录

验证环境：Unity 6000.3.22f1，项目当前的 PC_RPAsset / Universal 3D Renderer。第二阶段保持第一阶段 Simulation、World、Session 的 10 个 C# 源文件逐字节不变，SHA-256 对比记录位于 `Logs/MinimalPresentationCoreHashes.json`。

## 自动测试

23 项 EditMode、9 项 PlayMode 全部通过，无失败或跳过。包含原有 25 项回归测试与新增 7 项最小表现层测试。

报告：

- `Logs/MinimalPresentationEditModeResults.xml`
- `Logs/MinimalPresentationPlayModeResults.xml`

| 验证项 | 验证方式与结果 |
| --- | --- |
| 静态地块、塔保存于 Scene | 100 个原 BattleCell 各有一个 1×0.5×1 Cube 子物体，三座原塔各有一个 Cylinder 子物体；重开场景后网格、引用、Mesh 与材质正确。 |
| 颜色、材质、摄像机 | 六种地块颜色不同，标准 URP/Lit 材质；摄像机使用现有 UniversalRenderer，有简单方向光。地图、塔和士兵都位于 Game Camera 视锥内。 |
| Play 不重新生成地图和塔 | 原有 BattleCell / BattleTower 与所有静态 Mesh 子物体的实例 ID 保持不变；新增运行时对象只有单位 View。 |
| 显示插值 | UnitView 的半步显示位置等于两次模拟采样的中点；改变模型位置或调用插值不改变 UnitState 的位置或 HP。 |
| View 生命周期 | 生成、死亡、到达、重启、禁用及重新启用后 View 数量与存活状态一致，无重复订阅造成的重复 View。 |
| 禁用 Presentation | 从头重新运行，战斗结果、死亡数和到达数与启用时一致。 |
| 移除 Presentation 与全部 Renderer | 删除控制器、Gizmos 与静态/单位 Mesh 后重新运行，战斗结果一致，配置仍有效。 |
| 核心规则回归 | Transform 来源、路线与占位校验、伤害、冷却、选敌、终态事件、胜败、保存重开和 512 单位纯模拟测试继续通过；原场景的 128 单位压力正确性测试继续通过。 |

## 编辑器显示检查

在 Game View 关闭 Gizmos，已实际进入 Play，确认地图、堆叠地块、三座蓝色 Cylinder 塔和多个黄色 Capsule 士兵可见。调试线框、路线标记与射程球不是正常显示的必要条件。

## 范围与未验证项

本次仅实现 Primitive Mesh、共享颜色材质、简单方向光、单位只读状态绑定与线性显示插值。没有正式美术、动画、特效、弹丸、复杂 UI、自定义 Shader 或新战斗规则。

未执行独立 Player 打包、多平台显示兼容性测试或正式性能基准。单位 View 当前直接实例化/销毁，不包含对象池。

## Decorative Border Tiles 追加验证

外围新增两圈纯视觉地块，共 96 块（内圈 44，外圈 52），尺寸均为 1×0.5×1，直接保存于 Scene 的独立 `Decorative Border Tiles` 节点，与 BattleGrid 同级。每块仅有 Transform、MeshFilter 和 MeshRenderer，使用 Ignore Raycast 层，无 Collider、BattleCell 或其他运行时脚本。

本次完成 Scene 序列化结构检查：所有原场景对象块保持不变，仅主根节点的子节点列表追加装饰节点；原有 100 个战斗格子、路线引用和塔配置未改变；新增 ID 唯一，所有新增本地引用完整，坐标恰好覆盖两圈且没有进入战斗网格。核心源码 SHA-256 仍全部一致。记录位于 `Logs/DecorativeBorderValidation.json`。

上文 23 项 EditMode / 9 项 PlayMode 是 Minimal Presentation 阶段的测试记录。本次 Unity 窗口句柄失效，未重新运行 Unity 测试或目视复查 Game View；已有场景断言已补充装饰地块的隔离、尺寸、范围和数量检查。

### 第三圈扩展

在原两圈外追加 60 块，当前为三圈、总计 156 块（各圈 44 / 52 / 60），范围 X=-3…14、Z=-3…10。原 96 块及所有战斗实体、路线和配置的序列化对象保持不变，只扩充装饰根节点的子节点列表。静态检查确认全部地块尺寸、父节点、Ignore Raycast 层、纯 Mesh 组件、唯一 ID 与新增引用完整；本次未重跑 Unity 测试。记录：`Logs/DecorativeBorderThirdRingValidation.json`。
