using System;
using System.Collections.Generic;
using UnityEngine;

namespace Team5.Rebuild.Simulation
{
    public enum UnitRole { Test, Attack, Tank, Heal, Captain, Sniper, Vanguard, Demolisher, Frostweaver, Warcaller, Aegis }
    public enum UnitRarity { Common = 0, Rare = 1, Epic = 2, Legendary = 3 }
    public enum BattleObjective { DefendGoal, ReachGoal }
    // Keep numeric values 0..4 stable for existing serialized Unity scenes.
    // 1..4 are precision beam / laser strips; normal towers use Circle or Cone variants.
    public enum AttackArea { Square = 0, PositiveX = 1, NegativeX = 2, PositiveZ = 3, NegativeZ = 4,
        Circle = 5, ConePositiveX = 6, ConeNegativeX = 7, ConePositiveZ = 8, ConeNegativeZ = 9 }
    public enum ProjectileTargetKind { Tower, FormationSlot }

    public readonly struct RoleBaseStats
    {
        public readonly float MaxHp, Damage, Range, AttackInterval, Defense, HealPerSecond, TowerDamageMultiplier;
        public RoleBaseStats(
            float maxHp,
            float damage = 0f,
            float range = 2.5f,
            float interval = 1f,
            float defense = 0f,
            float heal = 0f,
            float towerDamageMultiplier = 1f)
        {
            MaxHp = maxHp;
            Damage = damage;
            Range = range;
            AttackInterval = interval;
            Defense = defense;
            HealPerSecond = heal;
            TowerDamageMultiplier = towerDamageMultiplier;
        }
    }

    public static class SquadCombatRules
    {
        public const float Speed = 0.75f; // Faster traversal keeps rebuilt 20-35 tile stages below two minutes.
        public const float AttackInterval = 1f;
        public const float AttackRange = 2.5f;
        public const float ProjectileSpeed = 8f;
        public const float HealInterval = 2f;
        public const float HealAmount = 0.4f;
        public const float LevelStatGrowth = 0.15f;

        // Modular role table including new offensive/hybrid units and tower-breaker multipliers
        private static readonly Dictionary<UnitRole, RoleBaseStats> RoleTable = new Dictionary<UnitRole, RoleBaseStats>
        {
            { UnitRole.Attack,     new RoleBaseStats(maxHp: 4.5f, damage: 1.4f, range: 2.8f, interval: 0.9f, towerDamageMultiplier: 1.5f) },
            { UnitRole.Tank,       new RoleBaseStats(maxHp: 11.0f, defense: 0.20f) },
            { UnitRole.Heal,       new RoleBaseStats(maxHp: 3.5f, heal: 0.35f) },
            { UnitRole.Captain,    new RoleBaseStats(maxHp: 11.0f, defense: 0.10f, range: 2.5f, interval: 1.05f) },
            { UnitRole.Sniper,     new RoleBaseStats(maxHp: 3.2f, damage: 1.6f, range: 4.5f, interval: 2.5f, towerDamageMultiplier: 1.85f) },
            { UnitRole.Vanguard,   new RoleBaseStats(maxHp: 7.0f, damage: 1.1f, range: 2.5f, interval: 1.05f, defense: 0.05f, towerDamageMultiplier: 1.35f) },
            { UnitRole.Demolisher, new RoleBaseStats(maxHp: 5.0f, damage: 1.8f, range: 2.5f, interval: 1.15f, defense: 0.10f, towerDamageMultiplier: 2.25f) },
            { UnitRole.Frostweaver, new RoleBaseStats(maxHp: 9.0f, defense: 0.10f) },
            { UnitRole.Warcaller,   new RoleBaseStats(maxHp: 10.0f, defense: 0.10f) },
            { UnitRole.Aegis,       new RoleBaseStats(maxHp: 11.0f, defense: 0.20f) }
        };

        // Shared summon rarity table. Keep this in the dependency-free simulation layer
        // so both the campaign shop and Captain reinforcements use identical odds.
        // The Captain excludes unique legendary support roles from its own pool.
        public static (int common, int rare, int epic, int legendary) GetRarityWeightsForTier(int tier) => tier switch
        {
            <= 1 => (75, 19, 3, 3),
            2 => (66, 26, 4, 4),
            3 => (58, 30, 7, 5),
            4 => (44, 39, 10, 7),
            5 => (35, 40, 15, 10),
            6 => (25, 42, 21, 12),
            _ => (15, 42, 28, 15)
        };

        // Deployment capacity stays six cards; battlefield unit output depends on role.
        // The first unit uses the deployed slot; extra units fill available slots.
        public static int StartingSummonCount(UnitRole role) => role switch
        {
            UnitRole.Attack => 2,
            UnitRole.Vanguard => 2,
            UnitRole.Sniper => 1,
            UnitRole.Tank => 1,
            UnitRole.Heal => 1,
            UnitRole.Demolisher => 1,
            _ => 1
        };

        public static bool IsLegendaryRole(UnitRole role) => role == UnitRole.Captain ||
            role == UnitRole.Frostweaver || role == UnitRole.Warcaller || role == UnitRole.Aegis;

        public static bool CanAttackTowers(UnitRole role) =>
            role == UnitRole.Attack ||
            role == UnitRole.Sniper || role == UnitRole.Vanguard || role == UnitRole.Demolisher;

        // Legendary value is in its unique active skill, not a huge passive combat stat multiplier.
        public static float RarityMultiplier(UnitRarity rarity) => rarity switch
        {
            UnitRarity.Common => 1.00f,
            UnitRarity.Rare => 1.85f,
            UnitRarity.Epic => 3.00f,
            UnitRarity.Legendary => 1.50f,
            _ => 1.00f
        };

        // Per-level stat growth also scales steeper with rarity
        public static float RarityLevelGrowth(UnitRarity rarity) => rarity switch
        {
            UnitRarity.Common => 0.12f,
            UnitRarity.Rare => 0.20f,
            UnitRarity.Epic => 0.30f,
            UnitRarity.Legendary => 0.18f,
            _ => LevelStatGrowth
        };

        public static float ScaleMultiplier(UnitRarity rarity, int level)
        {
            var clampedLevel = Mathf.Max(1, level);
            return RarityMultiplier(rarity) * (1f + RarityLevelGrowth(rarity) * (clampedLevel - 1));
        }

        public static UnitDefinition Create(UnitRole role, UnitRarity rarity = UnitRarity.Common, int level = 1)
        {
            if (!RoleTable.TryGetValue(role, out var baseStats))
                throw new ArgumentOutOfRangeException(nameof(role), $"No base stats configured for role {role}.");

            if (IsLegendaryRole(role)) rarity = UnitRarity.Legendary;
            else if (rarity == UnitRarity.Legendary)
                throw new ArgumentException("Legendary rarity is reserved for the four unique support characters.", nameof(rarity));

            var mult = ScaleMultiplier(rarity, level);
            var hp = Mathf.Round(baseStats.MaxHp * mult * 10f) / 10f;
            var dmg = Mathf.Round(baseStats.Damage * mult * 10f) / 10f;
            var heal = baseStats.HealPerSecond > 0f ? Mathf.Round(baseStats.HealPerSecond * mult * 10f) / 10f : 0.2f;
            var def = baseStats.Defense > 0f
                ? Mathf.Min(
                    rarity == UnitRarity.Legendary ? 0.35f : 0.78f,

                    baseStats.Defense +

                    (rarity == UnitRarity.Legendary
                        ? 0.03f
                        : (int)rarity * 0.06f) +

                    (Mathf.Max(1, level) - 1) *
                    (rarity == UnitRarity.Legendary
                        ? 0.01f
                        : 0.025f)
                )
                : 0f;

            return new UnitDefinition(
                maxHp: hp,
                speed: Speed,
                role: role,
                damage: dmg,
                range: baseStats.Range,
                attackInterval: baseStats.AttackInterval,
                defense: def,
                healPerSecond: heal,
                rarity: rarity,
                level: Mathf.Max(1, level),
                towerDamageMultiplier: baseStats.TowerDamageMultiplier,
                damageType: role == UnitRole.Demolisher ? AttackDamageType.Explosive : AttackDamageType.Normal
            );
        }

        public static Vector2Int Slot(int id) => new Vector2Int(id % 3, id / 3);
        public static Vector3 Offset(int id) => new Vector3(id % 3 - 1, 0, id / 3 - 1);
        public static bool AreNeighbors(int first, int second) =>
            first != second && Math.Abs(first % 3 - second % 3) <= 1 && Math.Abs(first / 3 - second / 3) <= 1;

        public const float ConeHalfAngleDegrees = 38f;
        public static bool IsCircle(AttackArea area) => area == AttackArea.Circle;
        public static bool IsCone(AttackArea area) => area >= AttackArea.ConePositiveX && area <= AttackArea.ConeNegativeZ;
        public static bool IsLaser(AttackArea area) => area >= AttackArea.PositiveX && area <= AttackArea.NegativeZ;

        public static Vector3 Direction(AttackArea area) => area switch
        {
            AttackArea.PositiveX or AttackArea.ConePositiveX => Vector3.right,
            AttackArea.NegativeX or AttackArea.ConeNegativeX => Vector3.left,
            AttackArea.PositiveZ or AttackArea.ConePositiveZ => Vector3.forward,
            AttackArea.NegativeZ or AttackArea.ConeNegativeZ => Vector3.back,
            _ => Vector3.zero
        };

        // Gameplay hit-testing and range visuals use the same geometry.
        public static bool Contains(Vector3 offset, AttackArea area, float range, float width)
        {
            var sqr = offset.x * offset.x + offset.z * offset.z;
            if (IsCircle(area)) return sqr <= range * range + .00001f;
            if (area == AttackArea.Square) return Mathf.Abs(offset.x) <= range && Mathf.Abs(offset.z) <= range;
            var direction = Direction(area);
            var forward = Vector3.Dot(offset, direction);
            if (forward < 0 || forward > range) return false;
            var side = Mathf.Abs(Vector3.Dot(offset, new Vector3(-direction.z, 0, direction.x)));
            if (IsCone(area))
                return sqr <= range * range + .00001f && side <= forward * Mathf.Tan(ConeHalfAngleDegrees * Mathf.Deg2Rad) + .00001f;
            return side <= width * .5f; // precision laser beam, never the default tower
        }

        public static Bounds AreaBounds(Vector3 position, AttackArea area, float range, float width)
        {
            if (IsCircle(area) || area == AttackArea.Square)
                return new Bounds(position, new Vector3(range * 2f, 0, range * 2f));
            var direction = Direction(area);
            var alongX = direction.x != 0;
            var lateral = IsCone(area) ? range * Mathf.Sin(ConeHalfAngleDegrees * Mathf.Deg2Rad) * 2f : width;
            return new Bounds(position + direction * (range * .5f),
                new Vector3(alongX ? range : lateral, 0, alongX ? lateral : range));
        }

    }

    public sealed class MemberSpawnDefinition
    {
        public int MemberId { get; }
        public int SlotId { get; }
        public float SpawnTime { get; }
        public UnitDefinition Unit { get; }

        public MemberSpawnDefinition(int memberId, int slotId, UnitDefinition unit, float spawnTime = 0)
        {
            if (memberId <= 0 || slotId < 0 || slotId > 8 || !BattleNumbers.Finite(spawnTime) || spawnTime < 0)
                throw new ArgumentOutOfRangeException(nameof(memberId));
            MemberId = memberId;
            SlotId = slotId;
            Unit = unit ?? throw new ArgumentNullException(nameof(unit));
            SpawnTime = spawnTime;
        }
    }
}
