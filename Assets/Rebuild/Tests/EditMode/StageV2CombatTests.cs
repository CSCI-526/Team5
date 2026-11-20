using System;
using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class StageV2CombatTests
    {
        [Test]
        public void CircularTowerRadiusRejectsOutsideAndAcceptsInside()
        {
            Assert.That(SquadCombatRules.Contains(new Vector3(3f, 0, 0), AttackArea.Circle, 3.1f, .5f), Is.True);
            Assert.That(SquadCombatRules.Contains(new Vector3(3f, 0, 3f), AttackArea.Circle, 3.1f, .5f), Is.False);
        }

        [Test]
        public void ConeFacingMattersAndRejectsBackAndFlank()
        {
            Assert.That(SquadCombatRules.Contains(new Vector3(0, 0, 3), AttackArea.ConePositiveZ, 4, .5f), Is.True);
            Assert.That(SquadCombatRules.Contains(new Vector3(0, 0, -2), AttackArea.ConePositiveZ, 4, .5f), Is.False);
            Assert.That(SquadCombatRules.Contains(new Vector3(3, 0, 1), AttackArea.ConePositiveZ, 4, .5f), Is.False);
        }

        [Test]
        public void LasersUseNarrowStripOnly()
        {
            Assert.That(SquadCombatRules.IsLaser(AttackArea.PositiveX), Is.True);
            Assert.That(SquadCombatRules.IsLaser(AttackArea.ConePositiveX), Is.False);
            Assert.That(SquadCombatRules.Contains(new Vector3(4, 0, .2f), AttackArea.PositiveX, 6, .5f), Is.True);
            Assert.That(SquadCombatRules.Contains(new Vector3(4, 0, .8f), AttackArea.PositiveX, 6, .5f), Is.False);
        }

        [Test]
        public void CaptainRollUsesTierAndNeverSummonsUniqueLegendary()
        {
            var definition = new BattleDefinition(
                new[] { new Vector3(0, .25f, 0), new Vector3(40, .25f, 0) },
                Array.Empty<TowerDefinition>(),
                new[] { new MemberSpawnDefinition(1, 4, SquadCombatRules.Create(UnitRole.Captain, level: 2)) },
                null, 7);
            var sim = new BattleSimulation(definition);
            sim.Step(.01f);
            Assert.That(sim.TryUseLegendarySkill(1, Vector3.zero), Is.True);
            var summoned = sim.Units.Single(u => u.MemberId != 1);
            Assert.That(summoned.Definition.Level, Is.EqualTo(7));
            Assert.That(SquadCombatRules.IsLegendaryRole(summoned.Role), Is.False);
            Assert.That(summoned.Definition.Rarity, Is.Not.EqualTo(UnitRarity.Legendary));
        }
    }
}
