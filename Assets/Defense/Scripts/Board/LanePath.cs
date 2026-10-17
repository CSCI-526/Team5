using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReverseTD.Defense
{
    public sealed class LanePath
    {
        private const float MinPieceLength = 0.00001f;
        private const float MinTurn = 0.001f;
        private const float MinRadius = 0.0001f;

        private readonly Vector2[] waypoints;
        private readonly float[] cornerStart;
        private readonly List<bool> pieceIsArc = new List<bool>();
        private readonly List<float> pieceStart = new List<float>();
        private readonly List<float> pieceLength = new List<float>();
        private readonly List<Vector2> pieceOrigin = new List<Vector2>();
        private readonly List<Vector2> pieceDirection = new List<Vector2>();
        private readonly List<float> pieceRadius = new List<float>();
        private readonly List<float> pieceAngle = new List<float>();
        private readonly List<float> pieceTurn = new List<float>();
        private readonly Vector2 startDirection;
        private readonly Vector2 endDirection;

        public LanePath(IReadOnlyList<Vector2> points, float cornerRadius)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            var cleaned = new List<Vector2>();
            for (int i = 0; i < points.Count; i++)
            {
                if (cleaned.Count == 0 || (points[i] - cleaned[cleaned.Count - 1]).sqrMagnitude > MinPieceLength * MinPieceLength)
                {
                    cleaned.Add(points[i]);
                }
            }
            if (cleaned.Count < 2)
            {
                throw new ArgumentException("A lane path needs at least 2 distinct waypoints.", nameof(points));
            }

            waypoints = cleaned.ToArray();
            int count = waypoints.Length;
            cornerStart = new float[count];
            startDirection = (waypoints[1] - waypoints[0]).normalized;
            endDirection = (waypoints[count - 1] - waypoints[count - 2]).normalized;
            MinCornerRadius = float.MaxValue;

            Vector2 cursor = waypoints[0];
            float distance = 0f;
            for (int i = 1; i < count - 1; i++)
            {
                Vector2 incoming = (waypoints[i] - waypoints[i - 1]).normalized;
                Vector2 outgoing = (waypoints[i + 1] - waypoints[i]).normalized;
                float turn = Vector2.SignedAngle(incoming, outgoing) * Mathf.Deg2Rad;
                float halfTan = Mathf.Tan(Mathf.Abs(turn) * 0.5f);
                float tangent = Mathf.Min(cornerRadius * halfTan,
                    Vector2.Distance(waypoints[i - 1], waypoints[i]) * 0.5f,
                    Vector2.Distance(waypoints[i], waypoints[i + 1]) * 0.5f);
                float radius = Mathf.Abs(turn) < MinTurn ? 0f : tangent / halfTan;

                if (radius < MinRadius || float.IsNaN(radius) || float.IsInfinity(radius))
                {
                    AddLine(cursor, waypoints[i], ref distance);
                    cornerStart[i] = distance;
                    cursor = waypoints[i];
                    if (Mathf.Abs(turn) >= MinTurn)
                    {
                        MinCornerRadius = 0f;
                    }
                    continue;
                }

                Vector2 arcStart = waypoints[i] - incoming * tangent;
                AddLine(cursor, arcStart, ref distance);
                cornerStart[i] = distance;

                float sign = Mathf.Sign(turn);
                Vector2 center = arcStart + Left(incoming) * radius * sign;
                Vector2 fromCenter = arcStart - center;
                AddArc(center, radius, Mathf.Atan2(fromCenter.y, fromCenter.x), sign, radius * Mathf.Abs(turn), ref distance);

                cursor = waypoints[i] + outgoing * tangent;
                MinCornerRadius = Mathf.Min(MinCornerRadius, radius);
            }

            AddLine(cursor, waypoints[count - 1], ref distance);
            cornerStart[count - 1] = distance;
            TotalLength = distance;
            if (MinCornerRadius == float.MaxValue)
            {
                MinCornerRadius = cornerRadius;
            }
        }

        public float TotalLength { get; }

        public float MinCornerRadius { get; }

        public Vector2 Sample(float distance, float sideOffset)
        {
            Vector2 point;
            Vector2 heading;
            Evaluate(distance, out point, out heading);
            return point + Left(heading) * sideOffset;
        }

        public float DivergeDistance(LanePath other)
        {
            if (other == null)
            {
                return TotalLength;
            }

            int shared = Mathf.Min(waypoints.Length, other.waypoints.Length);
            for (int i = 0; i < shared; i++)
            {
                if ((waypoints[i] - other.waypoints[i]).sqrMagnitude > MinPieceLength * MinPieceLength)
                {
                    return i <= 1 ? 0f : Mathf.Min(cornerStart[i - 1], other.cornerStart[i - 1]);
                }
            }

            if (waypoints.Length == other.waypoints.Length)
            {
                return TotalLength;
            }
            return shared <= 1 ? 0f : Mathf.Min(cornerStart[shared - 1], other.cornerStart[shared - 1]);
        }

        private void Evaluate(float distance, out Vector2 point, out Vector2 heading)
        {
            if (distance <= 0f)
            {
                heading = startDirection;
                point = waypoints[0] + startDirection * distance;
                return;
            }
            if (distance >= TotalLength)
            {
                heading = endDirection;
                point = waypoints[waypoints.Length - 1] + endDirection * (distance - TotalLength);
                return;
            }

            int last = pieceStart.Count - 1;
            for (int i = 0; i <= last; i++)
            {
                if (distance > pieceStart[i] + pieceLength[i] && i < last)
                {
                    continue;
                }

                float along = Mathf.Clamp(distance - pieceStart[i], 0f, pieceLength[i]);
                if (!pieceIsArc[i])
                {
                    heading = pieceDirection[i];
                    point = pieceOrigin[i] + heading * along;
                    return;
                }

                float sign = pieceTurn[i];
                float angle = pieceAngle[i] + sign * along / pieceRadius[i];
                var radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                point = pieceOrigin[i] + radial * pieceRadius[i];
                heading = new Vector2(-radial.y, radial.x) * sign;
                return;
            }

            heading = endDirection;
            point = waypoints[waypoints.Length - 1];
        }

        private void AddLine(Vector2 from, Vector2 to, ref float distance)
        {
            float length = Vector2.Distance(from, to);
            if (length < MinPieceLength)
            {
                return;
            }

            pieceIsArc.Add(false);
            pieceStart.Add(distance);
            pieceLength.Add(length);
            pieceOrigin.Add(from);
            pieceDirection.Add((to - from) / length);
            pieceRadius.Add(0f);
            pieceAngle.Add(0f);
            pieceTurn.Add(0f);
            distance += length;
        }

        private void AddArc(Vector2 center, float radius, float startAngle, float sign, float length, ref float distance)
        {
            if (length < MinPieceLength)
            {
                return;
            }

            pieceIsArc.Add(true);
            pieceStart.Add(distance);
            pieceLength.Add(length);
            pieceOrigin.Add(center);
            pieceDirection.Add(Vector2.zero);
            pieceRadius.Add(radius);
            pieceAngle.Add(startAngle);
            pieceTurn.Add(sign);
            distance += length;
        }

        private static Vector2 Left(Vector2 direction)
        {
            return new Vector2(-direction.y, direction.x);
        }
    }
}
