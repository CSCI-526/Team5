using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Presentation.LegacyUI;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class UnitDetailsTests
    {
        private static string[] Labels(UnitRole role) =>
            LegacyUpgradeScene.UnitDetailStats(SquadCombatRules.Create(role)).Select(stat => stat.label).ToArray();

        [Test]
        public void AttackersShowCombatStatsButNotHealing()
        {
            var labels = Labels(UnitRole.Demolisher);
            Assert.That(labels, Does.Contain("HP"));
            Assert.That(labels, Does.Contain("ATK"));
            Assert.That(labels, Does.Contain("ATK SPEED"));
            Assert.That(labels, Does.Contain("RANGE"));
            Assert.That(labels, Does.Contain("VS TOWERS"));
            Assert.That(labels, Does.Not.Contain("HEAL"));
        }

        [Test]
        public void SupportRolesHideAttackRows()
        {
            Assert.That(Labels(UnitRole.Tank), Does.Contain("DEF"));
            Assert.That(Labels(UnitRole.Tank), Does.Not.Contain("ATK"));
            Assert.That(Labels(UnitRole.Heal), Does.Contain("HEAL"));
            Assert.That(Labels(UnitRole.Heal), Does.Not.Contain("ATK"));
        }

        [Test]
        public void EveryRoleHasALongDescription()
        {
            foreach (UnitRole role in System.Enum.GetValues(typeof(UnitRole)))
            {
                if (role == UnitRole.Test) continue;
                Assert.That(LegacyUnitIntroductionUI.LongDescription(SquadCombatRules.Create(role)), Is.Not.Empty, role.ToString());
            }
        }

        [Test]
        public void CompactStorageCardHidesStatsAndShowsDetailsHint()
        {
            var panel = new GameObject("Card", typeof(RectTransform));
            try
            {
                LegacyUnitIntroductionUI.Build(panel.transform, SquadCombatRules.Create(UnitRole.Sniper), compact: true);
                Assert.That(panel.transform.Find("Details Hint"), Is.Not.Null);
                Assert.That(panel.transform.Find("Stat"), Is.Null);
                Assert.That(panel.transform.Find("Role"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(panel);
            }
        }
    }
}
