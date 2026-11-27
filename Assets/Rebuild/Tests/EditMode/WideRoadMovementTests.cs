using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class WideRoadMovementTests
    {
        private static Vector3 At(float x, float z) => new Vector3(x, .25f, z);
        private static BattleSimulation Setup(float speed = 1.28f, bool meteor = false)
        {
            var cells = new List<Vector3Int>();
            for (var x = -2; x <= 22; x++) for (var z = -2; z <= 2; z++) cells.Add(new Vector3Int(x, 0, z));
            // Three Attack cards each spawn two, three Tanks fill the remaining positions.
            var members = Enumerable.Range(0, 6).Select(i => new MemberSpawnDefinition(i + 1, i,
                SquadCombatRules.Create(i < 3 ? UnitRole.Attack : UnitRole.Tank))).ToArray();
            var sim = new BattleSimulation(new BattleDefinition(new[] { At(0, 0), At(20, 0) }, Array.Empty<TowerDefinition>(), members, cells));
            sim.EnableContinuousMovement(.86f, .18f, false, true, speed);
            if (meteor) sim.AddTacticalTrap(new TacticalTrapDefinition(-10001, TacticalTrapKind.Meteor,
                At(6, -2), 1.65f, .72f, 12f, Vector3.forward));
            sim.Step(.02f);
            return sim;
        }
        private static void Advance(BattleSimulation sim, float seconds)
        { for (var i = 0; i < Mathf.RoundToInt(seconds / .02f); i++) sim.Step(.02f); }

        [Test]
        public void FullFormationCanMoveSidewaysAndPreserveClearance()
        {
            var sim = Setup();
            Assert.That(sim.AliveCount, Is.EqualTo(9));
            Assert.That(sim.RequestMovement(new MovementRequest(At(0, 1.25f))), Is.True);
            Advance(sim, 4f);
            Assert.That(sim.Units.All(u => Mathf.Abs(u.Position.z - (1.25f + u.FormationOffset.z)) < .06f), Is.True);
            Assert.That(sim.RequestMovement(new MovementRequest(At(4, 1.25f))), Is.True);
            Advance(sim, 6f);
            Assert.That(sim.Units.All(u => Mathf.Abs(u.Position.x - (4f + u.FormationOffset.x)) < .06f), Is.True);
        }
        [Test]
        public void MeteorPunishesStraightMarchButOppositeLaneIsSafe()
        {
            var blind = Setup(meteor: true); var dodge = Setup(meteor: true);
            Assert.That(blind.RequestMovement(new MovementRequest(At(12, 0))), Is.True);
            Assert.That(dodge.RequestMovement(new MovementRequest(At(0, 1.25f))), Is.True);
            Advance(dodge, 3f);
            Assert.That(dodge.RequestMovement(new MovementRequest(At(12, 1.25f))), Is.True);
            Advance(blind, 13f); Advance(dodge, 13f);
            Assert.That(blind.Units.Any(u => u.Hp < u.MaxHp), Is.True);
            Assert.That(dodge.Units.All(u => u.Hp == u.MaxHp), Is.True);
        }
        [Test]
        public void CampaignSpeedMultiplierSurvivesCheckpoint()
        {
            var baseline = Setup(1f); var faster = Setup();
            Assert.That(baseline.RequestMovement(new MovementRequest(At(10, 0))), Is.True);
            Assert.That(faster.RequestMovement(new MovementRequest(At(10, 0))), Is.True);
            var restored = faster.CaptureCheckpoint().Restore();
            Advance(baseline, 1f); Advance(faster, 1f); Advance(restored, 1f);
            Assert.That(faster.SquadReferencePosition.x, Is.GreaterThan(baseline.SquadReferencePosition.x));
            Assert.That(restored.SquadReferencePosition, Is.EqualTo(faster.SquadReferencePosition));
        }
    }
}
