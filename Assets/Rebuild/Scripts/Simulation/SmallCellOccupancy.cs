using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Team5.Rebuild.Simulation
{
    // A derived query result, never a second editable map or a renderer/collider query.
    public sealed class SmallCellOccupancy
    {
        public ReadOnlyCollection<int> TowerIds { get; }
        public ReadOnlyCollection<int> UnitIds { get; }
        public bool IsOccupied => TowerIds.Count != 0 || UnitIds.Count != 0;
        internal SmallCellOccupancy(List<int> towers, List<int> units)
        { TowerIds = towers.AsReadOnly(); UnitIds = units.AsReadOnly(); }
        public static Vector3Int PositionToCell(Vector3 feet) => new Vector3Int(
            Mathf.FloorToInt(feet.x + 0.5f), Mathf.FloorToInt((feet.y - 0.25f) / 0.5f + 0.5f), Mathf.FloorToInt(feet.z + 0.5f));
    }
}
