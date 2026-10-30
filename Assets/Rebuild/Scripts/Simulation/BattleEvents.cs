using UnityEngine;

namespace Team5.Rebuild.Simulation
{
    // Value snapshots: listeners can retain events without observing later state mutations.
    public readonly struct UnitSpawnedEvent
    {
        public int UnitId { get; }
        public Vector3 Position { get; }
        public float Hp { get; }
        public UnitSpawnedEvent(UnitState unit) { UnitId = unit.Id; Position = unit.Position; Hp = unit.Hp; }
    }
    public readonly struct UnitMovedEvent
    {
        public int UnitId { get; }
        public Vector3 Position { get; }
        public float RouteProgress { get; }
        public UnitMovedEvent(UnitState unit) { UnitId = unit.Id; Position = unit.Position; RouteProgress = unit.RouteProgress; }
    }
    public readonly struct DamageEvent
    {
        public int TowerId { get; }
        public int UnitId { get; }
        public float Amount { get; }
        public float RemainingHp { get; }
        public Vector3 Position { get; }
        public DamageEvent(int towerId, UnitState unit, float amount)
        { TowerId = towerId; UnitId = unit.Id; Amount = amount; RemainingHp = unit.Hp; Position = unit.Position; }
    }
    public readonly struct UnitEndedEvent
    {
        public int UnitId { get; }
        public Vector3 Position { get; }
        public UnitEndedEvent(UnitState unit) { UnitId = unit.Id; Position = unit.Position; }
    }
    public readonly struct TowerAttackEvent
    {
        public int TowerId { get; }
        public int UnitId { get; }
        public Vector3 Origin { get; }
        public Vector3 TargetPosition { get; }
        public TowerAttackEvent(TowerState tower, UnitState unit)
        { TowerId = tower.Id; UnitId = unit.Id; Origin = tower.Position; TargetPosition = unit.Position; }
    }
    public readonly struct BattleEndedEvent
    {
        public BattleOutcome Outcome { get; }
        public int Killed { get; }
        public int ReachedGoal { get; }
        public BattleEndedEvent(BattleOutcome outcome, int killed, int reachedGoal)
        { Outcome = outcome; Killed = killed; ReachedGoal = reachedGoal; }
    }
    public readonly struct TowerDamageEvent
    {
        public int MemberId { get; }
        public int TowerId { get; }
        public float Amount { get; }
        public float RemainingHp { get; }
        public TowerDamageEvent(int memberId, TowerState tower, float amount)
        { MemberId = memberId; TowerId = tower.Id; Amount = amount; RemainingHp = tower.Hp; }
    }
    public readonly struct HealEvent
    {
        public int HealerId { get; }
        public int MemberId { get; }
        public float Amount { get; }
        public float RemainingHp { get; }
        public HealEvent(int healerId, UnitState unit, float amount)
        { HealerId = healerId; MemberId = unit.Id; Amount = amount; RemainingHp = unit.Hp; }
    }
}
