using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Team5.Rebuild.Tests
{
    public sealed class MinimalPresentationTests
    {
        private BattleSession session;
        private BattlePresentation presentation;
        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Rebuild/Scenes/GridBattlePrototype.unity");
            session = Object.FindFirstObjectByType<BattleSession>();
            session.enabled = false;
            typeof(BattleSession).GetField("logEvents", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, false);
            presentation = Object.FindFirstObjectByType<BattlePresentation>();
            session.StartBattle();
        }
        [UnityTearDown]
        public IEnumerator Unload()
        {
            var empty = SceneManager.CreateScene("Minimal Presentation Cleanup");
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(session.gameObject.scene);
        }
        private (BattleOutcome outcome, int killed, int goals) Finish()
        {
            for (var i = 0; i < 10000 && session.Simulation.Outcome == BattleOutcome.Running; i++) session.Advance(0.02f);
            Assert.That(session.Simulation.Outcome, Is.Not.EqualTo(BattleOutcome.Running));
            return (session.Simulation.Outcome, session.Simulation.KilledCount, session.Simulation.ReachedGoalCount);
        }

        [UnityTest]
        public IEnumerator TowerRangesMatchSimulationAndCanBeHiddenWithoutChangingCombat()
        {
            yield return null;
            var ranges = Object.FindObjectsByType<TowerRangeView>(FindObjectsSortMode.None);
            Assert.That(ranges.Length, Is.EqualTo(session.Simulation.Towers.Count));
            foreach (var range in ranges)
            {
                var tower = session.Simulation.Towers.Single(t => t.Id == range.TowerId);
                Assert.That(range.DisplayBounds, Is.EqualTo(SquadCombatRules.AreaBounds(tower.Position,
                    tower.Definition.Area, tower.Definition.Range, tower.Definition.AttackWidth)));
                Assert.That(range.GetComponent<MeshRenderer>().enabled, Is.True);
                Assert.That(range.GetComponentInChildren<LineRenderer>().positionCount, Is.EqualTo(4));
                Assert.That(range.GetComponentInChildren<Collider>(), Is.Null);
            }
            var expected = Finish();
            session.StartBattle();
            presentation.ShowTowerRanges = false;
            yield return null;
            Assert.That(Object.FindObjectsByType<TowerRangeView>(FindObjectsSortMode.None)
                .All(range => !range.GetComponent<MeshRenderer>().enabled && !range.GetComponentInChildren<LineRenderer>().enabled), Is.True);
            Assert.That(Finish(), Is.EqualTo(expected));
            presentation.ShowTowerRanges = true;
        }

        [UnityTest]
        public IEnumerator SquareViewsReadStateAndInterpolateWithoutChangingSimulation()
        {
            session.Advance(0.02f);
            var unit = session.Simulation.Units[0];
            Assert.That(presentation.TryGetView(unit.Id, out var view), Is.True);
            var firstPosition = unit.Position;
            var hp = unit.Hp;
            session.Advance(0.02f);
            var currentPosition = unit.Position;
            view.Present(0.5f);
            Assert.That(Vector3.Distance(view.transform.position, Vector3.Lerp(firstPosition, currentPosition, 0.5f)), Is.LessThan(0.0001f));
            Assert.That(unit.Position, Is.EqualTo(currentPosition));
            Assert.That(unit.Hp, Is.EqualTo(hp));
            Assert.That(view.GetComponentInChildren<MeshRenderer>().enabled, Is.True);
            Assert.That(view.GetComponentInChildren<Collider>(), Is.Null);
            view.transform.position = Vector3.one * 1000;
            view.Present(1);
            Assert.That(view.transform.position, Is.EqualTo(unit.Position));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisablingAndRemovingPresentationPreservesBattleResult()
        {
            var expected = Finish();
            presentation.enabled = false;
            session.StartBattle();
            Assert.That(Finish(), Is.EqualTo(expected));
            Assert.That(presentation.ViewCount, Is.Zero);
            presentation.enabled = true;
            session.StartBattle();
            session.Advance(0.02f);
            Assert.That(presentation.ViewCount, Is.GreaterThan(0));
            Object.Destroy(presentation.gameObject);
            Object.Destroy(Object.FindFirstObjectByType<BattleDebugGizmos>());
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if (renderer != null) Object.Destroy(renderer.gameObject);
            yield return null;
            Assert.That(Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None), Is.Empty);
            session.StartBattle();
            Assert.That(Finish(), Is.EqualTo(expected));
            Assert.That(session.ValidateConfiguration(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator ViewsMatchAliveUnitsAfterDeathsGoalsLateBindingAndRestart()
        {
            typeof(BattleSession).GetField("unitCount", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, 32);
            typeof(BattleSession).GetField("spawnInterval", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, 0.15f);
            typeof(BattleSession).GetField("goalLives", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, 100);
            session.StartBattle();
            presentation.enabled = false;
            for (var i = 0; i < 100; i++) session.Advance(0.02f);
            presentation.enabled = true;
            Assert.That(presentation.ViewCount, Is.EqualTo(session.Simulation.AliveCount));
            var deaths = 0;
            var goals = 0;
            session.Simulation.UnitDied += _ => deaths++;
            session.Simulation.UnitReachedGoal += _ => goals++;
            for (var i = 0; i < 10000 && session.Simulation.Outcome == BattleOutcome.Running; i++)
            {
                session.Advance(0.02f);
                Assert.That(presentation.ViewCount, Is.EqualTo(session.Simulation.AliveCount));
            }
            Assert.That(deaths, Is.GreaterThan(0));
            Assert.That(goals, Is.GreaterThan(0));
            Assert.That(presentation.ViewCount, Is.Zero);
            session.StartBattle();
            Assert.That(presentation.ViewCount, Is.Zero);
            session.Advance(0.02f);
            Assert.That(presentation.ViewCount, Is.EqualTo(1));
            yield return null;
            Assert.That(Object.FindObjectsByType<UnitView>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator GameCameraSeesStaticMeshesAndUnitsWithoutDebugGizmos()
        {
            Object.FindFirstObjectByType<BattleDebugGizmos>().enabled = false;
            for (var i = 0; i < 50; i++) session.Advance(0.02f);
            foreach (var view in Object.FindObjectsByType<UnitView>(FindObjectsSortMode.None)) view.Present(1);
            var camera = Camera.main;
            var frustum = GeometryUtility.CalculateFrustumPlanes(camera);
            Assert.That(session.Grid.SceneCells.All(cell => GeometryUtility.TestPlanesAABB(frustum, cell.GetComponentInChildren<MeshRenderer>().bounds)), Is.True);
            Assert.That(session.Towers.All(tower => GeometryUtility.TestPlanesAABB(frustum, tower.GetComponentInChildren<MeshRenderer>().bounds)), Is.True);
            Assert.That(Object.FindObjectsByType<UnitView>(FindObjectsSortMode.None).Any(view =>
                GeometryUtility.TestPlanesAABB(frustum, view.GetComponentInChildren<MeshRenderer>().bounds)), Is.True);
            yield return null;
        }
    }
}
