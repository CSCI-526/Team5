using NUnit.Framework;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class TowerRangeTests
    {
        [Test]
        public void SquareIncludesCornersAndUsesHorizontalWorldDistance()
        {
            Assert.That((int)AttackArea.Square, Is.Zero, "Existing Scene enum value must keep its meaning.");
            Assert.That(SquadCombatRules.Contains(new Vector3(4.5f, 7, 4.5f), AttackArea.Square, 4.5f, 1), Is.True);
            Assert.That(SquadCombatRules.Contains(new Vector3(-4.5f, 0, -4.5f), AttackArea.Square, 4.5f, 1), Is.True);
            Assert.That(SquadCombatRules.Contains(new Vector3(4.501f, 0, 0), AttackArea.Square, 4.5f, 1), Is.False);
            Assert.That(SquadCombatRules.Contains(new Vector3(0, 0, -4.501f), AttackArea.Square, 4.5f, 1), Is.False);
        }

        [TestCase(AttackArea.Square)]
        [TestCase(AttackArea.PositiveX)]
        [TestCase(AttackArea.NegativeX)]
        [TestCase(AttackArea.PositiveZ)]
        [TestCase(AttackArea.NegativeZ)]
        public void DisplayBoundsMatchTargetingForEveryArea(AttackArea area)
        {
            var origin = new Vector3(10, 0.25f, 20);
            var bounds = SquadCombatRules.AreaBounds(origin, area, 3, 1);
            for (var z = -4f; z <= 4; z += 0.25f)
            for (var x = -4f; x <= 4; x += 0.25f)
            {
                var offset = new Vector3(x, 0, z);
                Assert.That(bounds.Contains(origin + offset), Is.EqualTo(SquadCombatRules.Contains(offset, area, 3, 1)));
            }
        }

        [Test]
        public void TowerActuallyAttacksMemberInsideSquareCorner()
        {
            var tower = new TowerDefinition(1, new Vector3(3, 0.25f, 3), 3, 1, 1, TargetRule.Nearest);
            var sim = new BattleSimulation(new BattleDefinition(new[] { new Vector3(0, 0.25f, 0), new Vector3(10, 0.25f, 0) },
                new[] { tower }, new[] { new MemberSpawnDefinition(1, 4, new UnitDefinition(4, 0.01f)) }));
            sim.Step(0.02f);
            Assert.That(sim.Towers[0].TargetId, Is.EqualTo(1));
            Assert.That(sim.Projectiles.Count, Is.EqualTo(1));
            for (var i = 0; i < 30; i++) sim.Step(0.02f);
            Assert.That(sim.Units[0].Hp, Is.EqualTo(3));
        }
    }
}
