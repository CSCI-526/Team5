using NUnit.Framework;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class CampaignTankPressureTests
    {
        private static BattleSimulation Make(UnitDefinition unit, bool pressure, bool minion = false)
        {
            var tower = new TowerDefinition(1, new Vector3(0,.25f,3), 5f, 4.19f, 100f,
                TargetRule.Nearest, maxHp: 1000f, area: AttackArea.Circle, isMinion: minion);
            var sim = new BattleSimulation(new BattleDefinition(new[] { new Vector3(0,.25f,0), new Vector3(12,.25f,0) },
                new[] { tower }, new[] { new MemberSpawnDefinition(1, 4, unit) }));
            sim.EnableContinuousMovement();
            if (pressure) sim.EnableCampaignTankPressure();
            sim.Step(.02f);
            return sim;
        }
        [TestCase(UnitRarity.Common, 6)]
        [TestCase(UnitRarity.Rare, 7)]
        [TestCase(UnitRarity.Epic, 8)]
        public void TowerShotUsesCappedTankPressure(UnitRarity rarity, int level)
        {
            var unit = SquadCombatRules.Create(UnitRole.Tank, rarity, level);
            var sim = Make(unit, true);
            sim.Step(.5f);
            var expected = TacticalStageRules.TankHitDamage(4.19f, unit.MaxHp, unit.Defense);
            Assert.That(unit.MaxHp - sim.Units[0].Hp, Is.EqualTo(expected).Within(.0001f));
            Assert.That(expected, Is.GreaterThan(4.19f * (1f - unit.Defense)));
            Assert.That(expected, Is.LessThanOrEqualTo(4.19f * 2.25f));
        }
        [Test]
        public void TutorialAndMinionAttacksKeepNormalDefense()
        {
            var unit = SquadCombatRules.Create(UnitRole.Tank, UnitRarity.Rare, 7);
            foreach (var sim in new[] { Make(unit, false), Make(unit, true, true) })
            {
                sim.Step(.5f);
                Assert.That(unit.MaxHp - sim.Units[0].Hp, Is.EqualTo(4.19f * (1f - unit.Defense)).Within(.0001f));
            }
        }
        [Test]
        public void HazardsAndNonTanksDoNotReceiveTankBonus()
        {
            var tank = SquadCombatRules.Create(UnitRole.Tank, UnitRarity.Rare, 7);
            var sim = Make(tank, true);
            var hp = sim.Units[0].Hp;
            Assert.That(sim.TryApplyEnvironmentalDamage(sim.Units[0].Position, 1f, 1f, 1), Is.True);
            Assert.That(hp - sim.Units[0].Hp, Is.EqualTo(1f - tank.Defense).Within(.0001f));
            var attack = SquadCombatRules.Create(UnitRole.Attack, UnitRarity.Rare, 7);
            sim = Make(attack, true); sim.Step(.5f);
            Assert.That(attack.MaxHp - sim.Units[0].Hp, Is.EqualTo(4.19f * (1f - attack.Defense)).Within(.0001f));
        }
        [Test]
        public void CheckpointRestoresPressureForInFlightProjectiles()
        {
            var tank = SquadCombatRules.Create(UnitRole.Tank, UnitRarity.Rare, 7);
            var sim = Make(tank, true).CaptureCheckpoint().Restore();
            sim.Step(.5f);
            Assert.That(tank.MaxHp - sim.Units[0].Hp,
                Is.EqualTo(TacticalStageRules.TankHitDamage(4.19f, tank.MaxHp, tank.Defense)).Within(.0001f));
        }
    }
}
