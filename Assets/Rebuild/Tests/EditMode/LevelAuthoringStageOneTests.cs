using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Editor.LevelAuthoring;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Team5.Rebuild.Tests
{
    public sealed class LevelAuthoringStageOneTests
    {
        private string folder;
        private SceneSetup[] setup;

        [SetUp]
        public void SetUp()
        {
            setup = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            folder = "Assets/__LevelAuthoringStage1_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            Undo.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!string.IsNullOrEmpty(folder) && folder.StartsWith("Assets/__LevelAuthoringStage1_", StringComparison.Ordinal))
                AssetDatabase.DeleteAsset(folder);
            // A headless Test Runner can start with no loaded/saved scene setup.
            if (setup.Any(s => s.isLoaded && s.isActive && !string.IsNullOrEmpty(s.path)))
                EditorSceneManager.RestoreSceneManagerSetup(setup);
        }

        private Scene OpenCopy(string name = "LevelA")
        {
            var asset = LevelTemplateUtility.CreateLevelFromTemplate(folder + "/" + name + ".unity");
            return EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(asset));
        }

        [Test]
        public void CopyCreatesIndependentGuidWithoutChangingExistingScenesOrBuildList()
        {
            var template = File.ReadAllBytes(LevelTemplateUtility.TemplateScenePath);
            var test = File.Exists(LevelTemplateUtility.ReservedTestScenePath) ? File.ReadAllBytes(LevelTemplateUtility.ReservedTestScenePath) : null;
            var builds = EditorBuildSettings.scenes.Select(s => s.path + "|" + s.enabled).ToArray();
            var a = LevelTemplateUtility.CreateLevelFromTemplate(folder + "/LevelA.unity");
            var b = LevelTemplateUtility.CreateLevelFromTemplate(folder + "/LevelB.unity");
            var guids = new[] { LevelTemplateUtility.TemplateScenePath, AssetDatabase.GetAssetPath(a), AssetDatabase.GetAssetPath(b) }
                .Select(path => AssetDatabase.AssetPathToGUID(path)).ToArray();
            Assert.That(guids.Distinct().Count(), Is.EqualTo(3));
            Assert.That(File.ReadAllBytes(LevelTemplateUtility.TemplateScenePath), Is.EqualTo(template));
            if (test != null) Assert.That(File.ReadAllBytes(LevelTemplateUtility.ReservedTestScenePath), Is.EqualTo(test));
            Assert.That(EditorBuildSettings.scenes.Select(s => s.path + "|" + s.enabled), Is.EqualTo(builds));
        }

        [Test]
        public void CopyRefusesExistingTemplateTestLevelAndOutsideAssetsDestinations()
        {
            var path = folder + "/Existing.unity";
            LevelTemplateUtility.CreateLevelFromTemplate(path);
            var bytes = File.ReadAllBytes(path);
            Assert.Throws<IOException>(() => LevelTemplateUtility.CreateLevelFromTemplate(path));
            Assert.Throws<InvalidOperationException>(() => LevelTemplateUtility.CreateLevelFromTemplate(LevelTemplateUtility.TemplateScenePath));
            Assert.Throws<InvalidOperationException>(() => LevelTemplateUtility.CreateLevelFromTemplate(LevelTemplateUtility.ReservedTestScenePath));
            Assert.Throws<InvalidOperationException>(() => LevelTemplateUtility.CreateLevelFromTemplate("Assets/Rebuild/Scenes/../Scenes/TestLevel.unity"));
            Assert.Throws<ArgumentException>(() => LevelTemplateUtility.CreateLevelFromTemplate("Assets/../../Outside.unity"));
            Assert.Throws<ArgumentException>(() => LevelTemplateUtility.CreateLevelFromTemplate(folder + "/Wrong.asset"));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
        }

        [Test]
        public void TemplateAndCopyValidateWithoutMutatingOrStartingSession()
        {
            var scene = EditorSceneManager.OpenScene(LevelTemplateUtility.TemplateScenePath);
            var dirty = scene.isDirty;
            Assert.That(LevelValidation.Validate(scene).Where(i => i.IsError), Is.Empty);
            Assert.That(scene.isDirty, Is.EqualTo(dirty));
            Assert.That(LevelValidation.Components<BattleSession>(scene).Single().Simulation, Is.Null);
            var copy = OpenCopy();
            Assert.That(LevelValidation.Validate(copy).Where(i => i.IsError), Is.Empty);
            Assert.That(copy.isDirty, Is.False);
        }

        [Test]
        public void TemplateCopyPreservesSourceCameraAndCompletesItsBattle()
        {
            var sourceScene = EditorSceneManager.OpenScene(BattleSceneFlow.LevelTwoScene);
            var sourceSession = LevelValidation.Components<BattleSession>(sourceScene).Single();
            var definition = sourceSession.CaptureDefinition();
            var camera = LevelValidation.Components<Camera>(sourceScene).Single();
            var position = camera.transform.position;
            var rotation = camera.transform.rotation;
            var lens = (camera.orthographic, camera.orthographicSize, camera.fieldOfView, camera.nearClipPlane, camera.farClipPlane);
            var copy = OpenCopy();
            camera = LevelValidation.Components<Camera>(copy).Single();
            Assert.That(camera.transform.position, Is.EqualTo(position));
            Assert.That(camera.transform.rotation, Is.EqualTo(rotation));
            Assert.That((camera.orthographic, camera.orthographicSize, camera.fieldOfView, camera.nearClipPlane, camera.farClipPlane), Is.EqualTo(lens));
            var session = LevelValidation.Components<BattleSession>(copy).Single();
            var copied = session.CaptureDefinition();
            Assert.That(copied.Waypoints, Is.EqualTo(definition.Waypoints));
            Assert.That(copied.Towers.Select(t => (t.Position, t.Area, t.Range, t.Damage, t.AttackInterval, t.MaxHp, t.FootprintSize)),
                Is.EqualTo(definition.Towers.Select(t => (t.Position, t.Area, t.Range, t.Damage, t.AttackInterval, t.MaxHp, t.FootprintSize))));
            var simulation = new BattleSimulation(copied);
            for (var i = 0; i < 10000 && simulation.Outcome == BattleOutcome.Running; i++) simulation.Step(.02f);
            Assert.That(simulation.Outcome, Is.Not.EqualTo(BattleOutcome.Running));
            Assert.That(session.Simulation, Is.Null);
        }

        [Test]
        public void LevelNumberSupportsUndoRedoAndCannotEditTemplate()
        {
            var scene = OpenCopy();
            var session = LevelValidation.Components<BattleSession>(scene).Single();
            var before = session.DisplayedLevelNumber;
            LevelEditScope.SetDisplayedLevelNumber(session, 9);
            Assert.That(session.DisplayedLevelNumber, Is.EqualTo(9));
            Undo.PerformUndo();
            Assert.That(session.DisplayedLevelNumber, Is.EqualTo(before));
            Undo.PerformRedo();
            Assert.That(session.DisplayedLevelNumber, Is.EqualTo(9));
            EditorSceneManager.OpenScene(LevelTemplateUtility.TemplateScenePath);
            session = Object.FindFirstObjectByType<BattleSession>();
            Assert.Throws<InvalidOperationException>(() => LevelEditScope.SetDisplayedLevelNumber(session, 9));
        }

        [Test]
        public void GroupedCreationDeletionAndRollbackAreAtomic()
        {
            var scene = SceneManager.GetActiveScene();
            using (var edit = new LevelEditScope(scene, "Create pair"))
            {
                edit.RegisterCreated(new GameObject("Stage1 A"));
                edit.RegisterCreated(new GameObject("Stage1 B"));
                edit.Commit();
            }
            Undo.PerformUndo();
            Assert.That(GameObject.Find("Stage1 A"), Is.Null);
            Assert.That(GameObject.Find("Stage1 B"), Is.Null);
            Undo.PerformRedo();
            Assert.That(GameObject.Find("Stage1 A"), Is.Not.Null);
            Assert.That(GameObject.Find("Stage1 B"), Is.Not.Null);
            using (var edit = new LevelEditScope(scene, "Delete pair"))
            {
                edit.Destroy(GameObject.Find("Stage1 A"));
                edit.Destroy(GameObject.Find("Stage1 B"));
                edit.Commit();
            }
            Undo.PerformUndo();
            var original = GameObject.Find("Stage1 A");
            Assert.That(original, Is.Not.Null);
            Assert.That(GameObject.Find("Stage1 B"), Is.Not.Null);
            using (var edit = new LevelEditScope(scene, "Canceled edit"))
            {
                edit.Record(original);
                original.name = "Changed";
                edit.RegisterCreated(new GameObject("Stage1 C"));
                // No Commit: rollback restores the entire action.
            }
            Assert.That(original.name, Is.EqualTo("Stage1 A"));
            Assert.That(GameObject.Find("Stage1 C"), Is.Null);
        }

        [Test]
        public void UndoRejectsSharedAssetsAndObjectsInAnotherScene()
        {
            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene, folder + "/ScopeA.unity");
            var other = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var foreign = new GameObject("Foreign");
            SceneManager.MoveGameObjectToScene(foreign, other);
            using (var edit = new LevelEditScope(scene, "Scoped edit"))
            {
                Assert.Throws<ArgumentException>(() => edit.Record(foreign));
                Assert.Throws<ArgumentException>(() => edit.Record(AssetDatabase.LoadAssetAtPath<SceneAsset>(LevelTemplateUtility.TemplateScenePath)));
            }
        }

        [Test]
        public void BasicValidationFindsUnregisteredTowersAndBrokenRoute()
        {
            var scene = OpenCopy();
            var session = LevelValidation.Components<BattleSession>(scene).Single();
            var extra = new GameObject("Unregistered Tower").AddComponent<BattleTower>();
            var issues = LevelValidation.Validate(scene);
            Assert.That(issues.Any(i => i.IsError && i.Context == extra && i.Message.Contains("not registered")), Is.True);
            Object.DestroyImmediate(extra.gameObject);
            var route = new SerializedObject(session.Route);
            route.FindProperty("cells").ClearArray();
            route.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(LevelValidation.Validate(scene).Any(i => i.IsError && i.Message.Contains("SceneRoute")), Is.True);
        }

        [Test]
        public void TargetReferenceResolvesRenamedAssetWithoutSelectingOrModifyingIt()
        {
            var asset = LevelTemplateUtility.CreateLevelFromTemplate(folder + "/Original.unity");
            Assert.That(LevelSceneReference.TryFromAsset(asset, out var reference), Is.True);
            var storedGuid = reference.SceneGuid;
            Assert.That(AssetDatabase.MoveAsset(folder + "/Original.unity", folder + "/Renamed.unity"), Is.Empty);
            Assert.That(reference.TryResolvePath(out var path), Is.True);
            Assert.That(path, Is.EqualTo(folder + "/Renamed.unity"));
            Assert.That(reference.SceneGuid, Is.EqualTo(storedGuid));
            Assert.That(default(LevelSceneReference).TryResolvePath(out _), Is.False);
        }
    }
}
