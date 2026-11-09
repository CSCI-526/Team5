# Gameplay analytics

Assembly `Team5.Rebuild.Analytics` (`Scripts/Analytics`) references Simulation, World and
Session. Presentation and both test assemblies reference it. Session does not: it only
exposes `BattleSceneFlow.RunStarted` and `BattleSceneFlow.LevelSettled`, and Analytics
subscribes to them. `LevelSettled(outcome, level)` is raised at the end of `Settle`, after
rewards, lives and exhaustion are final, on every non-test path. Gameplay is unchanged.

`GameAnalytics` bootstraps itself before the first scene loads. It reads
`Resources/RebuildAnalytics.asset`. It stays inactive when the asset is missing or disabled,
or when there is no endpoint and no Editor logging. Every entry point that game code calls
catches its own exceptions. Simulation listeners run inside `BattleSimulation.Step` and also catch
everything. Analytics only reads simulation state. It never pauses, steps or edits it.

`AnalyticsDispatcher` (DontDestroyOnLoad) logs each event as `[Analytics] {...}` in the
Editor. On WebGL it passes each event to `Plugins/WebGL/Analytics.jslib`. The page batches
events and sends them with `sendBeacon` every interval, at 20 events, when the tab hides,
and on `pagehide`. On `pagehide` it also adds `page_exit` from the last context Unity sent.
Other players POST batches as `text/plain` and try twice only when there is no HTTP
response. The backend is `Tools/Analytics/AppsScript.gs`. Deploy steps and chart recipes
are in `Tools/Analytics/README.md`.

`BattleAnalytics` follows full scene loads (preview and additive loads are ignored). It
binds `BattleSession.SimulationCreated` and keeps one `BattleAnalyticsTracker` per attempt.
The attempt number is `AttemptsPerLevel - AttemptsRemaining + 1` when the simulation is
created. A simulation created with `ElapsedTime > 0` is a tutorial rewind: it emits
`tutorial_rewind` and starts a new attempt with `resumed = true`, because the defeat
before it already cost a life. Its counts (killed, towers destroyed) include the restored
checkpoint. `level_start` is emitted once every planned member has spawned. `level_end`
and the `tower_stats` rows are emitted from `LevelSettled`. Test runs never settle, so the
dispatcher finishes those attempts in the next `LateUpdate` with `gold_reward = 0`. A
scene load or a new simulation while the attempt is still running emits `level_abandon`.

Route progress is `UnitState.RouteProgress / route length * 100`, clamped to 0..100.
Route length is the summed distance between `SceneRoute.CaptureTopCenters()` points, the
same waypoints the simulation walks. Units that reached the goal count as 100.
`killer_tower_id` is the source of the last damage before a death (tower ids are 1-based,
0 means unknown). Simulated time per speed is sampled each frame from
`BattleSession.PlaybackSpeed`; inspection slow motion counts toward the selected speed.

## Common fields

Every event: `event, ts` (UTC ISO 8601), `player_id` (random, PlayerPrefs), `session_id`
(per launch), `run_id` (per BeginRun, empty before), `build` (buildLabel-version),
`platform, is_editor, is_test_run, scene, level, attempt, seq`.

Outside a battle, `level` and `attempt` describe the next battle of the current run, or 0
without a run. Events marked (battle) also carry `sim_s, progress_pct` of the current
attempt (0 when no attempt is being tracked).

## Events

| Event | Fields | Source |
| --- | --- | --- |
| session_start | returning_player, session_count, screen_w, screen_h | Bootstrap |
| scene_enter | previous_scene, previous_scene_s | Full scene load |
| run_start | run_number_in_session | `BattleSceneFlow.RunStarted` |
| prep_enter | gold, warehouse_count, deployed_count, summon_price | LegacyUpgradeScene.Start |
| summon_offer | price, gold, offer_0, offer_1, offer_2 (`Role/Rarity/LvN`) | LegacyClaimCardOverlay.Open |
| summon_purchase | chosen_index, role, rarity, unit_level, price, gold_before, gold_after | Summon card click |
| summon_cancel | price, gold | Summon cancel button |
| unit_dismiss | role, rarity, unit_level | LegacyUpgradeScene.TryDismissMember |
| map_preview_open | (none) | LegacyUpgradeScene.OpenNextLevelPreview |
| prep_exit | dwell_s, gold_unspent, deployed_count, roles, avg_unit_level, max_rarity, previewed, summons_this_visit | Next full scene load |
| level_start | resumed, squad_size, roles, slots, captain_present, squad_total_hp, avg_unit_level, max_rarity, tower_count | All members spawned |
| unit_death | role, rarity, unit_level, slot, sim_s, progress_pct, killer_tower_id, x, z | `UnitDied` |
| level_end | outcome, resumed, sim_s, real_s, spawned, killed, reached_goal, survivors, towers_destroyed, tower_count, furthest_progress_pct, squad_hp_left_pct, damage_taken, damage_to_towers, healing_done, formation_edits, pauses, speed_changes, sim_s_at_1x, sim_s_at_2x, sim_s_at_3x, inspections, gold_reward, lives_left, game_over | `LevelSettled` |
| tower_stats | outcome, tower_id, is_aoe, max_hp, damage_dealt, kills, destroyed, destroyed_at_s, hp_left_pct | One row per tower after level_end |
| level_abandon | resumed, sim_s, real_s, progress_pct, survivors | Scene change or restart while running |
| tutorial_rewind | checkpoint_sim_s, lives_left | Restored simulation |
| formation_open (battle) | source (`squad_button`, `captain_skill`) | LegacyBattleHud |
| formation_edit (battle) | result (`applied`, `rejected`, `cancel`), members_moved | LegacyFormationEditor |
| pause_toggle (battle) | paused | LegacyBattleHud.TogglePause |
| speed_change (battle) | from, to | LegacyBattleHud.SetSpeedFromSlider, only on change |
| unit_inspect (battle) | role, rarity, unit_level, slot | BattleUnitInspection.SelectUnit, new unit only |
| skill_click (battle) | role, skill_index, assigned | BattleUnitInspection.ClickUnitSkill |
| tutorial_step (battle) | step, total, heading, prev_step, seconds_on_prev_step | LevelOneTutorial.Show, step changes only |
| tutorial_skip (battle) | step, seconds_on_step | LevelOneTutorial.Skip |
| tutorial_complete (battle) | step, seconds_on_step | Last tutorial step confirmed |
| recovery_choice | choice (`restart`, `rewind`, `exit`) | LegacyLevelOneRecovery buttons |
| game_over | run_real_s, total_attempts, total_defeats | Defeat with lives exhausted |
| campaign_complete | run_real_s, total_attempts, total_defeats | Victory on the last level |
| page_exit | seconds_in_session (plus the common fields of the last event, with a new ts and seq) | WebGL `pagehide` |

`outcome` is `victory`, `defeat`, or `running` for an attempt that was finished without a
result. `roles` looks like `Attack:2|Tank:1`, `slots` like `0:Captain|4:Tank`.
`lives_left` is `BattleSession.AttemptsRemaining` after the attempt, or -1 when the session
is already gone. `destroyed_at_s` is -1 for a tower that survived. The StartGame scene has
no how-to-play button, so there is no event for it.

Tests: `Tests/EditMode/AnalyticsTests.cs` covers JSON escaping and invariant numbers, the
facade with no sink and with a failing sink, common fields, and trackers on real
simulations for victory, defeat and abandon. The WebGL page code needs a browser build
and is not covered by the EditMode tests.
