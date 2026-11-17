using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Editor;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Session;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Team5.Rebuild.Tests
{
    public sealed class FWUnitVisualTests
    {
        [Test]
        public void IndependentVisualUsesOriginalFourClipsAndNoDemoMovementOrColliders()
        {
            var visual = AssetDatabase.LoadAssetAtPath<GameObject>(FWVisualPrototypeBuilder.Visual);
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.GetComponent<UnitView>(), Is.Not.Null);
            Assert.That(visual.GetComponent<FWUnitVisual>(), Is.Not.Null);
            Assert.That(visual.GetComponentInChildren<SortingGroup>(), Is.Not.Null);
            Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(visual.GetComponentsInChildren<Collider2D>(true), Is.Empty);
            Assert.That(visual.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(visual.GetComponentsInChildren<MonoBehaviour>(true).Any(c => c != null && c.GetType().FullName == "FreewrokGame.AniManagers"), Is.False);
            var animators = visual.GetComponentsInChildren<Animator>(true);
            Assert.That(animators.Length, Is.EqualTo(2));
            Assert.That(animators.SelectMany(a => a.runtimeAnimatorController.animationClips).Select(c => c.name),
                Is.EquivalentTo(new[] { "F_IDLE01", "F_RUN01", "B_IDLE01", "B_RUN01" }));
            foreach (var animator in animators) Assert.That(animator.applyRootMotion, Is.False);
            foreach (var child in visual.GetComponentsInChildren<Transform>(true))
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject), Is.Zero, child.name);
            var sprites = visual.GetComponentsInChildren<SpriteRenderer>(true);
            Assert.That(sprites.Length, Is.GreaterThan(10));
            foreach (var sprite in sprites)
            {
                Assert.That(sprite.sprite, Is.Not.Null, sprite.name);
                Assert.That(sprite.sharedMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                Assert.That(sprite.sharedMaterial.GetFloat("_ZWrite"), Is.Zero);
            }
        }

        [Test]
        public void TestScenePreservesExistingMapCameraRosterAndCombatSettings()
        {
            var original = EditorSceneManager.OpenPreviewScene("Assets/Rebuild/Scenes/HaonUnitTest.unity");
            var test = EditorSceneManager.OpenPreviewScene(FWVisualPrototypeBuilder.TestScene);
            try
            {
                var session = test.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BattleSession>()).Single();
                var baseline = original.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BattleSession>()).Single();
                Assert.That(session.ValidateConfiguration(), Is.Empty);
                var definition = session.CaptureDefinition();
                var sourceDefinition = baseline.CaptureDefinition();
                Assert.That(definition.Members.Select(m => (m.MemberId, m.SlotId, m.Unit.Role, m.Unit.MaxHp, m.Unit.Speed, m.Unit.Damage)),
                    Is.EqualTo(sourceDefinition.Members.Select(m => (m.MemberId, m.SlotId, m.Unit.Role, m.Unit.MaxHp, m.Unit.Speed, m.Unit.Damage))));
                Assert.That(session.Grid.SceneCells.Select(c => (c.Occupancy, c.Kind)), Is.EquivalentTo(baseline.Grid.SceneCells.Select(c => (c.Occupancy, c.Kind))));
                Assert.That(session.Route.CaptureTopCenters(), Is.EqualTo(baseline.Route.CaptureTopCenters()));
                var camera = test.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>()).Single(c => c.CompareTag("MainCamera"));
                var sourceCamera = original.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>()).Single(c => c.CompareTag("MainCamera"));
                Assert.That(camera.transform.position, Is.EqualTo(sourceCamera.transform.position));
                Assert.That(camera.transform.rotation, Is.EqualTo(sourceCamera.transform.rotation));
                Assert.That(camera.orthographicSize, Is.EqualTo(sourceCamera.orthographicSize));
                Assert.That(test.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FWVisualTestSwitch>()).Count(), Is.EqualTo(1));
            }
            finally { EditorSceneManager.ClosePreviewScene(test); EditorSceneManager.ClosePreviewScene(original); }
        }
    }
}
