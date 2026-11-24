using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Presentation.LegacyUI;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class GameplayRepairRegressionTests
    {
        private static Vector3 At(float x, float z = 0f) => new Vector3(x, .25f, z);
        private static MemberSpawnDefinition Captain() =>
            new MemberSpawnDefinition(1, 4, SquadCombatRules.Create(UnitRole.Captain));

        [TestCase(true)]
        [TestCase(false)]
        public void CaptainSummonDuringMovementJoinsExistingDestinationWithoutAnotherClick(bool compact)
        {
            var random = UnityEngine.Random.state;
            try
            {
                var sim = new BattleSimulation(new BattleDefinition(new[] { At(0), At(20) },
                    Array.Empty<TowerDefinition>(), new[] { Captain() }, null, 6));
                sim.EnableContinuousMovement(.6f, .2f, compact);
                sim.Step(.02f);
                Assert.That(sim.RequestMovement(new MovementRequest(At(8))), Is.True);
                for (var i = 0; i < 100; i++) sim.Step(.02f);
                Assert.That(sim.IsSquadMoving, Is.True);
                Assert.That(sim.TryUseLegendarySkill(1, At(0)), Is.True);
                var backup = sim.Units.Single(u => u.Id != 1);
                var start = backup.Position;
                Assert.That(backup.Definition.Level, Is.EqualTo(6));
                Assert.That(sim.SquadDestination, Is.EqualTo(At(8)));
                for (var i = 0; i < 1500 && sim.IsSquadMoving; i++) sim.Step(.02f);
                Assert.That(sim.IsSquadMoving, Is.False);
                Assert.That(Vector3.Distance(backup.Position, start), Is.GreaterThan(2f));
                foreach (var unit in sim.Units)
                    Assert.That(Vector3.Distance(unit.Position, At(8) + unit.FormationOffset), Is.LessThan(.002f));
            }
            finally { UnityEngine.Random.state = random; }
        }

        [Test]
        public void ReinforcementFollowsSharedPathThroughSingleCellDoor()
        {
            var random = UnityEngine.Random.state;
            try
            {
                var cells = new List<Vector3Int>();
                for (var x = -3; x <= 12; x++) for (var z = -4; z <= 4; z++)
                    if (x != 3 || z == 0) cells.Add(new Vector3Int(x, 0, z));
                var sim = new BattleSimulation(new BattleDefinition(new[] { At(0), At(0, -1) },
                    Array.Empty<TowerDefinition>(), new[] { Captain() }, cells));
                sim.EnableContinuousMovement(.48f, .2f, true);
                sim.Step(.02f);
                Assert.That(sim.RequestMovement(new MovementRequest(At(8))), Is.True);
                for (var i = 0; i < 50; i++) sim.Step(.02f);
                Assert.That(sim.TryUseLegendarySkill(1, At(0)), Is.True);
                for (var i = 0; i < 2500 && sim.IsSquadMoving; i++)
                {
                    sim.Step(.02f);
                    foreach (var unit in sim.Units)
                        if (Mathf.Abs(unit.Position.x - 3) < .69f)
                            Assert.That(Mathf.Abs(unit.Position.z), Is.LessThanOrEqualTo(.301f));
                }
                Assert.That(sim.IsSquadMoving, Is.False);
                foreach (var unit in sim.Units)
                    Assert.That(Vector3.Distance(unit.Position, At(8) + unit.FormationOffset), Is.LessThan(.002f));
            }
            finally { UnityEngine.Random.state = random; }
        }

        [Test]
        public void CaptainSummonWhileIdleDoesNotStartAutoMarch()
        {
            var random = UnityEngine.Random.state;
            try
            {
                var sim = new BattleSimulation(new BattleDefinition(new[] { At(0), At(20) },
                    Array.Empty<TowerDefinition>(), new[] { Captain() }));
                sim.EnableContinuousMovement(.6f, .2f, true);
                sim.Step(.02f);
                Assert.That(sim.TryUseLegendarySkill(1, At(0)), Is.True);
                var positions = sim.Units.Select(u => u.Position).ToArray();
                sim.Step(2f);
                Assert.That(sim.IsSquadMoving, Is.False);
                Assert.That(sim.Units.Select(u => u.Position).ToArray(), Is.EqualTo(positions));
            }
            finally { UnityEngine.Random.state = random; }
        }

        [Test]
        public void CaptainSummonOnLegacyRouteInheritsProgressAndDoesNotWalkBackToSpawn()
        {
            var random = UnityEngine.Random.state;
            try
            {
                var sim = new BattleSimulation(new BattleDefinition(new[] { At(0), At(2), At(20) },
                    Array.Empty<TowerDefinition>(), new[] { Captain() }));
                sim.Step(5f);
                Assert.That(sim.Units[0].NextWaypoint, Is.EqualTo(2));
                Assert.That(sim.TryUseLegendarySkill(1, At(0)), Is.True);
                var backup = sim.Units[1];
                Assert.That(backup.RouteProgress, Is.EqualTo(sim.Units[0].RouteProgress));
                Assert.That(backup.NextWaypoint, Is.EqualTo(2));
                var x = backup.Position.x;
                sim.Step(.2f);
                Assert.That(backup.Position.x, Is.GreaterThan(x));
            }
            finally { UnityEngine.Random.state = random; }
        }

        [Test]
        public void CheckpointKeepsReinforcementPath()
        {
            var random = UnityEngine.Random.state;
            try
            {
                var sim = new BattleSimulation(new BattleDefinition(new[] { At(0), At(20) },
                    Array.Empty<TowerDefinition>(), new[] { Captain() }));
                sim.EnableContinuousMovement(.6f, .2f, true);
                sim.Step(.02f);
                Assert.That(sim.RequestMovement(new MovementRequest(At(6))), Is.True);
                sim.Step(1f);
                Assert.That(sim.TryUseLegendarySkill(1, At(0)), Is.True);
                var restored = sim.CaptureCheckpoint().Restore();
                for (var i = 0; i < 1200 && restored.IsSquadMoving; i++) restored.Step(.02f);
                Assert.That(restored.IsSquadMoving, Is.False);
                foreach (var unit in restored.Units)
                    Assert.That(Vector3.Distance(unit.Position, At(6) + unit.FormationOffset), Is.LessThan(.002f));
            }
            finally { UnityEngine.Random.state = random; }
        }

        [Test]
        public void LobberPlacementRejectsOutOfReachRearPositionAndUsesNearerSide()
        {
            var sites = new MinionSites();
            sites.AddHost(new Vector2Int(5, 7), At(5, 7));
            sites.AddHost(new Vector2Int(5, 5), At(5, 5));
            var tower = new TowerDefinition(1, At(5, 6), 8f, 1f, 1f, TargetRule.Nearest);
            var placements = MinionPlanner.Plan(new[] { At(0), At(5), At(10) }, new[] { tower }, 3, sites);
            Assert.That(placements.Count, Is.EqualTo(1));
            Assert.That(placements[0].Cells.Single(), Is.EqualTo(At(5, 5)));
        }

        [Test]
        public void InvalidEnvironmentalDamageCannotCorruptUnitHealth()
        {
            var sim = new BattleSimulation(new BattleDefinition(new[] { At(0), At(20) },
                Array.Empty<TowerDefinition>(), new[] { Captain() }));
            sim.EnableContinuousMovement(); sim.Step(.02f);
            var hp = sim.Units[0].Hp;
            Assert.That(sim.TryApplyEnvironmentalDamage(At(0), float.NaN, 1f), Is.False);
            Assert.That(sim.TryApplyEnvironmentalDamage(At(0), 1f, float.NaN), Is.False);
            Assert.That(sim.TryApplyEnvironmentalDamage(new Vector3(float.NaN, 0, 0), 1f, 1f), Is.False);
            Assert.That(sim.Units[0].Hp, Is.EqualTo(hp));
        }

        [Test]
        public void EnvironmentalDamageUsesCorrectBattleObjectiveWhenLastUnitDies()
        {
            var sim = new BattleSimulation(new BattleDefinition(new[] { At(0), At(20) },
                Array.Empty<TowerDefinition>(), new UnitDefinition(1f, 1f), 1, 1f, 1));
            sim.Step(.02f);
            Assert.That(sim.TryApplyEnvironmentalDamage(sim.Units[0].Position, 1f, 10f), Is.True);
            Assert.That(sim.Outcome, Is.EqualTo(BattleOutcome.Victory));
        }
    }
}
