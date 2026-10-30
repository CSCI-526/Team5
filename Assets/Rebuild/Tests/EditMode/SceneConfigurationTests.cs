using System.IO;
using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Editor;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class SceneConfigurationTests
    {
        private const string RoundTripPath = "Assets/Rebuild/Tests/EditMode/SceneRoundTrip.tmp.unity";
        private BattleSession session;
        [SetUp]
        public void LoadPrototype()
        {
            EditorSceneManager.OpenScene(PrototypeSceneBuilder.ScenePath, OpenSceneMode.Single);
            session = Object.FindFirstObjectByType<BattleSession>();
        }
        [TearDown]
        public void Cleanup()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (File.Exists(RoundTripPath)) AssetDatabase.DeleteAsset(RoundTripPath);
        }

        [Test]
        public void SavedPrototypeHasExpectedStaticLayoutAndValidRoute()
        {
            Assert.That(session.ValidateConfiguration(), Is.Empty);
            Assert.That(session.Grid.SceneCells.Count(cell => cell.Occupancy.y == 0), Is.EqualTo(96));
            Assert.That(session.Grid.SceneCells.Count(cell => cell.Occupancy.y > 0), Is.EqualTo(4));
            Assert.That(session.Towers.Count, Is.EqualTo(3));
            Assert.That(session.Route.Cells.Count, Is.EqualTo(18));
            Assert.That(session.Route.Cells[0].Kind, Is.EqualTo(CellKind.Spawn));
            Assert.That(session.Route.Cells[17].Kind, Is.EqualTo(CellKind.Goal));
            Assert.That(session.Grid.SceneCells.All(cell => cell.transform.lossyScale == Vector3.one), Is.True);
            Assert.That(session.Grid.GetComponentsInChildren<MeshRenderer>().Length +
                session.Towers.Sum(tower => tower.GetComponentsInChildren<MeshRenderer>().Length), Is.EqualTo(103));
            Assert.That(Object.FindObjectsByType<Animator>(FindObjectsSortMode.None), Is.Empty);
        }

        [Test]
        public void MovingSceneTowerChangesCapturedPositionAndActualAttacks()
        {
            var tower = session.Towers[0];
            var before = tower.CaptureDefinition(1);
            var sparePlatform = session.Grid.SceneCells.Single(cell => cell.Occupancy == new Vector3Int(1, 0, 5));
            tower.transform.position = sparePlatform.TopCenter;
            Assert.That(session.ValidateConfiguration(), Is.Empty);
            var after = tower.CaptureDefinition(1);
            Assert.That(after.Position, Is.EqualTo(tower.transform.position));
            Assert.That(after.Position, Is.Not.EqualTo(before.Position));
            // A unit beside the old tower is in its old range and outside its new range.
            var points = new[] { new Vector3(2, 0.25f, 2), new Vector3(3, 0.25f, 2) };
            BattleSimulation Make(TowerDefinition definition) => new BattleSimulation(new BattleDefinition(points,
                new[] { definition }, new UnitDefinition(100, 0.01f), 1, 1, 1));
            var oldSimulation = Make(before);
            var newSimulation = Make(after);
            oldSimulation.Step(0.02f);
            newSimulation.Step(0.02f);
            Assert.That(oldSimulation.Units[0].Hp, Is.LessThan(100));
            Assert.That(newSimulation.Units[0].Hp, Is.EqualTo(100));
        }

        [Test]
        public void SceneTowerCanMoveFreelyWithinPlatformWithoutSnapping()
        {
            var tower = session.Towers[0];
            tower.transform.position += new Vector3(0.2f, 0, -0.15f);
            var captured = session.CaptureDefinition();
            Assert.That(captured.Towers[0].Position, Is.EqualTo(tower.transform.position));
            Assert.That(captured.Towers[0].Position.x, Is.EqualTo(2.2f));
        }

        [Test]
        public void MovingRoadTransformChangesNextSimulationWaypointsAndMovement()
        {
            var old = session.CaptureDefinition();
            // Move an actual Road cell and Goal, swapping ground cells to retain unique occupancy.
            var road = session.Route.Cells[16];
            var roadGround = session.Grid.SceneCells.Single(cell => cell.Occupancy == new Vector3Int(9, 0, 1));
            var roadPosition = road.transform.position;
            road.transform.position = roadGround.transform.position;
            roadGround.transform.position = roadPosition;
            var goal = session.Route.Cells[17];
            var ground = session.Grid.SceneCells.Single(cell => cell.Occupancy == new Vector3Int(10, 0, 1));
            var position = goal.transform.position;
            goal.transform.position = ground.transform.position;
            ground.transform.position = position;
            var updated = session.CaptureDefinition();
            Assert.That(updated.Waypoints[16], Is.EqualTo(road.TopCenter));
            Assert.That(updated.Waypoints[16], Is.Not.EqualTo(old.Waypoints[16]));
            Assert.That(updated.Waypoints[17], Is.EqualTo(goal.TopCenter));
            Assert.That(updated.Waypoints[17], Is.Not.EqualTo(old.Waypoints[17]));
            var sim = new BattleSimulation(new BattleDefinition(updated.Waypoints, new TowerDefinition[0],
                new UnitDefinition(100, 16), 1, 1, 2));
            sim.Step(1);
            Assert.That(sim.Units[0].Position, Is.EqualTo(road.TopCenter));
            sim.Step(1);
            Assert.That(sim.Units[0].Position, Is.EqualTo(goal.TopCenter));
            Assert.That(sim.ReachedGoalCount, Is.EqualTo(1));
        }

        [Test]
        public void GridConflictsAndDisconnectedRoutesAreDetected()
        {
            session.Route.Cells[1].transform.position = session.Route.Cells[3].transform.position;
            var errors = session.ValidateConfiguration();
            Assert.That(errors.Any(error => error.Contains("Grid conflict")), Is.True);
            Assert.That(errors.Any(error => error.Contains("disconnected")), Is.True);
            Assert.Throws<System.InvalidOperationException>(() => session.CaptureDefinition());
        }

        [Test]
        public void UnequalHeightBlockedAndMissingRouteCellsAreDetected()
        {
            var road = session.Route.Cells[2];
            road.transform.position += Vector3.up * BattleCell.LayerHeight;
            Assert.That(session.ValidateConfiguration().Any(error => error.Contains("unequal height")), Is.True);
            road.transform.position -= Vector3.up * BattleCell.LayerHeight;
            var stacked = session.Grid.SceneCells.First(cell => cell.Occupancy.y == 1);
            stacked.transform.position = road.transform.position + Vector3.up * BattleCell.LayerHeight;
            Assert.That(session.ValidateConfiguration().Any(error => error.Contains("not passable")), Is.True);
            var data = new SerializedObject(session.Route);
            data.FindProperty("cells").GetArrayElementAtIndex(1).objectReferenceValue = null;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(session.ValidateConfiguration().Any(error => error.Contains("missing")), Is.True);
        }

        [Test]
        public void SaveAndReopenPreservesEditedTransformsAndSceneReferences()
        {
            var tower = session.Towers[0];
            tower.transform.position = session.Grid.SceneCells.Single(cell => cell.Occupancy == new Vector3Int(1, 0, 5)).TopCenter;
            var expected = session.CaptureDefinition();
            Assert.That(EditorSceneManager.SaveScene(session.gameObject.scene, RoundTripPath, true), Is.True);
            EditorSceneManager.OpenScene(RoundTripPath, OpenSceneMode.Single);
            var reloaded = Object.FindFirstObjectByType<BattleSession>();
            var actual = reloaded.CaptureDefinition();
            Assert.That(actual.Waypoints, Is.EqualTo(expected.Waypoints));
            Assert.That(actual.Towers.Select(value => value.Position), Is.EqualTo(expected.Towers.Select(value => value.Position)));
            Assert.That(reloaded.Grid.SceneCells.Length, Is.EqualTo(100));
            Assert.That(reloaded.Route.Cells.All(cell => cell.gameObject.scene == reloaded.gameObject.scene), Is.True);
        }
    }
}
