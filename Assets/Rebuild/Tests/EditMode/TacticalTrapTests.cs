using System;
using NUnit.Framework;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class TacticalTrapTests
    {
        private static Vector3 At(float x, float z = 0) => new Vector3(x, .25f, z);
        private static BattleSimulation Setup(TacticalTrapKind kind, UnitRole role = UnitRole.Tank)
        {
            var sim = new BattleSimulation(new BattleDefinition(new[] { At(0), At(30) }, Array.Empty<TowerDefinition>(),
                new[] { new MemberSpawnDefinition(1, 4, SquadCombatRules.Create(role)) }));
            sim.EnableContinuousMovement(.96f, .2f);
            sim.AddTacticalTrap(new TacticalTrapDefinition(-10001, kind, At(0), kind == TacticalTrapKind.Boulder ? .6f : 1.2f,
                .6f, 8f, Vector3.right));
            sim.Step(.02f); return sim;
        }
        private static void Advance(BattleSimulation sim, float seconds)
        { for (var i = 0; i < Mathf.RoundToInt(seconds / .02f); i++) sim.Step(.02f); }

        [Test]
        public void FreezeHasWarningAndPausesWithSimulation()
        {
            var sim = Setup(TacticalTrapKind.Freeze); var trap = sim.TacticalTraps[0]; var unit = sim.Units[0];
            Assert.That(trap.Phase, Is.EqualTo(TacticalTrapPhase.Warning));
            Assert.That(unit.Hp, Is.EqualTo(unit.MaxHp));
            sim.SetPaused(true); sim.Step(10); Assert.That(trap.Elapsed, Is.Zero);
            sim.SetPaused(false); Advance(sim, trap.Definition.WarningSeconds + .04f);
            Assert.That(unit.FrozenRemaining, Is.GreaterThan(1f));
            var pos = unit.Position; Assert.That(sim.RequestMovement(new MovementRequest(At(4))), Is.True);
            Advance(sim, .4f); Assert.That(unit.Position, Is.EqualTo(pos));
            Advance(sim, 1.5f); Assert.That(unit.Position.x, Is.GreaterThan(pos.x));
        }
        [Test]
        public void MeteorBurnsOnlyWhileInsideFieldAndExpires()
        {
            var sim = Setup(TacticalTrapKind.Meteor); var unit = sim.Units[0];
            Advance(sim, sim.TacticalTraps[0].Definition.WarningSeconds + .04f); var afterImpact = unit.Hp; Assert.That(afterImpact, Is.LessThan(unit.MaxHp));
            Advance(sim, .8f); Assert.That(unit.Hp, Is.LessThan(afterImpact));
            Assert.That(sim.RequestMovement(new MovementRequest(At(4))), Is.True);
            Advance(sim, 2.2f); var hpOutside = unit.Hp; Assert.That(unit.Position.x, Is.GreaterThan(1.2f));
            Advance(sim, 2f); Assert.That(unit.Hp, Is.EqualTo(hpOutside));
            Assert.That(sim.TacticalTraps[0].Phase, Is.EqualTo(TacticalTrapPhase.Recovering));
        }
        [Test]
        public void BoulderKillsTankButCanBeDodged()
        {
            var hit = Setup(TacticalTrapKind.Boulder); Advance(hit, 5f);
            Assert.That(hit.Units[0].IsAlive, Is.False); Assert.That(hit.KilledCount, Is.EqualTo(1));
            var dodge = Setup(TacticalTrapKind.Boulder); Assert.That(dodge.RequestMovement(new MovementRequest(At(0, 3))), Is.True);
            Advance(dodge, 5f); Assert.That(dodge.Units[0].IsAlive, Is.True);
        }
        [Test]
        public void AegisBarrierAbsorbsOneBoulderContact()
        {
            var sim = Setup(TacticalTrapKind.Boulder, UnitRole.Aegis);
            Assert.That(sim.TryUseLegendarySkill(1, At(0)), Is.True);
            var unit = sim.Units[0]; var charges = unit.BarrierHits;
            Advance(sim, 5f);
            Assert.That(unit.IsAlive, Is.True);
            Assert.That(unit.BarrierHits, Is.EqualTo(charges - 1));
            Assert.That(unit.Hp, Is.EqualTo(unit.MaxHp));
        }
        [Test]
        public void WideRoadBoulderRollCoversAllFiveTilesAndHasLongRecovery()
        {
            var d = new TacticalTrapDefinition(-10001, TacticalTrapKind.Boulder, At(0), .85f, 1f, 15f, Vector3.right);
            Assert.That(d.BoulderStart.x, Is.LessThan(-2.5f));
            Assert.That(d.BoulderEnd.x, Is.GreaterThan(2.5f));
            Assert.That(d.Recovery, Is.EqualTo(15f));
            Assert.That(d.WarningSeconds, Is.EqualTo(2.8f));
        }
        [Test]
        public void CheckpointRestoresTrapWarningIndependently()
        {
            var sim = Setup(TacticalTrapKind.Boulder); Advance(sim, 1f);
            var restored = sim.CaptureCheckpoint().Restore();
            Assert.That(restored.TacticalTraps[0].Elapsed, Is.EqualTo(sim.TacticalTraps[0].Elapsed));
            Advance(sim, 4f); Assert.That(restored.TacticalTraps[0].Phase, Is.EqualTo(TacticalTrapPhase.Warning));
            Assert.That(restored.Units[0].IsAlive, Is.True);
            Advance(restored, 4f); Assert.That(restored.Units[0].IsAlive, Is.False);
        }
    }
}
