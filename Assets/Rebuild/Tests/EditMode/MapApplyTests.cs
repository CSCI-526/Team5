using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Editor.LevelAuthoring;
using Team5.Rebuild.Presentation;
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
    public sealed class MapApplyTests
    {
        private MapDraft original, draft;
        private Scene scene;
        private SceneSetup[] setup;
        private BattleSession session;
        private string folder;

        [SetUp]
        public void SetUp()
        {
            setup = EditorSceneManager.GetSceneManagerSetup();
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            original = ScriptableObject.CreateInstance<MapDraft>();
            original.hideFlags = HideFlags.HideAndDontSave;
            original.ResetGrid(9, 6);
            for (var i = 0; i < original.tiles.Length; i++) original.tiles[i] = (int)CellKind.Ground;
            original.Paint(new Vector2Int(1, 1), (int)CellKind.Spawn, 3);
            original.Paint(new Vector2Int(4, 1), (int)CellKind.Goal, 3);
            original.Paint(new Vector2Int(7, 1), (int)CellKind.TowerPlatform, 3);
            original.AppendRoute(new Vector2Int(1, 1)); original.AppendRoute(new Vector2Int(4, 1));
            original.PlaceTower(new Vector2Int(7, 1), AssetDatabase.LoadAssetAtPath<GameObject>(
                MapBuilderUtility.Kit + "Towers/Tower_Attack_1x1_Square.prefab"), AttackArea.Square);
            session = MapBuilderUtility.Populate(original, scene);
            var settings = new SerializedObject(session);
            settings.FindProperty("manualSquadMovement").boolValue = true;
            settings.FindProperty("useSceneRosterOnly").boolValue = true;
            settings.FindProperty("autoStart").boolValue = false;
            settings.FindProperty("levelTimeMultiplier").floatValue = 1.7f;
            settings.FindProperty("displayedLevelNumber").intValue = 6;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var presentation = LevelValidation.Components<BattlePresentation>(scene).Single();
            var visuals = new SerializedObject(presentation);
            visuals.FindProperty("useTestUnitVisual").boolValue = true;
            visuals.FindProperty("testVisualMemberId").intValue = 111;
            visuals.FindProperty("testUnitPrefab").objectReferenceValue = presentation.UnitPrefab;
            visuals.ApplyModifiedPropertiesWithoutUndo();
            draft = MapBuilderUtility.Read(scene);
            folder = "Assets/__MapApplyTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            Undo.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            Object.DestroyImmediate(original); Object.DestroyImmediate(draft);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            AssetDatabase.DeleteAsset(folder);
            if (setup.Any(s => s.isLoaded && s.isActive && !string.IsNullOrEmpty(s.path)))
                EditorSceneManager.RestoreSceneManagerSetup(setup);
        }

        private BattleCell Cell(int x, int z) => session.Grid.SceneCells.Single(c => c.Occupancy == new Vector3Int(x, 0, z));

        [Test]
        public void SessionAcceptsExactScene1BodySpacingBoundary()
        {
            var settings = new SerializedObject(session);
            settings.FindProperty("continuousSquadMovement").boolValue = true;
            settings.FindProperty("continuousFormationSpacing").floatValue = 1;
            settings.FindProperty("continuousSoldierRadius").floatValue = .495f;
            settings.FindProperty("continuousVisualFootprint").floatValue = .99f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(session.ValidateConfiguration(), Is.Empty);
            var simulation = new BattleSimulation(session.CaptureDefinition());
            Assert.DoesNotThrow(() => simulation.EnableContinuousMovement(session.ContinuousFormationSpacing, session.ContinuousSoldierRadius));
        }

        [Test]
        public void ApplyOnlyChangesMapAndSupportsOneStepUndoRedo()
        {
            var camera = LevelValidation.Components<Camera>(scene).Single();
            var canvas = LevelValidation.Components<Canvas>(scene).First();
            var presentation = LevelValidation.Components<BattlePresentation>(scene).Single();
            var model = new GameObject("Existing character", typeof(UnitView));
            SceneManager.MoveGameObjectToScene(model, scene);
            var settingsBefore = EditorJsonUtility.ToJson(session);
            var viewBefore = EditorJsonUtility.ToJson(presentation);
            var cameraBefore = EditorJsonUtility.ToJson(camera);
            var cameraTransformBefore = EditorJsonUtility.ToJson(camera.transform);
            var unchanged = Cell(0, 0); var replaced = Cell(0, 5); var erased = Cell(1, 5);
            var replacedId = replaced.GetInstanceID(); var erasedId = erased.GetInstanceID();
            var tower = session.Towers.Single();
            var rigBytes = File.ReadAllBytes(MapBuilderUtility.Kit + "Systems/BattleRig_3D.prefab");
            var buildBytes = File.ReadAllBytes("ProjectSettings/EditorBuildSettings.asset");
            draft.Paint(new Vector2Int(0, 5), (int)CellKind.Obstacle, 1);
            draft.Paint(new Vector2Int(1, 5), -1, 1);
            draft.levelNumber = 99; // Applying a map must not apply the generator's level configuration.
            MapBuilderUtility.ApplyToScene(draft, scene);
            Assert.That(Cell(0, 0), Is.SameAs(unchanged));
            Assert.That(Cell(0, 5).Kind, Is.EqualTo(CellKind.Obstacle));
            Assert.That(session.Grid.SceneCells.Length, Is.EqualTo(53));
            Assert.That(session.Towers.Single(), Is.SameAs(tower));
            Assert.That(EditorJsonUtility.ToJson(session), Is.EqualTo(settingsBefore));
            Assert.That(EditorJsonUtility.ToJson(presentation), Is.EqualTo(viewBefore));
            Assert.That(EditorJsonUtility.ToJson(camera), Is.EqualTo(cameraBefore));
            Assert.That(EditorJsonUtility.ToJson(camera.transform), Is.EqualTo(cameraTransformBefore));
            Assert.That(canvas != null && model != null, Is.True);
            Assert.That(File.ReadAllBytes(MapBuilderUtility.Kit + "Systems/BattleRig_3D.prefab"), Is.EqualTo(rigBytes));
            Assert.That(File.ReadAllBytes("ProjectSettings/EditorBuildSettings.asset"), Is.EqualTo(buildBytes));
            for (var repeat = 0; repeat < 3; repeat++)
            {
                Undo.PerformUndo();
                Assert.That(session.Grid.SceneCells.Length, Is.EqualTo(54));
                Assert.That(Cell(0, 5).GetInstanceID(), Is.EqualTo(replacedId));
                Assert.That(Cell(1, 5).GetInstanceID(), Is.EqualTo(erasedId));
                Assert.That(Cell(0, 5).Kind, Is.EqualTo(CellKind.Ground));
                Assert.That(LevelValidation.Validate(scene).Where(i => i.IsError), Is.Empty);
                Undo.PerformRedo();
                Assert.That(session.Grid.SceneCells.Length, Is.EqualTo(53));
                Assert.That(Cell(0, 5).Kind, Is.EqualTo(CellKind.Obstacle));
                Assert.That(session.Route.Cells.All(c => c != null && session.Grid.SceneCells.Contains(c)), Is.True);
                Assert.That(LevelValidation.Validate(scene).Where(i => i.IsError), Is.Empty);
            }
        }

        [Test]
        public void ExistingTowerKeepsCombatStatsAndNewTowerCanBeAppliedRepeatedly()
        {
            var tower = session.Towers.Single();
            var settings = new SerializedObject(tower);
            settings.FindProperty("damage").floatValue = 42;
            settings.FindProperty("maxHp").floatValue = 73;
            settings.FindProperty("attackArea").enumValueIndex = (int)AttackArea.PositiveZ;
            settings.FindProperty("isAoe").boolValue = true;
            settings.FindProperty("goldBounty").intValue = 67;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var combatBefore = EditorJsonUtility.ToJson(tower);
            // The draft was read before those combat-stat edits; applying must not restore its stale snapshot.
            draft.Paint(new Vector2Int(7, 4), (int)CellKind.TowerPlatform, 1);
            draft.towers[0].cell = new Vector2Int(7, 4);
            draft.PlaceTower(new Vector2Int(7, 1), draft.towers[0].prefab, AttackArea.PositiveX);
            MapBuilderUtility.ApplyToScene(draft, scene);
            Assert.That(session.Towers[0], Is.SameAs(tower));
            Assert.That(tower.transform.position, Is.EqualTo(new Vector3(7, .25f, 4)));
            Assert.That(tower.Damage, Is.EqualTo(42));
            Assert.That(tower.CaptureDefinition(1).MaxHp, Is.EqualTo(73));
            Assert.That(EditorJsonUtility.ToJson(tower), Is.EqualTo(combatBefore));
            var added = session.Towers[1];
            MapBuilderUtility.ApplyToScene(draft, scene);
            Assert.That(session.Towers[1], Is.SameAs(added));
            Undo.PerformUndo();
            Assert.That(session.Towers.Count, Is.EqualTo(2));
            Undo.PerformUndo();
            Assert.That(session.Towers.Count, Is.EqualTo(1));
            Assert.That(tower.transform.position, Is.EqualTo(new Vector3(7, .25f, 1)));
        }

        [Test]
        public void InvalidLayoutRollsBackAllChangesAndLeavesRouteReferencesIntact()
        {
            var cells = session.Grid.SceneCells;
            var route = session.Route.Cells.ToArray();
            var tower = session.Towers.Single();
            var towerId = tower.GetInstanceID();
            draft.Paint(new Vector2Int(0, 5), (int)CellKind.Obstacle, 1);
            draft.Paint(new Vector2Int(1, 1), (int)CellKind.Obstacle, 1);
            draft.towers.Clear();
            Assert.Throws<InvalidOperationException>(() => MapBuilderUtility.ApplyToScene(draft, scene));
            Assert.That(session.Grid.SceneCells, Is.EquivalentTo(cells));
            Assert.That(session.Route.Cells, Is.EqualTo(route));
            Assert.That(session.Towers.Single().GetInstanceID(), Is.EqualTo(towerId));
            Assert.That(Cell(0, 5).Kind, Is.EqualTo(CellKind.Ground));
            Assert.That(LevelValidation.Validate(scene).Where(i => i.IsError), Is.Empty);
        }

        [Test]
        public void ApplySurvivesSaveReopenWithoutSavingAutomatically()
        {
            var path = folder + "/Existing.unity";
            EditorSceneManager.SaveScene(scene, path);
            var before = File.ReadAllBytes(path);
            draft.Paint(new Vector2Int(0, 5), (int)CellKind.Obstacle, 1);
            MapBuilderUtility.ApplyToScene(draft, scene);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
            Assert.That(scene.isDirty, Is.True);
            EditorSceneManager.SaveScene(scene);
            scene = EditorSceneManager.OpenScene(path);
            session = LevelValidation.Components<BattleSession>(scene).Single();
            Assert.That(Cell(0, 5).Kind, Is.EqualTo(CellKind.Obstacle));
            Assert.That(session.ManualSquadMovement && session.UsesSceneRosterOnly, Is.True);
            Assert.That(session.LevelTimeMultiplier, Is.EqualTo(1.7f));
            Assert.That(LevelValidation.Validate(scene).Where(i => i.IsError), Is.Empty);
        }

        [Test]
        public void RefusesAnotherSceneAndProtectsObjectsAttachedToReplacedTiles()
        {
            EditorSceneManager.SaveScene(scene, folder + "/Original.unity");
            var other = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                MapBuilderUtility.Populate(original, other);
                Assert.Throws<InvalidOperationException>(() => MapBuilderUtility.ApplyToScene(draft, other));
            }
            finally { EditorSceneManager.CloseScene(other, true); }
            var child = new GameObject("Attached character", typeof(UnitView));
            child.transform.SetParent(Cell(0, 5).transform, false);
            draft.Paint(new Vector2Int(0, 5), (int)CellKind.Obstacle, 1);
            Assert.Throws<InvalidOperationException>(() => MapBuilderUtility.ApplyToScene(draft, scene));
            Assert.That(child != null, Is.True);
            Assert.That(Cell(0, 5).Kind, Is.EqualTo(CellKind.Ground));
        }

        [Test]
        public void ReadAndApplyKeepRotatedWallOrientationAndSupportRemovalUndo()
        {
            draft.Paint(new Vector2Int(1, 4), (int)CellKind.Road, 3);
            draft.PlaceTower(new Vector2Int(1, 4), AssetDatabase.LoadAssetAtPath<GameObject>(
                MapBuilderUtility.Kit + "Towers/Wall_3x1_X.prefab"), AttackArea.Square);
            MapBuilderUtility.ApplyToScene(draft, scene);
            var wall = session.Towers.Single(t => t.IsWall);
            wall.transform.rotation = Quaternion.Euler(0, 90, 0);
            Object.DestroyImmediate(draft); draft = MapBuilderUtility.Read(scene);
            Assert.That(wall.Footprint, Is.EqualTo(new Vector2Int(1, 3)));
            MapBuilderUtility.ApplyToScene(draft, scene);
            Assert.That(session.Towers.Single(t => t.IsWall), Is.SameAs(wall));
            Assert.That(wall.Footprint, Is.EqualTo(new Vector2Int(1, 3)));
            Assert.That(Quaternion.Angle(wall.transform.rotation, Quaternion.Euler(0, 90, 0)), Is.LessThan(.01f));
            draft.towers.RemoveAll(t => t.source.IsWall);
            MapBuilderUtility.ApplyToScene(draft, scene);
            Assert.That(session.Towers.Any(t => t.IsWall), Is.False);
            Undo.PerformUndo();
            Assert.That(LevelValidation.Validate(scene).Where(i => i.IsError), Is.Empty);
            Assert.That(session.Towers.Single(t => t.IsWall).Footprint, Is.EqualTo(new Vector2Int(1, 3)));
        }
    }
}
