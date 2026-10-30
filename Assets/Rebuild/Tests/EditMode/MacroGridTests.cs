using System;
using System.Collections.Generic;
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
using Object = UnityEngine.Object;

namespace Team5.Rebuild.Tests
{
    public sealed class MacroGridTests
    {
        private BattleSession session;
        private const string RoundTrip = "Assets/Rebuild/Tests/EditMode/MacroRoundTrip.tmp.unity";
        [SetUp]
        public void Load()
        {
            EditorSceneManager.OpenScene(MacroPrototypeSceneBuilder.ScenePath);
            session = Object.FindFirstObjectByType<BattleSession>();
        }
        [TearDown]
        public void Close()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            if (File.Exists(RoundTrip)) AssetDatabase.DeleteAsset(RoundTrip);
        }

        [Test]
        public void MapIs36By24WithNineActualSmallCellsPerMacro()
        {
            Assert.That(session.ValidateConfiguration(), Is.Empty);
            var ground = session.Grid.SceneCells.Where(cell => cell.Occupancy.y == 0).ToArray();
            Assert.That(ground.Length, Is.EqualTo(36 * 24));
            Assert.That(ground.Select(cell => cell.Occupancy.x).Distinct().Count(), Is.EqualTo(36));
            Assert.That(ground.Select(cell => cell.Occupancy.z).Distinct().Count(), Is.EqualTo(24));
            Assert.That(session.Grid.SceneCells.Length, Is.EqualTo(900));
            for (var z = 0; z < 8; z++)
            for (var x = 0; x < 12; x++)
            {
                var group = session.Grid.GetMacroCells(new Vector3Int(x, 0, z));
                Assert.That(group.Length, Is.EqualTo(9));
                Assert.That(group.All(cell => cell != null), Is.True);
                Assert.That(group.Distinct().Count(), Is.EqualTo(9));
                Assert.That(group.All(cell => BattleGrid.ToMacroCell(cell.Occupancy) == new Vector3Int(x, 0, z)), Is.True);
            }
            Assert.That(BattleGrid.ToMacroCell(new Vector3Int(-1, 2, -3)), Is.EqualTo(new Vector3Int(-1, 2, -1)));
            Assert.That(session.Grid.SceneCells.All(cell => cell.transform.lossyScale == Vector3.one &&
                cell.transform.Find("Cell Mesh").localScale == BattleCell.Size), Is.True);
            Assert.That(session.Grid.transform.parent.Find("Decorative Border Tiles").childCount, Is.EqualTo(1404));
        }

        [Test]
        public void EachTowerOwnsExactlyNineCellsAndConflictsAreRejected()
        {
            var definition = session.CaptureDefinition();
            var sim = new BattleSimulation(definition);
            foreach (var tower in session.Towers)
            {
                Assert.That(tower.FootprintSize, Is.EqualTo(3));
                var key = SmallCellOccupancy.PositionToCell(tower.transform.position);
                var occupied = session.Grid.GetMacroOccupancy(BattleGrid.ToMacroCell(key), sim);
                Assert.That(occupied.Count(cell => cell.TowerIds.Count == 1), Is.EqualTo(9));
            }
            session.Towers[1].transform.position = session.Towers[0].transform.position;
            Assert.That(session.ValidateConfiguration().Any(error => error.Contains("overlap")), Is.True);
        }

        [Test]
        public void MovingOneTowerToSpareMacroChangesItsFootprintAndActualAttacks()
        {
            var before = session.CaptureDefinition();
            var tower = session.Towers[0];
            var oldCenter = SmallCellOccupancy.PositionToCell(tower.transform.position);
            tower.transform.position = new Vector3(4, 0.25f, 16);
            var after = session.CaptureDefinition();
            Assert.That(after.Towers[0].Position, Is.EqualTo(tower.transform.position));
            var updated = new BattleSimulation(after);
            Assert.That(updated.GetCellOccupancy(oldCenter).TowerIds, Is.Empty);
            Assert.That(session.Grid.GetMacroOccupancy(new Vector3Int(1, 0, 5), updated).All(cell => cell.TowerIds.SequenceEqual(new[] { 1 })), Is.True);
            BattleSimulation Make(TowerDefinition definition) => new BattleSimulation(new BattleDefinition(
                new[] { new Vector3(7, 0.25f, 7), new Vector3(8, 0.25f, 7) }, new[] { definition },
                new UnitDefinition(100, 0.01f), 1, 1, 1));
            var oldBattle = Make(before.Towers[0]);
            var newBattle = Make(after.Towers[0]);
            oldBattle.Step(0.02f);
            newBattle.Step(0.02f);
            Assert.That(oldBattle.Units[0].Hp, Is.EqualTo(82));
            Assert.That(newBattle.Units[0].Hp, Is.EqualTo(100));
        }

        [Test]
        public void ExactHalfCellMovementStillGivesNineDistinctOccupiedCells()
        {
            var sim = new BattleSimulation(new BattleDefinition(new[] { new Vector3(1, 0.25f, 1), new Vector3(5, 0.25f, 1) },
                Array.Empty<TowerDefinition>(), new UnitDefinition(100, 1), 9, 3, 10, 3));
            sim.Step(0.5f);
            Assert.That(sim.Units.Select(unit => unit.OccupiedCell).Distinct().Count(), Is.EqualTo(9));
            Assert.That(sim.Units.All(unit => sim.GetCellOccupancy(unit.OccupiedCell).UnitIds.Count == 1), Is.True);
        }

        [Test]
        public void TowerRequiresAllNineLegalEqualHeightSupportsAndMacroCenter()
        {
            var tower = session.Towers[0];
            Assert.That(session.ValidateConfiguration(), Is.Empty);
            var support = session.Grid.GetMacroCells(BattleGrid.ToMacroCell(SmallCellOccupancy.PositionToCell(tower.transform.position)))[0];
            support.transform.position += Vector3.up * 0.5f;
            Assert.That(session.ValidateConfiguration().Any(error => error.Contains("footprint")), Is.True);
            support.transform.position -= Vector3.up * 0.5f;
            var data = new SerializedObject(support);
            data.FindProperty("kind").enumValueIndex = (int)CellKind.Road;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(session.ValidateConfiguration().Any(error => error.Contains("footprint")), Is.True);
            tower.transform.position += Vector3.right * 0.2f;
            Assert.That(session.ValidateConfiguration().Any(error => error.Contains("centered")), Is.True);
        }

        [Test]
        public void MissingOrBlockedCornerClearancePreventsBattle()
        {
            Assert.That(session.ValidateConfiguration(), Is.Empty);
            var points = session.Route.Cells;
            var turn = Enumerable.Range(1, points.Count - 2).First(i =>
                points[i].Occupancy - points[i - 1].Occupancy != points[i + 1].Occupancy - points[i].Occupancy);
            var key = points[turn].Occupancy + new Vector3Int(1, 0, 1);
            Assert.That(session.Grid.TryGetCell(key, out var corner), Is.True);
            var data = new SerializedObject(corner);
            data.FindProperty("kind").enumValueIndex = (int)CellKind.Obstacle;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(session.ValidateConfiguration().Any(error => error.Contains("Formation clearance blocked")), Is.True);
            Assert.Throws<InvalidOperationException>(() => session.CaptureDefinition());
            corner.transform.position += Vector3.up * 0.5f;
            Assert.That(session.ValidateConfiguration().Any(error => error.Contains("Formation clearance blocked")), Is.True);
        }

        [Test]
        public void NineUnitsStayOnRoadThroughEveryCornerAndDoNotShareCells()
        {
            var captured = session.CaptureDefinition();
            var sim = new BattleSimulation(new BattleDefinition(captured.Waypoints, Array.Empty<TowerDefinition>(),
                captured.Unit, 9, 3, 10, 3));
            for (var i = 0; i < 2000 && sim.Outcome == BattleOutcome.Running; i++)
            {
                sim.Step(0.02f);
                var alive = sim.Units.Where(unit => unit.IsAlive).ToArray();
                Assert.That(alive.Select(unit => unit.OccupiedCell).Distinct().Count(), Is.EqualTo(alive.Length));
                foreach (var unit in alive)
                {
                    Assert.That(session.Grid.TryGetCell(unit.OccupiedCell, out var cell), Is.True);
                    Assert.That(cell.IsRouteSurface, Is.True);
                    Assert.That((unit.AnchorPosition - alive[0].AnchorPosition).magnitude, Is.LessThan(0.0002f));
                    Assert.That(sim.GetCellOccupancy(unit.OccupiedCell).UnitIds, Is.EqualTo(new[] { unit.Id }));
                }
            }
            Assert.That(sim.SpawnedCount, Is.EqualTo(9));
            Assert.That(sim.ReachedGoalCount, Is.EqualTo(9));
        }

        [Test]
        public void DeathLeavesFormationSlotVacantAndDoesNotChangeOtherOffsets()
        {
            var captured = session.CaptureDefinition();
            var tower = new TowerDefinition(1, captured.Waypoints[0] + new Vector3(-1, 0, -1), 0.2f, 1000, 100, TargetRule.Nearest);
            var sim = new BattleSimulation(new BattleDefinition(captured.Waypoints, new[] { tower }, captured.Unit, 9, 3, 10, 3));
            var died = new List<int>();
            sim.UnitDied += value => died.Add(value.UnitId);
            sim.Step(0.02f);
            Assert.That(died, Is.EqualTo(new[] { 1 }));
            var slots = sim.Units.Select(unit => unit.FormationSlot).ToArray();
            for (var i = 0; i < 300; i++) sim.Step(0.02f);
            Assert.That(sim.Units.Select(unit => unit.FormationSlot), Is.EqualTo(slots));
            Assert.That(sim.AliveCount, Is.EqualTo(8));
            Assert.That(sim.Units.Where(unit => unit.IsAlive).All(unit => unit.Hp == unit.MaxHp), Is.True);
        }

        [Test]
        public void SceneTransformEditsAndSaveReopenFeedNewBattleDefinitions()
        {
            var before = session.CaptureDefinition();
            session.Grid.transform.position += Vector3.right * 3;
            foreach (var tower in session.Towers) tower.transform.position += Vector3.right * 3;
            var updated = session.CaptureDefinition();
            Assert.That(updated.Waypoints[0], Is.EqualTo(before.Waypoints[0] + Vector3.right * 3));
            Assert.That(updated.Towers[0].Position, Is.EqualTo(before.Towers[0].Position + Vector3.right * 3));
            Assert.That(EditorSceneManager.SaveScene(session.gameObject.scene, RoundTrip, true), Is.True);
            EditorSceneManager.OpenScene(RoundTrip);
            var reloaded = Object.FindFirstObjectByType<BattleSession>().CaptureDefinition();
            Assert.That(reloaded.Waypoints, Is.EqualTo(updated.Waypoints));
            Assert.That(reloaded.Towers.Select(tower => tower.Position), Is.EqualTo(updated.Towers.Select(tower => tower.Position)));
            var sim = new BattleSimulation(reloaded);
            sim.Step(0.02f);
            Assert.That(sim.Units[4].Position.x, Is.GreaterThan(updated.Waypoints[0].x));
        }

        [Test]
        public void PerspectiveCameraIncludesAllTerrainAndBordersAt16By9()
        {
            var camera = Camera.main;
            Assert.That(camera.orthographic, Is.False);
            camera.aspect = 16f / 9f;
            var bounds = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Select(renderer => renderer.bounds).ToArray();
            var overall = bounds[0];
            foreach (var box in bounds) overall.Encapsulate(box);
            for (var z = -1; z <= 1; z += 2)
            for (var y = -1; y <= 1; y += 2)
            for (var x = -1; x <= 1; x += 2)
            {
                var viewport = camera.WorldToViewportPoint(overall.center + Vector3.Scale(overall.extents, new Vector3(x, y, z)));
                Assert.That(viewport.x, Is.InRange(0f, 1f));
                Assert.That(viewport.y, Is.InRange(0f, 1f));
                Assert.That(viewport.z, Is.InRange(camera.nearClipPlane, camera.farClipPlane));
            }
        }
    }
}
