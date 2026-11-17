using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class HaonUnitVisualTests
    {
        public const string TestScene = "Assets/Rebuild/Scenes/HaonUnitTest.unity";
        public const string Visual = "Assets/Rebuild/HaonTest/HaonUnitVisual.prefab";
        [Test]
        public void VisualIsCompleteUnarmedHumanoidWithRealIdleAndRunClips()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Visual);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<UnitView>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<HaonUnitAnimation>(), Is.Not.Null);
            Assert.That(prefab.transform.Find("Body"), Is.Null);
            Assert.That(prefab.transform.Find("Outline"), Is.Null);
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<SkinnedMeshRenderer>().Length, Is.GreaterThan(1));
            foreach (var child in prefab.GetComponentsInChildren<Transform>(true))
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject), Is.Zero, child.name);
            var animator = prefab.GetComponentInChildren<Animator>();
            Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True);
            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(animator.runtimeAnimatorController.animationClips.Select(c => c.name),
                Is.EquivalentTo(new[] { "StandA_idleA", "RunA_front@loop" }));
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>())
                foreach (var material in renderer.sharedMaterials)
                    Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"), material.name);
        }
        [TestCase("StandA_idleA.FBX")]
        [TestCase("RunA_Front.FBX")]
        public void ImportedAnimationRigIsPreserved(string file)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath("Assets/Haons SD series Pack/Animation/Common/" + file);
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Human));
            Assert.That(importer.clipAnimations.Single().loopTime, Is.True);
        }
        [Test]
        public void TestSceneUsesOriginalMapAndCameraAndOnlyOverridesMember111()
        {
            var original = EditorSceneManager.OpenPreviewScene("Assets/Rebuild/Scenes/LevelTemplate.unity");
            var test = EditorSceneManager.OpenPreviewScene(TestScene);
            try
            {
                var sourceSession = original.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BattleSession>()).Single();
                var session = test.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BattleSession>()).Single();
                Assert.That(session.ValidateConfiguration(), Is.Empty);
                Assert.That(session.Grid.SceneCells.Select(c => (c.Occupancy, c.Kind)),
                    Is.EquivalentTo(sourceSession.Grid.SceneCells.Select(c => (c.Occupancy, c.Kind))));
                Assert.That(session.Route.CaptureTopCenters(), Is.EqualTo(sourceSession.Route.CaptureTopCenters()));
                var camera = test.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>()).Single(c => c.CompareTag("MainCamera"));
                var sourceCamera = original.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>()).Single(c => c.CompareTag("MainCamera"));
                Assert.That(camera.transform.position, Is.EqualTo(sourceCamera.transform.position));
                Assert.That(camera.transform.rotation, Is.EqualTo(sourceCamera.transform.rotation));
                Assert.That(camera.orthographicSize, Is.EqualTo(sourceCamera.orthographicSize));
                var member = session.CaptureDefinition().Members.Single(m => m.MemberId == 111);
                var standard = SquadCombatRules.Create(UnitRole.Attack);
                Assert.That(member.SlotId, Is.EqualTo(4));
                Assert.That(member.Unit.Role, Is.EqualTo(UnitRole.Attack));
                Assert.That(member.Unit.MaxHp, Is.EqualTo(standard.MaxHp));
                Assert.That(member.Unit.Speed, Is.EqualTo(standard.Speed));
                Assert.That(member.Unit.Damage, Is.EqualTo(standard.Damage));
                var presentation = test.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BattlePresentation>()).Single();
                var serialized = new SerializedObject(presentation);
                Assert.That(serialized.FindProperty("useTestUnitVisual").boolValue, Is.True);
                Assert.That(serialized.FindProperty("testVisualMemberId").intValue, Is.EqualTo(111));
                Assert.That(serialized.FindProperty("testUnitPrefab").objectReferenceValue, Is.EqualTo(AssetDatabase.LoadAssetAtPath<UnitView>(Visual)));
            }
            finally { EditorSceneManager.ClosePreviewScene(test); EditorSceneManager.ClosePreviewScene(original); }
        }
    }
}
