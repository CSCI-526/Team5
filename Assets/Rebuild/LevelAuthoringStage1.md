# Level authoring — stage 1

Stage 1 provides a standalone template Scene, safe Scene copies, basic validation, grouped scene Undo, and contracts for a future unified test target. It does not implement brushes, route drawing, tower placement tools, or test-entry migration.

## Open and run the template

Open **Team5 → Rebuild → Level Authoring (Stage 1)**, then **Open LevelTemplate**.

`Assets/Rebuild/Scenes/LevelTemplate.unity` is an independent Scene containing the current saved Level2 example layout, its existing camera/light, connected HUD/presentation, an authored route, and towers. Only display names and the displayed level number were changed when creating the template. Its BattleSession uses Auto Start and can run through a battle directly in Unity Play Mode. It has no Level1 tutorial or TestLevelControls. The standalone Scene stays open after victory/defeat; it does not enter the campaign or the legacy test flow.

The authoring window treats the template as read-only. Create a copy before editing. This is a tool guard, not a filesystem lock: normal Unity editing can still modify the template if you explicitly do so outside the tool.

## Create an independent level

1. Click **Create Level from Template…**.
2. Choose a new `.unity` filename and an existing folder inside Assets.
3. The tool creates an independent Scene asset with its own GUID. It refuses existing files, existing metadata, the template path, and the reserved `TestLevel.unity` path, including path aliases.
4. Choose whether to open the new Scene. Opening uses Unity's normal unsaved-scene prompt.
5. Edit the copy and save it with Ctrl+S.

Creating a copy does not open or save any scene automatically, does not change Build Settings, and does not change the current test target. The follow-up Open action is separate. Scene-file creation itself is not advertised as a Ctrl+Z action.

## Basic Undo and validation

In the copied scene, use **Displayed Level Number → Apply Level Number (Undo)** for a small, concrete authoring action. Ctrl+Z restores the previous value; Ctrl+Y reapplies it. The displayed number does not register the Scene with any game flow. Camera parameters and combat rules are not edited by this action.

`LevelEditScope` supplies the foundation for later brush commands: one action forms one Undo group, scene-object recording, creation and deletion are supported, and an uncommitted scope rolls back. Project assets, objects from another Scene, Play Mode edits, and template edits are rejected. Prefab instance property changes are recorded. No map-painting command is implemented in this stage.

**Validate Active Level** checks only the active Scene. It calls the existing BattleSession configuration checks and creates an isolated, unstepped BattleSimulation snapshot to check rule construction. It also checks basic scene bindings, unregistered towers, misplaced cells, presentation references, and the inspection camera. It does not start the Session, repair data, move objects, change materials, or save the Scene. Issues with an object context have a Select button; precise grid overlays and complete error localization are later-stage work.

The Scene remains the only map authority: BattleCell Transforms/types, SceneRoute references and BattleTower components are read directly. Validation lookup tables and snapshots are temporary.

## Future unified test target contract

All new production tool code is in the Editor-only `Team5.Rebuild.Editor` assembly:

- `LevelSceneReference`: immutable Scene GUID; resolves the current asset path on demand, including after a rename/move.
- `ILevelTestTargetProvider.TryGetTestTarget`: the future single query point.
- `ILevelTestTargetSelection.SetTestTarget`: the future explicit selection operation, separate from creating a Scene.

Stage 1 intentionally has no active-target store, setter implementation, runtime adapter, or build-list mutation. The window displays the existing legacy target for information only. The current TestStartScene → TestUpdateScene → TestLevel flow, unlimited test gold, and the four-level campaign remain unchanged.

In the migration stage, selection will validate the chosen Scene and build registration, while loading, preview, direct-entry detection and test controls will all use one target. Runtime loading must receive a build-loadable path and must not depend on SceneAsset, AssetDatabase, or any UnityEditor type. This contract identifies a Scene; it stores no map layout.

## Validation and acceptance

Stage-specific EditMode tests cover safe copying/GUID independence, overwrite protection, template validation, camera/configuration preservation, simulation completion, Undo/Redo/rollback, scene scoping, malformed authoring data, and GUID resolution after asset renames. A PlayMode test loads the standalone template, runs its battle, checks static cells/camera preservation, and verifies that no campaign flow is created.

Reports are saved under `Logs/LevelAuthoringStage1/`. `BaselineHashes.json` records all pre-existing Assets, ProjectSettings and Packages files so preservation can be checked independently. `TemplateSource.json` records the template source. Compilation, actual Unity test execution, and any environment limitations are reported separately.

Final stage-1 results: offline compilation passed; actual Unity 6000.3.22f1 headless runs passed all 9 stage-specific EditMode tests and the standalone-template PlayMode test (1/1). The final preservation check found zero changes across 503 pre-existing files, including TestLevel, all campaign scenes, runtime scripts, assets, and Build Settings. Unity rewrote only the line endings of ProjectSettings.asset during PlayMode setup; the original bytes were restored and verified against the baseline. Test-created assets were removed. See `EditModeResults.xml`, `PlayModeResults.xml`, and `FinalValidation.json`. The authoring window's visual appearance has not been manually reviewed.

Stage 1 stops here for user acceptance. Stage 2 map brushes must not start automatically.
