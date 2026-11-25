using System.Collections.Generic;
using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public static class MinionRules
    {
        public const float BearerRange = 3.6f;
        public const float BearerInterval = 2f;
        public const float ShieldDamageScale = 0.3f;
        public const float LobberInterval = 1f;
        public const float BombInterval = 4f;
        public const float BombWarning = 1.8f;
        public const float BombRadius = 1f;
        public const float BombReach = 7f;
        public const float BombFirstDelay = 0.8f;
        public const float ThreatMargin = 0.5f;
        public const float ThreatBody = 0.6f;
        public const int MaxBearersPerTower = 2;
        public const float FreezeSeconds = 1.5f;
        public const int BurnTicks = 3;
        public const float FireImpactShare = 0.5f;
        public const float BurnTickShare = 0.2f;

        private static readonly MinionKind[][] kindsByLevel =
        {
            new MinionKind[0],
            new MinionKind[0],
            new[] { MinionKind.Bearer },
            new[] { MinionKind.Lobber },
            new[] { MinionKind.Bearer, MinionKind.Lobber },
            new[] { MinionKind.Bearer, MinionKind.Lobber },
            new[] { MinionKind.Bearer, MinionKind.Lobber, MinionKind.Bearer },
            new[] { MinionKind.Bearer, MinionKind.Lobber, MinionKind.Bearer, MinionKind.Lobber }
        };
        private static readonly float[] bearerHp = { 0f, 0f, 6f, 7f, 8f, 8f, 9f, 10f };
        private static readonly float[] bearerDamage = { 0f, 0f, 0.35f, 0.40f, 0.46f, 0.50f, 0.55f, 0.60f };
        private static readonly float[] lobberHp = { 0f, 0f, 5f, 5f, 6f, 6f, 7f, 8f };
        private static readonly float[] bombDamage = { 0f, 0f, 0.5f, 0.70f, 0.85f, 0.95f, 1.05f, 1.15f };
        private static readonly MinionElement[][] lobberElementsByLevel =
        {
            new MinionElement[0],
            new MinionElement[0],
            new MinionElement[0],
            new[] { MinionElement.Fire },
            new[] { MinionElement.Ice },
            new[] { MinionElement.Ice },
            new[] { MinionElement.Fire },
            new[] { MinionElement.Fire, MinionElement.Ice }
        };

        public static bool HasMinions(int level) => kindsByLevel[Row(level)].Length > 0;

        public static IReadOnlyList<MinionKind> Kinds(int level) => kindsByLevel[Row(level)];

        public static int Count(MinionKind kind, int level) => kind == MinionKind.Bearer && level >= 6 ? MaxBearersPerTower : 1;

        public static float Hp(MinionKind kind, int level) => kind == MinionKind.Bearer ? bearerHp[Row(level)] : lobberHp[Row(level)];

        public static float BearerDamage(int level) => bearerDamage[Row(level)];

        public static float BombDamage(int level) => bombDamage[Row(level)];

        public static MinionElement Element(int level, int lobberOrdinal)
        {
            var row = lobberElementsByLevel[Row(level)];
            return row.Length == 0 ? MinionElement.None : row[Mathf.Abs(lobberOrdinal) % row.Length];
        }

        public static float ImpactDamage(MinionElement element, float bomb) => element == MinionElement.Fire ? bomb * FireImpactShare : bomb;

        public static float BurnTickDamage(MinionElement element, float bomb) => element == MinionElement.Fire ? bomb * BurnTickShare : 0f;

        public static int BurnTickCount(MinionElement element) => element == MinionElement.Fire ? BurnTicks : 0;

        public static float FreezeTime(MinionElement element) => element == MinionElement.Ice ? FreezeSeconds : 0f;

        private static int Row(int level) => Mathf.Clamp(level, 0, kindsByLevel.Length - 1);
    }
}
