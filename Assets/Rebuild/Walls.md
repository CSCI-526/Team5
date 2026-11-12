# Destructible road walls

- Prefabs: `Prefabs/LevelBuilding/Towers/Wall_3x1_X.prefab` and `Wall_3x1_Z.prefab`.
- Walls use dedicated warm-gray stone and lighter capstone materials.
- The static brick models occupy three small cells along X or Z, one cell deep. Default HP is 20; select the root and change **Wall HP**.
- In **Team5 > Rebuild > Map Builder (2D Grid)**, select **Place tower**, then **Wall 3x1 (X)** or **Wall 1x3 (Z)**. Place the center on three clear, equal-height **Road** cells. Save/generate as usual; the tool registers the wall in the battle session.
- For direct Scene authoring, drag in the prefab, align its root to integer X/Z and the road top Y, The battle session automatically includes Scene walls; manual registration in **BattleSession > Towers** is optional. Either prefab can be rotated around world Y in 90-degree steps. Keep world scale at (1,1,1) and avoid tilting. World footprint and Map Builder import follow the rotated direction.
- Walls reuse the enemy health/target pipeline but never attack, have no attack range or gold bounty, and do not count as destroyed towers. Attack-capable units target them automatically; Tank/Heal cannot damage them.
- A live wall stops the formation before its models intersect the footprint. At zero HP its renderers and HP bar disappear and its occupancy clears; the squad resumes on the following tick. Restart/checkpoint restoration restores wall HP, occupancy and visuals. Static Scene objects are retained so restoration works.
- Existing scenes are unchanged; place the prefab in levels where a wall is wanted.

Validation: runtime and Editor source plus WallTests compile offline. Six standalone simulation checks cover both orientations, no attacks, stopping, destruction/clearance/victory, no rewards, checkpoint restoration, continuous non-penetration and delayed reinforcement spawning. Unity batch startup exits before running tests in this environment; prefab/Editor tests and in-game visual QA still require Unity.

## Camera occlusion

WallOcclusionView automatically follows BattlePresentation. Any projected overlap with a living unit behind a wall fades that entire wall to 22% opacity. Detection uses projected convex hulls of visible model bounds rather than center-only rays, excludes healing range rings, and runs after camera/position updates. Runtime-only transparent material copies retain brick colors and seams, disable depth writes and shadow casting, and restore original materials/shadows when clear, restarted or disabled. Source materials and wall combat rules remain unchanged.

Wall movement checks use living members and reserved reinforcement slots, not all nine empty/occupied slots. Completed route segments are excluded; changing to an unobstructed lane releases the stop. Ordinary tower attack coverage does not affect marching. Regression checks cover these cases.
