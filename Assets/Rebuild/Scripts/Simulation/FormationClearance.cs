using System;
using System.Collections.Generic;
using UnityEngine;

namespace Team5.Rebuild.Simulation
{
    public static class FormationClearance
    {
        // Positive-area terrain overlap of a 1x1 member footprint swept along an axis.
        // Corners consist of two segments. This never assumes that road width alone suffices.
        public static IEnumerable<Vector3Int> SweptCells(Vector3 from, Vector3 to)
        {
            if (Mathf.Abs(from.y - to.y) > 0.001f ||
                (Mathf.Abs(from.x - to.x) > 0.001f && Mathf.Abs(from.z - to.z) > 0.001f))
                throw new ArgumentException("Formation trajectories must be equal-height and axis aligned.");
            var minX = Mathf.FloorToInt(Mathf.Min(from.x, to.x) + 0.0001f);
            var maxX = Mathf.CeilToInt(Mathf.Max(from.x, to.x) - 0.0001f);
            var minZ = Mathf.FloorToInt(Mathf.Min(from.z, to.z) + 0.0001f);
            var maxZ = Mathf.CeilToInt(Mathf.Max(from.z, to.z) - 0.0001f);
            var layer = SmallCellOccupancy.PositionToCell(from).y;
            for (var z = minZ; z <= maxZ; z++)
            for (var x = minX; x <= maxX; x++) yield return new Vector3Int(x, layer, z);
        }
    }
}
