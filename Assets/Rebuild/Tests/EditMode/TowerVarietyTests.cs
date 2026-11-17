using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using Team5.Rebuild.Session;
using Object = UnityEngine.Object;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class TowerVarietyTests
    {
        private static BattleSimulation Make(TowerKind kind, params int[] slots)
        {
            var unit = new UnitDefinition(100, .1f, UnitRole.Tank, healPerSecond: 0);
            var tower = new TowerDefinition(1, new Vector3(-2, .25f, 0), 8,
                kind == TowerKind.Frost ? .5f : kind == TowerKind.Laser ? .6f : 2,
                kind == TowerKind.Frost ? 1 : 2, TargetRule.Nearest, maxHp: 100,
                area: kind == TowerKind.Laser ? AttackArea.PositiveX : AttackArea.Square,
                attackWidth: .65f, isAoe: kind == TowerKind.Blast, kind: kind);
            return new BattleSimulation(new BattleDefinition(
                new[] { new Vector3(0, .25f, 0), new Vector3(100, .25f, 0) },
                new[] { tower }, slots.Select((slot, i) => new MemberSpawnDefinition(i + 1, slot, unit))));
        }
        private static void Tick(BattleSimulation sim, int count)
        {
            for (var i = 0; i < count; i++) sim.Step(.02f);
        }
        [Test]
        public void CampaignPreservesTowerCountsAndIntroducesTypesProgressively()
        {
            var counts = new[] { 1, 2, 6, 7, 5, 6, 8 };
            for (var level = 1; level <= 7; level++)
            {
                EditorSceneManager.OpenScene(BattleSceneFlow.SceneForLevel(level));
                var towers = Object.FindFirstObjectByType<BattleSession>().Towers.Where(t => !t.IsWall)
                    .Select((t, i) => t.CaptureDefinition(i + 1)).ToArray();
                Assert.That(towers.Length, Is.EqualTo(counts[level - 1]), "Level " + level);
                Assert.That(towers.Any(t => t.Kind == TowerKind.Laser), Is.EqualTo(level >= 2));
                Assert.That(towers.Any(t => t.Kind == TowerKind.Frost), Is.EqualTo(level >= 4));
                Assert.That(towers.Any(t => t.Kind == TowerKind.Blast), Is.EqualTo(level >= 5));
                Assert.That(towers.Any(t => t.Kind == TowerKind.Standard && t.Area == AttackArea.Square), Is.EqualTo(level >= 3));
                Assert.That(towers.Any(t => t.Kind == TowerKind.Standard && t.Area != AttackArea.Square), Is.True);
                var cells = Object.FindFirstObjectByType<BattleSession>().Grid.SceneCells
                    .Where(c => c.Kind == Team5.Rebuild.World.CellKind.Road || c.Kind == Team5.Rebuild.World.CellKind.Spawn || c.Kind == Team5.Rebuild.World.CellKind.Goal).ToArray();
                foreach (var tower in towers.Where(t => t.Area != AttackArea.Square))
                {
                    var inLane = cells.Where(c => SquadCombatRules.Contains(c.transform.position - tower.Position, tower.Area, tower.Range, tower.AttackWidth)).ToArray();
                    Assert.That(inLane, Is.Not.Empty, $"Level {level} tower {tower.Id} points at empty ground");
                    if (tower.Kind == TowerKind.Laser)
                    {
                        var normal = towers.First(t => t.Kind == TowerKind.Standard && t.Area != AttackArea.Square);
                        Assert.That(tower.Damage, Is.EqualTo(normal.Damage * 5f));
                        Assert.That(tower.AttackInterval, Is.EqualTo(3f));
                        var last = cells.Where(c => SquadCombatRules.Contains(c.transform.position - tower.Position, tower.Area, 200, tower.AttackWidth))
                            .Max(c => Vector3.Dot(c.transform.position - tower.Position, SquadCombatRules.Direction(tower.Area)));
                        Assert.That(tower.Range, Is.EqualTo(last + .5f).Within(.001f));
                    }
                }
                foreach (var tower in towers.Where(t => t.Kind == TowerKind.Frost))
                {
                    Assert.That(tower.Area, Is.EqualTo(AttackArea.Square));
                    Assert.That(tower.Damage, Is.EqualTo(towers.First(t => t.Kind == TowerKind.Standard && t.Area == AttackArea.Square).Damage));
                }
            }
        }

        [Test]
        public void LaserWarnsThenPiercesAlignedUnitsButMissesAdjacentRow()
        {
            var sim = Make(TowerKind.Laser, 3, 4, 5, 0);
            Tick(sim, 1);
            Assert.That(sim.Towers[0].LaserChargeRemaining, Is.GreaterThan(.7f));
            Assert.That(sim.Units.All(u => u.Hp == 100), Is.True);
            Tick(sim, 41);
            foreach (var unit in sim.Units.Take(3)) Assert.That(unit.Hp, Is.EqualTo(99.4f).Within(.001f));
            Assert.That(sim.Units[3].Hp, Is.EqualTo(100));
        }
        [Test]
        public void FrostSlowsOnlyItsTargetAndRepeatedHitsDoNotStack()
        {
            var sim = Make(TowerKind.Frost, 3, 4);
            Tick(sim, 30);
            var slowed = sim.Units.First(u => u.SlowRemaining > 0);
            Assert.That(slowed.EffectiveSpeed, Is.EqualTo(slowed.Speed * .75f));
            Assert.That(sim.Units.Count(u => u.SlowRemaining > 0), Is.EqualTo(1));
            Tick(sim, 60);
            Assert.That(slowed.EffectiveSpeed, Is.EqualTo(slowed.Speed * .75f));
            Assert.That(slowed.SlowRemaining, Is.InRange(0f, 2f));
        }
        [Test]
        public void SlowPausesAndSurvivesCheckpointThenExpiresWithoutMoreShots()
        {
            var sim = Make(TowerKind.Frost, 4);
            Tick(sim, 30);
            var before = sim.Units[0].SlowRemaining;
            Assert.That(before, Is.GreaterThan(0));
            sim.SetPaused(true); Tick(sim, 100);
            Assert.That(sim.Units[0].SlowRemaining, Is.EqualTo(before));
            var restored = sim.CaptureCheckpoint().Restore();
            Assert.That(restored.Units[0].SlowRemaining, Is.EqualTo(before));
            restored.SetPaused(false);
            restored.SetTowerPosition(1, new Vector3(-1000, .25f, 0));
            Tick(restored, 110);
            Assert.That(restored.Units[0].SlowRemaining, Is.Zero);
            Assert.That(restored.Units[0].EffectiveSpeed, Is.EqualTo(restored.Units[0].Speed));
        }
        [Test]
        public void BlastHitsNearbyUnitsButNotEntireCoverageArea()
        {
            var sim = Make(TowerKind.Blast, 3, 4, 5, 0);
            var explosions = 0;
            sim.BlastImpact += _ => explosions++;
            Tick(sim, 30);
            Assert.That(explosions, Is.EqualTo(1));
            Assert.That(sim.Units[0].Hp, Is.EqualTo(98));
            Assert.That(sim.Units[1].Hp, Is.EqualTo(98));
            Assert.That(sim.Units[2].Hp, Is.EqualTo(100));
            Assert.That(sim.Units[3].Hp, Is.EqualTo(98), "Diagonal neighbour is inside the 2x2 square, outside the former circle.");
        }
    }
}
