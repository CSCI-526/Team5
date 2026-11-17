using System.Collections.Generic;
using NUnit.Framework;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class ManualSquadMovementTests
    {
        private static Vector3 At(float x, float z) => new Vector3(x, .25f, z);
        private static readonly Vector3[] TurnRoute = { At(0, 0), At(3, 0), At(3, 3) };

        private static BattleSimulation Create(bool manual = true, TowerDefinition tower = null, Vector3Int? blocked = null,
            bool secondMember = false)
        {
            var cells = new List<Vector3Int>();
            for (var x = -11; x < 11; x++)
                for (var z = -11; z < 11; z++)
                    if (new Vector3Int(x, 0, z) != blocked) cells.Add(new Vector3Int(x, 0, z));
            var members = new List<MemberSpawnDefinition> { new MemberSpawnDefinition(10, 4, SquadCombatRules.Create(UnitRole.Tank)) };
            if (secondMember) members.Add(new MemberSpawnDefinition(11, 7, SquadCombatRules.Create(UnitRole.Tank)));
            var simulation = new BattleSimulation(new BattleDefinition(new[] { At(0, 0), At(6, 0) },
                tower == null ? new TowerDefinition[0] : new[] { tower }, members, cells));
            if (manual) simulation.EnableManualMovement();
            simulation.Step(.02f);
            return simulation;
        }

        private static void Advance(BattleSimulation simulation, int ticks = 1000)
        {
            for (var i = 0; i < ticks; i++) simulation.Step(.02f);
        }

        [Test]
        public void WaitsAtSpawnUntilACommandAndKeepsMembersAliveAtDestination()
        {
            var simulation = Create(secondMember: true);
            Advance(simulation, 100);
            Assert.That(simulation.Units[0].AnchorPosition, Is.EqualTo(At(0, 0)));
            Assert.That(simulation.TrySetManualRoute(TurnRoute), Is.False, "Commands require a paused simulation.");
            simulation.SetPaused(true);
            Assert.That(simulation.TrySetManualRoute(TurnRoute), Is.True);
            Advance(simulation, 100);
            Assert.That(simulation.Units[0].AnchorPosition, Is.EqualTo(At(0, 0)));
            simulation.SetPaused(false);
            Advance(simulation);
            foreach (var member in simulation.Units)
            {
                Assert.That(member.AnchorPosition, Is.EqualTo(At(3, 3)));
                Assert.That(member.IsAlive, Is.True);
                Assert.That(member.Position, Is.EqualTo(At(3, 3) + member.FormationOffset));
            }
            Assert.That(simulation.Outcome, Is.EqualTo(BattleOutcome.Running));
            Assert.That(simulation.ReachedGoalCount, Is.Zero);
        }

        [Test]
        public void RejectsDiagonalNonMultiplesAndWrongStartWithoutReplacingTheRoute()
        {
            var simulation = Create();
            simulation.SetPaused(true);
            Assert.That(simulation.TrySetManualRoute(TurnRoute), Is.True);
            Assert.That(simulation.TrySetManualRoute(new[] { At(0, 0), At(1, 1) }), Is.False);
            Assert.That(simulation.TrySetManualRoute(new[] { At(0, 0), At(4, 0) }), Is.False);
            Assert.That(simulation.TrySetManualRoute(new[] { At(0, 0), At(1, 0), At(3, 0) }), Is.False);
            Assert.That(simulation.TrySetManualRoute(new[] { At(0, 0), At(3, 0), At(3, 2) }), Is.False);
            Assert.That(simulation.TrySetManualRoute(new[] { At(1, 0), At(4, 0) }), Is.False);
            simulation.SetPaused(false);
            Advance(simulation);
            Assert.That(simulation.Units[0].AnchorPosition, Is.EqualTo(At(3, 3)));
        }

        [Test]
        public void RejectsLivingWallsAndTerrainThatBlocksAnOffCenterMember()
        {
            var wall = new TowerDefinition(1, At(3, 0), 0, 0, 1, TargetRule.Nearest, isWall: true, wallAlongZ: true);
            var simulation = Create(tower: wall);
            simulation.SetPaused(true);
            Assert.That(simulation.TrySetManualRoute(new[] { At(0, 0), At(3, 0) }), Is.False);
            // The central member's endpoint is clear; the member in slot 7 would overlap this missing cell.
            simulation = Create(blocked: new Vector3Int(3, 0, 4), secondMember: true);
            simulation.SetPaused(true);
            Assert.That(simulation.TrySetManualRoute(TurnRoute), Is.False);
        }

        [Test]
        public void TowerAttackAreaRemainsTraversable()
        {
            var tower = new TowerDefinition(1, At(6, 6), 100, .001f, 1, TargetRule.Nearest);
            var simulation = Create(tower: tower);
            simulation.SetPaused(true);
            Assert.That(simulation.TrySetManualRoute(TurnRoute), Is.True);
            simulation.SetPaused(false);
            Advance(simulation);
            Assert.That(simulation.Units[0].AnchorPosition, Is.EqualTo(At(3, 3)));
            Assert.That(simulation.Units[0].Hp, Is.LessThan(simulation.Units[0].MaxHp));
        }

        [Test]
        public void CheckpointRetainsRouteAndCanAcceptANewCommandAfterArrival()
        {
            var simulation = Create();
            simulation.SetPaused(true);
            Assert.That(simulation.TrySetManualRoute(TurnRoute), Is.True);
            simulation.SetPaused(false);
            Advance(simulation, 150);
            var restored = simulation.CaptureCheckpoint().Restore();
            Advance(restored);
            Assert.That(restored.ManualMovement, Is.True);
            Assert.That(restored.Units[0].AnchorPosition, Is.EqualTo(At(3, 3)));
            restored.SetPaused(true);
            Assert.That(restored.TrySetManualRoute(new[] { At(3, 3), At(0, 3) }), Is.True);
            restored.SetPaused(false);
            Advance(restored);
            Assert.That(restored.Units[0].AnchorPosition, Is.EqualTo(At(0, 3)));
        }

        [Test]
        public void FormationEditCannotPutAMemberIntoAnObstacleFurtherAlongTheRoute()
        {
            var simulation = Create(blocked: new Vector3Int(3, 0, 4));
            simulation.SetPaused(true);
            Assert.That(simulation.TrySetManualRoute(TurnRoute), Is.True);
            // Swap the middle slot 4 with slot 7. The new offset is currently clear but not at the destination.
            Assert.That(simulation.TryApplyFormation(new[] { 0, 1, 2, 3, 7, 5, 6, 4, 8 }), Is.False);
            Assert.That(simulation.Units[0].SlotId, Is.EqualTo(4));
        }

        [Test]
        public void AcceptsThreeSixAndNineCellStraightSegmentsInBothDirections()
        {
            foreach (var length in new[] { 3, 6, 9, -3, -6, -9 })
            {
                var simulation = Create();
                simulation.SetPaused(true);
                Assert.That(simulation.TrySetManualRoute(new[] { At(0, 0), At(length, 0) }), Is.True);
                simulation.SetPaused(false);
                Advance(simulation);
                Assert.That(simulation.Units[0].AnchorPosition, Is.EqualTo(At(length, 0)));
            }
        }

        [Test]
        public void ThreeCellSegmentsCannotJumpOverBlockedIntermediateCells()
        {
            var route = new[] { At(0, 0), At(0, 3), At(3, 3) };
            var simulation = Create(blocked: new Vector3Int(2, 0, 3));
            simulation.SetPaused(true);
            Assert.That(simulation.TrySetManualRoute(route), Is.False);
            var wall = new TowerDefinition(1, At(2, 3), 0, 0, 1, TargetRule.Nearest, isWall: true, wallAlongZ: true);
            simulation = Create(tower: wall);
            simulation.SetPaused(true);
            Assert.That(simulation.TrySetManualRoute(route), Is.False);
        }

        [Test]
        public void ReplanningInMotionMeasuresThreeCellsFromTheActualPosition()
        {
            var simulation = Create();
            simulation.SetPaused(true);
            Assert.That(simulation.TrySetManualRoute(TurnRoute), Is.True);
            simulation.SetPaused(false);
            Advance(simulation, 40);
            var start = simulation.Units[0].AnchorPosition;
            Assert.That(start.x, Is.GreaterThan(0).And.LessThan(1));
            var end = start + Vector3.forward * 3;
            simulation.SetPaused(true);
            Assert.That(simulation.TrySetManualRoute(new[] { start, end }), Is.True);
            Assert.That(simulation.Units[0].AnchorPosition, Is.EqualTo(start));
            simulation.SetPaused(false);
            Advance(simulation);
            Assert.That(simulation.Units[0].AnchorPosition, Is.EqualTo(end));
        }

        [Test]
        public void DefaultAutomaticModeStillReachesVictory()
        {
            var simulation = Create(manual: false);
            Advance(simulation);
            Assert.That(simulation.ManualMovement, Is.False);
            Assert.That(simulation.Outcome, Is.EqualTo(BattleOutcome.Victory));
        }
    }
}
