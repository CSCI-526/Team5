using System.Collections;
using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Presentation.LegacyUI;
using Team5.Rebuild.Simulation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Team5.Rebuild.Tests
{
    public sealed class LiveFormationPlayModeTests
    {
        [UnityTest]
        public IEnumerator LiveGridAppliesRepeatedSwapsWithoutPausingOrTeleporting()
        {
            var cells = from x in Enumerable.Range(-3, 14) from z in Enumerable.Range(-3, 12) select new Vector3Int(x, 0, z);
            var sim = new BattleSimulation(new BattleDefinition(new[] { new Vector3(0, .25f, 0), new Vector3(0, .25f, -1) },
                new TowerDefinition[0], new[] {
                    new MemberSpawnDefinition(1, 4, SquadCombatRules.Create(UnitRole.Tank)),
                    new MemberSpawnDefinition(2, 5, SquadCombatRules.Create(UnitRole.Attack)) }, cells));
            sim.EnableContinuousMovement(.64f, .28f); sim.Step(.02f);
            sim.RequestMovement(new MovementRequest(new Vector3(7, .25f, 0)));
            var a = sim.Units[0]; var b = sim.Units[1]; var first = a.SlotId; var second = b.SlotId;
            var host = new GameObject("Live grid test", typeof(RectTransform));
            try
            {
                LegacyUiKit.CreateCanvas(host, 0, new Vector2(1280, 720), .5f);
                var grid = LegacyFormationGrid.Create(host.transform, (RectTransform)host.transform, sim);
                grid.ApplyLiveFormation = slots => sim.TryApplyFormation(slots);
                yield return null;
                var before = sim.Units.Select(u => u.Position).ToArray();
                grid.HandleClick(first); grid.HandleClick(second);
                Assert.That(sim.IsPaused, Is.False);
                Assert.That(a.SlotId, Is.EqualTo(second)); Assert.That(b.SlotId, Is.EqualTo(first));
                Assert.That(grid.CopySourceSlots(), Is.EqualTo(Enumerable.Range(0, 9)));
                Assert.That(sim.Units.Select(u => u.Position), Is.EqualTo(before));
                yield return null;
                for (var i = 0; i < 50; i++) sim.Step(.02f);
                Assert.That(a.Position, Is.Not.EqualTo(before[0]));
                Assert.That(sim.IsPaused, Is.False);
                grid.HandleClick(first); grid.HandleClick(second);
                Assert.That(a.SlotId, Is.EqualTo(first)); Assert.That(b.SlotId, Is.EqualTo(second));
                Assert.That(grid.CopySourceSlots(), Is.EqualTo(Enumerable.Range(0, 9)));
                yield return null;
            }
            finally { Object.Destroy(host); }
            yield return null;
        }
    }
}
