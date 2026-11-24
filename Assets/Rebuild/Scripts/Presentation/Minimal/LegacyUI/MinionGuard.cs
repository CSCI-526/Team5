using System.Collections.Generic;
using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public sealed class MinionGuard
    {
        private const float HopSeconds = 0.32f;
        private const float Hysteresis = 0.2f;

        private readonly Vector2Int center;
        private readonly List<MinionPost> bearers;
        private readonly List<MinionPost> movers = new List<MinionPost>();
        private readonly bool[] open = new bool[8];
        private readonly bool[] passable = new bool[8];
        private readonly Vector3[] tops = new Vector3[8];
        private int desired = -1;

        public MinionGuard(Vector3 tower, List<MinionPost> bearers, MinionSites sites)
        {
            this.bearers = bearers;
            center = new Vector2Int(Mathf.RoundToInt(tower.x), Mathf.RoundToInt(tower.z));
            for (var i = 0; i < MinionGuardRules.Ring.Length; i++)
            {
                var cell = center + MinionGuardRules.Ring[i];
                open[i] = sites.CanStand(cell);
                tops[i] = sites.Top(cell);
            }
            foreach (var bearer in bearers)
            {
                var index = IndexOf(bearer.Cell);
                if (index >= 0) open[index] = true;
            }
        }

        public void Reset() => desired = -1;

        public void Tick(Vector3 squad, HashSet<Vector2Int> blocked, HashSet<Vector2Int> unitBlocked = null)
        {
            var direction = new Vector3(squad.x - center.x, 0f, squad.z - center.y);
            if (direction.sqrMagnitude < 0.01f) return;
            movers.Clear();
            foreach (var bearer in bearers)
                if (bearer.IsAlive && !bearer.IsFrozen) movers.Add(bearer);
            if (movers.Count == 0) return;
            desired = MinionGuardRules.Desired(open, direction, desired, Hysteresis);
            if (desired < 0) return;
            var first = desired;
            var second = movers.Count > 1 ? MinionGuardRules.Partner(open, direction, desired) : -1;
            if (movers.Count > 1 && second >= 0)
            {
                var a = IndexOf(movers[0].Cell);
                var b = IndexOf(movers[1].Cell);
                var keep = Cost(a, first) + Cost(b, second);
                var swap = Cost(a, second) + Cost(b, first);
                if (swap < keep)
                {
                    first = second;
                    second = desired;
                }
            }
            for (var m = 0; m < movers.Count; m++)
            {
                var mover = movers[m];
                mover.Face(squad);
                var target = m == 0 ? first : m == 1 ? second : -1;
                if (target < 0 || mover.IsSliding) continue;
                var from = IndexOf(mover.Cell);
                if (from == target) continue;
                for (var i = 0; i < passable.Length; i++) passable[i] = open[i] && !blocked.Contains(center + MinionGuardRules.Ring[i]);
                var next = from < 0 ? (passable[target] ? target : -1) : MinionGuardRules.Step(from, target, open, passable);
                if (next < 0) continue;
                if (unitBlocked == null || !unitBlocked.Contains(mover.Cell)) blocked.Remove(mover.Cell);
                blocked.Add(center + MinionGuardRules.Ring[next]);
                mover.Hop(tops[next], HopSeconds);
            }
        }

        private int IndexOf(Vector2Int cell) => MinionGuardRules.IndexOf(cell - center);

        private static int Cost(int from, int to) => from < 0 || to < 0 ? 8 : MinionGuardRules.Distance(from, to);
    }
}
