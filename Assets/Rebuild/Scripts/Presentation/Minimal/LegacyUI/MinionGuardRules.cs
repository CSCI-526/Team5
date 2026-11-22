using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public static class MinionGuardRules
    {
        public static readonly Vector2Int[] Ring =
        {
            new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(0, 1), new Vector2Int(-1, 1),
            new Vector2Int(-1, 0), new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(1, -1)
        };

        public static int IndexOf(Vector2Int offset)
        {
            for (var i = 0; i < Ring.Length; i++)
                if (Ring[i] == offset) return i;
            return -1;
        }

        public static float Score(int index, Vector3 direction)
        {
            var flat = new Vector3(direction.x, 0f, direction.z);
            if (flat.sqrMagnitude < 0.0001f) return 0f;
            return Vector3.Dot(new Vector3(Ring[index].x, 0f, Ring[index].y).normalized, flat.normalized);
        }

        public static int Desired(bool[] open, Vector3 direction, int current, float hysteresis)
        {
            var best = -1;
            var bestScore = float.MinValue;
            for (var i = 0; i < Ring.Length; i++)
            {
                if (!open[i]) continue;
                var score = Score(i, direction);
                if (score <= bestScore) continue;
                best = i;
                bestScore = score;
            }
            if (best < 0) return -1;
            return current >= 0 && open[current] && Score(current, direction) >= bestScore - hysteresis ? current : best;
        }

        public static int Partner(bool[] open, Vector3 direction, int desired)
        {
            if (desired < 0) return -1;
            var right = (desired + 1) % Ring.Length;
            var left = (desired + Ring.Length - 1) % Ring.Length;
            if (open[right] || open[left])
            {
                if (!open[left]) return right;
                if (!open[right]) return left;
                return Score(right, direction) >= Score(left, direction) ? right : left;
            }
            var best = -1;
            var bestScore = float.MinValue;
            for (var i = 0; i < Ring.Length; i++)
            {
                if (!open[i] || i == desired) continue;
                var score = Score(i, direction);
                if (score <= bestScore) continue;
                best = i;
                bestScore = score;
            }
            return best;
        }

        public static int Distance(int a, int b)
        {
            var forward = (b - a + Ring.Length) % Ring.Length;
            return Mathf.Min(forward, Ring.Length - forward);
        }

        public static int Step(int from, int to, bool[] open, bool[] passable)
        {
            if (from < 0 || to < 0 || from == to) return -1;
            var forward = (to - from + Ring.Length) % Ring.Length;
            var first = forward <= Ring.Length / 2 ? 1 : -1;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var direction = attempt == 0 ? first : -first;
                if (!Clear(from, to, direction, open)) continue;
                var next = (from + direction + Ring.Length) % Ring.Length;
                return passable[next] ? next : -1;
            }
            return -1;
        }

        private static bool Clear(int from, int to, int direction, bool[] open)
        {
            var index = from;
            while (index != to)
            {
                index = (index + direction + Ring.Length) % Ring.Length;
                if (!open[index]) return false;
            }
            return true;
        }
    }
}
