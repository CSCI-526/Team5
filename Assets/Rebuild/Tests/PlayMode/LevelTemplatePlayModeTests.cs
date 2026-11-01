#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Team5.Rebuild.Tests
{
    public sealed class LevelTemplatePlayModeTests
    {
        [UnityTest]
        public IEnumerator TemplateStartsStandaloneAndFinishesWithoutChangingCameraOrEnteringCampaign()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/Rebuild/Scenes/LevelTemplate.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            var session = Object.FindFirstObjectByType<BattleSession>();
            var camera = Camera.main;
            var position = camera.transform.position;
            var rotation = camera.transform.rotation;
            Assert.That(session, Is.Not.Null);
            Assert.That(session.Simulation, Is.Not.Null);
            Assert.That(session.Simulation.Objective, Is.EqualTo(BattleObjective.ReachGoal));
            Assert.That(Object.FindFirstObjectByType<TestLevelControls>(), Is.Null);
            Assert.That(BattleSceneFlow.IsRunActive, Is.False);
            var cells = session.Grid.SceneCells;
            var firstId = cells[0].GetInstanceID();
            session.enabled = false;
            for (var i = 0; i < 10000 && session.Simulation.Outcome == BattleOutcome.Running; i++) session.Advance(.02f);
            yield return null;
            Assert.That(session.Simulation.Outcome, Is.Not.EqualTo(BattleOutcome.Running));
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo("Assets/Rebuild/Scenes/LevelTemplate.unity"));
            Assert.That(session.Grid.SceneCells.Length, Is.EqualTo(cells.Length));
            Assert.That(session.Grid.SceneCells[0].GetInstanceID(), Is.EqualTo(firstId));
            Assert.That(camera.transform.position, Is.EqualTo(position));
            Assert.That(camera.transform.rotation, Is.EqualTo(rotation));
        }

        [UnityTearDown]
        public IEnumerator Close()
        {
            var old = SceneManager.GetActiveScene();
            var clean = SceneManager.CreateScene("Stage1 Play Cleanup");
            SceneManager.SetActiveScene(clean);
            yield return SceneManager.UnloadSceneAsync(old);
        }
    }
}
#endif
