#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Session;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Team5.Rebuild.Tests
{
    public sealed class HaonUnitPlayModeTests
    {
        [UnityTest]
        public IEnumerator SingleMemberUsesHaonAndPauseResumeSwitchesIdleRunWithoutRootDrift()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/Rebuild/Scenes/HaonUnitTest.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
            var session = Object.FindFirstObjectByType<BattleSession>();
            var presentation = Object.FindFirstObjectByType<BattlePresentation>();
            Assert.That(session.Simulation, Is.Not.Null);
            // Render frames can run before the first fixed tick in a fast batch run.
            // Wait for actual deployment rather than assuming two render frames spawn members.
            for (var i = 0; i < 120 && !presentation.TryGetView(111, out _); i++)
            { session.AdvancePlaybackFrame(.02f); yield return null; }
            Assert.That(presentation.TryGetView(111, out var view), Is.True);
            var driver = view.GetComponent<HaonUnitAnimation>();
            Assert.That(driver, Is.Not.Null);
            Assert.That(view.State.SlotId, Is.EqualTo(4));
            Assert.That(view.transform.Find("Body"), Is.Null);
            Assert.That(presentation.TryGetView(110, out var captain), Is.True);
            Assert.That(captain.GetComponent<HaonUnitAnimation>(), Is.Null);
            Assert.That(captain.transform.Find("Body"), Is.Not.Null);
            var animator = driver.CharacterAnimator;
            Assert.That(animator.applyRootMotion, Is.False);
            var cameraPosition = Camera.main.transform.position;
            var modelOffset = animator.transform.localPosition;
            for (var i = 0; i < 90 && !driver.IsMoving; i++) yield return null;
            Assert.That(driver.IsMoving, Is.True);
            for (var i = 0; i < 30; i++) yield return null;
            Assert.That(animator.GetBool("IsMoving"), Is.True);
            Assert.That(Vector3.Distance(view.transform.position, view.State.Position), Is.LessThan(.05f));
            Assert.That(animator.transform.localPosition, Is.EqualTo(modelOffset));
            session.Simulation.SetPaused(true);
            var stopped = view.State.Position;
            yield return null; yield return null;
            Assert.That(driver.IsMoving, Is.False);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
            Assert.That(animator.speed, Is.Zero);
            Assert.That(view.State.Position, Is.EqualTo(stopped));
            session.Simulation.SetPaused(false);
            for (var i = 0; i < 90 && !driver.IsMoving; i++) yield return null;
            Assert.That(driver.IsMoving, Is.True);
            Assert.That(Camera.main.transform.position, Is.EqualTo(cameraPosition));
            Assert.That(animator.transform.localPosition, Is.EqualTo(modelOffset));
            Assert.That(BattleSceneFlow.IsRunActive, Is.False);
        }
        [UnityTearDown]
        public IEnumerator Close()
        {
            var scene = SceneManager.GetActiveScene();
            var clean = SceneManager.CreateScene("Haon Test Cleanup");
            SceneManager.SetActiveScene(clean);
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
#endif
