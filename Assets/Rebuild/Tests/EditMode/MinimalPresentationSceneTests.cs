using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Editor;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Session;
using Team5.Rebuild.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Team5.Rebuild.Tests
{
    public sealed class MinimalPresentationSceneTests
    {
        [SetUp]
        public void Load() => EditorSceneManager.OpenScene(PrototypeSceneBuilder.ScenePath);
        [TearDown]
        public void Close() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);

        [Test]
        public void SavedSceneUsesOriginalEntitiesWithEditableCorrectlySizedMeshes()
        {
            var session = Object.FindFirstObjectByType<BattleSession>();
            Assert.That(session.ValidateConfiguration(), Is.Empty);
            foreach (var cell in session.Grid.SceneCells)
            {
                var mesh = cell.transform.Find("Cell Mesh");
                Assert.That(mesh, Is.Not.Null);
                Assert.That(mesh.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(mesh.localScale, Is.EqualTo(BattleCell.Size));
                Assert.That(mesh.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
                Assert.That(mesh.GetComponent<MeshRenderer>().enabled, Is.True);
                Assert.That(mesh.gameObject.scene, Is.EqualTo(cell.gameObject.scene));
                Assert.That(PrefabUtility.IsPartOfPrefabAsset(mesh), Is.False);
            }
            foreach (var tower in session.Towers)
            {
                var mesh = tower.transform.Find("Tower Mesh");
                Assert.That(mesh.GetComponent<MeshRenderer>(), Is.Not.Null);
                Assert.That(mesh.position.y, Is.GreaterThan(tower.transform.position.y));
            }
            var colors = session.Grid.SceneCells.GroupBy(cell => cell.Kind).Select(group =>
                group.First().GetComponentInChildren<MeshRenderer>().sharedMaterial.GetColor("_BaseColor")).ToArray();
            Assert.That(colors.Distinct().Count(), Is.EqualTo(6));
            Assert.That(Object.FindObjectsByType<Collider>(FindObjectsSortMode.None), Is.Empty);
            var border = session.Grid.transform.parent.Find("Decorative Border Tiles");
            Assert.That(border, Is.Not.Null);
            Assert.That(border.IsChildOf(session.Grid.transform), Is.False);
            Assert.That(border.childCount, Is.EqualTo(156)); // 18x14 outer rectangle minus the 12x8 battle grid.
            Assert.That(border.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
            var positions = border.Cast<Transform>().Select(tile => tile.position).ToArray();
            Assert.That(positions.Distinct().Count(), Is.EqualTo(156));
            foreach (Transform tile in border)
            {
                Assert.That(tile.localScale, Is.EqualTo(BattleCell.Size));
                Assert.That(tile.gameObject.layer, Is.EqualTo(2));
                Assert.That(tile.position.y, Is.Zero);
                Assert.That(tile.position.x, Is.InRange(-3, 14));
                Assert.That(tile.position.z, Is.InRange(-3, 10));
                Assert.That(tile.position.x < 0 || tile.position.x > 11 || tile.position.z < 0 || tile.position.z > 7, Is.True);
                Assert.That(session.Grid.TryGetCell(new Vector3Int(Mathf.RoundToInt(tile.position.x), 0, Mathf.RoundToInt(tile.position.z)), out _), Is.False);
            }
        }

        [Test]
        public void SquarePrefabCameraAndMaterialsUseExistingUniversal3DRenderer()
        {
            var presentation = Object.FindFirstObjectByType<BattlePresentation>();
            Assert.That(presentation.Session, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(presentation.UnitPrefab), Is.EqualTo(MinimalPresentationSetup.UnitPrefabPath));
            Assert.That(presentation.UnitPrefab.GetComponentInChildren<MeshFilter>().sharedMesh.name, Is.EqualTo("Cube"));
            var pipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            Assert.That(pipeline, Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<Camera>().GetUniversalAdditionalCameraData().scriptableRenderer, Is.TypeOf<UniversalRenderer>());
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Any(light => light.type == LightType.Directional), Is.True);
        }

        [Test]
        public void ReopeningScenePreservesStaticMeshReferencesAndUnitPrefab()
        {
            var before = Object.FindFirstObjectByType<BattleSession>();
            var meshAssets = before.Grid.SceneCells.Select(cell => AssetDatabase.GetAssetPath(cell.GetComponentInChildren<MeshRenderer>().sharedMaterial)).ToArray();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            EditorSceneManager.OpenScene(PrototypeSceneBuilder.ScenePath);
            var after = Object.FindFirstObjectByType<BattleSession>();
            Assert.That(after.ValidateConfiguration(), Is.Empty);
            Assert.That(after.Grid.SceneCells.Select(cell => AssetDatabase.GetAssetPath(cell.GetComponentInChildren<MeshRenderer>().sharedMaterial)), Is.EqualTo(meshAssets));
            Assert.That(Object.FindFirstObjectByType<BattlePresentation>().UnitPrefab, Is.Not.Null);
        }
    }
}
