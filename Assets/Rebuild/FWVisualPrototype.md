# FW Cute Girl 2.5D visual prototype

The original prototype remains in `FWUnitVisualTest.unity`. It copies the existing
Haon test scene, preserving its camera, terrain, route, roster and combat settings.
Member 111 (Attack, slot 4) uses FW's SD Miho; member 110 retains its original view.
Scene 1 now uses FW for all members, with 0.6 preferred open-ground spacing and 0.3 obstacle
clearance radius. Compact squad mode allows allies to overlap and share a guide
route, with leading members waiting for the group. These logical settings remain
independent of the visible character size.
Its world-Z prefab scales the whole visual to 80% of the original size. Narrow route
sections contract offsets toward 0.45 spacing or merge onto the shared guide; open
sections restore 0.6 spacing during travel.

## View in Unity

Open `Assets/Rebuild/Scenes/FWUnitVisualTest.unity` and press Play.
Select `Minimal Presentation (Runtime Unit Views)` under the battle rig.
The `FW Visual Test Switch` component's **Use 2D** checkbox switches just member 111
between FW 2.5D and the existing Haon 3D prefab, including during Play Mode.
The battle is not restarted when switching. Use the existing battle pause control
to check that the character returns to Idle and stops animating.

## Assets and ownership

- `Assets/FW_MIHO`: original FW source files with their package GUIDs. The package's
  `Packages/manifest.json` and all project settings are excluded from import.
- `Assets/Rebuild/FWTest/FWUnitVisual.prefab`: independent copy of Miho, with the demo
  input/movement script and demo shadow removed. Original source stays available.
- `FWFront.controller`: original `F_IDLE01` / `F_RUN01` clips.
- `FWBack.controller`: original `B_IDLE01` / `B_RUN01` clips.
- `FWSprite.mat`: URP Sprite Unlit material with normal depth testing, no depth write.
- `FWUnitVisual`: simulation observer, billboard, directional facing and animation.
- `FWVisualTestSwitch`: test-only visual selection.
- `FWVisualPrototypeBuilder`: one-time asset generator; refuses existing outputs.

The visual's front/back rigs are fitted to a height of 1.65 world units at build
time. To change appearance size, edit their common `Mirror Entire Character`
transform scale in the independent prefab. No visible sprite bounds are used to
derive soldier collision radius, formation spacing, navigation or combat data.

The artwork has front/back diagonal views, rather than eight unique directions.
Whole-character mirroring supplies left/right. Front/back changes copy normalized
animation phase; Idle/Run blend over 0.12 seconds. Root Motion is disabled. The
billboard rotates only a visual child. UnitView alone follows BattleSimulation.
SortingGroup keeps each character's parts together, sorted by foot depth; ordinary
3D depth testing and the existing WallOcclusionView still apply.

## Dependencies and rebuilding

Unity 6000.3: 2D Animation 13.0.6 and PSD Importer 12.0.2. No Spine or Character Free
Set is used. PSD bone bindings must survive import; automated checks exercise real
bone movement in all four clips, not just Animator state names.

Selective importer: `python Tools/Rebuild/import_fw_visual.py` (refuses conflicts).
After dependencies resolve, use `Team5 > Rebuild > FW Visual > Build prototype assets`
only if prototype outputs do not yet exist. No production scenes are edited.

Tests: `Team5.Rebuild.Tests.FWUnitVisualTests` (EditMode) and
`Team5.Rebuild.Tests.FWUnitVisualPlayModeTests` (PlayMode).

Verified on Unity 6000.3.22f1: compilation, two EditMode checks, and ten PlayMode
checks (real bone animation in four clips, direction/phase/mirror, pause/resume,
simulation-preserving 3D/2.5D switch, and rendered 3D wall depth occlusion).
Results and actual rendered images are in `Logs/FWVisual/`. This is a visual-only
prototype; dense formations and dynamic wall fade have not received a manual
gameplay review in this pass.

Official Unity 6000.3 package compatibility:
[2D Animation](https://docs.unity.com/zh-cn/engine/6000.3/manual/packages-list/packages-all/pack-safe/com-unity-2d-animation),
[PSD Importer](https://docs.unity.com/zh-cn/engine/6000.3/manual/packages-list/packages-all/pack-safe/com-unity-2d-psdimporter).

## Scene 1 world-Z priority

Scene 1 references the independent `FWUnitVisual_WorldZ.prefab` and
`FWWorldZSprite.mat`, leaving the original depth-tested test prefab intact.
Smaller foot world Z gives characters a higher SortingGroup order. Transparent
scene geometry shares this order through the scene-local `WorldZSceneSorting`
component, including faded walls and range effects; Canvas UI is excluded.

Opaque geometry needs more than SortingGroup: the custom skinned sprite shader
uses the existing PC pipeline depth texture. It ignores an occluding surface
when its reconstructed world Z is larger than the character foot Z, and retains
normal depth occlusion for nearer-Z surfaces. This comparison uses the visible
surface Z (not a mesh pivot that might be far away from its geometry).
No camera transform/projection, map geometry, combat or navigation is changed.
The active PC pipeline already enables its depth texture; no pipeline asset is
modified. This material requires a URP depth texture if used with another pipeline.

World-Z verification: `Logs/FWVisual/WorldZPlayMode.xml` reports all four PlayMode
checks passed. Actual rendered fixtures cover rear opaque/transparent objects
(character remains visible), front opaque/transparent objects (normal occlusion),
and a reversed camera direction to confirm rear priority follows world Z.
Screenshots are `WorldZRearOpaque.png`, `WorldZRearTransparent.png`,
`WorldZFrontOpaque.png`, and `WorldZFrontTransparent.png` in the same log folder.

### GPU animation compatibility

Do not set MaterialPropertyBlock on these SpriteRenderers: Unity 2D Animation GPU
deformation requires SRP batching and explicitly excludes renderer property blocks.
`FWUnitVisual` owns one world-Z material per unit, shared by that unit's front/back
parts, and writes `_FWFootZ` on the owned material. It destroys the material with
the unit. The original material asset is never changed by foot-position updates.
The regression suite compares actual rendered Run poses and exercises continuous
movement, Run/Idle switching and independent materials in Scene 1.
`Logs/FWVisual/AnimationFixPlayMode.xml` reports all six checks passed. Compare
`WorldZRunPoseA.png` and `WorldZRunPoseB.png` in that folder for rendered animation
poses while world-Z sorting updates every frame. The Scene 1 integration check
issues a real continuous movement command, verifies Run, then pauses and verifies
Idle, and checks all four units have independent foot-Z materials.

## Scene 1 role hair colors

The world-Z prefab enables `matchRoleHairColor`. Colors come directly from
`LegacyUnitLooks.For(role).Body`, the icon palette: Attack red, Tank blue-gray,
Heal green, Captain gold-brown. The original FW resources and other visual prefabs
remain unchanged. Full hair sprites `F_B_HAIR` and `B_HAIR` use a hair material;
`F_FACE` uses a generated mask because the front fringe shares a sprite with skin.
Other sprites retain the original body material. Painted shading is preserved
while replacing the brown hue; renderer property blocks are not used.

`FWFrontHairMask.png` is generated by `FWHairMaskBuilder.Build` from the original
front atlas, restricted to warm hair pixels connected to the upper head region.
The source PSB is not modified. The body, hair and masked face materials are owned
per unit, share the same foot-Z value and are destroyed with the view.

`Logs/FWVisual/HairPlayMode.xml`: all seven PlayMode checks passed. Eight actual
front/back screenshots (`HairAttackFront.png`, `HairAttackBack.png`, and the Tank,
Heal and Captain equivalents) are in that folder. Image review confirms facial
features, clothing, and front/back hair coverage remain intact.

## Facing during formation exchanges

FWUnitVisual separates the direction used for facing from the displacement used
for Run/Idle. During explicit continuous formation edits, local slot corrections
still animate, but facing follows actual squad-reference movement. Stationary
exchanges keep the previous facing, and travelling exchanges follow the squad's
forward direction. The final reform tick is included so stopping does not leave
the character flipped. A formation event recenters the reference sample without
treating that logical recentering as physical movement. Normal travel still turns
using actual unit displacement. Combat and navigation state are unchanged.

`Logs/FWVisual/FormationFacingPlayMode.xml`: all eight PlayMode checks passed,
including actual Scene 1 stationary/moving slot exchanges, preserved front/back
and mirror state, movement animation, return to Idle, and subsequent normal turns.

## Display-plane tilt experiment

Scene 1's world-Z prefab sets `Display Tilt Degrees` to 30. The visual plane rotates
from direct camera-facing toward world upright by that many degrees; the Camera,
unit root, foot anchor, sprite scale, collision and navigation are untouched. Zero
restores the original camera-facing plane. The original test prefab defaults to 0.
This gives mild foreshortening; it does not create new top-of-head artwork.

`Logs/FWVisual/DisplayTiltPlayMode.xml`: all nine PlayMode checks passed, including
tilt angle, unchanged Camera and foot anchor, animation, formation facing, role
hair colors and Z occlusion. `DisplayTilt0.png` and `DisplayTilt15.png` show the
actual rendered comparison. Adjust the field on FWUnitVisual_WorldZ.prefab to
change the default for every Scene 1 member, or preview it on a runtime unit.

## Smaller head proportions

Scene 1's world-Z prefab scales `F_HEAD_bone_4` and `B_HEAD_bone_28` to 0.7 of their original scale.
The face and descendant hair bones therefore shrink together around the existing
neck joint. Their position, hierarchy and animation paths are preserved; the
original four Idle/Run clips have no head-scale tracks that override this value.
Torso/limb scales and the overall 0.8 visual scale remain unchanged. The 30-degree
display tilt, role hair colors, collision radius and formation spacing are retained.

`Logs/FWVisual/HeadScalePlayMode.xml`: all ten PlayMode checks passed at the previous 0.9 head setting, including
front/back head scaling through Idle/Run, hair attachment, unchanged torso scale,
30-degree tilt, role color renders, formation facing and world-Z occlusion.
The subsequent 0.7 head-size adjustment and matching assertion were checked in the
prefab/source only; PlayMode has not been rerun for that parameter change.
