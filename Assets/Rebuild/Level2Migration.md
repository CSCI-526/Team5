# Level2 and inter-level flow

Level2GridBattle.unity was imported from the preserved pre-compaction Level1 Scene.
Its latest compaction changes 39 x 27 Small Cells to 20 x 14, retaining unit-size
1 x 0.5 x 1 meshes and a three-cell-wide horizontal/vertical/horizontal road.
The road now has 24 centerline references from (10,16) to (19,16), down to
(19,10), then to Goal (27,10). Two complete 3x3 towers remain at (22,.25,16)
and (19,.25,7), with original combat values and firing directions. Spawn and
Goal remain 3x3. Five decorative rings follow the new perimeter (440 tiles),
without BattleCell or Collider components. The pre-edit Scene is backed up at
Logs/CompactLevel2Backup/Level2GridBattle.unity. Camera and UI are unchanged.
The Scene has level number 2, normal level time multiplier 1,
the shared warehouse roster, and no Level1 tutorial. Direct Editor Play uses
the existing six-member debug fallback; normal play uses the selected deployment.

StartGame -> Level1 -> LevelSettlement -> UpgradeScene -> Level2 -> EndGame.
Level1 remains Captain-only and retains its tutorial and 2x level time multiplier.
Level2 uses the warehouse's selected stable member IDs and slots (maximum six).
The existing 2x6 warehouse, empty initial formation and user-authored UI layout
remain unchanged. Scene configuration and roster assets are never mutated at runtime.

LevelSettlement ports Team5/Assets/Scenes/Level2Intro.unity, including its
authored Mission Complete layout, font and persistent Continue button. Its small
replacement controller displays actual victory/defeat and the destination level.
Continue enters the existing adapted UpgradeScene. Original gold rewards,
gold costs, OffenseData and analytics are intentionally excluded because the
rebuild's currency system was removed. All existing warehouse units remain
available; there is no additional Tank unlock or card-purchase system.

Level2 defeat settles once, preserves the current level and consumes one attempt.
Continue -> UpgradeScene -> Start Wave retries that level. After three defeats,
the shared Out Of Lives overlay only allows returning to the title to start over.
Life purchases are removed. Level1 uses its checkpoint/restart recovery before exhaustion;
see LevelOneRewind.md. Level1 retries still use its fixed Captain roster. Victory resets
attempts before the next level; a new run resets deployment and progress.

Validation: offline C# compile, saved Scene geometry and reference checks,
Level1 regression validation, and standalone combat rules on exported Level2.
PlayMode tests cover Level1 victory -> settlement -> preparation -> Level2,
selected member IDs, final victory, duplicate events and retry lives. Actual
Unity PlayMode/visual verification is left unexecuted per the user's preference.

Offline combat runner: 17 checks passed, including a three-member Captain/Tank/Heal
deployment reaching Level2's Goal and settling exactly once. Saved Level2 and
settlement references validate successfully. The Level1 regression validator
passes geometry/route/configuration checks but fails its older requirement that
all map corners fit the current 16:9 camera. No Level1 camera changes were made;
this existing framing condition was preserved rather than adjusted.
