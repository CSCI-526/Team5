using System.Collections.Generic;
using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public sealed class MinionPlacement
    {
        public MinionPlacement(MinionKind kind, int towerId, Vector3 towerPosition, IReadOnlyList<Vector3> cells, MinionElement element = MinionElement.None)
        {
            Kind = kind;
            TowerId = towerId;
            TowerPosition = towerPosition;
            Cells = cells;
            Element = element;
        }

        public MinionKind Kind { get; }
        public int TowerId { get; }
        public Vector3 TowerPosition { get; }
        public IReadOnlyList<Vector3> Cells { get; }
        public MinionElement Element { get; }
    }
}
