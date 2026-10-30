using System;
using NUnit.Framework;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class UnitDetailsTextTests
    {
        [TestCase(UnitRole.Attack, "Attack: 1", "HP: 4 / 4")]
        [TestCase(UnitRole.Tank, "Defense: 20% damage reduction", "HP: 8 / 8")]
        [TestCase(UnitRole.Heal, "Healing: 0.4 HP / 2 s", "HP: 3 / 3")]
        [TestCase(UnitRole.Captain, "Defense: 50% damage reduction", "HP: 6 / 6")]
        public void DetailsUseTheSelectedMembersActualRules(UnitRole role, string expected, string hp)
        {
            var sim = new BattleSimulation(new BattleDefinition(new[] { Vector3.zero, Vector3.right * 10 },
                Array.Empty<TowerDefinition>(), new[] { new MemberSpawnDefinition(731, 4, SquadCombatRules.Create(role)) }));
            sim.Step(0.02f);
            var unit = sim.Units[0];
            var before = (unit.Hp, unit.Position, unit.AttackCooldown, unit.SlotId);
            var text = UnitDetailsText.Format(unit);
            Assert.That(text, Does.Contain(expected));
            Assert.That(text, Does.Contain(hp));
            Assert.That(text, Does.Contain("Move speed: 0.5 units/s"));
            Assert.That((unit.Hp, unit.Position, unit.AttackCooldown, unit.SlotId), Is.EqualTo(before));
        }
    }
}
