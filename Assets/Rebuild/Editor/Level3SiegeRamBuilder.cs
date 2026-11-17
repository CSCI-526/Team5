using System;
using System.IO;
using System.Linq;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Session;
using Team5.Rebuild.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Team5.Rebuild.Editor
{
    public static class Level3SiegeRamBuilder
    {
        public const string ScenePath="Assets/Rebuild/Scenes/Level3GridBattle.unity";
        [MenuItem("Team5/Rebuild/Level 3/Enable siege ram gate introduction")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            Directory.CreateDirectory("Logs/Level3SiegeRam");
            if(!File.Exists("Logs/Level3SiegeRam/Before.unity"))File.Copy(ScenePath,"Logs/Level3SiegeRam/Before.unity");
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var owner=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BattlePresentation>(true)).Single();
            var session=owner.Session;
            var terrain=string.Join("|",session.Grid.SceneCells.Select(c=>$"{c.Occupancy}:{c.Kind}"));
            var route=session.Route.CaptureTopCenters();var settings=EditorJsonUtility.ToJson(session);
            var towers=Level3MapCompatibilityBuilder.TowerSignature(session);
            var start=session.Route.Cells[0].TopCenter;
            var extra=session.Grid.SceneCells.Where(c=>c.Kind==CellKind.Spawn&&Vector3.Distance(c.TopCenter,start)>2).ToArray();
            if(extra.Length!=9)throw new InvalidOperationException("Expected one separate 3x3 support start; found "+extra.Length+" cells.");
            var center=extra.Single(c=>extra.All(o=>o.Occupancy.y==c.Occupancy.y&&
                Mathf.Abs(o.Occupancy.x-c.Occupancy.x)<=1&&Mathf.Abs(o.Occupancy.z-c.Occupancy.z)<=1));
            var fleet=owner.GetComponent<SiegeRamFleet>()??owner.gameObject.AddComponent<SiegeRamFleet>();
            var data=new SerializedObject(fleet);
            data.FindProperty("owner").objectReferenceValue=owner;
            data.FindProperty("icon").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Sprite>(SiegeRamBuilder.Folder+"/SiegeRamIconOriginal.png");
            data.FindProperty("vehiclePrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<SiegeRamView>(SiegeRamBuilder.Visual);
            data.FindProperty("supportSpawnCenter").objectReferenceValue=center;
            data.FindProperty("unlockAfterWoodenGateStop").boolValue=true;
            data.ApplyModifiedPropertiesWithoutUndo();
            if(terrain!=string.Join("|",session.Grid.SceneCells.Select(c=>$"{c.Occupancy}:{c.Kind}"))||
                !route.SequenceEqual(session.Route.CaptureTopCenters())||settings!=EditorJsonUtility.ToJson(session)||
                towers!=Level3MapCompatibilityBuilder.TowerSignature(session))
                throw new InvalidOperationException("Map or battle configuration changed.");
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Logs/Level3SiegeRam/Build.txt",$"Support start: {center.TopCenter}\nTerrain, route, towers and battle settings preserved.\n");
            Debug.Log("LEVEL3_SIEGE_RAM_READY: support start "+center.TopCenter);
        }
    }
}
