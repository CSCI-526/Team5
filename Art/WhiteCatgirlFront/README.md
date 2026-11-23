# White Catgirl — front-view layered source

本文件夹只提供角色美术源文件，没有接入游戏、创建角色 Prefab 或改动现有场景。

## 文件

- `WhiteCatgirl_Front.psd`：1190 × 1322、RGB 8 位，14 个可见独立部件，另有一层默认隐藏的原图参考。
- `WhiteCatgirl_Front.psb`：相同内容的真正 PSB v2 格式，供后续 Unity PSD Importer 工作流使用。
- `Parts/`：14 个透明 PNG，按最终对齐尺寸裁切。坐标保存在 `layers.json`。
- `AlignedLayers/`：相同的 14 个部件，每张均保留完整 1190 × 1322 画布及其对齐位置。
- `WhiteCatgirl_Front_preview.png`：透明合成预览。
- `WhiteCatgirl_Front_review.jpg`：灰色背景检查图；灰色不是 PSD/PSB 的背景。
- `Source/Reference_Original.png`：未修改的用户原图。
- `Source/Generated_PartsAtlas.png`：内置 imagegen 补画的原始透明拆件图。
- `Source/imagegen_prompt.txt`、`Source/provenance.json`：生成提示词和制作记录。
- `validation.json`：文件结构、图层和通道像素回读检查结果。

## 用户要求

已删除原画不存在的 `F_COAT_BACK` 部件，PSD／PSB、预览和打包脚本均不再包含该层。Source 内的生成图集仅作为原始制作记录，其中第 3 格已弃用。另已按用户要求移除独立围巾 F_SCARF 图层和部件，第 16 格不再参与导出。

`F_BODY` 当前使用用户批准的边缘修整版 `Edits/F_BODY_cleaned.png`，副本保存在 `Source/Corrections/F_BODY.png`。打包时只裁去透明边距并适配现有图层范围。修整提示词和记录保存在 `Edits/`，上一版 AI 上衣及其历史提示词保存在 `Source/Corrections/`。

角色不露手。`F_SLEEVE_SCREEN_L` / `F_SLEEVE_SCREEN_R` 是两片完整宽袖，不存在手掌或手指图层。
尾巴独立保留为 `F_TAIL_STATIC`，便于编辑，但后续绑定时只需随身体整体移动，不做尾巴摆动动画。
每条腿连同靴子是一片连续图像；后续通过同一网格的骨骼权重产生膝盖弯曲，避免切成大腿、小腿后出现断缝。

## 后续接入当前 2.5D 的建议

源文件位于项目根目录 `Art/`，不会自动导入 Unity，也不会替换当前 Catgirl/FW 角色。
需要绑定时，将 **PSB 副本**放入一个新的 `Assets` 子目录，再使用项目已安装的 PSD Importer。
选择 Multiple / Mosaic（各图层独立 Sprite）及 Character Rig；保持隐藏原图参考不导入。
所有部件共用原点：画布左上为像素 (0,0)，建议角色脚底根位置约 (626,1265)。
`layers.json` 中 1000 pixels-per-unit 只是统一的起始建议，最终视觉高度应通过 Visual 根节点适配现有单位。
不要用可见 Sprite 宽度推导 BattleSimulation 的碰撞半径，也不要改动编队或寻路参数。

袖子将来可在完整网格内配置肩部/肘部权重；尾巴只挂身体，不额外添加动画轨道。
本次没有骨骼、蒙皮、Idle/Run 或背面。不能直接套用 FW Cute Girl 的动画。

## 制作与验证范围

使用用户原图作为内置 imagegen 编辑参考，补画了被遮住的头部、肩部、腰部及关节连接区域。
这是一套基于原图重建并对齐的可编辑部件，不是逐像素无损拆分；线条、表情和局部轮廓存在差异。
Python 仅用于裁切、缩放、图层定位、透明合成与 PSD/PSB 容器打包，没有程序绘制角色。

已检查透明合成外观；两种文件均已逐图层回读所有 RGBA 像素并与 PNG 对照，PSD 合成结果另经 Pillow 独立读取核对。
尚未在 Photoshop 或 Unity 中打开导入验证，也未做动画测试。

可重复打包：使用带 Pillow 的 Python 运行 `Tools/Art/build_white_catgirl_layers.py`，默认读取已保存的 Source 文件。
格式实现参考 [Adobe Photoshop File Formats Specification](https://www.adobe.com/devnet-apps/photoshop/fileformatashtml/)。
