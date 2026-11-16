using System;
using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class LegendarySkillsTests
    {
        private static BattleSimulation CreateSimulation(params MemberSpawnDefinition[] members)
        {
            return new BattleSimulation(new BattleDefinition(
                new[] { new Vector3(0, .25f, 0), new Vector3(100, .25f, 0) },
                new[]
                {
                    new TowerDefinition(1, new Vector3(2, .25f, 2), 5, .01f, 1, TargetRule.Nearest),
                    new TowerDefinition(2, new Vector3(20, .25f, 20), 5, .01f, 1, TargetRule.Nearest)
                }, members));
        }

        [Test]
        public void CaptainAndThreeSupportsAreLegendaryAndNeverAutoAttack()
        {
            foreach (var role in new[] { UnitRole.Captain, UnitRole.Frostweaver, UnitRole.Warcaller, UnitRole.Aegis })
            {
                var unit = SquadCombatRules.Create(role);
                Assert.That(unit.Rarity, Is.EqualTo(UnitRarity.Legendary));
                Assert.That(unit.Damage, Is.Zero);
                Assert.That(SquadCombatRules.CanAttackTowers(role), Is.False);
            }
            Assert.Throws<ArgumentException>(() => SquadCombatRules.Create(UnitRole.Attack, UnitRarity.Legendary));
        }

        [Test]
        public void LegendsCannotBeClaimedOrDismissedTwiceAndGainLevelsByStage()
        {
            var roster = new SquadPreparation(new[]
            {
                new MemberSpawnDefinition(1, 4, SquadCombatRules.Create(UnitRole.Captain))
            });
            var frost = SquadCombatRules.Create(UnitRole.Frostweaver, UnitRarity.Legendary, 3);
            Assert.That(roster.TryAddSummonedUnit(frost), Is.True);
            Assert.That(roster.TryAddSummonedUnit(frost), Is.False);
            Assert.That(roster.TryDismissMember(2), Is.False);
            roster.AdvanceLegendaryLevels(2);
            Assert.That(roster.FindMember(1).Unit.Level, Is.EqualTo(3));
            Assert.That(roster.FindMember(2).Unit.Level, Is.EqualTo(5));
            roster.AdvanceLegendaryLevels(2);
            Assert.That(roster.FindMember(2).Unit.Level, Is.EqualTo(5));
            Assert.That(roster.FindMember(2).Unit.MaxHp, Is.GreaterThan(frost.MaxHp));
            roster.EnsureStartingCaptain();
            Assert.That(roster.Members.Count(member => member.Unit.Role == UnitRole.Captain), Is.EqualTo(1));
        }

        [Test]
        public void FrostweaverFreezesOnlyTowersInClickedRadiusAndUsesCooldown()
        {
            var sim = CreateSimulation(new MemberSpawnDefinition(1, 4, SquadCombatRules.Create(UnitRole.Frostweaver)));
            sim.Step(.01f);
            Assert.That(sim.TryUseLegendarySkill(1, new Vector3(90, .25f, 90)), Is.False);
            Assert.That(sim.TryUseLegendarySkill(1, new Vector3(2, .25f, 2)), Is.True);
            Assert.That(sim.Towers[0].FrozenRemaining, Is.GreaterThan(4f));
            Assert.That(sim.Towers[1].FrozenRemaining, Is.Zero);
            Assert.That(sim.TryUseLegendarySkill(1, new Vector3(2, .25f, 2)), Is.False);
            var towerAttacks = 0;
            sim.TowerAttacked += _ => towerAttacks++;
            sim.Step(1f);
            Assert.That(towerAttacks, Is.Zero, "Frozen towers cannot create shots.");
            Assert.That(sim.Units[0].SkillCooldownRemaining, Is.GreaterThan(28f));
            sim.SetPaused(true);
            var remaining = sim.Units[0].SkillCooldownRemaining;
            sim.Step(3f);
            Assert.That(sim.Units[0].SkillCooldownRemaining, Is.EqualTo(remaining));
            Assert.That(sim.CanUseLegendarySkill(1), Is.False);
        }

        [Test]
        public void WarcallerEmpowersAttackersAndAegisGivesLimitedHitBlocks()
        {
            var sim = CreateSimulation(
                new MemberSpawnDefinition(1, 3, SquadCombatRules.Create(UnitRole.Warcaller, level: 3)),
                new MemberSpawnDefinition(2, 4, SquadCombatRules.Create(UnitRole.Attack)),
                new MemberSpawnDefinition(3, 5, SquadCombatRules.Create(UnitRole.Aegis)));
            sim.Step(.01f);
            Assert.That(sim.TryUseLegendarySkill(1, Vector3.zero), Is.True);
            Assert.That(sim.Units.Single(unit => unit.Id == 2).DamageBuffMultiplier,
                Is.EqualTo(LegendarySkillRules.RallyMultiplier(3)).Within(.001f));
            Assert.That(sim.Units.Single(unit => unit.Id == 2).DamageBuffRemaining, Is.EqualTo(LegendarySkillRules.RallyDuration));
            Assert.That(sim.TryUseLegendarySkill(3, Vector3.zero), Is.True);
            foreach (var unit in sim.Units)
            {
                Assert.That(unit.BarrierHits, Is.EqualTo(2));
                Assert.That(unit.BarrierRemaining, Is.EqualTo(LegendarySkillRules.BarrierDuration));
            }
        }

        [Test]
        public void GuardianWardActuallyNegatesTwoTowerHitsButNotTheThird()
        {
            var sim = CreateSimulation(new MemberSpawnDefinition(1, 4, SquadCombatRules.Create(UnitRole.Aegis)));
            sim.Step(.01f);
            var originalHp = sim.Units[0].Hp;
            var blocked = 0;
            sim.TowerHitBlocked += (_, __) => blocked++;
            Assert.That(sim.TryUseLegendarySkill(1, Vector3.zero), Is.True);
            sim.Step(.5f);
            sim.Step(.6f);
            Assert.That(blocked, Is.EqualTo(2));
            Assert.That(sim.Units[0].Hp, Is.EqualTo(originalHp));
            sim.Step(1.1f);
            Assert.That(sim.Units[0].Hp, Is.LessThan(originalHp));
        }

        [Test]
        public void DeadLegendaryCannotCastAgain()
        {
            var fragile = new UnitDefinition(.1f, SquadCombatRules.Speed, UnitRole.Frostweaver,
                rarity: UnitRarity.Legendary);
            var definition = new BattleDefinition(
                new[] { new Vector3(0, .25f, 0), new Vector3(100, .25f, 0) },
                new[] { new TowerDefinition(1, new Vector3(2, .25f, 2), 5, 100, 1, TargetRule.Nearest) },
                new[] { new MemberSpawnDefinition(1, 4, fragile) });
            var sim = new BattleSimulation(definition);
            sim.Step(.01f);
            sim.Step(.5f);
            Assert.That(sim.Units[0].IsAlive, Is.False);
            Assert.That(sim.CanUseLegendarySkill(1), Is.False);
            Assert.That(sim.TryUseLegendarySkill(1, new Vector3(2, .25f, 2)), Is.False);
        }

        [Test]
        public void OffersCannotIncludeOwnedOrDuplicateLegendaryCharacters()
        {
            var roster = new SquadPreparation(new[]
            {
                new MemberSpawnDefinition(1, 4, SquadCombatRules.Create(UnitRole.Captain)),
                new MemberSpawnDefinition(2, 2, SquadCombatRules.Create(UnitRole.Frostweaver)),
                new MemberSpawnDefinition(3, 3, SquadCombatRules.Create(UnitRole.Warcaller)),
                new MemberSpawnDefinition(4, 5, SquadCombatRules.Create(UnitRole.Aegis))
            });
            var shop = new ClaimCardShop(new CampaignWallet(unlimited: true), roster);
            shop.SetStageProgress(7);
            Assert.That(shop.GetRarityWeights().legendary, Is.Zero);
            for (var i = 0; i < 10; i++)
            {
                Assert.That(shop.BeginOffer().All(unit => unit.Rarity != UnitRarity.Legendary), Is.True);
                Assert.That(shop.TrySummon(0), Is.True);
                if (!roster.CanAddMember) break;
            }
        }
    }
}
