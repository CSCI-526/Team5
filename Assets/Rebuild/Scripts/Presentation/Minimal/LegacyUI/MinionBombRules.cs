using System.Collections.Generic;
using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public static class MinionBombRules
    {
        public static int PickTarget(IReadOnlyList<Vector3> units, Vector3 origin, float reach, float radius, out Vector3 center)
        {
            center = origin;
            if (units == null || units.Count == 0) return 0;
            var best = 0;
            var bestSpread = float.MaxValue;
            for (var i = 0; i < units.Count; i++)
                for (var j = i; j < units.Count; j++)
                {
                    var candidate = i == j ? units[i] : (units[i] + units[j]) * 0.5f;
                    if (FlatDistanceSquared(candidate, origin) > reach * reach) continue;
                    var count = 0;
                    var spread = 0f;
                    for (var k = 0; k < units.Count; k++)
                    {
                        var distance = FlatDistanceSquared(units[k], candidate);
                        if (distance > radius * radius) continue;
                        count++;
                        spread += distance;
                    }
                    if (count < best || (count == best && spread >= bestSpread)) continue;
                    best = count;
                    bestSpread = spread;
                    center = candidate;
                }
            return best;
        }

        // Predict almost the full flight, but leave the last fraction untracked.
        // Clip the lead along its original direction if it would exceed mortar reach.
        // The caller snapshots the result at launch; flight never re-aims.
        public static Vector3 PredictTarget(Vector3 position, Vector3 previous, float dt, Vector3 origin, float reach)
        {
            if (dt <= .0001f || reach <= 0f || FlatDistanceSquared(position, origin) > reach * reach)
                return position;
            var velocity = (position - previous) / dt;
            velocity.y = 0f;
            var lead = Vector3.ClampMagnitude(velocity * MinionRules.BombPredictionSeconds, MinionRules.BombMaxLead);
            if (FlatDistanceSquared(position + lead, origin) <= reach * reach) return position + lead;
            // Start is already in reach. Find the last in-range point on the segment.
            var low = 0f;
            var high = 1f;
            for (var i = 0; i < 12; i++)
            {
                var mid = (low + high) * .5f;
                if (FlatDistanceSquared(position + lead * mid, origin) <= reach * reach) low = mid;
                else high = mid;
            }
            return position + lead * low;
        }

        public static float FlatDistanceSquared(Vector3 a, Vector3 b)
        {
            var x = a.x - b.x;
            var z = a.z - b.z;
            return x * x + z * z;
        }
    }
}
