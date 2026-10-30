using System;
using UnityEngine;

namespace Team5.Rebuild.Simulation
{
    public enum UnitRole { Test, Attack, Tank, Heal, Captain }
    public enum BattleObjective { DefendGoal, ReachGoal }
    // Value 0 preserves existing Scene configurations while replacing the old circle.
    public enum AttackArea { Square = 0, PositiveX = 1, NegativeX = 2, PositiveZ = 3, NegativeZ = 4 }
    public enum ProjectileTargetKind { Tower, FormationSlot }

    // Verified against old Team5 SquadRules/OffenseSoldier. No old project dependency.
    public static class SquadCombatRules
    {
        public const float Speed = 0.5f;
        public const float AttackInterval = 1;
        public const float AttackRange = 2.5f; // Actual scene override, not the old field default (2).
        public const float ProjectileSpeed = 8;
        public const float HealInterval = 2;
        public const float HealAmount = 0.4f;
        public static UnitDefinition Create(UnitRole role)
        {
            switch (role)
            {
                case UnitRole.Attack: return new UnitDefinition(4, Speed, role, 1, AttackRange, AttackInterval);
                case UnitRole.Tank: return new UnitDefinition(8, Speed, role, defense: 0.2f);
                case UnitRole.Heal: return new UnitDefinition(3, Speed, role);
                case UnitRole.Captain: return new UnitDefinition(6, Speed, role, 0.5f, AttackRange, AttackInterval, defense: 0.5f);
                default: throw new ArgumentOutOfRangeException(nameof(role));
            }
        }
        public static Vector2Int Slot(int id) => new Vector2Int(id % 3, id / 3);
        public static Vector3 Offset(int id) => new Vector3(id % 3 - 1, 0, id / 3 - 1);
        public static bool AreNeighbors(int first, int second) => first != second &&
            Math.Abs(first % 3 - second % 3) <= 1 && Math.Abs(first / 3 - second / 3) <= 1;
        public static Vector3 Direction(AttackArea area)
        {
            switch (area)
            {
                case AttackArea.PositiveX: return Vector3.right;
                case AttackArea.NegativeX: return Vector3.left;
                case AttackArea.PositiveZ: return Vector3.forward;
                case AttackArea.NegativeZ: return Vector3.back;
                default: return Vector3.zero;
            }
        }
        public static bool Contains(Vector3 offset, AttackArea area, float range, float width)
        {
            if (area == AttackArea.Square) return Mathf.Abs(offset.x) <= range && Mathf.Abs(offset.z) <= range;
            var direction = Direction(area);
            var forward = Vector3.Dot(offset, direction);
            var side = Vector3.Dot(offset, new Vector3(-direction.z, 0, direction.x));
            return forward >= 0 && forward <= range && Mathf.Abs(side) <= width / 2;
        }

        // The same XZ geometry is used by targeting, Game View indicators and Gizmos.
        public static Bounds AreaBounds(Vector3 position, AttackArea area, float range, float width)
        {
            if (area == AttackArea.Square) return new Bounds(position, new Vector3(range * 2, 0, range * 2));
            var direction = Direction(area);
            var alongX = direction.x != 0;
            return new Bounds(position + direction * (range / 2),
                new Vector3(alongX ? range : width, 0, alongX ? width : range));
        }
    }

    // Stable identity and scheduled slot are separate even before the member exists.
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
            MemberId = memberId; SlotId = slotId; Unit = unit ?? throw new ArgumentNullException(nameof(unit)); SpawnTime = spawnTime;
        }
    }
}
