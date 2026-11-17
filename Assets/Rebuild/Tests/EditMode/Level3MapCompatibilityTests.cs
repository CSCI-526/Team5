using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Editor;
using Team5.Rebuild.Editor.LevelAuthoring;
using Team5.Rebuild.Session;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Team5.Rebuild.Tests
{
    public sealed class Level3MapCompatibilityTests
    {
        [Test]public void SavedLevel3CanBeReadValidatedAndAppliedWithoutChangingTowerData()
        {
            var previous=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.OpenScene(Level3MapCompatibilityBuilder.ScenePath,OpenSceneMode.Additive);
            try
            {
                var session=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BattleSession>(true)).Single();
                var signature=Level3MapCompatibilityBuilder.TowerSignature(session);
                var settings=EditorJsonUtility.ToJson(session);var route=session.Route.CaptureTopCenters();var count=session.Grid.SceneCells.Length;
                foreach(var tower in session.Towers)
                {
                    var source=PrefabUtility.GetCorrespondingObjectFromSource(tower.gameObject);
                    Assert.That(source,Is.Not.Null,tower.name);Assert.That(source.GetComponent<Team5.Rebuild.World.BattleTower>(),Is.Not.Null,tower.name);
                    Assert.That(tower.transform.Find("Tower Visual"),Is.Not.Null,"Keep the new tower model, not the placeholder cube.");
                    var oldMesh=tower.transform.Find("Tower Mesh");
                    Assert.That(oldMesh==null||!oldMesh.gameObject.activeSelf,Is.True,"The old cube mesh must be hidden.");
                    var visual=tower.transform.Find("Tower Visual");
                    var area=tower.CaptureDefinition(1).Area;
                    if(area!=Team5.Rebuild.Simulation.AttackArea.Square)
                        Assert.That(Vector3.Dot(visual.TransformDirection(Vector3.left),Team5.Rebuild.Simulation.SquadCombatRules.Direction(area)),Is.GreaterThan(.999f));
                }
                var draft=MapBuilderUtility.Read(scene);
                try
                {
                    Assert.That(draft.towers.Count,Is.EqualTo(session.Towers.Count));
                    Assert.That(MapBuilderUtility.Validate(draft).Where(s=>s.StartsWith("ERROR:")),Is.Empty);
                    MapBuilderUtility.ApplyToScene(draft,scene);
                    Assert.That(Level3MapCompatibilityBuilder.TowerSignature(session),Is.EqualTo(signature));
                    Assert.That(EditorJsonUtility.ToJson(session),Is.EqualTo(settings));
                    Assert.That(session.Route.CaptureTopCenters(),Is.EqualTo(route));Assert.That(session.Grid.SceneCells.Length,Is.EqualTo(count));
                }
                finally {Object.DestroyImmediate(draft);}
            }
            finally
            {
                EditorSceneManager.CloseScene(scene,true);
                if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
            }
        }
    }
}
