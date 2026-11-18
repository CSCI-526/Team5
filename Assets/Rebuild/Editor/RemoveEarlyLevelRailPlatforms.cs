using System;
using System.IO;
using System.Linq;
using Team5.Rebuild.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Team5.Rebuild.Editor
{
    public static class RemoveEarlyLevelRailPlatforms
    {
        [MenuItem("Team5/Rebuild/Levels 1 and 2/Remove railway artwork and squad platforms")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            Directory.CreateDirectory("Logs/RemoveEarlyRailPlatforms");
            foreach(var level in new[]{1,2})
            {
                var path=$"Assets/Rebuild/Scenes/Level{level}GridBattle.unity";
                var backup=$"Logs/RemoveEarlyRailPlatforms/Level{level}Before.unity";
                if(!File.Exists(backup))File.Copy(path,backup);
                var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                var owner=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BattlePresentation>(true)).Single();
                var session=owner.Session;var settings=EditorJsonUtility.ToJson(session);
                var terrain=string.Join("|",session.Grid.SceneCells.Select(c=>$"{c.Occupancy}:{c.Kind}"));
                var route=session.Route.CaptureTopCenters();var towers=Level3MapCompatibilityBuilder.TowerSignature(session);
                var platform=owner.GetComponent<SquadPlatformView>();
                if(platform!=null)
                {
                    if(platform.Model!=null)Object.DestroyImmediate(platform.Model.gameObject);
                    Object.DestroyImmediate(platform);
                }
                foreach(var root in scene.GetRootGameObjects().Where(g=>g.name=="Railway Path Artwork"))Object.DestroyImmediate(root);
                if(settings!=EditorJsonUtility.ToJson(session)||terrain!=string.Join("|",session.Grid.SceneCells.Select(c=>$"{c.Occupancy}:{c.Kind}"))||
                    !route.SequenceEqual(session.Route.CaptureTopCenters())||towers!=Level3MapCompatibilityBuilder.TowerSignature(session))
                    throw new InvalidOperationException("Map, route or battle configuration changed.");
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                Debug.Log($"Level{level}: removed rail artwork and platform opt-in; original map, route, units and combat settings preserved.");
            }
        }
    }
}
