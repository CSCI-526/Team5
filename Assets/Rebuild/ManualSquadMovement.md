# Scene 1 manual squad movement

Scene 1 now enables the continuous click-to-move system described in `ContinuousMovement.md`.
The route-drawing implementation below remains available for scenes configured for manual movement without
continuous movement, but is not the active Scene 1 control scheme.

Only `Assets/Rebuild/Scenes/1.unity` enables `BattleSession.manualSquadMovement`.
Other scenes keep their automatic movement. The authored map, camera and SceneRoute are retained.
Manual mode makes clear Ground cells traversable in addition to Road cells; it does not recolor or replace the map.

## Play Mode

1. Click the first squad icon on the left to open the formation UI.
2. Click **MOVE SQUAD** below the formation controls. This applies the formation, closes the panel,
   restores the camera, and freezes game time. The squad and the ground under its 3x3 footprint remain bright.
   Other world visuals and existing UI dim; UI and unit inspection are blocked.
3. Press on the squad or its footprint and drag across the map. The cyan route advances in 3-Small-Cell steps;
   each straight stretch is 3, 6, 9, ... cells, with 90-degree corners. Backtracking removes a 3-cell step.
   A drag shorter than 3 cells adds no segment. Turns and endpoints snap relative to the actual starting position,
   including when replanning while the squad is moving; there is no short alignment segment or position jump.
4. Release to resume time and follow the drawn route. The squad stops at its endpoint and remains alive.
   The line starts at the squad's current anchor and shows only untravelled waypoints; travelled portions
   disappear as it moves, and the entire line disappears on arrival or when no living member remains.
   Open the squad UI again to issue another route. **Esc** cancels planning and restores the previous pause state/route.

Routes check the swept footprint of every living member, not just the squad center. Towers, living walls,
obstacles, unsupported ground and map edges block movement. Tower firing ranges do not block movement.
Spawn can be exited but not reentered after leaving; Goal can be crossed or used as a route endpoint. A blocked drag keeps its last valid
endpoint in full 3-cell steps, without automatic pathfinding. All intermediate cells must be clear;
a blocked step is rejected as a whole. A click without a route issues no command.

The automatic-route Level 1 tutorial is skipped in manual mode because it depends on automatic movement and timed
tower encounters. Its behavior in automatic scenes is unchanged. Manual destinations do not trigger the old
automatic goal/victory transition.

## Implementation and verification

`SquadRouteInput` owns temporary time, input and visual masking; cancellation, disabling and simulation replacement
restore these states. It submits validated waypoints to `BattleSimulation`; models and animations still follow
simulation coordinates. Formation edits also validate the remaining planned route.

`ManualSquadMovementTests` covers waiting, pause gating, right-angle movement, endpoint stopping, formation offsets,
invalid routes, wall/terrain blocking, tower-range travel, checkpoint restoration, repeated commands, formation-edit
clearance and unchanged automatic victory. Offline compile/simulation checks can run without taking the Editor lock.
Play Mode UI/camera/dimming verification still requires running Scene 1 in Unity.
