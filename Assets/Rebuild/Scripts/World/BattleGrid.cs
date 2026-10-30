using System.Collections.Generic;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.World
{
    [DisallowMultipleComponent]
    public sealed class BattleGrid : MonoBehaviour
    {
        public const int MacroSize = 3;
        // Only a temporary lookup. The Scene's child BattleCells remain the authority.
        private readonly Dictionary<Vector3Int, BattleCell> index = new Dictionary<Vector3Int, BattleCell>();
        public BattleCell[] SceneCells => GetComponentsInChildren<BattleCell>(true);

        public void RebuildIndex(List<string> errors)
        {
            index.Clear();
            var cells = SceneCells;
            if (cells.Length == 0) errors.Add("BattleGrid contains no cells.");
            foreach (var cell in cells)
            {
                cell.CollectErrors(errors);
                if (!cell.gameObject.activeInHierarchy) errors.Add($"{cell.name}: configured cells must be active.");
                if (index.TryGetValue(cell.Occupancy, out var other))
                    errors.Add($"Grid conflict at {cell.Occupancy}: {other.name} and {cell.name}.");
                else index.Add(cell.Occupancy, cell);
            }
        }

        // RebuildIndex must be called before querying after an edit.
        public bool TryGetCell(Vector3Int occupancy, out BattleCell cell) => index.TryGetValue(occupancy, out cell);
        public bool Contains(BattleCell cell) => cell != null && index.TryGetValue(cell.Occupancy, out var value) && value == cell;
        public bool HasClearTop(BattleCell cell) => !index.ContainsKey(cell.Occupancy + Vector3Int.up);

        public static Vector3Int ToMacroCell(Vector3Int smallCell) => new Vector3Int(
            Mathf.FloorToInt(smallCell.x / 3f), smallCell.y, Mathf.FloorToInt(smallCell.z / 3f));

        // Macro coordinates are (macro X, unchanged height layer, macro Z). No serialized Macro positions.
        public BattleCell[] GetMacroCells(Vector3Int macro)
        {
            var cells = new BattleCell[9];
            for (var z = 0; z < 3; z++)
            for (var x = 0; x < 3; x++)
                index.TryGetValue(new Vector3Int(macro.x * 3 + x, macro.y, macro.z * 3 + z), out cells[z * 3 + x]);
            return cells;
        }

        public SmallCellOccupancy[] GetMacroOccupancy(Vector3Int macro, BattleSimulation simulation)
        {
            var result = new SmallCellOccupancy[9];
            for (var z = 0; z < 3; z++)
            for (var x = 0; x < 3; x++)
                result[z * 3 + x] = simulation.GetCellOccupancy(new Vector3Int(macro.x * 3 + x, macro.y, macro.z * 3 + z));
            return result;
        }
    }
}
