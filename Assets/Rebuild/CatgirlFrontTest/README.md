# Front-only cutout character prototype

## View in Play Mode

Open `Assets/Rebuild/Scenes/CatgirlFrontTest.unity` and click Play. This is a copy of Scene 1 with the existing camera, map, squad, UI and battle configuration. Only Attack member **111**, slot **4**, now uses `FWLowerBody/CatgirlFWLowerBody.prefab`: Catgirl upper body, clothes and original boots with FW's native lower-body mesh/bones/animation. See `FWLowerBody/README.md` for the current hybrid. The other members keep the current FW look. Use the same click-to-move squad interaction as Scene 1; the new character plays Run while its simulation position changes, then returns to Idle. Pause selects Idle and freezes animation. Movement never selects a back view.

For the old look, exit Play Mode, select the BattlePresentation component and uncheck **Use Test Unit Visual** before playing again. This falls back to its existing FW Unit Prefab. The original Scene 1 is unchanged by this prototype implementation.

To restore the earlier full Catgirl test, keep **Use Test Unit Visual** enabled and assign `CatgirlFrontUnitVisual.prefab` as **Test Unit Prefab** instead. All original prototype assets below are retained; the following sections document that earlier independent Catgirl implementation.

## Assets and binding

- `Source/CatgirlFront.psb`: layered front art from the earlier generated asset pack. No back art is imported.
- `Source/Parts/*.png`, `Source/front_layers.json`: 25 source pieces, registration rectangles and layer order. The optional combined shoulder-yoke is hidden; 24 pieces are used in the prefab.
- `SkinnedSprites/*.asset`: 24 Unity Sprite assets with native bind matrices, sprite bones and vertex bone weights.
- `CatgirlFrontUnitVisual.prefab`: independent UnitView + camera-facing, mirrored rig + one Animator. It now has 20 active SpriteSkins: 18 rigid parts and two continuous three-bone leg meshes. Seven original split-leg/pelvis objects remain inactive for reference.
- `CatgirlFrontIdle.anim`, `CatgirlFrontRun.anim`: new looping animation clips, authored for this skeleton. They do not retarget FW animation and do not animate the Unit root.
- `CatgirlFront.controller`: Idle/Run states controlled by the existing `IsMoving` parameter, with 0.12-second transitions.

Run arm swing now follows the relative shoulder/elbow angular excursions and timing of FW's front Run, resampled to this character's existing 0.64-second cycle. Absolute FW joint angles and bone positions are not copied. Bent-elbow offsets and small wrist counter-rotations are authored for this cutout skeleton. The update changes only six arm rotation curves; head adjustments, all position/scale curves, legs and Idle are preserved. A copy of the previous Run is stored under `Logs/CatgirlFront/RunBeforeArmSwing_*.anim`.

Elbow refinement: both forearms now fold forward from their downward bind pose, rather than slightly oscillating around a nearly straight arm. The front and rear elbows use different bind-relative flexion ranges and follow the step cycle; horizontal mirroring reverses the complete pose. This refinement changes only the two forearm rotation curves and preserves the user's head/position/scale edits. The revision's 3/3 passing tests (`Logs/CatgirlFrontElbowsResults.xml`) measure actual shoulder-to-elbow versus elbow-to-wrist directions at nine phases across Run, ensuring both elbows remain visibly bent, in addition to skin deformation and rendered-image checks. The unchanged-curve check is recorded in `Logs/CatgirlFront/ElbowPreservation.json`.

## Continuous leg repair (current prefab)

`Source/ContinuousLegs` contains AI-repaired front-only continuous leg-and-boot images and shorts without duplicate skin beneath the hems. Each leg has one uninterrupted thigh-to-sole silhouette. The old internal black knee end-caps have been removed. The atlas, exact built-in imagegen prompt, registration metadata, raw pieces and registered PNGs are retained there. Original PSB and original split parts are unchanged; edits to that PSB do not automatically update these new images.

`ContinuousSprites` contains the new Sprite assets. Each leg has **637 serialized vertices**, a shared triangle grid, and three bone influences (`Thigh`, `Shin`, `Boot`). Weights blend smoothly over a 0.12-unit knee region and a 0.09-unit calf-to-ankle region above the boot cuff; the complete boot stays rigid under the ankle influence. Geometry/UV/index channels are written with native Sprite data APIs so they survive Unity restarts. The boot is part of the continuous image and mesh, retaining independent ankle influence. Existing bone names and animation paths stay the same.

`Assets/Rebuild/Editor/CatgirlContinuousLegBuilder.cs` installs this repair without rebuilding the character or its clips. That initial mesh-only repair preserved both clips byte-for-byte; the later knee-cycle update below modifies Run's leg curves. The old prefab is preserved at `Backup/CatgirlFrontUnitVisual_SplitLegs.prefab`; to inspect the previous art/binding, assign that backup as the test scene's Test Unit Prefab before Play. It shares the current animation assets; the earlier clips are separately preserved in Logs. Do not re-enable inactive split parts on top of the continuous legs.

Current verification: **4/4 Play Mode tests passed, no skips**, recorded in `Logs/CatgirlContinuousLegResults.xml`. Checks include mesh persistence after restarting Unity, mixed knee/ankle weights, all weights summing to one, actual native deformation/rendered movement, elbow bending, paused Idle, simulation-driven movement, original FW regression, and each isolated leg at nine Run phases. In all 18 leg captures, more than 99.5% of foreground pixels belong to one connected silhouette. This checks separation/cracks; it is not a guarantee of production-quality anatomy or foot planting in every future pose.

## Run knee cycle (latest)

Run now alternates ground-contact extension and recovery flexion, with a half-cycle phase difference between legs. Shin curves fold backward by 6–64 degrees relative to the new skeleton; the actual hip/knee/ankle joint angle is tested at below 20 degrees for support and between 50–80 degrees for recovery. Thigh rotations compensate for the illustration's wide standing pose. Ankle counter-rotation keeps sole pitch around +/-8 degrees. Small local thigh-Y adjustments approximate ground contact during support without moving the Unit root or changing upper-body curves. This is an authored visual cycle, not terrain-aware IK or dynamic foot planting.

Only eight Run leg curves changed: rotations of both thighs, shins and boots, plus the two thigh Y curves. All head, arm, pelvis and other upper-body curves are preserved; Idle is unchanged. The previous Run is in `Logs/CatgirlFront/RunBeforeKneeCycle_*.anim`. Previous leg weights are in `Logs/CatgirlFront/BeforeKneeAnkleWeights_*.asset`. `KneeCyclePreservation.json` records the unchanged upper-body bindings.

Latest validation: `Logs/CatgirlKneeCycleResults.xml`, **4/4 passed, 0 skipped**. In addition to the continuous-mesh tests above, the tests check alternating knee flexion and loop-boundary position/rotation continuity. Isolated silhouettes now use a magenta test background, since charcoal can match the dark boot cuff and falsely classify it as a gap. The game Camera/background is unchanged. Whole-character captures remain on a dark test background in `Logs/CatgirlFront/RunA.png` and `RunB.png`.

The rig has nested pelvis/chest/head, separate hair and coat joints, upper/lower arms/hands, thighs/shins/boots. Registration uses 635 pixels per unit and a shared foot origin at source pixel (363,1053). Display scale is 0.8 and visual tilt is 30 degrees. The new head retains the source art's proportion; the old FW-specific 70% head bone adjustment is not applied a second time.

`FWUnitVisual` is reused to observe simulation movement, set the animation state, mirror the entire character and apply the existing world-Z material/sorting. Its new `artworkFacesRight` flag accounts for the new artwork's opposite neutral direction. It now also supports a front-only rig without a Back Animator. Those options do not change the defaults of the original FW prefab.

All true Unit positions, identities, slots, collision radii, formation spacing, pathfinding and combat remain owned by the existing simulation. Sprite size is never used to compute a collision radius. Animator Root Motion is disabled. The new art retains its source white/olive hair; the FW brown-hair recoloring mask is not applied to it.

## Validation

Unity 6000.3.22f1 imported the front PSB as 24 visible Sprite layers. The builder completed in BatchMode and the project compiled. D3D11 Play Mode verification passed **3/3** tests, with **0 skips**:

1. New native SpriteSkin binding and actual deformed vertices move; rendered Run poses differ; Idle bones animate; pause freezes Idle; four movement directions keep the front view; left/right mirroring is correct; the Unit root stays fixed in the isolated animation test.
2. The copied test scene replaces only member 111; real simulation movement starts Run and pause selects Idle.
3. Existing FW front/back animation and root-position regression test still passes.

Results: `Logs/CatgirlFrontResults.xml`. Captures: `Logs/CatgirlFront/Idle.png`, `RunA.png`, `RunB.png`. Logs are verification artifacts, not runtime dependencies.

Arm-swing revision: `Logs/CatgirlFrontArmsResults.xml` reports 3/3 passing Play Mode tests, including native deformed-vertex movement for both hands, rendered pose changes, pause, simulation-driven Run and the original FW regression. `Logs/CatgirlFront/ArmSwingPreservation.json` records that only the six arm rotation curves changed and all position/scale/non-arm curves were preserved.

Not yet polished or exhaustively tested: foot planting, joint seams during larger poses, attack/weapons, directional back views, role hair recoloring for this art, and every wall/transparent-object arrangement. World-Z rendering uses the existing shader and sorting implementation; no Camera or map redesign is included.

The source is AI-redrawn/completed art based on the supplied image. It is not pixel-identical to the source illustration, and the prototype adds no third-party character license. Intended use remains the user's noncommercial research context.

## Builder and tests

`Assets/Rebuild/Editor/CatgirlFrontPrototypeBuilder.cs` documents and generates the bind poses, native Sprite assets, skeleton, clips, prefab and copied test scene. Menu: **Team5 > Rebuild > Catgirl Front > Build prototype (once)**. Assets are already generated; the builder refuses to overwrite an existing prefab or test scene. Edit the delivered prefab/clips directly for art iteration.

`Assets/Rebuild/Tests/PlayMode/CatgirlFrontVisualTests.cs` contains the two prototype tests. No new packages, Spine Runtime or external movement scripts are used. No Git operations are required.
