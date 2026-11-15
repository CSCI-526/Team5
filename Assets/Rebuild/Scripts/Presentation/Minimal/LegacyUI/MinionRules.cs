using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public static class MinionRules
    {
        public const float StopDistance = 1.94f;
        public const float MaxTowerDistance = 3.5f;
        public const int EdgeCells = 3;
        public const int MinSpacing = 4;
        public const float AttackRange = 1.5f;
        public const float AttackInterval = 1f;

        private static readonly int[][] countsByLevel =
        {
            new int[0], new int[0], new[] { 1, 1 }, new[] { 1, 2 }, new[] { 2, 2 }, new[] { 2, 2, 2 }, new[] { 2, 2, 3 }, new[] { 3, 3, 3, 3 }
        };
        private static readonly float[] hpByLevel = { 0f, 0f, 8f, 6f, 8f, 6f, 15f, 24f };
        private static readonly float[] damageByLevel = { 0f, 0f, 0.5f, 0.7f, 0.5f, 0.8f, 1f, 2.5f };

        public static bool HasMinions(int level) => level >= 2;

        public static int CountAt(int level, int spot)
        {
            var counts = countsByLevel[Row(level)];
            return counts.Length == 0 ? 0 : counts[Mathf.Clamp(spot, 0, counts.Length - 1)];
        }

        public static float Hp(int level) => hpByLevel[Row(level)];

        public static float Damage(int level) => damageByLevel[Row(level)];

        private static int Row(int level) => Mathf.Clamp(level, 0, hpByLevel.Length - 1);
    }
}
