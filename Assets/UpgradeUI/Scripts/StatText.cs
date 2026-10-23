using ReverseTD.Offense;

namespace ReverseTD.UpgradeUI
{
    public static class StatText
    {
        public static string Name(UnitStat stat)
        {
            switch (stat)
            {
                case UnitStat.AttackDamage:
                    return "ATK";
                case UnitStat.TankDefense:
                    return "DEF";
                case UnitStat.HealRate:
                    return "HEAL";
                default:
                    return "HP";
            }
        }

        public static string Step(UnitStat stat)
        {
            float step = SquadRules.Step(stat);
            return stat == UnitStat.TankDefense ? $"+{step * 100f:0}% DEF" : $"+{step:0.##} {Name(stat)}";
        }

        public static string Value(UnitStat stat, int[] levels)
        {
            return Format(stat, SquadRules.StatValue(stat, levels));
        }

        public static string Change(UnitStat stat, int[] levels)
        {
            float now = SquadRules.StatValue(stat, levels);
            return $"{Name(stat)} {Format(stat, now)} -> {Format(stat, now + SquadRules.Step(stat))}";
        }

        private static string Format(UnitStat stat, float value)
        {
            switch (stat)
            {
                case UnitStat.TankDefense:
                    return $"{value * 100f:0}%";
                case UnitStat.HealRate:
                    return $"{value:0.##}/s";
                default:
                    return $"{value:0.##}";
            }
        }
    }
}
