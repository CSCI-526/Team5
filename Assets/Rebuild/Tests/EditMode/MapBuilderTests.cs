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
    public sealed class MapBuilderTests
    {
        private MapDraft draft;
        private string folder;
        private SceneSetup[] setup;

        [SetUp]
        public void SetUp()
        {
            setup = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            draft = ScriptableObject.CreateInstance<MapDraft>(); draft.hideFlags = HideFlags.HideAndDontSave;
            draft.ResetGrid(9, 6);
            folder = "Assets/__MapBuilderTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
        }
        [TearDown]
        public void Close()
        {
            Undo.ClearAll();
            Object.DestroyImmediate(draft);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            AssetDatabase.DeleteAsset(folder);
            if (setup.Any(s => s.isLoaded && s.isActive && !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
        private void BuildValidMap()
        {
            for (var i = 0; i < draft.tiles.Length; i++) draft.tiles[i] = (int)CellKind.Ground;
            draft.Paint(new Vector2Int(1,1), (int)CellKind.Spawn, 3);
            draft.Paint(new Vector2Int(4,1), (int)CellKind.Goal, 3);
            draft.Paint(new Vector2Int(7,1), (int)CellKind.TowerPlatform, 3);
            draft.AppendRoute(new Vector2Int(1,1)); draft.AppendRoute(new Vector2Int(4,1));
        }
        private GameObject Tower => AssetDatabase.LoadAssetAtPath<GameObject>(MapBuilderUtility.Kit + "Towers/Tower_Attack_1x1_Square.prefab");

        [Test]
        public void MacroBrushBoundsRouteExpansionAndUndoAreAtomic()
        {
            Assert.Throws<ArgumentException>(() => draft.ResetGrid(2,6));
            Undo.RegisterCompleteObjectUndo(draft, "Paint test");
            draft.Paint(new Vector2Int(1,1), (int)CellKind.Spawn, 3);
            Assert.That(draft.tiles.Count(value => value == (int)CellKind.Spawn), Is.EqualTo(9));
            Undo.PerformUndo(); Assert.That(draft.tiles.All(value => value == -1), Is.True);
            Undo.PerformRedo(); Assert.That(draft.tiles.Count(value => value == (int)CellKind.Spawn), Is.EqualTo(9));
            BuildValidMap();
            Assert.That(draft.route, Is.EqualTo(new[] { new Vector2Int(1,1), new Vector2Int(2,1), new Vector2Int(3,1), new Vector2Int(4,1) }));
            Assert.Throws<ArgumentException>(() => draft.AppendRoute(new Vector2Int(1,1)));
            Assert.Throws<ArgumentException>(() => draft.AppendRoute(new Vector2Int(5,2)));
            Assert.That(draft.route.Count, Is.EqualTo(4));
        }

        [Test]
        public void TowerNeedsSupportAndCannotOverlapOrHideClearanceErrors()
        {
            BuildValidMap();
            Assert.Throws<ArgumentException>(() => draft.PlaceTower(new Vector2Int(4,1), Tower, AttackArea.Square));
            draft.PlaceTower(new Vector2Int(7,1), Tower, AttackArea.Square);
            Assert.Throws<ArgumentException>(() => draft.PlaceTower(new Vector2Int(7,1), Tower, AttackArea.Square));
            Assert.That(MapBuilderUtility.Validate(draft), Is.Empty);
            draft.Paint(new Vector2Int(2,0), (int)CellKind.Obstacle, 1);
            Assert.That(MapBuilderUtility.Validate(draft).Any(message => message.Contains("clearance")), Is.True);
        }

        [Test]
        public void ValidationDoesNotModifyActiveSceneOrBuildSettings()
        {
            BuildValidMap();
            var active = SceneManager.GetActiveScene();
            var build = File.ReadAllBytes("ProjectSettings/EditorBuildSettings.asset");
            Assert.That(MapBuilderUtility.Validate(draft), Is.Empty);
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(active));
            Assert.That(active.rootCount, Is.Zero);
            Assert.That(File.ReadAllBytes("ProjectSettings/EditorBuildSettings.asset"), Is.EqualTo(build));
        }

        [Test]
        public void GeneratedCameraFitsWideGridWithoutChangingRigAsset()
        {
            draft.ResetGrid(96,6); BuildValidMap();
            var rigPath = MapBuilderUtility.Kit + "Systems/BattleRig_3D.prefab";
            var before = File.ReadAllBytes(rigPath);
            var generated = MapBuilderUtility.Generate(draft);
            var path = folder + "/WideCamera.unity";
            MapBuilderUtility.SaveNew(generated,path);
            var reopened = EditorSceneManager.OpenScene(path);
            {
                var camera = LevelValidation.Components<Camera>(reopened).Single();
                camera.aspect = 16f/9;
                foreach (var x in new[] { -.5f, 95.5f })
                foreach (var z in new[] { -.5f, 5.5f })
                foreach (var y in new[] { -.5f, 3.5f })
                {
                    var point = camera.WorldToViewportPoint(new Vector3(x,y,z));
                    Assert.That(point.x, Is.InRange(0f,1f)); Assert.That(point.y, Is.InRange(0f,1f));
                    Assert.That(point.z, Is.InRange(camera.nearClipPlane,camera.farClipPlane));
                }
            }
            Assert.That(File.ReadAllBytes(rigPath), Is.EqualTo(before));
        }

        [Test]
        public void GeneratedSceneSavesReopensAndReadsBackWithActualCellReferences()
        {
            BuildValidMap(); draft.PlaceTower(new Vector2Int(7,1), Tower, AttackArea.Square);
            var scene = MapBuilderUtility.Generate(draft);
            var path = folder + "/Generated.unity";
            MapBuilderUtility.SaveNew(scene,path);
            Assert.Throws<IOException>(() => MapBuilderUtility.SaveNew(scene,path));
            Assert.Throws<InvalidOperationException>(() => MapBuilderUtility.SaveNew(scene,LevelTemplateUtility.ReservedTestScenePath));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            scene = EditorSceneManager.OpenScene(path);
            var session = LevelValidation.Components<BattleSession>(scene).Single();
            Assert.That(session.Grid.SceneCells.Length, Is.EqualTo(54));
            Assert.That(session.Route.Cells.All(cell => session.Grid.SceneCells.Contains(cell)), Is.True);
            Assert.That(session.Towers.Count, Is.EqualTo(1));
            Assert.That(session.Towers[0].FootprintSize, Is.EqualTo(1));
            Assert.That(LevelValidation.Validate(scene).Where(issue => issue.IsError), Is.Empty);
            var copy = MapBuilderUtility.Read(scene);
            try
            {
                Assert.That(copy.tiles, Is.EqualTo(draft.tiles)); Assert.That(copy.route, Is.EqualTo(draft.route));
                Assert.That(copy.towers.Single().area, Is.EqualTo(AttackArea.Square));
                Assert.That(MapBuilderUtility.Validate(copy), Is.Empty);
            }
            finally { Object.DestroyImmediate(copy); }
            var simulation = new BattleSimulation(session.CaptureDefinition());
            for (var i = 0; i < 2000 && simulation.Outcome == BattleOutcome.Running; i++) simulation.Step(.02f);
            Assert.That(simulation.Outcome, Is.Not.EqualTo(BattleOutcome.Running));
        }
    }
}
