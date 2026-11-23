#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Session;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Team5.Rebuild.Tests
{
    public sealed class TacticalAssaultTests
    {
        [UnityTest]
        public IEnumerator Scene2StartsPausedAndExecutesAPlannedWaypointInTheSameScene()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(TacticalAssault.ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            var scene = SceneManager.GetActiveScene();
            var planner = Object.FindFirstObjectByType<TacticalAssault>();
            var session = Object.FindFirstObjectByType<BattleSession>();
            Assert.That(planner, Is.Not.Null);
            Assert.That(session, Is.Not.Null);
            var simulation = session.Simulation;
            Assert.That(simulation, Is.Not.Null);
            var battleCamera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>())
                .Single(camera => camera.CompareTag("MainCamera"));
            Assert.That(battleCamera.enabled, Is.False);
            var deploymentCamera = planner.GetComponentInChildren<Camera>();
            Assert.That(deploymentCamera, Is.Not.Null);
            Assert.That(deploymentCamera.enabled && deploymentCamera.orthographic, Is.True);
            Assert.That(deploymentCamera.cullingMask, Is.EqualTo(battleCamera.cullingMask));
            Assert.That(deploymentCamera.cullingMask, Is.Not.Zero);
            Assert.That(simulation.IsPaused, Is.True);
            Assert.That(simulation.ElapsedTime, Is.Zero);
            var squadSlot = planner.GetComponentsInChildren<Button>().Single(button => button.name == "SQUAD");
            var squadBadge = squadSlot.GetComponentsInChildren<TMP_Text>(true).Single(label => label.name == "Number");
            Assert.That(squadBadge.gameObject.activeInHierarchy, Is.False);
            squadSlot.onClick.Invoke();
            Assert.That(squadBadge.gameObject.activeInHierarchy, Is.True);
            Assert.That(squadBadge.text, Is.EqualTo("1"));
            var origin = simulation.SquadReferencePosition;
            var target = session.Grid.SceneCells.Select(cell => cell.TopCenter)
                .Where(point => (point - origin).sqrMagnitude >= 4)
                .OrderBy(point => (point - origin).sqrMagnitude)
                .First(point => simulation.PreviewSquadPath(origin, point) != null);

            Invoke(planner, "SetSquadRoute", new[] { origin, target });
            Invoke(planner, "BeginAssault");
            Assert.That(battleCamera.enabled, Is.True);
            Assert.That(deploymentCamera.enabled, Is.False);
            Assert.That(simulation.IsPaused, Is.False);
            Assert.That(session.Simulation, Is.SameAs(simulation));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(scene));
            for (var frame = 0; frame < 160 && Vector3.Distance(simulation.SquadReferencePosition, origin) < 1; frame++)
            {
                session.Advance(.1f);
                yield return null;
            }
            Assert.That(Vector3.Distance(simulation.SquadReferencePosition, origin), Is.GreaterThanOrEqualTo(1));
        }

        private static void Invoke(TacticalAssault planner, string method, params object[] args) =>
            typeof(TacticalAssault).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(planner, args);
    }
}
#endif
