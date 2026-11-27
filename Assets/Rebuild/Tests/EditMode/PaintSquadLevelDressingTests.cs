using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Editor;
using Team5.Rebuild.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class PaintSquadLevelDressingTests
    {
        [TearDown]
        public void CloseScene() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        [Test]
        public void PropPrefabsArePresentationOnly()
        {
            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { PaintSquadLevelDressing.PrefabFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            Assert.That(prefabs.Length, Is.EqualTo(16));
            foreach (var prefab in prefabs)
            {
                Assert.That(prefab.name, Does.StartWith("PaintSquad"));
                Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty, prefab.name);
                Assert.That(prefab.GetComponentsInChildren<Renderer>(true), Is.Not.Empty, prefab.name);
                Assert.That(prefab.GetComponentsInChildren<Transform>(true).All(t => t.gameObject.layer == PaintSquadLevelDressing.IgnoreRaycastLayer), prefab.name);
            }
        }

        [Test]
        public void EveryLevelDressingKeepsCombatSurfacesClear([ValueSource(nameof(LevelNames))] string level)
        {
            var scene = EditorSceneManager.OpenScene(PaintSquadLevelDressing.ScenePath(level), OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects().Where(g => g.name == PaintSquadLevelDressing.RootName).ToArray();
            Assert.That(roots.Length, Is.EqualTo(1), "one dressing root");
            var root = roots[0];
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty, "no colliders");
            var transforms = root.GetComponentsInChildren<Transform>(true);
            Assert.That(transforms.All(t => t.gameObject.layer == PaintSquadLevelDressing.IgnoreRaycastLayer), "Ignore Raycast layer");
            Assert.That(root.GetComponentsInChildren<BattleCell>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<BattleTower>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThan(0), "scenery placed");
            // V7 intentionally permits scenery on non-walkable meadow and obstacle cells.
            // Unlike the old border-only test, protect the actual road and tower platforms.
            var occupied = new HashSet<Vector2Int>(scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BattleCell>(true))
                .Where(c => c.IsRouteSurface || c.Kind == CellKind.TowerPlatform)
                .Select(c => new Vector2Int(c.Occupancy.x, c.Occupancy.z)));
            Assert.That(occupied, Is.Not.Empty);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                Assert.That(PaintSquadLevelDressing.OverlapsCell(renderer.bounds, occupied), Is.False, $"{renderer.name} at {renderer.bounds.center} overlaps the combat surface");
        }

        private static IEnumerable<string> LevelNames => PaintSquadLevelDressing.Levels;
    }
}
