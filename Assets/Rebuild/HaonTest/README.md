# Haon single-unit visual test

Open `Assets/Rebuild/Scenes/HaonUnitTest.unity` in the CURRENT Team5 project, allow Unity to import/compile, then press Play.

## What to see

- A normal block Captain remains member 110, slot 0.
- Attack member **111**, slot **4** (center), uses the complete **Haon Costume02 Misaki** character. This is a new test roster, not a change to a production roster.
- The character has empty hands. Original block meshes are absent from this visual prefab.
- Actual route progress drives Run, and unchanged progress drives Idle. Movement direction is smoothed to the route direction.
- Use the existing battle HUD **Pause** button: the character switches immediately to an Idle pose and freezes. Resume to return to Run. If movement stops while the simulation continues (e.g. a surviving wall), the Idle loop plays normally.
- Root Motion is disabled. Animation never writes the simulation's position, MemberId, SlotId, stats, or formation.

## Switch back

Before Play, select the scene object with **BattlePresentation** (Minimal Presentation). Turn off **Use Test Unit Visual** to use the original MinimalUnitView for every member. The original prefab is retained. The optional override defaults to OFF in all existing scenes.

## Source assets actually used

Local cache: `AppData/Roaming/Unity/Asset Store-5.x/Haon/3D ModelsCharactersHumanoids/Haon SD series Free Bundle.unitypackage`.

Source character: `Assets/Haons SD series Pack/Prefab/CharacterSet/prf_Set Costume02 Misaki.prefab`.

Shared Avatar: `Assets/Haons SD series Pack/Animation Setting/_Avatar_HaonSdCharacter_Common.fbx` (Humanoid).

- Idle: `Animation/Common/StandA_idleA.FBX`, clip **StandA_idleA**, loop enabled, Humanoid.
- Run: `Animation/Common/RunA_Front.FBX`, clip **RunA_front@loop**, loop enabled, Humanoid.
- `HaonIdleRun.controller`: boolean **IsMoving**, Idle <-> Run, 0.15 second transitions.

The complete unarmed character sets present include Costume01 Unity-Chan, Costume02 Misaki/UTC/Yuko, and Costume03 Misaki/Unity-Chan/Yuko; the bundle also contains prop/weapon/demo variants that this test does not use. Mesh parts retain their original Rig settings; the complete character uses the supplied Humanoid Avatar. No Rig import setting was changed.

## Asset handling

499 new files were unpacked from the local package, including source models, prefab parts, animation, materials, textures and license files. Existing files were checked for conflicts before writing; zero existing files were overwritten. The automatic dependency installer, sample scenes/settings and guide assets were excluded. Packages/manifest.json and packages-lock.json were not modified.

The source package's optional SpringBone behaviours require another package, and its materials use ToonShader. For this isolated test, **Parts/** contains independent copies with optional spring/demo scripts removed; all meshes, bones and visual part references remain. **Materials/** contains 10 independent URP/Lit material conversions using the source Toon material's actual _MainTex and color. Original vendor assets remain available, with their original configuration. No optional dependency is installed.

`HaonUnitVisual.prefab` is a UnitView root with a separate animated child. Its initial fitting samples 16 Idle/Run poses using temporary baked skinned bounds, fits within 0.72 ground width/depth and at most 1.2 height, and aligns the feet to the simulation position. The model child's position offset is then preserved; only its visual rotation changes. No Collider, Rigidbody, movement controller, weapon or combat animation is added.

## Files

- Modified: `Scripts/Presentation/Minimal/BattlePresentation.cs` (optional member-specific visual override only).
- New: `Scripts/Presentation/Minimal/HaonUnitAnimation.cs`.
- New: this HaonTest folder: visual prefab, controller, roster, 10 sanitized part prefabs, 10 URP materials.
- New: `Scenes/HaonUnitTest.unity`, copied from LevelTemplate; only the session roster binding and Presentation override differ.
- New: `Tests/EditMode/HaonUnitVisualTests.cs`, `Tests/PlayMode/HaonUnitPlayModeTests.cs`.
- New: `Tools/Rebuild/create_haon_unit_test.py` (generation script; re-running intentionally regenerates test assets).
- Imported: `Assets/Haons SD series Pack/` (selected local package resources).
- Associated .meta files accompany the new Assets.

## Verification status

PASS: offline C# compile of runtime, Editor and new tests against the installed Unity references; all sanitized prefab local references and external asset GUIDs resolve; Idle/Run clip IDs, names, loop flags and Humanoid importer settings checked; cloned scene camera/map/route/tower blocks unchanged; BattleSimulation, UnitView and package manifests unchanged relative to Git HEAD.

NOT RUN: Unity EditMode/PlayMode tests, actual Avatar import validity, skinned animation playback/scale/facing/foot grounding, visual URP appearance and manual Game View inspection. The existing Unity Editor (process 8048 at inspection time) holds this project's lock; no second Unity process or GUI automation was used. No Commit, Push, Pull or Merge was performed.

The new Unity tests are ready to run from Test Runner after import. The scene does not need Build Settings registration for direct Editor Play; it is deliberately outside the campaign.

Reports: `Logs/HaonUnitTest/ImportReport.json`, `SetupReport.json`, `StaticValidation.json`.

## Vendor umbrella import issue

The bundled `Prefab/Prop_AttachToHand(OptUseAnim)/prf_Prop_R_Umbrella0_v1_dummy.prefab` references a variant parent (GUID 9e370e0536bb6da4c98fcdb74ddfcf0e) absent from the unitypackage. It is not used by this character. The unused prefab and its .meta have been moved to `Logs/HaonUnitTest/ExcludedVendorAssets/` to prevent a project import error, with SHA-256 checks in `ExcludedVendorAssets.json`. FBX blendshape-normal warnings are upstream asset warnings and were not suppressed by changing Rig or animation settings.

## Scene 1: additional 3D role variants

Scene 1 uses Attack 111/slot 4, Tank 112/slot 7, Heal 113/slot 3 and Captain 114/slot 1. The three new units use existing UnitRole values and SquadCombatRules stats. Their prefabs live under Tank3D, Heal3D and Captain3D, selected through BattlePresentation Additional Test Visuals. The single-member override still selects the enlarged Attack. Disabling Use Test Unit Visual restores block visuals for all four members.

Tank3D uses Costume02 Yuko with independent gray-blue hair material copies. Its source right-eye preset points at a material missing from the bundle; the independent test copy uses the matching left-eye A4 material. Heal3D uses Costume02 Misaki with green hair material copies (no green preset exists in the supplied bundle). Captain3D uses Costume02 UTC and retains its golden hair. All use empty hands, Idle/Run clips, and 1.65 height / 0.99 footprint fitting caps. Source assets are preserved.

Validation: runtime sources compile offline; all prefab local/external references resolve; battle rules and stat tables match Git HEAD. Actual hair colors, models and animation in Play Mode remain to be checked in the open Unity Editor.
