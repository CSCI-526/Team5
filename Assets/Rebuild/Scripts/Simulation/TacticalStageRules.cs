using System.Collections.Generic;
using UnityEngine;

namespace Team5.Rebuild.Simulation
{
    // Fixed, readable firing lanes; the same immutable definition is used by combat and preview.
    public static class TacticalStageRules
    {
        private static readonly AttackArea[] Cones = { AttackArea.ConePositiveX, AttackArea.ConeNegativeX, AttackArea.ConePositiveZ, AttackArea.ConeNegativeZ };
        private static readonly AttackArea[] Lasers = { AttackArea.PositiveX, AttackArea.NegativeX, AttackArea.PositiveZ, AttackArea.NegativeZ };
        // Regular tower shots only: tanks retain their full armor against hazards/minions.
        // The percent-HP component is bounded by the authored shot so even an Epic tank
        // never takes an unbounded bonus. Other roles are not affected.
        public static float TankHitDamage(float rawDamage, float maxHp, float defense) =>
            (rawDamage + Mathf.Min(maxHp * .05f, rawDamage * 1.25f)) * (1f - defense * .5f);

        public static float DamageScale(int level) => 1f + .065f * Mathf.Clamp(level - 1, 0, 6);
        public static float HealthScale(int level) => 1f + .035f * Mathf.Clamp(level - 1, 0, 6);
        public static float IntervalScale(int level) => 1f - .02f * Mathf.Clamp(level - 2, 0, 5);

        public static TowerDefinition Tune(TowerDefinition tower, IReadOnlyList<Vector3> route, int level, bool applyStatScaling = true)
        {
            if (tower.IsWall || route == null || route.Count < 2) return tower;
            var area = tower.Area;
            var range = tower.Range;
            if (area == AttackArea.Square) area = AttackArea.Circle;
            // Lasers are deliberately reserved for the late game, and remain dodgeable.
            if (SquadCombatRules.IsLaser(area) && level < 6) area = Cones[(int)area - 1];
            if (SquadCombatRules.IsCone(area) || SquadCombatRules.IsLaser(area))
            {
                var candidates = SquadCombatRules.IsLaser(area) ? Lasers : Cones;
                var score = Coverage(tower.Position, area, range, tower.AttackWidth, route);
                if (score <= 0f)
                    foreach (var candidate in candidates)
                    {
                        var candidateScore = Coverage(tower.Position, candidate, range, tower.AttackWidth, route);
                        if (candidateScore <= score) continue;
                        area = candidate; score = candidateScore;
                    }
                // Only extend a blind tower when a small range correction lets it cover the road.
                // Do not turn a precision beam into an unavoidable full-width attack.
                if (score == 0)
                    for (var extra = .5f; extra <= 2f; extra += .5f)
                    {
                        var best = area; var bestScore = 0f;
                        foreach (var candidate in candidates)
                        {
                            var candidateScore = Coverage(tower.Position, candidate, range + extra, tower.AttackWidth, route);
                            if (candidateScore <= bestScore) continue;
                            best = candidate; bestScore = candidateScore;
                        }
                        if (bestScore <= 0) continue;
                        area = best; range += extra; break;
                    }
            }
            else if (SquadCombatRules.IsCircle(area) && Coverage(tower.Position, area, range, tower.AttackWidth, route) == 0)
            {
                for (var extra = .5f; extra <= 2f; extra += .5f)
                    if (Coverage(tower.Position, area, range + extra, tower.AttackWidth, route) > 0)
                    { range += extra; break; }
            }
            return new TowerDefinition(tower.Id, tower.Position, range, tower.Damage * (applyStatScaling ? DamageScale(level) : 1f),
                tower.AttackInterval * (applyStatScaling ? IntervalScale(level) : 1f), tower.TargetRule, tower.FootprintSize,
                tower.MaxHp * (applyStatScaling ? HealthScale(level) : 1f), area, tower.AttackWidth, tower.IsAoe, tower.GoldBounty);
        }

        // Score samples along the authored centerline and both sides of the formation.
        // Centerline coverage is favored over catching just one corner of the road.
        public static float Coverage(Vector3 origin, AttackArea area, float range, float width, IReadOnlyList<Vector3> route)
        {
            var score = 0f;
            for (var i = 1; i < route.Count; i++)
            {
                var delta = route[i] - route[i - 1]; delta.y = 0;
                var length = delta.magnitude;
                if (length < .0001f) continue;
                var side = new Vector3(-delta.z, 0, delta.x) / length;
                var samples = Mathf.Max(1, Mathf.CeilToInt(length * 4f));
                for (var j = 0; j < samples; j++)
                {
                    var center = Vector3.Lerp(route[i - 1], route[i], (j + .5f) / samples);
                    for (var lane = -1; lane <= 1; lane++)
                        if (SquadCombatRules.Contains(center + side * lane - origin, area, range, width))
                            score += (lane == 0 ? 3f : 1f) * length / samples;
                }
            }
            return score;
        }

        public static Vector3 Facing(TowerDefinition tower, IReadOnlyList<Vector3> route)
        {
            var direction = SquadCombatRules.Direction(tower.Area);
            if (direction != Vector3.zero) return direction;
            if (route == null || route.Count == 0) return Vector3.zero;
            var best = Vector3.zero; var nearest = float.PositiveInfinity;
            for (var i = 0; i < route.Count; i++)
            {
                var offset = route[i] - tower.Position; offset.y = 0;
                if (offset.sqrMagnitude < .0001f || offset.sqrMagnitude >= nearest) continue;
                nearest = offset.sqrMagnitude; best = offset.normalized;
            }
            return best;
        }
    }
}
