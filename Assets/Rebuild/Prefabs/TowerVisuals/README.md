# Campaign tower visuals

These prefabs contain models and materials only. They have no BattleTower, Collider, or combat scripts.

- `DirectionalTower_Visual`: the current saved single-cell cannon model.
- `SquareTower_Visual`: the current saved Square energy model, including the two badges added to the variant by the user.

Level1, Level2, and Level3 reference these visual prefabs beneath their original tower entities. The original BattleTower components, Session tower references, attack areas/directions, ranges, widths, damage, HP, cooldowns, positions, and 3x3 logical footprints are preserved. The original blue platform cells are unchanged.

Each visual has local Position `(0,0,0)` beneath the original tower's foot position. The cannon model faces -X by default. Its visual root uses Y rotation 0 for -X, 180 for +X, 90 for +Z, and -90 for -Z. Square visuals use rotation 0. These rotations affect appearance only; the original BattleTower Attack Area remains authoritative.

The old large Cube mesh is kept as an inactive child for reference. The active model occupies approximately one cell in the center of the existing nine-cell blue platform. Do not add a second BattleTower component to a visual child or reduce the original tower's footprint.

These are snapshots of the saved model prefabs at migration time. Editing the two visual prefabs here updates their campaign instances; editing the original attack prefabs separately does not automatically regenerate these snapshots. Do not apply scene-specific visual rotations back to the shared visual prefab.

Migration script: `Tools/Rebuild/replace_campaign_tower_visuals.py`. It refuses to overwrite an existing migration. Backups and the preservation report are in `Logs/CampaignTowerVisuals/`. Offline checks verified that scene edits are limited to visual parenting, hiding the old meshes, and adding visual instances. Unity Play Mode verification was not performed.
