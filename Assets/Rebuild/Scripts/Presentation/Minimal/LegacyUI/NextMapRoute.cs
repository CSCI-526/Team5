using System.Collections.Generic;
using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public sealed class NextMapRoute
    {
        private readonly Vector3[] points;

        public NextMapRoute(IReadOnlyList<Vector3> waypoints)
        {
            points = new Vector3[waypoints != null ? waypoints.Count : 0];
            var length = 0f;
            for (var i = 0; i < points.Length; i++)
            {
                points[i] = waypoints[i];
                if (i > 0) length += Vector3.Distance(points[i - 1], points[i]);
            }
            Length = length;
        }

        public float Length { get; }
        public int Count => points.Length;
        public Vector3 this[int index] => points[index];

        public Vector3 Sample(float distance)
        {
            if (points.Length == 0) return Vector3.zero;
            for (var i = 1; i < points.Length; i++)
            {
                var segment = Vector3.Distance(points[i - 1], points[i]);
                if (segment <= 0f) continue;
                if (distance <= segment) return Vector3.Lerp(points[i - 1], points[i], distance / segment);
                distance -= segment;
            }
            return points[points.Length - 1];
        }

        public Vector3 Direction(float distance)
        {
            var last = Vector3.forward;
            for (var i = 1; i < points.Length; i++)
            {
                var segment = Vector3.Distance(points[i - 1], points[i]);
                if (segment <= 0f) continue;
                last = (points[i] - points[i - 1]) / segment;
                if (distance <= segment) return last;
                distance -= segment;
            }
            return last;
        }
    }
}
