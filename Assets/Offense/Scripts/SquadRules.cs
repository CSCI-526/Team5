using System.Collections.Generic;

namespace ReverseTD.Offense
{
    public static class SquadRules
    {
        public const int Rows = 3;
        public const int Columns = 3;
        public const int CellCount = Rows * Columns;
        public const int CenterCell = 4;
        public const int StatCount = 6;
        public const float Spacing = 0.36f;
        public const float UnitSize = 0.3f;
        public const float CornerRadius = 0.5f;
        public const float Speed = 1f;
        public const float HealInterval = 1f;
        public const float HealReach = 1f;
        public const double CardCost = 10;
        public const double GoldBonusPerCard = 0.5;

        public const float AttackBaseHealth = 4f;
        public const float AttackBaseDamage = 1f;
        public const float TankBaseHealth = 10f;
        public const float TankBaseDefense = 0.3f;
        public const float HealBaseHealth = 3f;
        public const float HealBaseRate = 0.5f;

        private static readonly float[] steps = { 1f, 2f, 0.1f, 4f, 0.25f, 2f };
        private static readonly double[] baseCosts = { 8, 6, 8, 6, 8, 6 };
        private static readonly double[] costMultipliers = { 1.8, 1.6, 1.6, 1.6, 1.6, 1.6 };
        private static readonly int[] maxLevels = { 5, 5, 4, 5, 5, 5 };
        private static readonly double[] quotas = { 8, 20, 40, 65, 95, 130, 170, 215, 265 };
        private static readonly AugmentCard[] unitCards = { AugmentCard.AddAttack, AugmentCard.AddTank, AugmentCard.AddHeal };

        public static int QuotaCount => quotas.Length;

        public static double Quota(int index)
        {
            return quotas[index];
        }

        public static float Step(UnitStat stat)
        {
            return steps[(int)stat];
        }

        public static int MaxLevel(UnitStat stat)
        {
            return maxLevels[(int)stat];
        }

        public static double UpgradeCost(UnitStat stat, int level)
        {
            return System.Math.Round(baseCosts[(int)stat] * System.Math.Pow(costMultipliers[(int)stat], level));
        }

        public static UnitType OwnerOf(UnitStat stat)
        {
            switch (stat)
            {
                case UnitStat.TankDefense:
                case UnitStat.TankHealth:
                    return UnitType.Tank;
                case UnitStat.HealRate:
                case UnitStat.HealHealth:
                    return UnitType.Heal;
                default:
                    return UnitType.Attack;
            }
        }

        public static float MaxHealth(UnitType type, int[] levels)
        {
            switch (type)
            {
                case UnitType.Tank:
                    return TankBaseHealth + Bonus(UnitStat.TankHealth, levels);
                case UnitType.Heal:
                    return HealBaseHealth + Bonus(UnitStat.HealHealth, levels);
                default:
                    return AttackBaseHealth + Bonus(UnitStat.AttackHealth, levels);
            }
        }

        public static float AttackDamage(int[] levels)
        {
            return AttackBaseDamage + Bonus(UnitStat.AttackDamage, levels);
        }

        public static float Defense(UnitType type, int[] levels)
        {
            return type == UnitType.Tank ? TankBaseDefense + Bonus(UnitStat.TankDefense, levels) : 0f;
        }

        public static float HealRate(int[] levels)
        {
            return HealBaseRate + Bonus(UnitStat.HealRate, levels);
        }

        public static float StatValue(UnitStat stat, int[] levels)
        {
            switch (stat)
            {
                case UnitStat.AttackDamage:
                    return AttackDamage(levels);
                case UnitStat.TankDefense:
                    return Defense(UnitType.Tank, levels);
                case UnitStat.HealRate:
                    return HealRate(levels);
                default:
                    return MaxHealth(OwnerOf(stat), levels);
            }
        }

        public static UnitType UnitOf(AugmentCard card)
        {
            switch (card)
            {
                case AugmentCard.AddAttack:
                    return UnitType.Attack;
                case AugmentCard.AddTank:
                    return UnitType.Tank;
                case AugmentCard.AddHeal:
                    return UnitType.Heal;
                default:
                    return UnitType.None;
            }
        }

        public static void RollOffer(int cardsTaken, bool squadFull, List<AugmentCard> offer)
        {
            offer.Clear();
            if (!squadFull)
            {
                if (cardsTaken == 0)
                {
                    offer.Add(AugmentCard.AddTank);
                    offer.Add(AugmentCard.AddHeal);
                }
                else
                {
                    int skipped = UnityEngine.Random.Range(0, unitCards.Length);
                    for (int i = 0; i < unitCards.Length; i++)
                    {
                        if (i != skipped)
                        {
                            offer.Add(unitCards[i]);
                        }
                    }
                }
            }
            offer.Add(AugmentCard.GoldBonus);
        }

        private static float Bonus(UnitStat stat, int[] levels)
        {
            int index = (int)stat;
            int level = levels != null && index < levels.Length ? levels[index] : 0;
            return steps[index] * level;
        }
    }
}
