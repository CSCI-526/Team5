using NUnit.Framework;
using Team5.Rebuild.Presentation.LegacyUI;

namespace Team5.Rebuild.Tests
{
    public sealed class BattleHotkeyLabelTests
    {
        [TestCase(0, "1")]
        [TestCase(1, "2")]
        [TestCase(2, "3")]
        [TestCase(3, "4")]
        [TestCase(-1, "")]
        [TestCase(4, "")]
        public void LabelsFollowVisibleSlot(int slot, string expected)
        {
            Assert.AreEqual(expected, BattleHotkeys.SkillLabel(slot));
        }
    }
}
