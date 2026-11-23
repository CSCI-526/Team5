using System.Collections.Generic;
using Team5.BasicTowerTrial.Simulation;
using UnityEngine;

namespace Team5.BasicTowerTrial.Presentation.LegacyUI
{
    public static class MinionPlanner
    {
        public static List<MinionPlacement> Plan(IReadOnlyList<Vector3> route, IReadOnlyList<TowerDefinition> towers, int level)
        {
            var spots = new List<MinionPlacement>();
            if (!MinionRules.HasMinions(level) || route == null || towers == null || route.Count <= 2 * MinionRules.EdgeCells) return spots;
            var fronts = new SortedDictionary<int, TowerDefinition>();
            foreach (var tower in towers)
            {
                var index = FrontCell(route, tower);
                if (index < 0) continue;
                if (!fronts.TryGetValue(index, out var current) || Facing(route, index, tower) > Facing(route, index, current)) fronts[index] = tower;
            }
            var last = -MinionRules.MinSpacing;
            foreach (var pair in fronts)
            {
                var index = pair.Key;
                if (index - last < MinionRules.MinSpacing) continue;
                var approach = Flat(route[index] - route[index - 1]);
                if (approach.sqrMagnitude < 0.0001f) continue;
                last = index;
                var count = MinionRules.CountAt(level, spots.Count);
                spots.Add(new MinionPlacement(index, route[index], approach.normalized, Line(route, index, pair.Value, count)));
            }
            return spots;
        }

        private static int FrontCell(IReadOnlyList<Vector3> route, TowerDefinition tower)
        {
            if (tower == null || tower.IsWall || tower.IsAoe || tower.Area == AttackArea.Square) return -1;
            var best = -1;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < route.Count; i++)
            {
                var distance = Flat(route[i] - tower.Position).magnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }
            if (best < MinionRules.EdgeCells || best > route.Count - 1 - MinionRules.EdgeCells || bestDistance > MinionRules.MaxTowerDistance) return -1;
            return SquadCombatRules.Contains(Flat(route[best] - tower.Position), tower.Area, tower.Range, tower.AttackWidth) ? best : -1;
        }

        private static float Facing(IReadOnlyList<Vector3> route, int index, TowerDefinition tower) =>
            Vector3.Dot(Flat(tower.Position - route[index]).normalized, Flat(route[index] - route[index - 1]).normalized);

        private static List<Vector3> Line(IReadOnlyList<Vector3> route, int index, TowerDefinition tower, int count)
        {
            var center = route[index];
            var offset = Flat(tower.Position - center);
            var normal = Mathf.Abs(offset.x) >= Mathf.Abs(offset.z) ? new Vector3(Mathf.Sign(offset.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(offset.z));
            var along = new Vector3(normal.z, 0f, normal.x);
            var middle = center + normal;
            var a = middle - along;
            var b = middle + along;
            var aFirst = Adjacency(route, a) <= Adjacency(route, b);
            var cells = new List<Vector3> { middle, aFirst ? a : b, aFirst ? b : a };
            return cells.GetRange(0, Mathf.Clamp(count, 0, cells.Count));
        }

        private static int Adjacency(IReadOnlyList<Vector3> route, Vector3 cell)
        {
            for (var i = 0; i < route.Count; i++)
            {
                var offset = route[i] - cell;
                if (Mathf.Abs(offset.x) + Mathf.Abs(offset.z) <= 1.01f) return i;
            }
            return int.MaxValue;
        }

        private static Vector3 Flat(Vector3 value) => new Vector3(value.x, 0f, value.z);
    }
}
