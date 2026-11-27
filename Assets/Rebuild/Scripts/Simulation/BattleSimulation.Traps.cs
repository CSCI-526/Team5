using System;
using System.Collections.Generic;
using UnityEngine;

namespace Team5.Rebuild.Simulation
{
    public enum TacticalTrapKind { Freeze, Meteor, Boulder }
    public enum TacticalTrapPhase { Ready, Warning, Active, Recovering }

    // Scene implementations live in Presentation; Session depends only on this contract.
    public interface ITacticalTrapSite
    {
        bool TryCaptureTrap(out TacticalTrapDefinition trap);
    }

    public sealed class TacticalTrapDefinition
    {
        public readonly int Id;
        public readonly TacticalTrapKind Kind;
        public readonly Vector3 Center, Direction;
        public readonly float Radius, Damage, Recovery;
        public const float BoulderHalfLength = 2.2f;
        public float WarningSeconds => Kind == TacticalTrapKind.Boulder ? 3.2f : Kind == TacticalTrapKind.Meteor ? 2.5f : 1.65f;
        public float ActiveSeconds => Kind == TacticalTrapKind.Boulder ? 1.65f : Kind == TacticalTrapKind.Meteor ? 4.5f : .45f;
        public float TriggerRadius => Kind == TacticalTrapKind.Boulder ? 2.15f : Radius + .55f;
        public Vector3 BoulderStart => Center - Direction * BoulderHalfLength;
        public Vector3 BoulderEnd => Center + Direction * BoulderHalfLength;
        public TacticalTrapDefinition(int id, TacticalTrapKind kind, Vector3 center, float radius, float damage, float recovery, Vector3 direction)
        {
            direction.y = 0;
            if (id >= -10000 || !Enum.IsDefined(typeof(TacticalTrapKind), kind) || !BattleNumbers.Finite(center) ||
                !BattleNumbers.Finite(radius) || radius <= 0 || !BattleNumbers.Finite(damage) || damage <= 0 ||
                !BattleNumbers.Finite(recovery) || recovery <= 0 || !BattleNumbers.Finite(direction) || direction.sqrMagnitude < .01f)
                throw new ArgumentException("Invalid tactical trap definition.");
            Id = id; Kind = kind; Center = center; Radius = radius; Damage = damage; Recovery = recovery; Direction = direction.normalized;
        }
    }

    public sealed class TacticalTrapState
    {
        public TacticalTrapDefinition Definition { get; }
        public TacticalTrapPhase Phase { get; internal set; }
        public float Elapsed { get; internal set; }
        public float Progress => Mathf.Clamp01(Elapsed / (Phase == TacticalTrapPhase.Warning ? Definition.WarningSeconds :
            Phase == TacticalTrapPhase.Active ? Definition.ActiveSeconds : Phase == TacticalTrapPhase.Recovering ? Definition.Recovery : 1f));
        public Vector3 BoulderPosition => Vector3.Lerp(Definition.BoulderStart, Definition.BoulderEnd, Progress);
        internal float NextBurn = .75f;
        internal readonly HashSet<int> Hit = new HashSet<int>();
        internal TacticalTrapState(TacticalTrapDefinition definition) { Definition = definition; }
        internal TacticalTrapState Copy()
        {
            var copy = new TacticalTrapState(Definition) { Phase = Phase, Elapsed = Elapsed, NextBurn = NextBurn };
            copy.Hit.UnionWith(Hit); return copy;
        }
    }

    public sealed partial class BattleSimulation
    {
        private readonly List<TacticalTrapState> tacticalTraps = new List<TacticalTrapState>();
        public IReadOnlyList<TacticalTrapState> TacticalTraps { get; }
        public void AddTacticalTrap(TacticalTrapDefinition definition)
        {
            if (definition == null || stepping || SpawnedCount != 0 || tacticalTraps.Exists(t => t.Definition.Id == definition.Id))
                throw new InvalidOperationException("Register unique tactical traps before the battle starts.");
            tacticalTraps.Add(new TacticalTrapState(definition));
        }

        private void TickTacticalTraps(float dt)
        {
            foreach (var trap in tacticalTraps)
            {
                var d = trap.Definition;
                if (trap.Phase == TacticalTrapPhase.Ready)
                {
                    if (units.Exists(u => u.IsAlive && HorizontalDistanceSquared(u.Position, d.Center) <= d.TriggerRadius * d.TriggerRadius))
                    { trap.Phase = TacticalTrapPhase.Warning; trap.Elapsed = 0; }
                    // Always give a complete warning, even when a frame is unusually long.
                    continue;
                }
                var remaining = dt;
                while (remaining > .000001f && trap.Phase != TacticalTrapPhase.Ready)
                {
                    var duration = trap.Phase == TacticalTrapPhase.Warning ? d.WarningSeconds :
                        trap.Phase == TacticalTrapPhase.Active ? d.ActiveSeconds : d.Recovery;
                    var advance = Mathf.Min(remaining, Mathf.Max(0, duration - trap.Elapsed));
                    var old = trap.Elapsed;
                    trap.Elapsed += advance; remaining -= advance;
                    if (trap.Phase == TacticalTrapPhase.Active)
                    {
                        if (d.Kind == TacticalTrapKind.Boulder)
                        {
                            var from = Vector3.Lerp(d.BoulderStart, d.BoulderEnd, old / duration);
                            HitBoulder(trap, from, trap.BoulderPosition);
                        }
                        else if (d.Kind == TacticalTrapKind.Meteor)
                        {
                            while (trap.NextBurn <= trap.Elapsed + .00001f && trap.NextBurn <= duration)
                            { HitTrapCircle(d, false, true); trap.NextBurn += .75f; }
                        }
                    }
                    if (trap.Elapsed + .00001f < duration) break;
                    trap.Elapsed = 0;
                    if (trap.Phase == TacticalTrapPhase.Warning)
                    {
                        trap.Phase = TacticalTrapPhase.Active; trap.NextBurn = .75f; trap.Hit.Clear();
                        if (d.Kind == TacticalTrapKind.Boulder) HitBoulder(trap, d.BoulderStart, d.BoulderStart);
                        else HitTrapCircle(d, d.Kind == TacticalTrapKind.Freeze, false);
                    }
                    else if (trap.Phase == TacticalTrapPhase.Active) trap.Phase = TacticalTrapPhase.Recovering;
                    else trap.Phase = TacticalTrapPhase.Ready;
                }
            }
        }

        private void HitTrapCircle(TacticalTrapDefinition d, bool freeze, bool burn)
        {
            foreach (var unit in units)
            {
                if (!unit.IsAlive || HorizontalDistanceSquared(unit.Position, d.Center) > d.Radius * d.Radius) continue;
                var warded = unit.BarrierRemaining > 0 && unit.BarrierHits > 0;
                var damage = freeze ? d.Damage + unit.MaxHp * .03f : burn ? d.Damage * .22f + unit.MaxHp * .025f : d.Damage + unit.MaxHp * .075f;
                DamageUnit(d.Id, unit, damage);
                if (freeze && !warded && unit.IsAlive) unit.FrozenRemaining = Mathf.Max(unit.FrozenRemaining, 1.35f);
            }
        }

        private void HitBoulder(TacticalTrapState trap, Vector3 from, Vector3 to)
        {
            var d = trap.Definition;
            var delta = to - from; delta.y = 0;
            foreach (var unit in units)
            {
                if (!unit.IsAlive || trap.Hit.Contains(unit.Id)) continue;
                var offset = unit.Position - from; offset.y = 0;
                var t = delta.sqrMagnitude > .000001f ? Mathf.Clamp01(Vector3.Dot(offset, delta) / delta.sqrMagnitude) : 0;
                if ((offset - delta * t).sqrMagnitude > d.Radius * d.Radius) continue;
                trap.Hit.Add(unit.Id);
                // A direct hit is lethal regardless of defense; a legendary barrier still blocks one contact.
                DamageUnit(d.Id, unit, unit.Hp, ignoreDefense: true);
            }
        }
    }
}
