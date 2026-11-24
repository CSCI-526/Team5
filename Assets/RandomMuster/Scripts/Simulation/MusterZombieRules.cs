using System;

namespace Team5.BasicTowerTrial.Simulation
{
    public static class MusterZombieRules
    {
        public const float HealRadius = 3.392857f;
        public const float HealCooldown = 8f;
        public const float HealReleaseTime = .25f;
        public const float HealAnimationTime = .62f;
        public const int HealTargetLimit = 3;
        public const int UpgradedHealTargetLimit = 5;

        public static int Tier(UnitRole role) => role switch
        {
            UnitRole.Zombie or UnitRole.BruteZombie => 1,
            UnitRole.ShamanZombie or UnitRole.TankZombie => 2,
            _ => int.MaxValue
        };

        public static bool IsZombie(UnitRole role) => Tier(role) != int.MaxValue;

        public static int GoalLifeCost(UnitRole role) => role == UnitRole.TankZombie ? 3 : 1;

        public static UnitDefinition Create(UnitRole role, bool golden = false)
        {
            var (hp, speed, min, max, interval, armor, magic) = role switch
            {
                UnitRole.Zombie => (20, 1.2f, 1, 4, 1f, 0f, 0f),
                UnitRole.BruteZombie => (80, .8f, 4, 8, 1f, .3f, 0f),
                UnitRole.ShamanZombie => (100, 1f, 3, 5, 1f, 0f, .85f),
                UnitRole.TankZombie => (800, .7f, 40, 60, 1f, 0f, 0f),
                _ => throw new ArgumentOutOfRangeException(nameof(role))
            };
            var scale = golden ? 2 : 1;
            return new UnitDefinition(hp * scale, speed, role, min * scale, 1.1f, interval, armor,
                0f, level: scale, magicResistance: magic, maximumDamage: max * scale);
        }
    }
}
