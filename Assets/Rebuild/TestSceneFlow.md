# Independent test scene flow

Entry: **Team5 → Rebuild → Test → Open Test Start Scene**.

`TestStartScene → TestUpdateScene → TestLevel`

TestStartScene copies the authored StartGame layout and font, with Test Mode / Start Test labels. Its existing StartGame button callback chooses the test route based on the scene path. TestUpdateScene copies UpgradeScene's authored UI and uses the existing warehouse, deployment editor, and Start Wave action. Both test preparation and TestLevel use the same initial roster asset. No initial units were added. Test mode uses an unlimited wallet: the preparation UI displays `$ UNLIMITED`, purchases never deduct gold, and direct Editor Play of TestUpdateScene also enables it. The test preparation uses the new three-choice summon shop, including level/rarity rolls and increasing prices. The former three-purchase quota is gone; warehouse capacity and deployment limits still apply. Starting the normal campaign creates a fresh finite wallet using RebuildEconomy; the shared economy asset is unchanged.

The test route is separate from the four campaign scenes. Each new test run resets its wallet, purchases, deployment, attempts, and outcome. Direct Editor Play of TestUpdateScene or TestLevel also selects test mode. Test victories and defeats remain in TestLevel for inspection: they do not advance campaign levels, award campaign rewards, consume attempts, or load LevelSettlement / EndGame. The test toolbar provides Restart Test and Test Preparation. Returning to preparation retains the current test deployment; beginning a new test run resets it.

## Author the map yourself

TestLevel intentionally contains **zero BattleCells, zero route nodes, zero towers, and zero decorative tiles**. Only the empty GameplayGrid, SceneRoute, Scene Towers container, BattleSession, presentation, HUD, camera, light, and debug components are included. There is no tutorial or runtime map generator.

1. Open `Assets/Rebuild/Scenes/TestLevel.unity` in Edit Mode.
2. Place your desired tile prefabs under GameplayGrid. Keep cells at integer X/Z coordinates and Y increments of 0.5. Do not overlap cells.
3. Set SceneRoute → Cells to the ordered Small Cell centerline from Spawn to Goal. Required Width is 3: every node needs full three-cell-wide clearance, including corners.
4. Place towers on suitable TowerPlatform cells and register their BattleTower components in BattleSession → Towers. Single-cell towers need one support cell; Macro towers need all nine.
5. Adjust the saved camera for your layout. No map framing was designed for this empty scene.
6. Save TestLevel, open TestStartScene, enter Play, deploy at least one warehouse member, and press Start Wave.

To use the preparation screen's selected deployment, disable BattleSession → Use Scene Roster Only. When enabled, that existing option deliberately uses the scene roster instead. The conflict integration preserves the current TestLevel scene and its authored setting.

The test-only controls validate the map on entry. A valid map starts combat automatically. An unfinished map displays "Map not configured yet" without starting an invalid simulation or logging a configuration error. The BattleSession's authored Auto Start remains disabled because the test controls perform this validation first. Restart Test repeats the same validation before starting a fresh simulation.

Preview Next Level targets TestLevel, with simulation, HUD, and the test toolbar suppressed in the temporary preview scene. Before any map renderers exist, the copied preview UI cannot display a map; after authoring the map, it uses the actual saved scene.

## Implementation and validation

BattleSceneFlow uses a separate test mode and test-only scene paths. Its campaign list and first Build Settings scene remain unchanged. The three test scenes are additional enabled build entries. Shared roster/economy assets and existing scene files are not modified.

PlayMode tests cover navigation into an intentionally empty test scene, deployment retention, a completed test staying outside the campaign with no rewards, restarting, and preview suppression. Their temporary in-memory map fixtures are not saved into TestLevel. Offline compilation and scene/reference checks are performed separately; Unity PlayMode execution and visual checks are not automatic.
