using System;
using System.Collections.Generic;
using NUnit.Framework;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class GoalCaptureTests
    {
        private static Vector3 At(float x) => new Vector3(x, .25f, 0);
        private static BattleSimulation ReadyAtExit()
        {
            var cells = new List<Vector3Int>();
            for (int x = -3; x <= 8; x++) for (int z = -3; z <= 3; z++) cells.Add(new Vector3Int(x, 0, z));
            var sim = new BattleSimulation(new BattleDefinition(new[] { At(0), At(5) },
                Array.Empty<TowerDefinition>(), new[] { new MemberSpawnDefinition(1, 4, SquadCombatRules.Create(UnitRole.Captain)) }, cells));
            sim.EnableContinuousMovement(.96f, .2f);
            sim.Step(.02f);
            EnterExit(sim);
            return sim;
        }
        private static void EnterExit(BattleSimulation sim)
        {
            Assert.That(sim.RequestMovement(new MovementRequest(At(5))), Is.True);
            for (var i = 0; i < 1000 && !sim.IsSquadReadyToCapture && sim.Outcome == BattleOutcome.Running; i++) sim.Step(.02f);
            Assert.That(sim.IsSquadReadyToCapture, Is.True);
            Assert.That(sim.GoalHoldProgress, Is.Zero);
            // Stay at the first qualifying position, rather than letting the arrival accrue time.
            Assert.That(sim.RequestMovement(new MovementRequest(sim.Units[0].Position)), Is.True);
        }
        private static void Go(BattleSimulation sim, float x)
        {
            Assert.That(sim.RequestMovement(new MovementRequest(At(x))), Is.True);
            for (var i = 0; i < 1000 && sim.IsSquadMoving; i++) sim.Step(.02f);
            Assert.That(sim.IsSquadMoving, Is.False);
        }
        [Test]
        public void CaptureRequiresTwoSecondsInside()
        {
            var sim = ReadyAtExit();
            sim.Step(1f);
            Assert.That(sim.Outcome, Is.EqualTo(BattleOutcome.Running));
            Assert.That(sim.GoalHoldProgress, Is.EqualTo(.5f).Within(.0001f));
            sim.Step(.9f);
            Assert.That(sim.Outcome, Is.EqualTo(BattleOutcome.Running));
            sim.Step(.1f);
            Assert.That(sim.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(sim.ReachedGoalCount, Is.EqualTo(1));
            Assert.That(sim.GoalHoldProgress, Is.EqualTo(1f));
        }
        [Test]
        public void PauseAndCheckpointPreserveProgress()
        {
            var sim = ReadyAtExit();
            sim.Step(.75f); sim.SetPaused(true); sim.Step(3f);
            Assert.That(sim.GoalHoldProgress, Is.EqualTo(.375f).Within(.0001f));
            var restored = sim.CaptureCheckpoint().Restore();
            Assert.That(restored.GoalHoldProgress, Is.EqualTo(.375f).Within(.0001f));
            Assert.That(restored.IsPaused, Is.True);
            restored.SetPaused(false); restored.Step(1.25f);
            Assert.That(restored.Outcome, Is.EqualTo(BattleOutcome.Victory));
        }
        [Test]
        public void LeavingAndReturningResetsProgress()
        {
            var sim = ReadyAtExit(); sim.Step(1f);
            Go(sim, 0);
            Assert.That(sim.GoalHoldProgress, Is.Zero);
            Assert.That(sim.IsSquadReadyToCapture, Is.False);
            EnterExit(sim);
            sim.Step(1f);
            Assert.That(sim.Outcome, Is.EqualTo(BattleOutcome.Running));
        }
        [Test]
        public void WalkingInsideContinuesCaptureWithoutResettingOnNewClick()
        {
            var sim = ReadyAtExit();
            sim.Step(.25f);
            Assert.That(sim.RequestMovement(new MovementRequest(At(5))), Is.True);
            Assert.That(sim.GoalHoldProgress, Is.EqualTo(.125f).Within(.0001f));
            sim.Step(.5f);
            Assert.That(sim.IsSquadMoving, Is.True);
            Assert.That(sim.GoalHoldProgress, Is.EqualTo(.375f).Within(.0001f));
            // Two seconds total, even when the movement command has not yet ended.
            Assert.That(sim.RequestMovement(new MovementRequest(At(5.9f))), Is.True);
            sim.Step(1.25f);
            Assert.That(sim.Outcome, Is.EqualTo(BattleOutcome.Victory));
        }
        [Test]
        public void SoldierLeftBehindPreventsCapture()
        {
            var cells = new List<Vector3Int>();
            for (int x = -3; x <= 8; x++) for (int z = -3; z <= 3; z++) cells.Add(new Vector3Int(x, 0, z));
            var unit = SquadCombatRules.Create(UnitRole.Tank);
            var sim = new BattleSimulation(new BattleDefinition(new[] { At(0), At(5) }, Array.Empty<TowerDefinition>(),
                new[] { new MemberSpawnDefinition(1, 4, unit), new MemberSpawnDefinition(2, 5, unit) }, cells));
            sim.EnableContinuousMovement(.96f, .2f, true); sim.Step(.02f);
            Assert.That(sim.RequestMovement(new MovementRequest(At(5), memberId: 1)), Is.True);
            for (var i = 0; i < 120; i++) sim.Step(.1f);
            Assert.That(sim.Units[0].Position.x, Is.EqualTo(5f).Within(.01f));
            Assert.That(sim.Outcome, Is.EqualTo(BattleOutcome.Running));
            Assert.That(sim.GoalHoldProgress, Is.Zero);
        }
        [Test]
        public void LastSoldierKilledDuringCaptureCannotWin()
        {
            var sim = ReadyAtExit(); sim.Step(1f);
            Assert.That(sim.TryApplyEnvironmentalDamage(sim.Units[0].Position, 1f, 100000f), Is.True);
            Assert.That(sim.Outcome, Is.EqualTo(BattleOutcome.Defeat));
        }
    }
}
