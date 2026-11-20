using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class ClickMoveRedesignTests
    {
        private static BattleDefinition Definition(params MemberSpawnDefinition[] squad)
        {
            return new BattleDefinition(new[] { new Vector3(0, .25f, 0), new Vector3(30, .25f, 0) },
                Array.Empty<TowerDefinition>(), squad);
        }

        [Test]
        public void SixDeploymentCardsCanFieldMoreSoldiersDependingOnRole()
        {
            Assert.That(SquadPreparation.MaxDeployed, Is.EqualTo(6));
            Assert.That(SquadCombatRules.StartingSummonCount(UnitRole.Sniper), Is.EqualTo(1));
            Assert.That(SquadCombatRules.StartingSummonCount(UnitRole.Vanguard), Is.EqualTo(2));
            var sim = new BattleSimulation(Definition(
                new MemberSpawnDefinition(1, 4, SquadCombatRules.Create(UnitRole.Captain)),
                new MemberSpawnDefinition(2, 5, SquadCombatRules.Create(UnitRole.Vanguard)),
                new MemberSpawnDefinition(3, 3, SquadCombatRules.Create(UnitRole.Sniper))));
            sim.Step(.01f);
            Assert.That(sim.SpawnedCount, Is.EqualTo(4));
            Assert.That(sim.PendingSpawnCount, Is.Zero);
            Assert.That(sim.Units.Count(u => u.Role == UnitRole.Vanguard), Is.EqualTo(2));
            Assert.That(sim.Units.Where(u => u.IsAlive).Select(u => u.SlotId).Distinct().Count(), Is.EqualTo(4));
            Assert.That(sim.TryApplyFormation(Enumerable.Range(0, 9).ToArray()), Is.False);
        }

        [Test]
        public void CaptainCallsReinforcementsAndRespectsCooldown()
        {
            var sim = new BattleSimulation(new BattleDefinition(
                new[] { new Vector3(0, .25f, 0), new Vector3(30, .25f, 0) },
                Array.Empty<TowerDefinition>(),
                new[] { new MemberSpawnDefinition(1, 4, SquadCombatRules.Create(UnitRole.Captain)) },
                null, 6));
            sim.Step(.01f);
            Assert.That(sim.CanUseLegendarySkill(1), Is.True);
            Assert.That(sim.TryUseLegendarySkill(1, Vector3.zero), Is.True);
            Assert.That(sim.AliveCount, Is.EqualTo(2));
            Assert.That(new[] { UnitRole.Attack, UnitRole.Tank, UnitRole.Heal,
                UnitRole.Sniper, UnitRole.Vanguard, UnitRole.Demolisher }.Contains(sim.Units[1].Role), Is.True,
                "Captain reinforcement must be a regular unit role.");
            Assert.That(sim.Units[1].Definition.Level, Is.EqualTo(6),
                "Captain must summon at current summon tier, not Captain level.");
            Assert.That(sim.Units[1].Definition.Rarity, Is.Not.EqualTo(UnitRarity.Legendary));
            Assert.That(sim.CanUseLegendarySkill(1), Is.False);
            Assert.That(sim.Units[0].SkillCooldownRemaining, Is.EqualTo(LegendarySkillRules.CaptainCooldown));
        }

        [Test]
        public void CaptainStrikesTowerWhenBattlefieldIsFull()
        {
            var six = new[]
            {
                new MemberSpawnDefinition(1, 4, SquadCombatRules.Create(UnitRole.Captain)),
                new MemberSpawnDefinition(2, 0, SquadCombatRules.Create(UnitRole.Attack)),
                new MemberSpawnDefinition(3, 1, SquadCombatRules.Create(UnitRole.Attack)),
                new MemberSpawnDefinition(4, 2, SquadCombatRules.Create(UnitRole.Attack)),
                new MemberSpawnDefinition(5, 3, SquadCombatRules.Create(UnitRole.Vanguard)),
                new MemberSpawnDefinition(6, 5, SquadCombatRules.Create(UnitRole.Vanguard))
            };
            var sim = new BattleSimulation(new BattleDefinition(
                new[] { new Vector3(0, .25f, 0), new Vector3(30, .25f, 0) },
                new[] { new TowerDefinition(1, new Vector3(2, .25f, 2), 2.5f, .1f, 5,
                    TargetRule.Nearest) }, six));
            sim.Step(.01f);
            Assert.That(sim.AliveCount, Is.EqualTo(9));
            var hp = sim.Towers[0].Hp;
            Assert.That(sim.TryUseLegendarySkill(1, Vector3.zero), Is.True);
            Assert.That(sim.AliveCount, Is.EqualTo(9));
            Assert.That(sim.Towers[0].Hp, Is.LessThan(hp));
        }

        [Test]
        public void LegendaryRatesAreImprovedAndSumToOneHundredPercent()
        {
            var preparation = new SquadPreparation(new[]
            { new MemberSpawnDefinition(1, 4, SquadCombatRules.Create(UnitRole.Captain)) });
            var shop = new ClaimCardShop(new CampaignWallet(unlimited: true), preparation);
            Assert.That(shop.GetRarityWeights(), Is.EqualTo((75, 19, 3, 3)));
            shop.SetStageProgress(7);
            var weights = shop.GetRarityWeights();
            Assert.That(weights.legendary, Is.EqualTo(15));
            Assert.That(weights.common + weights.rare + weights.epic + weights.legendary, Is.EqualTo(100));
        }

        [Test]
        public void ClickingGoalFinishesContinuousBattleInsteadOfAutoMarching()
        {
            var allowed = new List<Vector3Int>();
            for (var x = -3; x <= 6; x++) for (var z = -3; z <= 3; z++)
                allowed.Add(new Vector3Int(x, 0, z));
            var sim = new BattleSimulation(new BattleDefinition(
                new[] { new Vector3(0, .25f, 0), new Vector3(3, .25f, 0) },
                Array.Empty<TowerDefinition>(),
                new[] { new MemberSpawnDefinition(1, 4, SquadCombatRules.Create(UnitRole.Captain)) }, allowed));
            sim.EnableContinuousMovement(.48f, .2f, true);
            sim.Step(.01f);
            Assert.That(sim.Units[0].Position.x, Is.EqualTo(0).Within(.001));
            sim.Step(2f);
            Assert.That(sim.Units[0].Position.x, Is.EqualTo(0).Within(.001), "No auto march without a click.");
            Assert.That(sim.RequestMovement(new MovementRequest(new Vector3(3, .25f, 0))), Is.True);
            for (var i = 0; i < 150 && sim.Outcome == BattleOutcome.Running; i++) sim.Step(.1f);
            Assert.That(sim.Outcome, Is.EqualTo(BattleOutcome.Victory));
        }
    }
}
