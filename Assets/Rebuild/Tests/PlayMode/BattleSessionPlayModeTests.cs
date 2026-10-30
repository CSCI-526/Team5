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
    public sealed class BattleSessionPlayModeTests
    {
        private BattleSession session;
        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Rebuild/Scenes/GridBattlePrototype.unity");
            session = Object.FindFirstObjectByType<BattleSession>();
            session.enabled = false; // Tests control ticks; StartBattle remains an explicit public operation.
            typeof(BattleSession).GetField("logEvents", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, false);
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            var empty = SceneManager.CreateScene("Rebuild Test Cleanup");
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(session.gameObject.scene);
        }
        private static void Finish(BattleSession value)
        {
            for (var i = 0; i < 10000 && value.Simulation.Outcome == BattleOutcome.Running; i++) value.Advance(0.02f);
            Assert.That(value.Simulation.Outcome, Is.Not.EqualTo(BattleOutcome.Running));
        }

        [UnityTest]
        public IEnumerator DeletingSceneTowerLeavesRemainingTowersAlignedAcrossRestart()
        {
            var original = session.Towers.ToArray();
            Object.Destroy(original[1].gameObject);
            yield return null;
            Assert.That(session.ValidateConfiguration(), Is.Empty);
            Assert.That(session.Towers, Is.EqualTo(new[] { original[0], original[2] }));
            for (var restart = 0; restart < 2; restart++)
            {
                session.StartBattle();
                Assert.That(session.Simulation.Towers.Count, Is.EqualTo(2));
                for (var i = 0; i < session.Towers.Count; i++)
                {
                    Assert.That(session.Towers[i].RuntimeState, Is.SameAs(session.Simulation.Towers[i]));
                    Assert.That(session.Simulation.Towers[i].Definition.Id, Is.EqualTo(i + 1));
                    Assert.That(session.Simulation.Towers[i].Position, Is.EqualTo(session.Towers[i].transform.position));
                }
                Finish(session);
            }
        }

        [UnityTest]
        public IEnumerator NormalFixedUpdateStartsAndCompletesBattle()
        {
            Time.timeScale = 20;
            try
            {
                session.enabled = true;
                session.StartBattle();
                var deadline = Time.realtimeSinceStartup + 15;
                while (session.Simulation.Outcome == BattleOutcome.Running && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(session.Simulation.Outcome, Is.Not.EqualTo(BattleOutcome.Running));
                Assert.That(session.Simulation.KilledCount + session.Simulation.ReachedGoalCount, Is.GreaterThan(0));
            }
            finally { Time.timeScale = 1; }
        }

        [UnityTest]
        public IEnumerator CombatRunsWithDebugViewDisabledAndStaticEntitiesAreNeverCloned()
        {
            var cells = session.Grid.SceneCells;
            var towers = session.Towers.ToArray();
            var cellPositions = cells.Select(value => value.transform.position).ToArray();
            var initialObjects = cells.SelectMany(value => value.GetComponentsInChildren<Transform>(true))
                .Concat(towers.SelectMany(value => value.GetComponentsInChildren<Transform>(true)))
                .Select(value => value.GetInstanceID()).ToArray();
            var view = Object.FindFirstObjectByType<BattleDebugGizmos>();
            view.enabled = false;
            session.StartBattle();
            var damage = 0;
            var deaths = 0;
            var ended = 0;
            session.Simulation.UnitDamaged += _ => damage++;
            session.Simulation.UnitDied += _ => deaths++;
            session.Simulation.BattleEnded += _ => ended++;
            Finish(session);
            Assert.That(damage, Is.GreaterThan(0));
            Assert.That(deaths, Is.GreaterThan(0));
            Assert.That(ended, Is.EqualTo(1));
            Assert.That(session.Grid.SceneCells, Is.EquivalentTo(cells));
            Assert.That(session.Towers, Is.EqualTo(towers));
            Assert.That(cells.Select(value => value.transform.position), Is.EqualTo(cellPositions));
            Assert.That(cells.SelectMany(value => value.GetComponentsInChildren<Transform>(true))
                .Concat(towers.SelectMany(value => value.GetComponentsInChildren<Transform>(true)))
                .Select(value => value.GetInstanceID()), Is.EqualTo(initialObjects));
            Assert.That(Object.FindObjectsByType<BattleCell>(FindObjectsSortMode.None).Length, Is.EqualTo(100));
            Assert.That(Object.FindObjectsByType<BattleTower>(FindObjectsSortMode.None).Length, Is.EqualTo(3));
            Assert.That(Object.FindObjectsByType<Animator>(FindObjectsSortMode.None), Is.Empty);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RemovingDebugViewPreservesResultAndRestartResetsRuntimeOnly()
        {
            session.StartBattle();
            Finish(session);
            var expected = (session.Simulation.Outcome, session.Simulation.KilledCount, session.Simulation.ReachedGoalCount);
            Object.Destroy(Object.FindFirstObjectByType<BattleDebugGizmos>());
            yield return null;
            session.StartBattle();
            Assert.That(session.Simulation.SpawnedCount, Is.Zero);
            Assert.That(session.Simulation.Towers.All(tower => tower.CooldownRemaining == 0), Is.True);
            Finish(session);
            Assert.That((session.Simulation.Outcome, session.Simulation.KilledCount, session.Simulation.ReachedGoalCount), Is.EqualTo(expected));
        }

        [UnityTest]
        public IEnumerator SceneTowerTransformDrivesAttackOriginAtRuntime()
        {
            session.StartBattle();
            var tower = session.Towers[0];
            tower.transform.position += new Vector3(0.2f, 0, 0.1f);
            TowerAttackEvent? attack = null;
            session.Simulation.TowerAttacked += value => { if (value.TowerId == 1) attack = value; };
            for (var i = 0; i < 500 && !attack.HasValue; i++) session.Advance(0.02f);
            Assert.That(attack.HasValue, Is.True);
            Assert.That(attack.Value.Origin, Is.EqualTo(tower.transform.position));
            Assert.That(tower.RuntimeState, Is.SameAs(session.Simulation.Towers[0]));
            yield return null;
        }

        [UnityTest]
        public IEnumerator LargerWaveSpawnsManyUnitsAndGoalEventsResolveCorrectly()
        {
            typeof(BattleSession).GetField("unitCount", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, 128);
            typeof(BattleSession).GetField("spawnInterval", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, 0.02f);
            typeof(BattleSession).GetField("goalLives", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, 129);
            session.StartBattle();
            var goals = 0;
            var maximumAlive = 0;
            session.Simulation.UnitReachedGoal += _ => goals++;
            for (var i = 0; i < 10000 && session.Simulation.Outcome == BattleOutcome.Running; i++)
            {
                session.Advance(0.02f);
                maximumAlive = System.Math.Max(maximumAlive, session.Simulation.AliveCount);
            }
            Assert.That(maximumAlive, Is.GreaterThan(100));
            Assert.That(session.Simulation.SpawnedCount, Is.EqualTo(128));
            Assert.That(goals, Is.GreaterThan(0));
            Assert.That(goals, Is.EqualTo(session.Simulation.ReachedGoalCount));
            Assert.That(session.Simulation.KilledCount + goals, Is.EqualTo(128));
            Assert.That(session.Simulation.Outcome, Is.EqualTo(BattleOutcome.Victory));
            yield return null;
        }
    }
}
