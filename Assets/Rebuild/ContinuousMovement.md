# Scene 1 continuous movement

Scene `Assets/Rebuild/Scenes/1.unity` alone enables `BattleSession.continuousSquadMovement`.
Other scenes keep their existing automatic/manual route modes. Camera, map layout, roster assets, HP, attack
stats, damage-type rules and scene progression settings are not replaced.

## Controls

- Click ground to submit a squad destination at the exact world coordinate. There is no cell-center snap,
  three-cell length rule, axis restriction, planning overlay or automatic pause in this mode.
- Click again while moving to replace the destination. An invalid/unreachable command shows a red marker
  and keeps the previous valid command. A green marker disappears when every living member arrives.
- If the clicked center is clear but an ideal slot would clip a nearby wall, first try a small translation of the
  whole formation onto nearby clear ground (within 1.5 formation spacings). The green marker shows that resolved
  destination. Open-ground destinations retain the exact clicked coordinate; clicks on blocked ground stay invalid.
- UI clicks do not issue movement. Clicking a character opens its inspection; click outside to close inspection,
  then click the ground to move. The squad icon opens live formation editing without pausing or slowing the battle.
- Formation swaps apply immediately on drop (or two slot clicks); DONE/Esc closes the panel without reverting
  those changes. The current manual pause state is respected. Legacy scenes retain their paused editor.
- Formation edits change ideal slots. Characters exchange nearby positions while preserving forward travel,
  then continue to the original destination. Unchanged members are not sent on new long-distance paths.
  Narrow spaces defer a local exchange until it can fit. No member teleports or exceeds its normal movement speed.
  The old MOVE SQUAD drawing button is hidden in Scene 1.

## Simulation

Each member retains its own Position, travel path, destination, collision radius and movement progress.
SlotId/MemberId are stable identities; FormationOffset is only an ideal relative standing position. SquadReference
is separate state with its own reference path, rather than being inferred from an arbitrary soldier.

Both squad and single-member `MovementRequest` commands share one executor. Player requests replace earlier
requests; AI requests cannot interrupt an unfinished player order. Single-unit UI controls and autonomous AI
behaviour are not introduced by this change.

Navigation uses a 0.25-world-unit search lattice, eight directions and swept clearance tests, then string-pulls
the path to retain fractional endpoints. BattleCells continue to supply terrain/obstacle information.
Circle bodies can share a cell. Static clearance is conservative at corners, and every movement step also checks
other living bodies in the original noncompact mode; there is no Rigidbody pushing or Animator root motion.

Scene 1 enables **Compact Squad Movement**. Squad members can cross and overlap, while the unchanged 0.3
obstacle radius still applies to terrain, towers, walls and gates. Whole-squad commands derive member waypoints
from one shared center route, so members do not independently choose opposite sides of a building. Offset
waypoints reconnect to the same guide corners; narrow sections temporarily merge members toward that route.
Leading members stop moving farther away when the group exceeds a formation-sized leash, rather than merely
slowing to 45%. Trailing members remain free to catch up. Final destinations restore the ideal compact slots.
Checkpoint copies preserve the compact option and shared-waypoint progress. Other scenes retain their old mode.
The preferred open-ground spacing is 0.6. Shared routes sample clearance every 0.5 world units. Members whose
expanded offset would clip terrain first try 75% of that offset (0.45 spacing), then merge onto the guide as
needed. They expand again on clear samples after a doorway, without waiting to arrive at the final destination.

In the original noncompact mode, members navigate individually to ideal final slots, so they can temporarily abandon the formation through a
narrow passage and fan back out in open space. Destination slots that cannot fit first search for a nearby complete
formation, avoiding a queue just because one slot is near a wall. If that cannot fit, use a stable front-to-back
queue instead; later members follow once there is safe clearance, without waiting for the predecessor to arrive.
Projected forward clearance prevents overtaking at a narrow entrance. A subsequent wide-space destination
restores the original slots. ID-based temporary yielding resolves head-on slot exchanges without swapping IDs.
Yield candidates are sampled around the unit and scored by total movement cost; long detours are rejected.
Reciprocal exchanges first try matched short semicircles around the pair's midpoint. Nearby stationary members
only nudge outward as much as needed, wait for the pair to pass, then return to their original positions.
During formation exchange, a shared forward step uses up to 35% of the normal movement budget and the remainder
is available for local corrections. Leading members that get too far from the group slow down to encourage cohesion.
This is an initial local avoidance implementation, not a general crowd/traffic system for multiple squads.

Inspector settings on Scene 1's BattleSession:

- Compact Squad Movement: Scene 1 enables this; other scenes default to false.
- Continuous Formation Spacing: default 0.48; Scene 1 overrides this to 0.6 for open ground.
- Continuous Soldier Radius: default 0.2; Scene 1 overrides this to 0.3 for obstacle clearance.
  In noncompact mode spacing must exceed two radii plus 0.01. Compact mode permits overlapping squad bodies.
- Continuous Visual Footprint: default 0.38; Scene 1 overrides this to 0.99 world units (Small Cells).
  Haon visuals fit inside this horizontal enclosing diameter, capped by the body diameter, with a height cap of
  4.75 times the radius. Model proportions remain unchanged. The other scenes' models
  retain their original sizing. Idle/Run and facing follow the actual soldier's movement, not reference movement.
  Scene 1 now uses FW 2.5D visuals, whose size is independent of these logical parameters.
  Its visual prefab uses a uniform 0.8 scale relative to the original prototype (not 80% of the previous 0.9).
- Healing keeps the existing SlotId-neighbour rule; its ring display scales with formation spacing.

Supported navigation is the current flat map layer. Walls/gates block until destroyed, and their existing damage
immunities remain. Arrival, including arrival on Goal terrain, stops movement without triggering a new win rule;
this retains the previous manual mode's destination behaviour.

## Verification

ContinuousMovementTests exercise multiple non-overlapping bodies in one cell, exact diagonal endpoints,
single-cell passages and restored formation, narrow destinations and queues, single-member requests/player
priority, unreachable commands, pause/replacement/checkpoints and collision-safe slot exchanges.
Legacy movement, wall, wooden-gate and Map Builder application tests are also included in regression checks.
Human Play Mode review of visual density, animation and real mouse interaction is still required.

CompactSquadMovementTests also cover overlapping members and stationary allies, same-side obstacle detours
with pairwise spread <= 1.35 units for the four-member cross formation, speed limits, unchanged wall clearance,
single-cell doors, pause/checkpoint restore, direct formation exchanges, and blocked terrain rejection.
Latest compact regression: `Logs/FWVisual/OpenSpacingEditMode.xml` (21 tests passed) and
`Logs/FWVisual/OpenSpacingPlayMode.xml` (6 tests passed, including actual Scene 1 movement and animation).
The spacing tests also check that open ground preserves the wider offsets throughout movement and that
members expand again after the narrow door while still on the way to their destination.
