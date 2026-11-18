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
        public void EveryLevelIsDressedOffTheGameplayGrid([ValueSource(nameof(LevelNames))] string level)
        {
            var scene = EditorSceneManager.OpenScene(PaintSquadLevelDressing.ScenePath(level), OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects().Where(g => g.name == PaintSquadLevelDressing.RootName).ToArray();
            Assert.That(roots.Length, Is.EqualTo(1), "one dressing root");
            var root = roots[0];
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty, "no colliders");
            var transforms = root.GetComponentsInChildren<Transform>(true);
            Assert.That(transforms.All(t => t.gameObject.layer == PaintSquadLevelDressing.IgnoreRaycastLayer), "Ignore Raycast layer");
            Assert.That(transforms.All(t => GameObjectUtility.GetStaticEditorFlags(t.gameObject).HasFlag(StaticEditorFlags.BatchingStatic)), "static");
            Assert.That(root.GetComponentsInChildren<BattleCell>(true), Is.Empty);
            var props = root.transform.Cast<Transform>().Where(t => t.name.StartsWith("PaintSquad")).ToArray();
            Assert.That(props.Length, Is.GreaterThan(20), "props placed");
            Assert.That(props.Count(p => p.name.Contains("Tree")), Is.GreaterThan(5), "trees placed");

            // Every BattleCell is gameplay (decorative tiles carry no BattleCell), so no footprint may overlap one.
            var occupied = new HashSet<Vector2Int>(scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BattleCell>(true))
                .Select(c => new Vector2Int(c.Occupancy.x, c.Occupancy.z)));
            Assert.That(occupied, Is.Not.Empty);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                Assert.That(PaintSquadLevelDressing.OverlapsCell(renderer.bounds, occupied), Is.False, $"{renderer.name} at {renderer.bounds.center} overlaps a BattleCell");
        }

        private static IEnumerable<string> LevelNames => PaintSquadLevelDressing.Levels;
    }
}
