using NUnit.Framework;
using Team5.Rebuild.Editor;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class StageV3LayoutTests
    {
        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        [TestCase(5)] [TestCase(6)] [TestCase(7)]
        public void AuthoredLevelHasAReachableFullSquadRoute(int stage)
        {
            var problems = ClickMoveStageInstaller.ValidateCatalogGeometry(stage);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [TestCase(1, 75, 19, 3, 3)]
        [TestCase(2, 66, 26, 4, 4)]
        [TestCase(3, 58, 30, 7, 5)]
        [TestCase(4, 44, 39, 10, 7)]
        [TestCase(5, 35, 40, 15, 10)]
        [TestCase(6, 25, 42, 21, 12)]
        [TestCase(7, 15, 42, 28, 15)]
        public void SummonTierRarityWeightsStayCentralizedInSimulation(int tier,
            int common, int rare, int epic, int legendary)
        {
            var odds = SquadCombatRules.GetRarityWeightsForTier(tier);
            Assert.That(odds, Is.EqualTo((common, rare, epic, legendary)));
            Assert.That(odds.Item1 + odds.Item2 + odds.Item3 + odds.Item4, Is.EqualTo(100));
        }

        [Test]
        public void TowerShapesContainNoLegacySquareAsDefault()
        {
            Assert.That(SquadCombatRules.IsCircle(AttackArea.Circle), Is.True);
            Assert.That(SquadCombatRules.IsCone(AttackArea.ConePositiveZ), Is.True);
            Assert.That(SquadCombatRules.IsLaser(AttackArea.NegativeX), Is.True);
            Assert.That(SquadCombatRules.IsLaser(AttackArea.ConeNegativeZ), Is.False);
            Assert.That(SquadCombatRules.Contains(new Vector3(4, 0, 1), AttackArea.ConePositiveX, 5, .6f), Is.True);
            Assert.That(SquadCombatRules.Contains(new Vector3(-4, 0, 1), AttackArea.ConePositiveX, 5, .6f), Is.False);
        }
    }
}
