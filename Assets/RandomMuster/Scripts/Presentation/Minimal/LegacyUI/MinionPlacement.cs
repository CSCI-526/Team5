using System.Collections.Generic;
using UnityEngine;

namespace Team5.BasicTowerTrial.Presentation.LegacyUI
{
    public sealed class MinionPlacement
    {
        public MinionPlacement(int routeIndex, Vector3 center, Vector3 approach, IReadOnlyList<Vector3> cells)
        {
            RouteIndex = routeIndex;
            Center = center;
            Approach = approach;
            Cells = cells;
        }

        public int RouteIndex { get; }
        public Vector3 Center { get; }
        public Vector3 Approach { get; }
        public IReadOnlyList<Vector3> Cells { get; }
    }
}
