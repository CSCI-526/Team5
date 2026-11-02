# 2D Map Builder

Open **Team5 > Rebuild > Map Builder (2D Grid)**, or use the new shortcut in Level Authoring.

1. Enter Width and Length (3–96 cells each), then choose **New empty grid**. Multiples of three are recommended for 3x3 formations. Each displayed square is one actual BattleCell.
2. Choose Ground, Road, Obstacle or TowerPlatform. Click/drag to paint, select 1x1 or world-aligned 3x3 brushes, or Fill grid. Empty cells are allowed. Erase tile removes only terrain; it does not silently delete routes or towers.
3. Paint Spawn and Goal with their dedicated 3x3 macro brush. The existing runtime determines spawning from the route, not from nine independent spawn events.
4. Select Route. Click the Spawn center, orthogonal waypoints and the Goal center in order. Straight segments are filled automatically; diagonals and repeated nodes are rejected. Provide a full 3-cell-wide passable corridor, including turns. Route numbers show travel order. Trim or clear the route to correct it.
5. Paint TowerPlatform first, then select Place tower, a prefab and its attack area. The two shortcut buttons select the existing modern 1x1 towers. Hover tint previews the configured attack coverage; arrows/marks show placed attack areas. Custom model rotation is independent of the attack setting. A tower's entire 1x1 or 3x3 footprint must fit on platforms without overlap. Remove tower deletes the whole stamp, including when clicking its footprint.
6. Validate map. This creates and destroys a temporary preview scene, running existing configuration and simulation-constructor checks. Errors must be fixed before generation. Full route/formation clearance and tower support are checked again.
7. Generate & preview opens a NEW unsaved scene using BattleRig_3D and the existing tile/tower prefabs. Unity prompts about unsaved scenes before switching. Existing scene assets, camera values, battle rules and scene-flow targets are not changed. Only the newly generated camera is positioned to fit the grid at 16:9; its far plane is extended if necessary. The generated scene is opened alone so existing scenes' cameras and sessions do not run alongside it.
8. Save new level chooses a new .unity path. Existing assets, LevelTemplate and TestLevel are protected. Then press Play. After further grid edits, generate a new snapshot before saving again.

Ctrl+Z/Ctrl+Y undo and redo draft edits; generation also registers the new scene objects with Undo. Draft state is temporary Editor window state, not a persistent map asset. Save a completed map as a Scene before closing/restarting Unity. The window warns about unsaved draft edits. Invalid/incomplete drafts cannot yet be saved through Generate; keep the window open while authoring.

**Read layout from active scene** reads a single flat layer, its route, prefab tower settings and original grid origin. It prepares a draft for a NEW copy. Custom scene-only towers and stacked maps are rejected. It does not import cameras, lighting, arbitrary scene objects or custom tile meshes: generated copies use the standard rig and tile kit. Original scene assets remain untouched.

This version supports one flat layer, terrain types, routing and tower placement. It does not implement stacked terrain, runtime editing, automatic campaign registration or unified test-target migration. New levels run independently until explicitly connected to a game flow. Editor scripts stay in the Editor assembly and are excluded from player builds.

## Resize the workspace

The grid viewport and footer use explicit separate rectangles. Use the **Bottom panel height** slider above the grid for direct sizing (smaller values give the grid more space), or drag the horizontal handle between the grid and action buttons to change the split between the grid and information panel; drag downward for more grid space. Double-click the handle to reset. Zoom changes individual cell size independently. The panel split is window state and does not modify the map draft or scene.
