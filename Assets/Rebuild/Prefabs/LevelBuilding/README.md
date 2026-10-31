# Level Building Prefab Kit

This directory contains 27 prefabs. The complete example comes from the saved Level2 scene and preserves its camera, UI, and combat rules. Existing scenes, the user's tower prefab, shared rosters, and economy settings were not modified.

## Start with the complete example

1. Create a new scene in Unity. Remove its default Main Camera and Directional Light to avoid duplicating the camera and light included in the kit.
2. Drag `Maps/LevelStarter_3D.prefab` into the Hierarchy. Keep the root Position at `(0,0,0)`, Rotation at `(0,0,0)`, and Scale at `(1,1,1)`.
3. Save the scene under a new name in `Assets/Rebuild/Scenes/`. It is a separate scene from the original Level2.
4. This prefab includes a connected GameplayGrid, a 24-node route, two towers, BattleSession, unit presentation, HUD, camera, light, and debug Gizmos. It is configured for standalone combat testing in Play Mode. Its initial displayed level number is 4, and it has no Level1 tutorial.
5. In Edit Mode, right-click the root object and choose Prefab → **Unpack Completely** before editing the map as an independent scene layout. Do not use Apply All on the entire LevelStarter, since that would change the template used for future levels.

The new scene is a standalone combat test. Currently, `BattleSceneFlow` recognizes four configured campaign scenes (Level1 through Level4). **Setting a displayed level number or adding a new scene to Build Settings does not automatically add that scene to the campaign.** To enable campaign progression, settlement, defeat retries, and warehouse map previews, register the scene in the level list in `BattleSceneFlow` and configure `RebuildEconomy` as needed. This kit does not change the existing campaign flow.

## Asset inventory

| Folder | Contents | Purpose |
|---|---|---|
| Maps | LevelStarter_3D | Complete example with a map and connected systems; recommended starting point |
| Systems | BattleRig_3D | Empty level skeleton with an empty grid, route, and tower container, plus connected Session, HUD, presentation, camera, light, and Gizmos |
| Tiles | Ground / Road / Obstacle / TowerPlatform / Spawn / Goal `_Cell` prefabs | Six individual Small Cell types with rule components, models, and matching materials |
| Blocks | Six `_3x3` prefabs | Nine-cell groups for building three-cell-wide roads, spawn areas, goals, and platforms |
| Towers | Square / PositiveX / NegativeX / PositiveZ / NegativeZ, plus Tower_Attack_1x1 and Tower_Attack_1x1_Square | Five Macro towers, a detailed single-cell attack tower, and its Square Prefab Variant |
| TowerPlatforms | Five `_WithPlatform` prefabs | One tower and a complete nine-cell platform; the tower's local foot height is already set to 0.25 |
| Decoration | DecorativeBorder_Cell | Visual border tile that does not participate in grid occupancy or combat |

Unit models reuse `Assets/Rebuild/Prefabs/MinimalUnitView.prefab`, which is already referenced by the system prefabs. Role appearances are generated from the actual simulation members. The HUD includes the required font and UI references. Existing scripts initialize pause controls, inspection, formation editing, health bars, and the UI EventSystem; these do not require separate prefab placement. The initial roster still references `PrototypeSquad.asset`, which currently contains only the Captain. The kit does not automatically add roles or modify existing assets.

## Placement rules

- Small Cell: the parent object contains BattleCell, and its visual child is named `Cell Mesh`. Physical size is 1×0.5×1. Place it under GameplayGrid, use integer X/Z coordinates and multiples of 0.5 for Y, and keep rotation at zero and scale at one.
- 3×3 groups: the root is at the center, with nine cells at local X/Z coordinates `-1..1`. Align tower platforms and Spawn/Goal Macro groups to world centers of `3n+1`, such as `(22,0,13)` or `(25,0,13)`.
- **Replace existing cells instead of overlapping them.** Before placing a group on an existing grid, delete or move the original nine cells it covers. Hiding only the Renderer is insufficient: the original BattleCell still exists.
- Place the entire `_WithPlatform` group under GameplayGrid. For example, a group root at `(22,0,13)` puts its tower at `(22,0.25,13)`. Add the tower child containing BattleTower to the Session's Towers list.
- Place standalone towers in the `Scene Towers` container, with their feet on the platform's top surface. For example, a platform root at `(22,0,13)` requires a tower Position of `(22,0.25,13)`. All nine supporting cells must be TowerPlatform.
- **Single-cell tower:** `Towers/Tower_Attack_1x1.prefab` has Uses Macro Footprint disabled and needs only one clear TowerPlatform cell. Place its feet at that cell's top center, for example `(22,0.25,13)` above a cell centered at `(22,0,13)`, and add it to BattleSession → Towers. Integer X/Z positions are supported; Macro-center alignment is not required. Its model fits inside one 1×1 cell and is 1.58 units tall, with a stepped base, stone shaft, battlements, and a cannon. Range=3.5 uses the existing square attack area; HP=20, Damage=1, Attack Interval=1. The cannon is a visual model and does not rotate to track targets. It uses six shared materials in `Assets/Rebuild/Materials/SingleCellTower/`.
- **Square variant:** `Towers/Tower_Attack_1x1_Square.prefab` is a Unity Prefab Variant of the single-cell tower. Its visual overrides replace the directional cannon silhouette with a central cyan energy core, four symmetric emitters, and a gold square frame. It uses the dedicated emissive `AreaEnergy.mat` without changing the base tower's materials. Attack Area is explicitly Square, Uses Macro Footprint is false, and combat stats are inherited. The base tower currently also uses Square; the separate variant lets you tune its parameters independently. It uses the same single-cell placement and Towers registration steps as the base. The emitters are visual only: the existing Square targeting, projectile, and damage rules are unchanged; this model does not add splash damage.
- A directional tower's firing direction comes from **BattleTower → Attack Area**. Rotating the model alone does not change its firing direction. The Square preset uses Range=3.5; directional presets use Range=9 and Attack Width=0.9. All use HP=20, Damage=1, and Attack Interval=1. Adjust these values for your level as needed.
- Place decorative tiles under Decorative Border Tiles. They have no BattleCell or Collider and cannot replace gameplay cells.
- Cell rule types and visual materials are separate settings. The kit includes matching materials for each type, but changing Kind later does not automatically update the color.

## Register routes and towers

A prefab cannot reference scene objects that you will place in the future. The complete example already has its default bindings; maintain the following when adding, replacing, or deleting objects:

1. **SceneRoute → Cells**: assign actual BattleCell references in travel order. Consecutive nodes must be one cell apart along X or Z, at equal height, with no duplicates. Start with Spawn and end with Goal. Even on a 3×3 road, include each Small Cell along the centerline; do not skip directly between Macro centers three cells apart.
2. Keep the route's **Required Width=3**. The complete 3×3 area around every centerline node, including corners, must consist of Road / Spawn / Goal cells. The formation cannot cross Ground, Obstacle, TowerPlatform, or missing cells.
3. **BattleSession → Towers**: add the BattleTower component of every tower that should participate in combat. For combined prefabs, select the tower child rather than the platform root.
4. The Session's Grid and Route, Presentation's Session, and HUD's Session are already connected inside the templates. Do not add a second Session, camera, or HUD.
5. The Session's **Displayed Level Number** controls the label. **Level Time Multiplier** defaults to 1, and **Attempts Per Level** is 3. The displayed number does not register a scene with the campaign.
6. Save the scene, open the menu at the top-right of the BattleSession component, select **Validate Scene Configuration**, and check the Console. Enter Play Mode after the configuration is valid.

Use `Systems/BattleRig_3D` to build a map entirely from scratch. Its Cells and Towers lists are empty. **Combat cannot start until the route, cells, and towers are configured.** Choose either the complete example or the empty skeleton; do not place both in the same scene.

## Validation scope

Offline serialization checks covered all 27 prefabs: complete hierarchies, unique fileIDs/GUIDs, local and external resource references, cell types and sizes, nine-cell coordinates, and tower directions. The single-cell tower was also checked for its one-cell rule configuration and model bounds. The Square variant was checked for its base asset, visual/material override targets, four emitters, central core, one-cell model bounds, and explicit Square/one-cell settings. The complete example was checked for route continuity, nine-slot formation clearance, tower platforms, and Session/HUD/Presentation bindings. Unity was not automated, and Unity import, Play Mode, and player build verification were not performed.

Read-only validation command: `python Tools/Rebuild/create_level_prefab_kit.py --validate`. Creation mode refuses to overwrite an existing kit to protect subsequent manual edits.
