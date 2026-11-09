# Team5 gameplay analytics backend

The game sends flat JSON events to a Google Apps Script web app. The script appends
each event to a tab of one Google Sheet, one tab per event name. The event schema
and hook points are in `Assets/Rebuild/Analytics.md`.

## Deploy

1. Create a Google Sheet (for example "Team5 Analytics").
2. Open Extensions > Apps Script. Delete the sample code and paste `AppsScript.gs`.
3. Save, then choose Deploy > New deployment. Pick type "Web app".
4. Set "Execute as" to Me and "Who has access" to Anyone. Deploy and approve the
   permissions prompt.
5. Copy the web app URL (it ends in `/exec`). Opening it in a browser should print `ok`.
6. In Unity, select `Assets/Rebuild/Resources/RebuildAnalytics.asset` and paste the URL
   into `Endpoint Url`. Leave `Send From Editor` off unless you are testing the pipeline.
7. Build WebGL as usual. The page batches events and sends them with `navigator.sendBeacon`
   as `text/plain`, so the browser needs no CORS preflight.

After you edit the script, use Deploy > Manage deployments > Edit > New version. That
keeps the same URL. A brand new deployment gets a new URL, which you would then have to
put back into the asset.

## Settings (`RebuildAnalytics.asset`)

| Field | Meaning |
| --- | --- |
| endpointUrl | Apps Script `/exec` URL. Empty means nothing is sent anywhere. |
| enabled | Master switch. |
| sendFromEditor | Also send from Play Mode in the Editor. Off by default. |
| logToConsole | In the Editor, log every event as `[Analytics] {...}`. |
| buildLabel | Prefixed to `Application.version` in the `build` column, for example `beta-0.1`. |
| flushIntervalSeconds | Batch interval. WebGL also flushes at 20 events and when the tab hides or closes. |

## Filtering out noise

Every row has `is_editor`, `is_test_run` and `build`. For real player data, filter on
`is_editor = FALSE`, `is_test_run = FALSE` and the build label you shipped. `player_id`
is a random id kept in PlayerPrefs (IndexedDB on WebGL), so clearing site data looks like
a new player. `session_id` changes on every page load and `run_id` on every new game.
`seq` orders the events of one session.

## Questions and the charts that answer them

| Question | Tab | Chart |
| --- | --- | --- |
| How far do players get? | level_start, level_end | Funnel: distinct `run_id` per `level` in level_start, then with `outcome = victory` in level_end. |
| Where do they quit? | page_exit, level_abandon, scene_enter | Count of page_exit by `scene` and `level`. level_abandon by `level`. |
| Is a level too hard? | level_end | Pivot: rows `level`, columns `attempt`, value share of `outcome = victory`. |
| How many lives does a level cost? | level_end, game_over | Average `attempt` of the winning row per level. game_over count per level. |
| How far did defeated squads get? | level_end | Histogram of `furthest_progress_pct` where `outcome = defeat`, per level. |
| Where do units die? | unit_death | Histogram of `progress_pct` per level, or scatter of `x` and `z`. |
| Which tower is overtuned? | tower_stats, unit_death | Bar chart of `kills` and `damage_dealt` by `level` and `tower_id`. Cross check with `killer_tower_id`. |
| Which towers never fall? | tower_stats | Share of `destroyed` by `level` and `tower_id`. |
| Does squad makeup matter? | level_start + level_end | Join on `run_id`, `level`, `attempt`. Win rate by `roles`, `max_rarity` or `avg_unit_level` bucket. |
| What do players summon? | summon_offer, summon_purchase | Count by `role` and `rarity`. `chosen_index` shows position bias. summon_cancel count shows rejected offers. |
| Do players hoard gold? | prep_exit | Average `gold_unspent` per level. |
| Do players find mechanics? | level_end, formation_edit, speed_change, pause_toggle, unit_inspect, skill_click, prep_exit | Share of level_end rows with `formation_edits > 0`, `speed_changes > 0`, `pauses > 0`, `inspections > 0`. Share of prep_exit with `previewed = TRUE`. |
| When do they use them? | formation_edit, speed_change | Histogram of `sim_s` or `progress_pct`. |
| Which speed do they play at? | level_end | Sum of `sim_s_at_1x`, `sim_s_at_2x` and `sim_s_at_3x` per level. |
| Where does the tutorial lose people? | tutorial_step, tutorial_skip, tutorial_complete | Distinct `session_id` per `step`. Average `seconds_on_prev_step`. Count of tutorial_skip by `step`. |
| Do players use the Level 1 recovery card? | recovery_choice, tutorial_rewind | Count of `choice`. |

Join keys: `run_id` + `level` + `attempt` identify one battle attempt. level_start,
unit_death, tower_stats, level_end and level_abandon all carry them. A tutorial rewind
starts a new attempt with `resumed = TRUE`, because it already cost a life.

## Limits

- Apps Script runs one request at a time per lock. A burst of players waits up to 20
  seconds, which is fine for a beta.
- A Google Sheet holds 10 million cells. At about 30 columns, budget roughly 300k rows
  in total. Copy the data out and clear the tabs if it fills up.
- Browsers cap beacon payloads at about 64KB. The page splits batches at 60KB.
