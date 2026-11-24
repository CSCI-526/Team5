using NUnit.Framework;
using Team5.Rebuild.Presentation.LegacyUI;
using Team5.Rebuild.Simulation;

namespace Team5.Rebuild.Tests
{
    public sealed class CampaignMinionBalanceTests
    {
        [TestCase(3, .70f)]
        [TestCase(4, .85f)]
        [TestCase(5, .95f)]
        [TestCase(6, 1.05f)]
        [TestCase(7, 1.15f)]
        public void BombDamageScalesButStaysBelowQuarterOfFreshCommonSniperHp(int stage, float expected)
        {
            var damage = MinionRules.BombDamage(stage);
            var sniper = SquadCombatRules.Create(UnitRole.Sniper, UnitRarity.Common, stage - 1);
            Assert.That(damage, Is.EqualTo(expected).Within(.0001f));
            Assert.That(damage, Is.LessThanOrEqualTo(sniper.MaxHp * .25f));
        }

        [Test]
        public void BombCounterplayAndShieldStrengthRemainUnchanged()
        {
            Assert.That(MinionRules.BombReach, Is.EqualTo(7f));
            Assert.That(MinionRules.BombWarning, Is.EqualTo(1.8f));
            Assert.That(MinionRules.BombRadius, Is.EqualTo(1f));
            Assert.That(MinionRules.BombInterval, Is.EqualTo(4f));
            Assert.That(MinionRules.ShieldDamageScale, Is.EqualTo(.3f));
            Assert.That(MinionRules.Hp(MinionKind.Lobber, 7), Is.EqualTo(8f));
            Assert.That(MinionRules.BearerDamage(7), Is.EqualTo(.60f));
            Assert.That(MinionRules.HasMinions(1), Is.False);
        }
    }
}
