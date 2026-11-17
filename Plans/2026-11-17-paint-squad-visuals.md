# Paint Squad visuals for Team5

Branch: `feature/minion-visuals` (cut from `origin/main` at f023188). Repo: `~/Team5` (github.com/CSCI-526/Team5).
Source project (read only): `~/TactileWars` ("Paint Squad Wars", the owner's own original-asset Unity game).

Spec: the owner's request, approved 2026-11-17:
1. Copy Paint Squad Wars' visual design language into Team5: cards, buttons, background, banners, fonts, on ALL screens.
2. Copy the player units (tiny helmeted chibi soldiers): models, weapons, animations, shadows, VFX. One look per Team5 role, distinguished by helmet colour plus role gear (table below).
3. Add environment dressing (trees, bushes, rocks, flowers etc.) to the levels.
4. A character select at game start: "Paint Squad" vs "Catgirl" (the teammates' existing full role set). Haon and FW are NOT offered (single-character prototypes).
5. Leave enemy tower guards (`MinionLook`, `MinionField`, etc.) unchanged.

## Global Constraints

- Unity 6000.3.22f1 at `/Applications/Unity/Hub/Editor/6000.3.22f1/Unity.app/Contents/MacOS/Unity`, URP. The owner has the TactileWars project open in the Unity editor: NEVER open, modify, or close it; only read its files from disk. Team5 batchmode runs are fine (`-batchmode -nographics -projectPath ~/Team5 -quit`, add `-runTests -testPlatform EditMode|PlayMode -testResults <file>` for tests). The machine is an M2 with 8 GB RAM: run one Team5 Unity process at a time.
- All new Team5 content lives under `Assets/Rebuild/PaintSquad/` (assets) and Team5's existing assemblies/namespaces (`Team5.Rebuild.*`). Name everything `PaintSquad*`. Never use the word "Minion" for the new player units: `Minion*` already means enemy tower guards in Team5.
- Copy only art (FBX, materials, shaders, textures, fonts with their OFL licence files, sprites, animation clips/controllers/masks). Do NOT copy TactileWars C# scripts or prefabs that carry TactileWars scripts; rebuild prefabs on Team5 components (`UnitView`, etc.). If a shader or material references a TactileWars script/asset, port the minimum needed. Copy `.meta` files with assets so imports keep settings, and confirm no GUID collides with an existing Team5 GUID.
- Simulation, Session, World and Analytics rules do not change, except the single analytics field in Task 3. No change to gameplay numbers, routes, occupancy, or level layouts.
- Existing tests (EditMode and PlayMode under `Assets/Rebuild/Tests`) must keep passing, including Catgirl/Haon/FW visual tests. If a test was already failing on `origin/main` before your change, record that with evidence instead of "fixing" it.
- Catgirl must look and behave exactly as before when selected.
- Batch-generated content is produced by editor scripts under `Assets/Rebuild/Editor/` (menu `Team5/Rebuild/Paint Squad/...`) so it can be rebuilt, matching the repo's existing builder pattern (e.g. `CatgirlRoleVisualBuilder`).
- Writing style in comments, docs, commit messages: no em dashes. Commit messages: subject plus body, no AI attribution or Co-Authored-By lines.
- Commit per task on `feature/minion-visuals`. Do not push.
- Docs: add/extend `Assets/Rebuild/PaintSquad.md` describing what was added and how to rebuild it.

## Role look table (binding)

| Role | Helmet colour | Base model | Role gear |
|---|---|---|---|
| Attack | Red #DA4029 | Soldier | standard paint rifle |
| Tank | Orange #F59A1A | SoldierHeavy | heavy body |
| Heal | Green #8DB52B | Soldier | medic cross on helmet, no gun |
| Captain | Gold #E8B923 | Hero | spiked hero helmet |
| Sniper | Blue #3F7FD0 | Soldier | long rifle |
| Vanguard | Teal #229A8A | Soldier | small shield plus rifle |
| Demolisher | Brown #8A5A34 | Soldier | bazooka |
| Frostweaver | Ice cyan #7FD6F0 | Soldier | ice staff |
| Warcaller | Pink #DB4793 | Soldier | banner on back |
| Aegis | Purple #8F63B0 | Soldier | big tower shield |

`UnitRole.Test` uses the Attack look. Dark variant = primary at about 65 percent value.

## Task 1: Port Paint Squad art into Team5

Copy from `~/TactileWars/Assets` into `~/Team5/Assets/Rebuild/PaintSquad/` (keep a sensible subfolder layout: Models/Characters, Models/Environment, Animations, Materials, Shaders, Fonts, UI, VFX):
- `Art/Models/Characters/` Soldier.fbx, SoldierHeavy.fbx, Hero.fbx (and anything they need), with all takes (Idle, Run, Shoot, Hit, Die, Cheer).
- `Art/Animations/` Soldier, SoldierHeavy, Hero controllers and Upper masks.
- `Art/Shaders/` TWToon, TWToonOutline, TWToonCommon.hlsl, TWDecal, TWParticle (others only if required).
- `Art/Materials/` the slot materials the characters and environment use, outlined unit variants, blob shadow / decal and particle materials needed by the VFX below.
- `Art/Fonts/` Fredoka and Lilita One TTF, SDF assets, outline materials, licence files.
- `Art/UI/` Shapes and Icons (Renders only if needed by later tasks; they are TactileWars-specific renders, so skip them unless useful).
- Muzzle flash, hit splash and paintball VFX (from `Assets/Prefabs` / `Art/VFX`), rebuilt without TactileWars scripts.
- `Art/Models/Environment/` Tree_A/B/C, Bush_A/B, Rock_A/B/C, GrassTuft, Flower_A/B, Fence, Crate, Barrel, Log, plus Tent from Structures.
Read `~/TactileWars/Docs/ASSET_INVENTORY.md` and `VISUAL_STYLE_GUIDE.md` first.
Acceptance: Team5 batchmode import has zero errors and no missing-script/missing-shader warnings for the copied assets (paste the relevant log lines); an EditMode test `PaintSquadAssetTests` asserts each copied model, controller, font asset and shader loads via `AssetDatabase` and that every shader compiles for URP (`ShaderUtil.ShaderHasError` false); existing suites still pass.

## Task 2: Paint Squad role prefabs and catalog

- An editor builder `PaintSquadRoleVisualBuilder` (menu `Team5/Rebuild/Paint Squad/Build role visuals`) creates 10 prefabs `Assets/Rebuild/PaintSquad/RoleVisuals/PaintSquad<Role>.prefab` and the catalog `PaintSquadAllRoles.asset` (`UnitRoleVisualSet`, all 11 `UnitRole` values incl. Test).
- Each prefab: root is the foot point with Team5's `UnitView` (same contract as `MinimalUnitView.prefab` and the Catgirl prefabs: read `BattlePresentation`, `UnitView`, `ContinuousMovementView`, `HaonUnitAnimation`, `FWUnitVisual`, and `CatgirlRoleVisualBuilder` to learn it); model scaled so the max horizontal width is at most 0.88 (fits one Small Cell) with a height comparable to the Catgirl prefabs; role helmet colour per the table via material instances saved as assets (no per-frame material creation); role gear built from simple meshes parented to the right bones; blob shadow; no colliders.
- A runtime component `PaintSquadUnitAnimation` (Presentation assembly) drives the Animator from the same signals the existing character components receive from `BattlePresentation` (movement, attack, damage, death, victory): Run while moving, Idle otherwise, Shoot plus muzzle flash on attack, Hit plus hit splash on damage, Die on death, Cheer on win. Wire it into `BattlePresentation` the same way `HaonUnitAnimation` / `FWUnitVisual` are, without breaking them.
- Facing: units turn toward movement or target, as the other 3D character does.
Acceptance: EditMode test that the catalog has an entry for every `UnitRole`, every prefab has `UnitView` + Animator + no Collider, width <= 0.88, and each role's helmet material colour matches the table; PlayMode test that spawns a Paint Squad unit through `BattlePresentation` in a test battle and observes the animator state change Idle -> Run -> Shoot/Die; existing suites pass.

## Task 3: Character select at game start

- Add a "Choose your squad" screen reached from Start Game, before the first battle (read `GameMenuScenes`, `LegacySceneMenu`, `StartGame.unity` flow and `Assets/Rebuild/MenuScenes.md`). Two cards: Paint Squad and Catgirl, each with a preview image of its Captain (render previews to PNG in the editor builder; do not render live 3D in the menu). Tapping a card selects it and continues.
- Persist the choice in PlayerPrefs key `Team5.CharacterSet` (values `PaintSquad`, `Catgirl`; default `PaintSquad`). A small static `CharacterSetSelection` API reads/writes it.
- Battle scenes choose the catalog at runtime: both catalogs referenced from a `CharacterSetCatalogs` asset in `Assets/Rebuild/Resources/`, and `BattlePresentation` uses the selected set's `UnitRoleVisualSet`, falling back to the scene-serialized `roleVisuals` if lookup fails. Every place that shows unit portraits/icons for a role (upgrade/warehouse cards, formation editor, claim cards, unit intro popup, legendary panel, next-level preview) uses the selected set's look too. Find them all with grep.
- Analytics: add `character_set` to the session/run start event (read `Assets/Rebuild/Analytics.md` and the Analytics assembly) and document the field in Analytics.md. The Apps Script backend writes unknown fields as new columns or ignores them: check `Tools/Analytics` and, if a backend change is needed, change the script file in the repo only (do not deploy).
Acceptance: tests for the PlayerPrefs API (default, round trip, unknown value -> default), for runtime catalog selection in a battle scene (both sets), for the analytics payload containing the field; Catgirl tests still pass.

## Task 4: UI restyle, all screens

- Extract the Paint Squad UI language from `~/TactileWars/Assets/Scripts` UI kit (`UIFactory` and friends; read only) and `Docs/VISUAL_STYLE_GUIDE.md`: palette, chunky rounded buttons with darker drop edge, card frames, ribbon banners, outlined Lilita One titles, Fredoka body, paper-beige backgrounds with subtle pattern, icon style.
- Restyle `LegacyUiKit` first (colours, button/card/panel sprites, fonts, text outline), keeping its public API and signatures so callers compile unchanged. Generate any needed 9-slice sprites into `Assets/Rebuild/PaintSquad/UI/` via an editor builder or reuse the copied Shapes.
- Then a per-screen pass so each looks like the Paint Squad game: start/menu scenes, character select (Task 3), upgrade/warehouse with unit cards, formation editor, claim cards, unit intro popup, battle HUD (speed, formation panel, life counter, health bars), legendary skill panel, next level preview, level settlement, win/lose result banners, EndGame, tutorials and notices.
- Preserve layout and all interactions and analytics hooks; only appearance changes. Text must stay readable at 960x600 and 1920x1080.
Acceptance: existing UI tests pass; screenshots of every listed screen at 1920x1080 saved under `~/Team5/Logs/PaintSquadScreens/` (gitignored Logs folder) by an editor/PlayMode capture helper, listed in the report.

## Task 5: Level dressing

- Editor tool `PaintSquadLevelDressing` (menu `Team5/Rebuild/Paint Squad/Dress levels`) places environment prefabs (built from the Task 1 models with Team5 materials) into Level1GridBattle..Level7GridBattle under a root `Paint Squad Dressing`, deterministic per scene (seeded), only on decorative/border/off-grid ground: never on BattleCell route, spawn, goal, road, tower platform, deploy cells, or anywhere a unit or tower model can stand, and not blocking the camera's view of the playfield. Read `Assets/Rebuild/Prefabs/LevelBuilding/README.md` and `DecorativeBorder_Cell.prefab`.
- Props: no colliders, Ignore Raycast layer, static, mostly trees and bushes at the edges, rocks/flowers/grass scattered, density tasteful (does not hide gameplay). Re-running the tool replaces the root instead of duplicating.
- Optional ground tint to match the Paint Squad palette is allowed only on decorative border materials, not gameplay tiles.
Acceptance: EditMode test loads each level scene and asserts the dressing root exists, has no Collider in its hierarchy, all on Ignore Raycast, and no prop's footprint overlaps any BattleCell that is not decorative; existing level validators (`Tools/Rebuild/validate_level*.py`) still pass; a screenshot per level in `Logs/PaintSquadScreens/`.

## Task 6: Verification pass

- Full EditMode and PlayMode suites; record counts.
- Screenshot every screen and every level with both character sets selected into `Logs/PaintSquadScreens/`, and a short index file listing them.
- WebGL build compiles to a temp folder outside `docs/` (e.g. `Logs/WebGLCheck/`), not committed; do not touch `docs/`.
- Fix only real breakages found; update `Assets/Rebuild/PaintSquad.md` with a screenshots/verification section.
