using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using Team5.Rebuild.Editor.LevelAuthoring;
using UnityEditor;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class WoodenGateTests
    {
        [Test]
        public void MinionStaysSingleCellAndVulnerableToNormalAttacksAfterGateIntegration()
        {
            var minion = new TowerDefinition(1, new Vector3(3, .25f, 0), 0, 0, 1, TargetRule.Nearest,
                maxHp: 4, isMinion: true);
            Assert.That(minion.IsWall && minion.IsMinion, Is.True);
            Assert.That(minion.IsWoodenGate || minion.RequiresExplosiveDamage, Is.False);
            Assert.That(minion.Footprint, Is.EqualTo(Vector2Int.one));
            var sim = new BattleSimulation(new BattleDefinition(new[] { new Vector3(0, .25f, 0), new Vector3(12, .25f, 0) },
                new[] { minion }, new[] { new MemberSpawnDefinition(1, 4, SquadCombatRules.Create(UnitRole.Attack)) }));
            for (var i = 0; i < 2000 && sim.Outcome == BattleOutcome.Running && sim.Towers[0].IsAlive; i++) sim.Step(.02f);
            Assert.That(sim.Towers[0].Hp, Is.Zero);
            Assert.That(sim.GetCellOccupancy(new Vector3Int(3, 0, 0)).TowerIds, Is.Empty);
        }
        [TestCase(UnitRole.Attack)]
        [TestCase(UnitRole.Sniper)]
        [TestCase(UnitRole.Vanguard)]
        [TestCase(UnitRole.Demolisher)]
        public void NormalAndExplosiveAttacksDestroyGateAndClearItsBlock(UnitRole role)
        {
            var gate = new TowerDefinition(1, new Vector3(5, .25f, 0), 0, 0, 1, TargetRule.Nearest,
                maxHp: 4, isWall: true, wallAlongZ: true, isWoodenGate: true);
            var sim = new BattleSimulation(new BattleDefinition(new[] { new Vector3(0, .25f, 0), new Vector3(12, .25f, 0) },
                new[] { gate }, new[] { new MemberSpawnDefinition(1, 4, SquadCombatRules.Create(role)) }));
            for (var i = 0; i < 2000 && sim.Outcome == BattleOutcome.Running; i++)
            {
                var wasAlive = sim.Towers[0].IsAlive;
                sim.Step(.02f);
                if (wasAlive) Assert.That(sim.Units[0].RouteProgress, Is.LessThanOrEqualTo(4.06f));
            }
            Assert.That(sim.Towers[0].Hp, Is.Zero);
            Assert.That(sim.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(sim.TowerBountyGoldEarned, Is.Zero);
            Assert.That(sim.TowersDestroyedCount, Is.Zero);
            Assert.That(sim.GetCellOccupancy(new Vector3Int(5, 0, 0)).TowerIds, Is.Empty);
        }

        [TestCase("X", 3, 1)]
        [TestCase("Z", 1, 3)]
        public void PrefabIsDistinctTimberModelWithNormalDamageEnabled(string axis, int width, int depth)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapBuilderUtility.Kit + "Towers/WoodenGate_3x1_" + axis + ".prefab");
            Assert.That(prefab, Is.Not.Null);
            var tower = prefab.GetComponent<BattleTower>();
            Assert.That(tower.IsWall && tower.IsWoodenGate, Is.True);
            Assert.That(tower.Footprint, Is.EqualTo(new Vector2Int(width, depth)));
            Assert.That(tower.CaptureDefinition(1).RequiresExplosiveDamage, Is.False);
            Assert.That(tower.Range, Is.Zero);
            Assert.That(tower.GoldBounty, Is.Zero);
            Assert.That(prefab.GetComponentsInChildren<Renderer>().Length, Is.GreaterThan(10));
            Assert.That(prefab.GetComponentsInChildren<Transform>().Any(t => t.name.Contains("Timber Plank")), Is.True);
        }
    }
}
