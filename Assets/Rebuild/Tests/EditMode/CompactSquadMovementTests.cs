using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class CompactSquadMovementTests
    {
        private static Vector3 At(float x, float z) => new Vector3(x, .25f, z);
        private static BattleSimulation Create(bool block = false, bool passage = false, float spacing = .45f)
        {
            var cells = new List<Vector3Int>();
            for (var x = -3; x <= 12; x++) for (var z = -5; z <= 7; z++)
            {
                if (block && x >= 2 && x <= 4 && z >= -1 && z <= 1) continue;
                if (passage && x == 3 && z != 0) continue;
                cells.Add(new Vector3Int(x, 0, z));
            }
            var slots = new[] { 4, 7, 3, 1 };
            var members = slots.Select((slot, i) => new MemberSpawnDefinition(111 + i, slot, SquadCombatRules.Create(UnitRole.Tank)));
            var sim = new BattleSimulation(new BattleDefinition(new[] { At(0, 0), At(0, -1) }, new TowerDefinition[0], members, cells));
            sim.EnableContinuousMovement(spacing, .3f, true); sim.Step(.02f);
            return sim;
        }
        private static void Finish(BattleSimulation sim)
        {
            for (var tick = 0; tick < 5000 && sim.IsSquadMoving; tick++) sim.Step(.02f);
            Assert.That(sim.IsSquadMoving, Is.False, string.Join("; ", sim.Units.Select(u => u.Position)));
        }

        [Test]
        public void TightSpacingAllowsMembersToCrossAndSharePositionsWithoutShrinkingObstacleRadius()
        {
            Assert.That(BattleSimulation.ValidContinuousBodySettings(.45f, .3f), Is.False);
            Assert.That(BattleSimulation.ValidContinuousBodySettings(.45f, .3f, true), Is.True);
            var sim = Create();
            var stationary = sim.Units.Skip(1).Select(u => u.Position).ToArray();
            Assert.That(sim.RequestMovement(new MovementRequest(sim.Units[1].Position, sim.Units[0].Id)), Is.True);
            Finish(sim);
            Assert.That(Vector3.Distance(sim.Units[0].Position, sim.Units[1].Position), Is.LessThan(.001f));
            Assert.That(sim.Units.Skip(1).Select(u => u.Position), Is.EqualTo(stationary));
            foreach (var unit in sim.Units) Assert.That(unit.CollisionRadius, Is.EqualTo(.3f));
        }

        [Test]
        public void ObstacleDetourSharesOneSideAndKeepsMembersCloseThroughoutMovement()
        {
            var sim = Create(block: true);
            Assert.That(sim.RequestMovement(new MovementRequest(At(8, 0))), Is.True);
            var sides = new HashSet<int>();
            var maximumSpread = 0f;
            for (var tick = 0; tick < 5000 && sim.IsSquadMoving; tick++)
            {
                var previous = sim.Units.Select(u => u.Position).ToArray();
                sim.Step(.02f);
                foreach (var a in sim.Units)
                {
                    foreach (var b in sim.Units) maximumSpread = Mathf.Max(maximumSpread, Vector3.Distance(a.Position, b.Position));
                    if (a.Position.x > 2 && a.Position.x < 4)
                    { Assert.That(Mathf.Abs(a.Position.z), Is.GreaterThanOrEqualTo(1.799f)); sides.Add(a.Position.z > 0 ? 1 : -1); }
                }
                for (var i = 0; i < previous.Length; i++)
                    Assert.That(Vector3.Distance(previous[i], sim.Units[i].Position), Is.LessThanOrEqualTo(sim.Units[i].Speed * .02f + .0001f));
            }
            Assert.That(sim.IsSquadMoving, Is.False);
            Assert.That(sides.Count, Is.EqualTo(1), "Members should not split around opposite sides of the obstacle.");
            Assert.That(maximumSpread, Is.LessThanOrEqualTo(1.35f), "Leader must wait instead of continuing away from the group.");
            foreach (var unit in sim.Units) Assert.That(Vector3.Distance(unit.Position, At(8, 0) + unit.FormationOffset), Is.LessThan(.001f));
            for (var tick = 0; tick < 150; tick++) sim.Step(.02f);
            Assert.That(Vector3.Distance(sim.SquadReferencePosition, At(8, 0)), Is.LessThan(.001f), "The reference must catch up when it trails behind the squad.");
        }

        [Test]
        public void SingleCellDoorAllowsOverlapAndRestoresCompactSlotsAfterPassage()
        {
            var sim = Create(passage: true);
            Assert.That(sim.RequestMovement(new MovementRequest(At(8, 0))), Is.True);
            for (var tick = 0; tick < 5000 && sim.IsSquadMoving; tick++)
            {
                sim.Step(.02f);
                foreach (var unit in sim.Units)
                    if (Mathf.Abs(unit.Position.x - 3) < .79f) Assert.That(Mathf.Abs(unit.Position.z), Is.LessThanOrEqualTo(.201f));
            }
            Assert.That(sim.IsSquadMoving, Is.False);
            foreach (var unit in sim.Units) Assert.That(Vector3.Distance(unit.Position, At(8, 0) + unit.FormationOffset), Is.LessThan(.001f));
        }

        [Test]
        public void OpenGroundKeepsSlightlyWiderSpacingThroughoutMovement()
        {
            var sim = Create(spacing: .6f);
            Assert.That(sim.RequestMovement(new MovementRequest(At(8, 0))), Is.True);
            for (var tick = 0; tick < 100; tick++)
            {
                sim.Step(.02f);
                for (var i = 1; i < sim.Units.Count; i++)
                    Assert.That(Vector3.Distance(sim.Units[i].Position - sim.Units[0].Position,
                        sim.Units[i].FormationOffset - sim.Units[0].FormationOffset), Is.LessThan(.001f));
            }
            Finish(sim);
        }

        [Test]
        public void WiderFormationContractsThroughDoorThenExpandsBeforeDestination()
        {
            var sim = Create(passage: true, spacing: .6f);
            Assert.That(sim.RequestMovement(new MovementRequest(At(8, 0))), Is.True);
            var expandedAfterDoor = false;
            for (var tick = 0; tick < 5000 && sim.IsSquadMoving; tick++)
            {
                sim.Step(.02f);
                foreach (var unit in sim.Units)
                {
                    if (Mathf.Abs(unit.Position.x - 3) < .79f) Assert.That(Mathf.Abs(unit.Position.z), Is.LessThanOrEqualTo(.201f));
                    if (unit.Position.x > 4.5f && unit.Position.x < 6.5f && Mathf.Abs(unit.Position.z) > .5f)
                        expandedAfterDoor = true;
                }
            }
            Assert.That(sim.IsSquadMoving, Is.False);
            Assert.That(expandedAfterDoor, Is.True, "Squad should expand on clear ground while still travelling.");
            foreach (var unit in sim.Units) Assert.That(Vector3.Distance(unit.Position, At(8, 0) + unit.FormationOffset), Is.LessThan(.001f));
        }

        [Test]
        public void PauseCheckpointAndFormationExchangePreserveCompactMode()
        {
            var sim = Create(block: true);
            sim.RequestMovement(new MovementRequest(At(8, 0)));
            for (var i = 0; i < 40; i++) sim.Step(.02f);
            sim.SetPaused(true); var positions = sim.Units.Select(u => u.Position).ToArray();
            sim.Step(.5f); Assert.That(sim.Units.Select(u => u.Position), Is.EqualTo(positions));
            var restored = sim.CaptureCheckpoint().Restore(); restored.SetPaused(false); Finish(restored);
            Assert.That(restored.TryApplyFormation(new[] { 0, 1, 2, 4, 3, 5, 6, 7, 8 }), Is.True);
            Finish(restored);
            foreach (var unit in restored.Units) Assert.That(Vector3.Distance(unit.Position, At(8, 0) + unit.FormationOffset), Is.LessThan(.001f));
        }

        [Test]
        public void BlockedTerrainStillRejectsMovementWithoutTeleporting()
        {
            var sim = Create(block: true); var positions = sim.Units.Select(u => u.Position).ToArray();
            Assert.That(sim.RequestMovement(new MovementRequest(At(3, 0))), Is.False);
            sim.Step(.1f); Assert.That(sim.Units.Select(u => u.Position), Is.EqualTo(positions));
        }
    }
}
