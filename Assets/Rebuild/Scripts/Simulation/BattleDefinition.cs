using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Team5.Rebuild.Simulation
{
    public enum TargetRule { FurthestAlongRoute, Nearest }
    public enum UnitLifeState { Alive, Dead, ReachedGoal }
    public enum BattleOutcome { Running, Victory, Defeat }

    internal static class BattleNumbers
    {
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }

    public sealed class UnitDefinition
    {
        public float MaxHp { get; }
        public float Speed { get; }
        public UnitRole Role { get; }
        public UnitRarity Rarity { get; }
        public int Level { get; }
        public float Damage { get; }
        public float Range { get; }
        public float AttackInterval { get; }
        public float Defense { get; }
        public float HealPerSecond { get; }
        public float TowerDamageMultiplier { get; }

        public UnitDefinition(
            float maxHp,
            float speed,
            UnitRole role = UnitRole.Test,
            float damage = 0,
            float range = 2.5f,
            float attackInterval = 1,
            float defense = 0,
            float healPerSecond = 0.2f,
            UnitRarity rarity = UnitRarity.Common,
            int level = 1,
            float towerDamageMultiplier = 1f)
        {
            if (!BattleNumbers.Finite(maxHp) || maxHp <= 0 || !BattleNumbers.Finite(speed) || speed <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxHp), "HP and speed must be finite and positive.");
            if (!Enum.IsDefined(typeof(UnitRole), role) || !BattleNumbers.Finite(damage) || damage < 0 ||
                !BattleNumbers.Finite(range) || range < 0 || !BattleNumbers.Finite(attackInterval) || attackInterval <= 0 ||
                !BattleNumbers.Finite(defense) || defense < 0 || defense >= 1)
                throw new ArgumentOutOfRangeException(nameof(role));
            if (!BattleNumbers.Finite(healPerSecond) || healPerSecond < 0)
                throw new ArgumentOutOfRangeException(nameof(healPerSecond));
            if (!BattleNumbers.Finite(towerDamageMultiplier) || towerDamageMultiplier <= 0)
                throw new ArgumentOutOfRangeException(nameof(towerDamageMultiplier));

            MaxHp = maxHp;
            Speed = speed;
            Role = role;
            Rarity = rarity;
            Level = Math.Max(1, level);
            Damage = damage;
            Range = range;
            AttackInterval = attackInterval;
            Defense = defense;
            HealPerSecond = healPerSecond;
            TowerDamageMultiplier = towerDamageMultiplier;
        }
    }

    public sealed class TowerDefinition
    {
        public int Id { get; }
        public Vector3 Position { get; }
        public float Range { get; }
        public float Damage { get; }
        public float AttackInterval { get; }
        public TargetRule TargetRule { get; }
        public int FootprintSize { get; }
        public bool IsWall { get; }
        public Vector2Int Footprint { get; }
        public float MaxHp { get; }
        public AttackArea Area { get; }
        public float AttackWidth { get; }
        public bool IsAoe { get; }
        public int GoldBounty { get; }

        public TowerDefinition(int id, Vector3 position, float range, float damage, float attackInterval, TargetRule targetRule,
            int footprintSize = 1, float maxHp = 20, AttackArea area = AttackArea.Square, float attackWidth = 0.3f,
            bool isAoe = false, int goldBounty = 20, bool isWall = false, bool wallAlongZ = false)
        {
            if (!BattleNumbers.Finite(position) || !BattleNumbers.Finite(range) || range < 0 ||
                !BattleNumbers.Finite(damage) || (isWall ? damage < 0 : damage <= 0) || !BattleNumbers.Finite(attackInterval) || attackInterval <= 0)
                throw new ArgumentOutOfRangeException(nameof(range), "Invalid tower configuration.");
            if (!Enum.IsDefined(typeof(TargetRule), targetRule)) throw new ArgumentOutOfRangeException(nameof(targetRule));
            if (footprintSize != 1 && footprintSize != 3) throw new ArgumentOutOfRangeException(nameof(footprintSize));
            if (!BattleNumbers.Finite(maxHp) || maxHp <= 0 || !BattleNumbers.Finite(attackWidth) || attackWidth <= 0 ||
                !Enum.IsDefined(typeof(AttackArea), area)) throw new ArgumentOutOfRangeException(nameof(maxHp));

            Id = id;
            Position = position;
            Range = isWall ? 0 : range;
            Damage = isWall ? 0 : damage;
            AttackInterval = attackInterval;
            TargetRule = targetRule;
            FootprintSize = footprintSize;
            MaxHp = maxHp;
            Area = area;
            AttackWidth = attackWidth;
            IsAoe = !isWall && isAoe;
            IsWall = isWall;
            Footprint = isWall ? (wallAlongZ ? new Vector2Int(1, 3) : new Vector2Int(3, 1)) : new Vector2Int(footprintSize, footprintSize);
            GoldBounty = isWall ? 0 : Math.Max(0, goldBounty);
        }
    }

    public sealed class BattleDefinition
    {
        public ReadOnlyCollection<Vector3> Waypoints { get; }
        public ReadOnlyCollection<TowerDefinition> Towers { get; }
        public UnitDefinition Unit { get; }
        public int UnitCount { get; }
        public float SpawnInterval { get; }
        public int GoalLives { get; }
        public int FormationSize { get; }
        public int SquadCapacity => FormationSize * FormationSize;
        public BattleObjective Objective { get; private set; }
        public ReadOnlyCollection<MemberSpawnDefinition> Members { get; private set; }
        public ReadOnlyCollection<Vector3Int> WalkableCells { get; private set; }

        public BattleDefinition(IEnumerable<Vector3> waypoints, IEnumerable<TowerDefinition> towers,
            IEnumerable<MemberSpawnDefinition> members, IEnumerable<Vector3Int> walkableCells = null)
            : this(waypoints, towers, FirstUnit(members, out var captured), captured.Count, 1, 1, 3)
        {
            Objective = BattleObjective.ReachGoal;
            var ids = new HashSet<int>();
            var slots = new HashSet<int>();
            foreach (var member in captured)
                if (member == null || !ids.Add(member.MemberId) || !slots.Add(member.SlotId) || Mathf.Abs(member.Unit.Speed - Unit.Speed) > 0.00001f)
                    throw new ArgumentException("Members require unique IDs/slots and a shared formation speed.");
            Members = captured.AsReadOnly();
            WalkableCells = walkableCells == null ? null : new List<Vector3Int>(walkableCells).AsReadOnly();
        }

        private static UnitDefinition FirstUnit(IEnumerable<MemberSpawnDefinition> members, out List<MemberSpawnDefinition> captured)
        {
            captured = members == null ? throw new ArgumentNullException(nameof(members)) : new List<MemberSpawnDefinition>(members);
            if (captured.Count < 1 || captured.Count > 9 || captured[0] == null)
                throw new ArgumentException("A squad needs 1..9 planned members.");
            return captured[0].Unit;
        }

        public BattleDefinition(IEnumerable<Vector3> waypoints, IEnumerable<TowerDefinition> towers,
            UnitDefinition unit, int unitCount, float spawnInterval, int goalLives, int formationSize = 1)
        {
            if (waypoints == null || towers == null || unit == null) throw new ArgumentNullException();
            var points = new List<Vector3>(waypoints);
            if (points.Count < 2) throw new ArgumentException("A route needs at least two waypoints.");
            for (var i = 0; i < points.Count; i++)
            {
                if (!BattleNumbers.Finite(points[i])) throw new ArgumentException("Invalid waypoint.");
                if (i > 0 && Vector3.Distance(points[i - 1], points[i]) < 0.0001f)
                    throw new ArgumentException("Consecutive waypoints must differ.");
            }
            if (unitCount <= 0 || goalLives <= 0 || !BattleNumbers.Finite(spawnInterval) || spawnInterval <= 0)
                throw new ArgumentOutOfRangeException(nameof(unitCount));
            if (formationSize != 1 && formationSize != 3) throw new ArgumentOutOfRangeException(nameof(formationSize));
            if (formationSize == 3 && unitCount > 9)
                throw new ArgumentException("This prototype supports one squad of up to nine independent members.");
            var towerList = new List<TowerDefinition>(towers);
            var ids = new HashSet<int>();
            foreach (var tower in towerList)
                if (tower == null || !ids.Add(tower.Id)) throw new ArgumentException("Tower IDs must be unique.");

            Waypoints = points.AsReadOnly();
            Towers = towerList.AsReadOnly();
            Unit = unit;
            UnitCount = unitCount;
            SpawnInterval = spawnInterval;
            GoalLives = goalLives;
            FormationSize = formationSize;
        }
    }
}