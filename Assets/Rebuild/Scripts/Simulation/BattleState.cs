using UnityEngine;

namespace Team5.Rebuild.Simulation
{
    public sealed class UnitState
    {
        internal UnitState Copy() => (UnitState)MemberwiseClone();
        public int Id { get; }
        public int MemberId => Id;
        public UnitDefinition Definition { get; }
        public UnitRole Role => Definition.Role;
        public int SlotId => FormationSlot.y * 3 + FormationSlot.x;
        public float AttackCooldown { get; internal set; }
        public float MaxHp { get; }
        public float Hp { get; internal set; }
        public float Speed { get; }
        public int SquadId { get; }
        public Vector2Int FormationSlot { get; internal set; }
        public Vector3 FormationOffset { get; internal set; }
        public Vector3 AnchorPosition => Position - FormationOffset;
        // Half-open cell boundaries avoid banker's rounding collisions at exact half-cell steps.
        public Vector3Int OccupiedCell => SmallCellOccupancy.PositionToCell(Position);
        public Vector3 Position { get; internal set; }
        public float RouteProgress { get; internal set; }
        public int NextWaypoint { get; internal set; } = 1;
        public UnitLifeState LifeState { get; internal set; } = UnitLifeState.Alive;
        public bool IsAlive => LifeState == UnitLifeState.Alive;
        internal UnitState(int id, UnitDefinition definition, Vector3 position, int squadId = 1,
            Vector2Int formationSlot = default, Vector3 formationOffset = default)
        {
            Id = id;
            Definition = definition;
            MaxHp = Hp = definition.MaxHp;
            Speed = definition.Speed;
            Position = position;
            SquadId = squadId;
            FormationSlot = formationSlot;
            FormationOffset = formationOffset;
        }
    }

    public sealed class TowerState
    {
        internal TowerState Copy() => (TowerState)MemberwiseClone();
        public TowerDefinition Definition { get; }
        public int Id => Definition.Id;
        public Vector3 Position { get; internal set; }
        public float CooldownRemaining { get; internal set; }
        public int? TargetId { get; internal set; }
        public float Hp { get; internal set; }
        public bool IsAlive => Hp > 0;
        internal TowerState(TowerDefinition definition)
        {
            Definition = definition;
            Position = definition.Position;
            Hp = definition.MaxHp;
        }
    }

    public sealed class ProjectileState
    {
        internal ProjectileState Copy() => (ProjectileState)MemberwiseClone();
        public int Id { get; internal set; }
        public int SourceId { get; internal set; }
        public ProjectileTargetKind TargetKind { get; internal set; }
        public int TargetId { get; internal set; }
        public int SquadId { get; internal set; } = 1;
        public Vector3 Position { get; internal set; }
        public bool IsActive { get; internal set; } = true;
        internal float Damage, Age, Travel, Range, Width;
        internal Vector3 Origin, Direction;
        internal bool Straight;
    }
}
