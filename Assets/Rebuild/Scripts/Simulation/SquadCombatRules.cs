using System;
using System.Collections.Generic;
using UnityEngine;

namespace Team5.Rebuild.Simulation
{
    public enum UnitRole { Test, Attack, Tank, Heal, Captain }
    public enum UnitRarity { Common = 0, Rare = 1, Epic = 2, Legendary = 3 }
    public enum BattleObjective { DefendGoal, ReachGoal }
    public enum AttackArea { Square = 0, PositiveX = 1, NegativeX = 2, PositiveZ = 3, NegativeZ = 4 }
    public enum ProjectileTargetKind { Tower, FormationSlot }

    public readonly struct RoleBaseStats
    {
        public readonly float MaxHp, Damage, Range, AttackInterval, Defense, HealPerSecond;
        public RoleBaseStats(float maxHp, float damage = 0f, float range = 2.5f, float interval = 1f, float defense = 0f, float heal = 0f)
        {
            MaxHp = maxHp; Damage = damage; Range = range; AttackInterval = interval; Defense = defense; HealPerSecond = heal;
        }
    }

    public static class SquadCombatRules
    {
        public const float Speed = 0.5f;
        public const float AttackInterval = 1f;
        public const float AttackRange = 2.5f;
        public const float ProjectileSpeed = 8f;
        public const float HealInterval = 2f;
        public const float HealAmount = 0.4f;
        public const float LevelStatGrowth = 0.15f; // +15% stats per level above 1

        // Modular role table: add new unit roles here
        private static readonly Dictionary<UnitRole, RoleBaseStats> RoleTable = new Dictionary<UnitRole, RoleBaseStats>
        {
            { UnitRole.Attack,  new RoleBaseStats(maxHp: 4f, damage: 1f,   range: AttackRange, interval: AttackInterval) },
            { UnitRole.Tank,    new RoleBaseStats(maxHp: 8f, defense: 0.2f) },
            { UnitRole.Heal,    new RoleBaseStats(maxHp: 3f, heal: 0.2f) },
            { UnitRole.Captain, new RoleBaseStats(maxHp: 6f, damage: 0.5f, range: AttackRange, interval: AttackInterval, defense: 0.5f) }
        };

        public static float RarityMultiplier(UnitRarity rarity) => rarity switch
        {
            UnitRarity.Common => 1.00f,
            UnitRarity.Rare => 1.25f,
            UnitRarity.Epic => 1.60f,
            UnitRarity.Legendary => 2.00f,
            _ => 1.00f
        };

        public static float ScaleMultiplier(UnitRarity rarity, int level)
        {
            var clampedLevel = Mathf.Max(1, level);
            return RarityMultiplier(rarity) * (1f + LevelStatGrowth * (clampedLevel - 1));
        }

        public static UnitDefinition Create(UnitRole role, UnitRarity rarity = UnitRarity.Common, int level = 1)
        {
            if (!RoleTable.TryGetValue(role, out var baseStats))
                throw new ArgumentOutOfRangeException(nameof(role), $"No base stats configured for role {role}.");

            var mult = ScaleMultiplier(rarity, level);
            var hp = Mathf.Round(baseStats.MaxHp * mult * 10f) / 10f;
            var dmg = Mathf.Round(baseStats.Damage * mult * 10f) / 10f;
            var heal = baseStats.HealPerSecond > 0f ? Mathf.Round(baseStats.HealPerSecond * mult * 10f) / 10f : 0.2f;
            // Defense scales gently with rarity up to a 75% cap
            var def = baseStats.Defense > 0f
                ? Mathf.Min(0.75f, baseStats.Defense + (int)rarity * 0.05f + (Mathf.Max(1, level) - 1) * 0.02f)
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
                level: Mathf.Max(1, level)
            );
        }

        public static Vector2Int Slot(int id) => new Vector2Int(id % 3, id / 3);
        public static Vector3 Offset(int id) => new Vector3(id % 3 - 1, 0, id / 3 - 1);
        public static bool AreNeighbors(int first, int second) =>
            first != second && Math.Abs(first % 3 - second % 3) <= 1 && Math.Abs(first / 3 - second / 3) <= 1;

        public static Vector3 Direction(AttackArea area) => area switch
        {
            AttackArea.PositiveX => Vector3.right,
            AttackArea.NegativeX => Vector3.left,
            AttackArea.PositiveZ => Vector3.forward,
            AttackArea.NegativeZ => Vector3.back,
            _ => Vector3.zero
        };

        public static bool Contains(Vector3 offset, AttackArea area, float range, float width)
        {
            if (area == AttackArea.Square) return Mathf.Abs(offset.x) <= range && Mathf.Abs(offset.z) <= range;
            var direction = Direction(area);
            var forward = Vector3.Dot(offset, direction);
            var side = Vector3.Dot(offset, new Vector3(-direction.z, 0, direction.x));
            return forward >= 0 && forward <= range && Mathf.Abs(side) <= width / 2;
        }

        public static Bounds AreaBounds(Vector3 position, AttackArea area, float range, float width)
        {
            if (area == AttackArea.Square) return new Bounds(position, new Vector3(range * 2, 0, range * 2));
            var direction = Direction(area);
            var alongX = direction.x != 0;
            return new Bounds(position + direction * (range / 2), new Vector3(alongX ? range : width, 0, alongX ? width : range));
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