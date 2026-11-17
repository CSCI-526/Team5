using System;
using System.IO;
using System.Linq;
using Team5.Rebuild.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Team5.Rebuild.Editor
{
    public static class Level3CameraBuilder
    {
        [MenuItem("Team5/Rebuild/Level 3/Enable bounded squad camera")]
        public static void Build()
            =>BuildScene("Assets/Rebuild/Scenes/Level3GridBattle.unity","Logs/Level3FollowCamera");
        [MenuItem("Team5/Rebuild/Level 4/Enable bounded squad camera")]
        public static void BuildLevel4()
            =>BuildScene("Assets/Rebuild/Scenes/Level4GridBattle.unity","Logs/Level4FollowCamera");
        private static void BuildScene(string path,string logFolder)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            Directory.CreateDirectory(logFolder);
            if(!File.Exists(logFolder+"/Before.unity"))File.Copy(path,logFolder+"/Before.unity");
            var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
            var roots=scene.GetRootGameObjects();
            var owner=roots.SelectMany(g=>g.GetComponentsInChildren<BattlePresentation>(true)).Single();
            var camera=roots.SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).Single(c=>c.CompareTag("MainCamera"));
            var follow=camera.GetComponent<Level3SquadCamera>()??camera.gameObject.AddComponent<Level3SquadCamera>();
            var data=new SerializedObject(follow);data.FindProperty("owner").objectReferenceValue=owner;data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log(path+": bounded squad camera ready; original camera framing retained as runtime boundary; zoom 1.6x.");
        }
    }
}
