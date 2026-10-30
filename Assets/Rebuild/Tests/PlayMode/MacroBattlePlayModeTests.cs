using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Team5.Rebuild.Tests
{
    public sealed class MacroBattlePlayModeTests
    {
        private BattleSession session;
        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Rebuild/Scenes/GridBattleMacroPrototype.unity");
            session = Object.FindFirstObjectByType<BattleSession>();
            session.enabled = false;
            typeof(BattleSession).GetField("logEvents", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, false);
            session.StartBattle();
        }
        [UnityTearDown]
        public IEnumerator Close()
        {
            var empty = SceneManager.CreateScene("Macro Test Cleanup");
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(session.gameObject.scene);
        }
        private void Finish()
        {
            for (var i = 0; i < 5000 && session.Simulation.Outcome == BattleOutcome.Running; i++) session.Advance(0.02f);
            Assert.That(session.Simulation.Outcome, Is.Not.EqualTo(BattleOutcome.Running));
        }

        [UnityTest]
        public IEnumerator NineIndependentViewsSpawnAndCombatCompletesWithoutCloningStaticMap()
        {
            var staticIds = Object.FindObjectsByType<BattleCell>(FindObjectsSortMode.InstanceID).Select(cell => cell.GetInstanceID()).ToArray();
            var meshIds = session.Grid.transform.parent.GetComponentsInChildren<MeshRenderer>().Select(mesh => mesh.GetInstanceID()).ToArray();
            session.Advance(0.02f);
            Assert.That(session.Simulation.SpawnedCount, Is.EqualTo(9));
            Assert.That(session.Simulation.Units.Select(unit => unit.FormationSlot).Distinct().Count(), Is.EqualTo(9));
            var presentation = Object.FindFirstObjectByType<BattlePresentation>();
            Assert.That(presentation.ViewCount, Is.EqualTo(9));
            foreach (var unit in session.Simulation.Units)
            {
                Assert.That(presentation.TryGetView(unit.Id, out var view), Is.True);
                view.Present(1);
                Assert.That(view.transform.position, Is.EqualTo(unit.Position));
            }
            var damaged = 0;
            session.Simulation.UnitDamaged += _ => damaged++;
            Finish();
            Assert.That(damaged, Is.GreaterThan(0));
            Assert.That(session.Simulation.KilledCount, Is.GreaterThan(0));
            Assert.That(session.Simulation.KilledCount + session.Simulation.ReachedGoalCount, Is.EqualTo(9));
            Assert.That(Object.FindObjectsByType<BattleCell>(FindObjectsSortMode.InstanceID).Select(cell => cell.GetInstanceID()), Is.EqualTo(staticIds));
            yield return null;
            Assert.That(session.Grid.transform.parent.GetComponentsInChildren<MeshRenderer>().Select(mesh => mesh.GetInstanceID()), Is.EqualTo(meshIds));
        }

        [UnityTest]
        public IEnumerator RemovingPresentationKeepsMacroBattleOutcomeAndOccupancy()
        {
            Finish();
            var expected = (session.Simulation.Outcome, session.Simulation.KilledCount, session.Simulation.ReachedGoalCount);
            Object.Destroy(Object.FindFirstObjectByType<BattlePresentation>().gameObject);
            Object.Destroy(Object.FindFirstObjectByType<BattleDebugGizmos>());
            yield return null;
            session.StartBattle();
            Finish();
            Assert.That((session.Simulation.Outcome, session.Simulation.KilledCount, session.Simulation.ReachedGoalCount), Is.EqualTo(expected));
            Assert.That(session.ValidateConfiguration(), Is.Empty);
        }
    }
}
