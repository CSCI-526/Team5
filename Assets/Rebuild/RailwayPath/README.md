> Current campaign status: Level1 and Level2 no longer use rail artwork or squad platforms. Units walk directly on the original route. These assets and editor tools are retained for optional reuse.

# Level1GridBattle 平面铁轨

仅为 `Assets/Rebuild/Scenes/Level1GridBattle.unity` 的固定 SceneRoute 添加透明铁轨贴图。Hierarchy 中 `Railway Path Artwork` 包含左、中、右三条轨道，占据三格宽道路；共 45 个平面贴图面，其中 42 个直线、3 个弯道。未覆盖 Spawn/Goal 的颜色标记。

- `RailStraight.png` / `RailCorner.png`：256×256 原创程序绘制 PNG，透明背景、木枕和金属轨道。生成源在 `Tools/Rebuild/create_railway_textures.py`，没有第三方美术依赖。
- 同名 `.mat`：URP Unlit，透明混合、无深度写入、不投射阴影。
- 所有贴图面贴地高度为 0.012，无 Collider，无运动脚本，无实际轨道立体建模。
- 原有 BattleCell、SceneRoute、单位配置、塔及 Camera 均保持不变；不改变寻路、碰撞、编队和战斗逻辑。

打开 Level1GridBattle 即可在 Scene/Game 视图查看。可隐藏 `Railway Path Artwork` 整组来切换回原道路外观。

地图固定路径调整后，保存场景，再使用菜单 **Team5 → Rebuild → Level 1 → Rebuild flat railway artwork** 重建。工具会读取该场景的固定路径，仅替换同名装饰组；首次备份保存到忽略提交的 `Logs/RailwayPath/Level1GridBattleBeforeRails.unity`。

Unity 6000.3.22f1 已完成编译、实际相机离屏渲染及原地图/路径/单位配置/Camera 不变的签名检查。预览为 `Logs/RailwayPath/Before.png`、`After.png`，检查记录为 `Verification.txt`。没有自动操作桌面 GUI，未执行 Git Commit/Push。

## Level2

Level2GridBattle 同样铺设左、中、右三条平面铁轨，共 60 个贴图面、6 个弯道，覆盖原固定道路；起点和终点颜色标记保留。使用菜单 Team5 → Rebuild → Level 2 → Add railway and squad platform 重建。轨道和蓝白平台不改写原道路、路线、塔、小兵或战斗配置。验证/预览在 `Logs/Level2RailPlatform`，16 项 Play Mode 检查通过。
