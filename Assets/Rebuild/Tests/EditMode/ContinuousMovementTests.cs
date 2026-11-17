using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class ContinuousMovementTests
    {
        private static Vector3 At(float x, float z) => new Vector3(x, .25f, z);
        private static BattleSimulation Create(bool passage = false, bool sealedWall = false, float spacing = .48f, float radius = .2f)
        {
            var cells = new List<Vector3Int>();
            for (var x = -3; x <= 10; x++) for (var z = -3; z <= 8; z++)
                if (!passage || x != 3 || !sealedWall && z == 0) cells.Add(new Vector3Int(x, 0, z));
            var members = Enumerable.Range(0, 6).Select(i => new MemberSpawnDefinition(i + 1, i, SquadCombatRules.Create(UnitRole.Tank)));
            var sim = new BattleSimulation(new BattleDefinition(new[] { At(0, 0), At(0, -1) }, new TowerDefinition[0], members, cells));
            sim.EnableContinuousMovement(spacing, radius); sim.Step(.02f); return sim;
        }
        private static void NoOverlap(BattleSimulation sim)
        {
            for (var i = 0; i < sim.Units.Count; i++) for (var j = i + 1; j < sim.Units.Count; j++)
                Assert.That(Vector3.Distance(sim.Units[i].Position, sim.Units[j].Position),
                    Is.GreaterThanOrEqualTo(sim.Units[i].CollisionRadius + sim.Units[j].CollisionRadius - .001f));
        }
        private static void Finish(BattleSimulation sim, bool passage = false)
        {
            for (var tick = 0; tick < 5000 && sim.IsSquadMoving; tick++)
            {
                sim.Step(.02f); NoOverlap(sim);
                if (passage)
                    foreach (var u in sim.Units)
                        if (Mathf.Abs(u.Position.x - 3) < .65f) Assert.That(Mathf.Abs(u.Position.z), Is.LessThanOrEqualTo(.301f));
            }
            Assert.That(sim.IsSquadMoving, Is.False, string.Join("; ", sim.Units.Select(u => u.Id + ":" + u.Position)));
        }
        [Test]
        public void ExactBodyClearanceBoundaryAcceptsScene1ValuesAndStillRejectsOverlap()
        {
            Assert.That(BattleSimulation.ValidContinuousBodySettings(1f, .495f), Is.True);
            Assert.That(BattleSimulation.ValidContinuousBodySettings(.99f, .495f), Is.False);
            Assert.That(BattleSimulation.ValidContinuousBodySettings(float.NaN, .495f), Is.False);
            Assert.That(BattleSimulation.ValidContinuousBodySettings(1, float.PositiveInfinity), Is.False);
            Assert.That(BattleSimulation.ValidContinuousBodySettings(1, .019f), Is.False);
            var sim = Create(spacing: 1f, radius: .495f);
            Assert.That(sim.ContinuousMovementEnabled, Is.True);
        }
        [Test]
        public void SeveralIndependentBodiesShareACellAndReachFractionalDiagonalTargets()
        {
            var sim = Create(); NoOverlap(sim);
            Assert.That(sim.GetCellOccupancy(Vector3Int.zero).UnitIds.Count, Is.EqualTo(6));
            Assert.That(sim.RequestMovement(new MovementRequest(At(6.13f, 3.27f))), Is.True);
            Finish(sim);
            foreach (var u in sim.Units) Assert.That(Vector3.Distance(u.Position, At(6.13f, 3.27f) + u.FormationOffset), Is.LessThan(.001f));
            Assert.That(sim.Outcome, Is.EqualTo(BattleOutcome.Running));
        }
        [Test]
        public void SingleCellPassageTemporarilyBreaksFormationThenRestoresSlots()
        {
            var sim = Create(passage: true);
            Assert.That(sim.RequestMovement(new MovementRequest(At(7.15f, 0))), Is.True);
            Finish(sim, passage: true);
            foreach (var u in sim.Units) Assert.That(Vector3.Distance(u.Position, At(7.15f, 0) + u.FormationOffset), Is.LessThan(.001f));
        }
        [Test]
        public void NarrowDestinationQueuesMembersThenWideCommandRestoresFormation()
        {
            var sim = Create(passage: true);
            Assert.That(sim.RequestMovement(new MovementRequest(At(3, 0))), Is.True);
            Finish(sim, passage: true);
            Assert.That(sim.RequestMovement(new MovementRequest(At(3, 0))), Is.True);
            Finish(sim, passage: true);
            Assert.That(sim.RequestMovement(new MovementRequest(At(7, 1))), Is.True);
            Finish(sim, passage: true);
            foreach (var u in sim.Units) Assert.That(Vector3.Distance(u.Position, At(7, 1) + u.FormationOffset), Is.LessThan(.001f));
        }
        [Test]
        public void NearWallWithWideGroundUsesNearbyFormationAndMovesTogether()
        {
            var sim = Create(passage: true, spacing: .64f, radius: .28f);
            var requested = At(2.15f, 2);
            Assert.That(sim.RequestMovement(new MovementRequest(requested)), Is.True);
            Assert.That(Vector3.Distance(sim.SquadDestination, requested), Is.LessThanOrEqualTo(.96f));
            Assert.That(sim.SquadDestination.x, Is.LessThan(requested.x));
            var initial = sim.Units.Select(u => u.Position).ToArray();
            for (var i = 0; i < 30; i++) { sim.Step(.02f); NoOverlap(sim); }
            Assert.That(sim.Units.Where((u, i) => Vector3.Distance(u.Position, initial[i]) > .05f).Count(), Is.EqualTo(6));
            Finish(sim);
            foreach (var u in sim.Units) Assert.That(Vector3.Distance(u.Position, sim.SquadDestination + u.FormationOffset), Is.LessThan(.001f));
        }
        [Test]
        public void RealQueueFollowsWithClearanceInsteadOfWaitingForArrival()
        {
            var sim = Create(passage: true, spacing: .64f, radius: .28f);
            Assert.That(sim.RequestMovement(new MovementRequest(At(3, 0))), Is.True);
            var initial = sim.Units.Select(u => u.Position).ToArray();
            for (var i = 0; i < 150; i++) { sim.Step(.02f); NoOverlap(sim); }
            Assert.That(sim.Units.Where((u, i) => Vector3.Distance(u.Position, initial[i]) > .1f).Count(), Is.GreaterThan(1));
            Assert.That(sim.Units.All(u => Vector3.Distance(u.Position, At(3, 0)) > .1f), Is.True);
            Finish(sim, passage: true);
        }
        [Test]
        public void SingleMemberCommandsAndPlayerPriorityUseTheSameExecutor()
        {
            var sim = Create(); var others = sim.Units.Skip(1).Select(u => u.Position).ToArray();
            Assert.That(sim.RequestMovement(new MovementRequest(At(-2, 2), 1)), Is.True);
            Assert.That(sim.RequestMovement(new MovementRequest(At(2, 2), 1, MovementCommandSource.AI)), Is.False);
            Finish(sim);
            Assert.That(sim.Units[0].Position, Is.EqualTo(At(-2, 2)));
            Assert.That(sim.Units.Skip(1).Select(u => u.Position), Is.EqualTo(others));
        }
        [Test]
        public void ConfigurableSpacingAndRadiusArePreservedByCheckpoints()
        {
            var sim = Create(spacing: .56f, radius: .24f);
            Assert.That(sim.RequestMovement(new MovementRequest(At(4.1f, 3.2f))), Is.True);
            var restored = sim.CaptureCheckpoint().Restore(); Finish(restored);
            foreach (var u in restored.Units)
            {
                Assert.That(u.CollisionRadius, Is.EqualTo(.24f));
                Assert.That(u.FormationOffset, Is.EqualTo(SquadCombatRules.Offset(u.SlotId) * .56f));
            }
        }
        [Test]
        public void UnreachableCommandDoesNotMoveOrTeleportMembers()
        {
            var sim = Create(true, true); var before = sim.Units.Select(u => u.Position).ToArray();
            Assert.That(sim.RequestMovement(new MovementRequest(At(7, 0))), Is.False);
            for (var i = 0; i < 100; i++) sim.Step(.02f);
            Assert.That(sim.Units.Select(u => u.Position), Is.EqualTo(before));
        }
        [Test]
        public void PauseRecommandAndCheckpointKeepIndependentPaths()
        {
            var sim = Create(); sim.RequestMovement(new MovementRequest(At(7, 0)));
            for (var i = 0; i < 80; i++) sim.Step(.02f);
            sim.SetPaused(true); var before = sim.Units.Select(u => u.Position).ToArray();
            for (var i = 0; i < 50; i++) sim.Step(.02f);
            Assert.That(sim.Units.Select(u => u.Position), Is.EqualTo(before));
            sim.SetPaused(false);
            Assert.That(sim.RequestMovement(new MovementRequest(At(2.17f, 5.31f))), Is.True);
            var restored = sim.CaptureCheckpoint().Restore(); Finish(restored);
            foreach (var u in restored.Units) Assert.That(Vector3.Distance(u.Position, At(2.17f, 5.31f) + u.FormationOffset), Is.LessThan(.001f));
        }
        [Test]
        public void MovingFormationEditStaysLocalAndContinuesForwardWithoutSpeedSpikes()
        {
            var sim = Create(spacing: .64f, radius: .28f);
            Assert.That(sim.RequestMovement(new MovementRequest(At(7, 0))), Is.True);
            for (var i = 0; i < 100; i++) sim.Step(.02f);
            var before = sim.Units.Select(u => u.Position).ToArray();
            var progress = sim.Units.Select(u => u.RouteProgress).ToArray();
            Assert.That(sim.TryApplyFormation(new[] { 1, 0, 2, 3, 4, 5, 6, 7, 8 }), Is.True);
            Assert.That(sim.IsPaused, Is.False);
            Assert.That(sim.Units.Select(u => u.Position), Is.EqualTo(before));
            var origin = sim.SquadReferencePosition;
            for (var i = 0; i < 1500 && sim.IsFormationAdjusting; i++)
            {
                var prior = sim.Units.Select(u => u.Position).ToArray();
                sim.Step(.02f); NoOverlap(sim);
                for (var j = 0; j < prior.Length; j++)
                    Assert.That(Vector3.Distance(prior[j], sim.Units[j].Position), Is.LessThanOrEqualTo(sim.Units[j].Speed * .02f + .0001f));
            }
            Assert.That(sim.IsFormationAdjusting, Is.False);
            var forward = Vector3.Distance(origin, sim.SquadReferencePosition);
            Assert.That(forward, Is.GreaterThan(.05f));
            for (var i = 0; i < 2; i++)
                Assert.That(sim.Units[i].RouteProgress - progress[i] - forward, Is.LessThan(2.5f));
            for (var i = 2; i < sim.Units.Count; i++)
                Assert.That(sim.Units[i].RouteProgress - progress[i] - forward, Is.LessThan(sim.Units[i].CollisionRadius * 2 + .1f));
            Finish(sim);
            foreach (var u in sim.Units) Assert.That(Vector3.Distance(u.Position, At(7, 0) + u.FormationOffset), Is.LessThan(.001f));
        }
        [Test]
        public void CheckpointDuringMovingExchangeKeepsLocalTargetsAndOriginalJourney()
        {
            var sim = Create(spacing: .64f, radius: .28f);
            sim.RequestMovement(new MovementRequest(At(7, 0)));
            for (var i = 0; i < 100; i++) sim.Step(.02f);
            sim.TryApplyFormation(new[] { 1, 0, 2, 3, 4, 5, 6, 7, 8 });
            for (var i = 0; i < 20; i++) sim.Step(.02f);
            Assert.That(sim.IsFormationAdjusting, Is.True);
            var restored = sim.CaptureCheckpoint().Restore(); Finish(restored);
            Assert.That(restored.IsFormationAdjusting, Is.False);
            foreach (var u in restored.Units) Assert.That(Vector3.Distance(u.Position, At(7, 0) + u.FormationOffset), Is.LessThan(.001f));
        }
        [Test]
        public void Scene1CrossFormationUsesShortExchangesAndSmallNeighbourNudges()
        {
            var cells = new List<Vector3Int>();
            for (var x = -3; x <= 10; x++) for (var z = -3; z <= 8; z++) cells.Add(new Vector3Int(x, 0, z));
            var slots = new[] { 4, 7, 3, 1 };
            var members = slots.Select((slot, i) => new MemberSpawnDefinition(111 + i, slot, SquadCombatRules.Create(UnitRole.Tank)));
            var sim = new BattleSimulation(new BattleDefinition(new[] { At(0, 0), At(0, -1) }, new TowerDefinition[0], members, cells));
            sim.EnableContinuousMovement(.64f, .28f); sim.Step(.02f);
            var progress = sim.Units.Select(u => u.RouteProgress).ToArray();
            Assert.That(sim.TryApplyFormation(new[] { 0, 1, 2, 4, 3, 5, 6, 7, 8 }), Is.True);
            Finish(sim);
            foreach (var unit in sim.Units) Assert.That(Vector3.Distance(unit.Position, unit.FormationOffset + At(0, 0)), Is.LessThan(.001f));
            Assert.That(sim.Units[0].RouteProgress - progress[0], Is.LessThan(1.2f));
            Assert.That(sim.Units[2].RouteProgress - progress[2], Is.LessThan(1.2f));
            Assert.That(sim.Units[1].RouteProgress - progress[1], Is.LessThan(.6f));
            Assert.That(sim.Units[3].RouteProgress - progress[3], Is.LessThan(.6f));
        }
        [Test]
        public void FormationEditChangesIdealSlotsWithoutChangingActualPosition()
        {
            var sim = Create(); sim.SetPaused(true); var before = sim.Units.Select(u => u.Position).ToArray();
            Assert.That(sim.TryApplyFormation(new[] { 1, 0, 2, 3, 4, 5, 6, 7, 8 }), Is.True);
            Assert.That(sim.Units.Select(u => u.Position), Is.EqualTo(before));
            sim.SetPaused(false); Finish(sim);
        }
    }
}
