using System;
using System.Collections.Generic;
using NUnit.Framework;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.Session;
using Team5.Rebuild.World;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class TacticalStageRulesTests
    {
        [Test]
        public void ConeFacesRoadInsteadOfAway()
        {
            var route = new[] { new Vector3(-4,.25f,0), new Vector3(4,.25f,0) };
            var tower = new TowerDefinition(1, new Vector3(0,.25f,3), 4f, 1f, 1f, TargetRule.Nearest,
                area: AttackArea.ConePositiveZ);
            var result = TacticalStageRules.Tune(tower, route, 4);
            Assert.That(result.Area, Is.EqualTo(AttackArea.ConeNegativeZ));
            Assert.That(SquadCombatRules.Contains(new Vector3(0,0,-3), result.Area, result.Range, result.AttackWidth), Is.True);
            Assert.That(result.Damage, Is.GreaterThan(tower.Damage));
            Assert.That(result.GoldBounty, Is.EqualTo(tower.GoldBounty));
        }
        [Test]
        public void LaserStaysNarrowAndFacesRoadInLateStage()
        {
            var route = new[] { new Vector3(-4,.25f,0), new Vector3(4,.25f,0) };
            var tower = new TowerDefinition(1, new Vector3(0,.25f,3), 5f, 1f, 1f, TargetRule.Nearest, area: AttackArea.PositiveZ, attackWidth: .6f);
            var result = TacticalStageRules.Tune(tower, route, 6);
            Assert.That(result.Area, Is.EqualTo(AttackArea.NegativeZ));
            Assert.That(result.AttackWidth, Is.EqualTo(.6f));
            Assert.That(SquadCombatRules.Contains(new Vector3(1,0,-3), result.Area, result.Range, result.AttackWidth), Is.False);
        }
        [Test]
        public void AuthoredLaserCrossingIsNotReaimedAlongRoad()
        {
            var route = new[] { new Vector3(0,.25f,-4), new Vector3(0,.25f,4), new Vector3(5,.25f,4) };
            var tower = new TowerDefinition(1, new Vector3(3,.25f,0), 6f, 1f, 1f, TargetRule.Nearest,
                area: AttackArea.NegativeX, attackWidth: .6f);
            var result = TacticalStageRules.Tune(tower, route, 6);
            Assert.That(result.Area, Is.EqualTo(AttackArea.NegativeX));
            Assert.That(result.Range, Is.EqualTo(tower.Range));
        }
        [Test]
        public void WallsAndMinionsAreNotRetuned()
        {
            var tower = new TowerDefinition(1, Vector3.zero, 0, 0, 1, TargetRule.Nearest, isWall: true);
            Assert.That(TacticalStageRules.Tune(tower, new[] { Vector3.zero, Vector3.forward }, 7), Is.SameAs(tower));
        }
        [Test]
        public void CampaignSurfacePredicateRejectsOffRoadGround()
        {
            var host = new GameObject("Tactical rule test");
            try
            {
                var session = host.AddComponent<BattleSession>();
                JsonUtility.FromJsonOverwrite("{\"squadCombat\":true,\"continuousSquadMovement\":true,\"displayedLevelNumber\":4,\"restrictToTacticalRoad\":true}", session);
                var cell = host.AddComponent<BattleCell>();
                JsonUtility.FromJsonOverwrite("{\"kind\":" + (int)CellKind.Ground + "}", cell);
                Assert.That(session.UsesTacticalRoad, Is.True);
                Assert.That(session.IsMovementSurface(cell), Is.False);
                JsonUtility.FromJsonOverwrite("{\"kind\":" + (int)CellKind.Road + "}", cell);
                Assert.That(session.IsMovementSurface(cell), Is.True);
                JsonUtility.FromJsonOverwrite("{\"kind\":" + (int)CellKind.Goal + "}", cell);
                Assert.That(session.IsMovementSurface(cell), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }
        [Test]
        public void OutsideClickStillRejectedWhenRoadProjectionIsDisabled()
        {
            var cells = new List<Vector3Int>();
            for (int x = -1; x <= 6; x++) for (int z = -1; z <= 1; z++) cells.Add(new Vector3Int(x,0,z));
            var sim = new BattleSimulation(new BattleDefinition(new[] { new Vector3(0,.25f,0), new Vector3(5,.25f,0) },
                Array.Empty<TowerDefinition>(), new[] { new MemberSpawnDefinition(1, 4, SquadCombatRules.Create(UnitRole.Tank)) }, cells));
            sim.EnableContinuousMovement(.96f,.2f); sim.Step(.02f);
            Assert.That(sim.RequestMovement(new MovementRequest(new Vector3(2,.25f,3))), Is.False);
        }
        [Test]
        public void RoadOnlyMovementSnapsGroundAndCanTurnCorner()
        {
            var cells = new HashSet<Vector3Int>();
            for (int x = -1; x <= 6; x++) for (int z = -1; z <= 1; z++) cells.Add(new Vector3Int(x,0,z));
            for (int x = 4; x <= 6; x++) for (int z = -1; z <= 6; z++) cells.Add(new Vector3Int(x,0,z));
            var origin = new Vector3(0,.25f,0); var corner = new Vector3(5,.25f,0); var goal = new Vector3(5,.25f,5);
            var members = new List<MemberSpawnDefinition>();
            for (var slot = 0; slot < 9; slot++) members.Add(new MemberSpawnDefinition(slot + 1, slot, SquadCombatRules.Create(UnitRole.Tank)));
            var sim = new BattleSimulation(new BattleDefinition(new[] { origin, corner, goal }, Array.Empty<TowerDefinition>(), members, cells));
            sim.EnableContinuousMovement(.96f,.2f, false, true); sim.Step(.02f);
            var outside = new Vector3(2,.25f,-3);
            Assert.That(sim.RequestMovement(new MovementRequest(outside)), Is.True);
            Assert.That(sim.SquadDestination.z, Is.LessThan(0));
            Assert.That(sim.SquadDestination.z, Is.GreaterThan(-.5f));
            Assert.That(sim.SquadDestination, Is.Not.EqualTo(outside));
            for (var i = 0; i < 1500 && sim.IsSquadMoving; i++)
            {
                sim.Step(.02f);
                foreach (var unit in sim.Units) Assert.That(cells.Contains(unit.OccupiedCell), Is.True);
            }
            Assert.That(sim.IsSquadMoving, Is.False);
            Assert.That(sim.RequestMovement(new MovementRequest(goal)), Is.True);
            for (var i = 0; i < 2000 && sim.IsSquadMoving; i++)
            {
                sim.Step(.02f);
                foreach (var unit in sim.Units) Assert.That(cells.Contains(unit.OccupiedCell), Is.True);
            }
            Assert.That(sim.IsSquadMoving, Is.False);
            Assert.That(sim.Outcome == BattleOutcome.Victory || sim.IsSquadReadyToCapture, Is.True);
        }
    }
}
