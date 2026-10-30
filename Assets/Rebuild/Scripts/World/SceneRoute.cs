using System.Collections.Generic;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.World
{
    [DisallowMultipleComponent]
    public sealed class SceneRoute : MonoBehaviour
    {
        [SerializeField] private List<BattleCell> cells = new List<BattleCell>();
        [SerializeField] private int requiredWidth = 1;
        public IReadOnlyList<BattleCell> Cells => cells.AsReadOnly();
        public int RequiredWidth => requiredWidth;

        public void CollectErrors(BattleGrid grid, List<string> errors)
        {
            if (cells.Count < 2) { errors.Add("SceneRoute needs a Spawn and a Goal with an ordered road."); return; }
            var visited = new HashSet<BattleCell>();
            if (requiredWidth != 1 && requiredWidth != 3) errors.Add("Route width must be 1 or 3.");
            var previousPhase = 0;
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (cell == null) { errors.Add($"Route entry {i} is missing."); continue; }
                if (!visited.Add(cell)) errors.Add($"Route repeats {cell.name}.");
                if (!grid.Contains(cell)) errors.Add($"{cell.name}: route cell is outside its BattleGrid.");
                if (!cell.IsRouteSurface || !grid.HasClearTop(cell)) errors.Add($"{cell.name}: route surface is not passable.");
                var expected = i == 0 ? CellKind.Spawn : i == cells.Count - 1 ? CellKind.Goal : CellKind.Road;
                if (requiredWidth == 1 || i == 0 || i == cells.Count - 1)
                {
                    if (cell.Kind != expected) errors.Add($"Route entry {i} must be {expected}.");
                }
                else
                {
                    var phase = cell.Kind == CellKind.Spawn ? 0 : cell.Kind == CellKind.Road ? 1 : 2;
                    if (phase < previousPhase) errors.Add($"Route entry {i}: Spawn/Road/Goal order is invalid.");
                    previousPhase = phase;
                }
                if (requiredWidth == 3)
                    for (var z = -1; z <= 1; z++)
                    for (var x = -1; x <= 1; x++)
                    {
                        var key = cell.Occupancy + new Vector3Int(x, 0, z);
                        if (!grid.TryGetCell(key, out var neighbor) || !neighbor.IsRouteSurface || !grid.HasClearTop(neighbor))
                            errors.Add($"Formation clearance blocked at {key} around route entry {i}.");
                    }
                if (i == 0 || cells[i - 1] == null) continue;
                var offset = cell.Occupancy - cells[i - 1].Occupancy;
                if (offset.y != 0 || Mathf.Abs(offset.x) + Mathf.Abs(offset.z) != 1)
                    errors.Add($"Route disconnected or unequal height between entries {i - 1} and {i}.");
                else if (requiredWidth == 3)
                    for (var slot = 0; slot < 9; slot++)
                        foreach (var swept in FormationClearance.SweptCells(cells[i - 1].TopCenter + SquadCombatRules.Offset(slot),
                            cell.TopCenter + SquadCombatRules.Offset(slot)))
                            if (!grid.TryGetCell(swept, out var support) || !support.IsRouteSurface || !grid.HasClearTop(support))
                                errors.Add($"Formation clearance blocked along slot {slot} trajectory, segment {i - 1}, cell {swept}.");
            }
        }

        public Vector3[] CaptureTopCenters()
        {
            var points = new Vector3[cells.Count];
            for (var i = 0; i < points.Length; i++) points[i] = cells[i].TopCenter;
            return points;
        }
    }
}
