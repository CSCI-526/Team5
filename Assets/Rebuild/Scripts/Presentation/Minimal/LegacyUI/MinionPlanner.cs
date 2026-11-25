using System.Collections.Generic;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public static class MinionPlanner
    {
        public static List<MinionPlacement> Plan(IReadOnlyList<Vector3> route, IReadOnlyList<TowerDefinition> towers, int level, MinionSites sites)
        {
            var placements = new List<MinionPlacement>();
            var kinds = MinionRules.Kinds(level);
            if (kinds.Count == 0 || route == null || route.Count < 2 || towers == null || sites == null) return placements;
            var taken = new HashSet<Vector2Int>();
            var next = 0;
            foreach (var tower in Order(route, towers))
            {
                if (next >= kinds.Count) break;
                var kind = kinds[next];
                var cells = Cells(route, tower, kind, MinionRules.Count(kind, level), sites, taken);
                if (cells.Count == 0) continue;
                var element = kind == MinionKind.Lobber ? MinionRules.Element(level, LobberOrdinal(kinds, next)) : MinionElement.None;
                placements.Add(new MinionPlacement(kind, tower.Id, tower.Position, cells, element));
                next++;
            }
            return placements;
        }

        private static int LobberOrdinal(IReadOnlyList<MinionKind> kinds, int index)
        {
            var ordinal = 0;
            for (var i = 0; i < index; i++)
                if (kinds[i] == MinionKind.Lobber) ordinal++;
            return ordinal;
        }

        private static List<TowerDefinition> Order(IReadOnlyList<Vector3> route, IReadOnlyList<TowerDefinition> towers)
        {
            var path = new NextMapRoute(route);
            var candidates = new List<TowerDefinition>();
            var contact = new Dictionary<TowerDefinition, float>();
            var distance = new Dictionary<TowerDefinition, float>();
            foreach (var tower in towers)
            {
                if (tower == null || tower.IsWall || tower.Damage <= 0f) continue;
                candidates.Add(tower);
                contact[tower] = NextMapRules.FirstContact(path, tower);
                distance[tower] = Flat(route[Nearest(route, tower.Position)] - tower.Position).magnitude;
            }
            candidates.Sort((a, b) =>
            {
                var compare = contact[a].CompareTo(contact[b]);
                if (compare != 0) return compare;
                compare = distance[a].CompareTo(distance[b]);
                return compare != 0 ? compare : a.Id.CompareTo(b.Id);
            });
            return candidates;
        }

        private static List<Vector3> Cells(IReadOnlyList<Vector3> route, TowerDefinition tower, MinionKind kind, int count, MinionSites sites, HashSet<Vector2Int> taken)
        {
            var result = new List<Vector3>();
            var index = Nearest(route, tower.Position);
            var toward = Axis(Flat(route[index] - tower.Position), Vector3.forward);
            var travel = Flat(route[Mathf.Min(index + 1, route.Count - 1)] - route[Mathf.Max(index - 1, 0)]);
            var side = new Vector3(toward.z, 0f, toward.x);
            var up = Vector3.Dot(side, -travel) >= 0f ? side : -side;
            var down = -up;
            var back = -toward;
            var path = new NextMapRoute(route);
            var pattern = kind == MinionKind.Bearer
                ? new[] { toward, toward + up, toward + down, up, down, back + up, back + down, back }
                : new[] { back, back + down, back + up, down, up, back * 2f, back * 2f + down, back * 2f + up, toward, toward + up, toward + down,
                    toward * 2f, toward * 2f + up, toward * 2f + down, toward * 3f };
            var origin = new Vector2Int(Mathf.RoundToInt(tower.Position.x), Mathf.RoundToInt(tower.Position.z));
            for (var pass = 0; pass < 2 && result.Count < count; pass++)
                foreach (var step in pattern)
                {
                    if (result.Count >= count) break;
                    var cell = origin + new Vector2Int(Mathf.RoundToInt(step.x), Mathf.RoundToInt(step.z));
                    if (taken.Contains(cell) || !sites.CanHost(cell) || (pass == 0 && !sites.IsClean(cell)) || Seals(cell, sites, taken)) continue;
                    var top = sites.Top(cell);
                    // A mortar behind a long-range tower can otherwise be too far away to
                    // ever acquire a squad. Use distance to the route segments, not just nodes.
                    var distance = DistanceToRoute(top, path);
                    if (kind == MinionKind.Lobber && distance > MinionRules.BombReach - MinionRules.BombRadius) continue;
                    // A shield should be reachable by ordinary attackers near the road edge.
                    if (kind == MinionKind.Bearer && distance > 3.4f) continue;
                    taken.Add(cell);
                    result.Add(top);
                }
            return result;
        }

        private static float DistanceToRoute(Vector3 point, NextMapRoute route)
        {
            var best = float.PositiveInfinity;
            for (var i = 1; i < route.Count; i++)
            {
                var a = Flat(route[i - 1]);
                var delta = Flat(route[i]) - a;
                var t = delta.sqrMagnitude < .000001f ? 0f : Mathf.Clamp01(Vector3.Dot(Flat(point) - a, delta) / delta.sqrMagnitude);
                best = Mathf.Min(best, (Flat(point) - a - delta * t).magnitude);
            }
            return best;
        }

        private static bool Seals(Vector2Int cell, MinionSites sites, HashSet<Vector2Int> taken)
        {
            if (!sites.IsWalkable(cell)) return false;
            var blockedX = !Open(cell + Vector2Int.left, sites, taken) && !Open(cell + Vector2Int.right, sites, taken);
            var blockedZ = !Open(cell + Vector2Int.down, sites, taken) && !Open(cell + Vector2Int.up, sites, taken);
            return blockedX || blockedZ;
        }

        private static bool Open(Vector2Int cell, MinionSites sites, HashSet<Vector2Int> taken) => sites.IsWalkable(cell) && !taken.Contains(cell);

        private static int Nearest(IReadOnlyList<Vector3> route, Vector3 position)
        {
            var best = 0;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < route.Count; i++)
            {
                var distance = Flat(route[i] - position).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = i;
            }
            return best;
        }

        private static Vector3 Axis(Vector3 offset, Vector3 fallback)
        {
            if (offset.sqrMagnitude < 0.0001f) return fallback;
            return Mathf.Abs(offset.x) >= Mathf.Abs(offset.z) ? new Vector3(Mathf.Sign(offset.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(offset.z));
        }

        private static Vector3 Flat(Vector3 value) => new Vector3(value.x, 0f, value.z);
    }
}
