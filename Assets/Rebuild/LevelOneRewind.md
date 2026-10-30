# Level1 recovery

Read-only references: Team5/Assets/Offense/Scripts/LevelOneTutorialRewind.cs,
OffenseData.cs, OffenseWaveManager.cs; Shared/Scripts/HowToPlay.cs;
UpgradeUI/Scripts/OutOfLivesScreen.cs.

The old rules are retained: three lives per level; squad defeat costs one life;
Level1 offers Restart Level and Back To Last Hint. Checkpoints are replaced when
a tutorial banner is displayed, including the formation drag/result hints.
Restart repeats the tutorial but preserves consumed lives. Rewind restores the
last hint and battle checkpoint without refunding lives. No checkpoint means the
rewind button is disabled. Zero lives blocks both restart and rewind.

BattleSimulation.Checkpoint holds isolated copies of units, tower HP/targets/
cooldowns, projectiles (including hit targets, age and travel), spawn membership,
formation, heal timer, clock and outcome. Restoring creates a new simulation with
fresh event bindings. Presentation reconstructs runtime views from restored state;
Scene terrain/towers are never cloned. Snapshots do not contain wallet, run reward
records, warehouse, or failed-attempt counters. Repeated restores cannot mutate
the checkpoint or award currency/units. No global Time.timeScale changes.

All levels default to three lives (BattleSession.DefaultLivesPerLevel). Once
lives are exhausted, the shared Out Of Lives overlay has only Exit & Restart
Game. It returns to the title; starting a new run resets progress. Buying lives
has been removed. Upgrade entry, Start Wave, restart and rewind cannot bypass
exhaustion. Victory starts the next level with a fresh life allowance. Normal
Level2 defeats with lives remaining retain the settlement/upgrade retry flow.

Level2 now shares this exhaustion rule. Existing authored cameras, maps, tutorial
wording, Speed Bar and formation layout remain intact. Tutorial replay reapplies
its existing temporary focus and highlights and restores the authored camera on
exit. Disabling Presentation still permits simulation snapshot/restore and lives
accounting, but intentionally provides no recovery buttons.

Verified by offline compilation and deterministic simulation/session checks.
PlayMode tests were added for live checkpoint replay, tutorial re-entry, failures,
remaining lives, exhaustion in both levels and title/new-run reset, but were not executed in Unity.
