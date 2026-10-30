using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Presentation.LegacyUI;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Team5.Rebuild.Tests
{
    public sealed class SquadBattlePlayModeTests
    {
        private BattleSession session;
        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Rebuild/Scenes/SquadBattlePrototype.unity");
            session = Object.FindFirstObjectByType<BattleSession>();
            session.enabled = false;
            typeof(BattleSession).GetField("logEvents", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(session, false);
            session.StartBattle();
        }
        [UnityTearDown]
        public IEnumerator Close()
        {
            var empty = SceneManager.CreateScene("Squad Test Cleanup");
            SceneManager.SetActiveScene(empty);
            if (session != null) yield return SceneManager.UnloadSceneAsync(session.gameObject.scene);
        }
        private void Finish()
        {
            for (var i = 0; i < 10000 && session.Simulation.Outcome == BattleOutcome.Running; i++) session.Advance(0.02f);
            Assert.That(session.Simulation.Outcome, Is.Not.EqualTo(BattleOutcome.Running));
        }

        [UnityTest]
        public IEnumerator ThreeRolesRunWithSixViewsWithoutDuplicatingTerrainOrTowers()
        {
            Assert.That(session.ValidateConfiguration(), Is.Empty);
            var cells = Object.FindObjectsByType<BattleCell>(FindObjectsSortMode.InstanceID).Select(c => c.GetInstanceID()).ToArray();
            var towers = session.Towers.Select(t => t.GetInstanceID()).ToArray();
            session.Advance(0.02f);
            var sim = session.Simulation;
            Assert.That(sim.Units.Count, Is.EqualTo(6));
            Assert.That(sim.Units.Select(u => u.Role).Distinct().Count(), Is.EqualTo(3));
            Assert.That(Object.FindFirstObjectByType<BattlePresentation>().ViewCount, Is.EqualTo(6));
            var damage = 0; var towerDamage = 0; var heals = 0; var ends = 0;
            sim.UnitDamaged += _ => damage++;
            sim.TowerDamaged += _ => towerDamage++;
            sim.UnitHealed += _ => heals++;
            sim.BattleEnded += _ => ends++;
            Finish();
            Assert.That(damage, Is.GreaterThan(0)); Assert.That(towerDamage, Is.GreaterThan(0));
            Assert.That(heals, Is.GreaterThan(0)); Assert.That(ends, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<BattleCell>(FindObjectsSortMode.InstanceID).Select(c => c.GetInstanceID()), Is.EqualTo(cells));
            Assert.That(session.Towers.Select(t => t.GetInstanceID()), Is.EqualTo(towers));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisablingAllPresentationAndRenderersPreservesExactCombatResult()
        {
            Finish();
            var first = session.Simulation;
            var result = (first.Outcome, first.KilledCount, first.ReachedGoalCount);
            var states = first.Units.Select(u => (u.MemberId, u.Hp, u.LifeState, u.Position)).ToArray();
            Object.FindFirstObjectByType<LegacyBattleHud>().enabled = false;
            Object.FindFirstObjectByType<BattleCombatControls>().enabled = false;
            Object.FindFirstObjectByType<BattlePresentation>().enabled = false;
            Object.FindFirstObjectByType<BattleDebugGizmos>().enabled = false;
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) renderer.enabled = false;
            yield return null;
            session.StartBattle(); Finish();
            var second = session.Simulation;
            Assert.That((second.Outcome, second.KilledCount, second.ReachedGoalCount), Is.EqualTo(result));
            Assert.That(second.Units.Select(u => (u.MemberId, u.Hp, u.LifeState, u.Position)), Is.EqualTo(states));
        }

        [UnityTest]
        public IEnumerator SwappingAtFractionalRouteProgressSnapsViewsAndPreservesMembers()
        {
            session.Advance(0.02f); session.Advance(0.02f);
            var sim = session.Simulation;
            sim.SetPaused(true);
            var before = sim.Units.Select(u => (u.MemberId, u.Hp, u.AttackCooldown, u.RouteProgress)).ToArray();
            var permutation = Enumerable.Range(0, 9).ToArray(); permutation[4] = 1; permutation[1] = 4;
            Assert.That(sim.TryApplyFormation(permutation), Is.True);
            Assert.That(sim.Units.Select(u => (u.MemberId, u.Hp, u.AttackCooldown, u.RouteProgress)), Is.EqualTo(before));
            var view = Object.FindFirstObjectByType<BattlePresentation>();
            foreach (var unit in sim.Units)
            {
                Assert.That(view.TryGetView(unit.MemberId, out var model), Is.True);
                model.Present(0);
                Assert.That(model.transform.position, Is.EqualTo(unit.Position));
            }
            Assert.That(sim.Units.Select(u => u.OccupiedCell).Distinct().Count(), Is.EqualTo(6));
            sim.SetPaused(false);
            for (var i = 0; i < 2200 && sim.Outcome == BattleOutcome.Running; i++)
            {
                session.Advance(0.02f);
                foreach (var member in sim.Units.Where(u => u.IsAlive))
                {
                    Assert.That(session.Grid.TryGetCell(member.OccupiedCell, out var support), Is.True);
                    Assert.That(support.IsRouteSurface, Is.True);
                }
            }
            yield return null;
        }
    }
}
