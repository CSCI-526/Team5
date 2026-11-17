using System;
using System.IO;
using System.Linq;
using Team5.Rebuild.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Team5.Rebuild.Editor
{
    public static class Level2RailPlatformBuilder
    {
        public const string ScenePath="Assets/Rebuild/Scenes/Level2GridBattle.unity";
        [MenuItem("Team5/Rebuild/Level 2/Add railway and squad platform")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            RailwayPathBuilder.BuildScene(ScenePath,"Logs/Level2RailPlatform");
            var scene=SceneManager.GetSceneByPath(ScenePath);
            var owner=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BattlePresentation>(true)).Single();
            var session=owner.Session;
            if(session.ContinuousSquadMovement||session.Route.RequiredWidth!=3)throw new InvalidOperationException("Level2 requires a fixed three-wide route for the platform.");
            var signature=EditorJsonUtility.ToJson(session);
            var platform=owner.GetComponent<SquadPlatformView>();
            if(platform==null)platform=owner.gameObject.AddComponent<SquadPlatformView>();
            var model=platform.Model;
            if(model==null)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(SquadPlatformBuilder.PrefabPath);
                if(prefab==null)throw new InvalidOperationException("Missing existing blue-white platform prefab.");
                model=((GameObject)PrefabUtility.InstantiatePrefab(prefab,owner.transform)).transform;
            }
            model.position=session.Route.Cells[0].TopCenter;model.rotation=Quaternion.identity;
            var rig=model.Find("Wheel Rig");
            var direction=session.Route.Cells[1].TopCenter-session.Route.Cells[0].TopCenter;
            rig.localRotation=Quaternion.Euler(0,Mathf.Atan2(-direction.z,direction.x)*Mathf.Rad2Deg,0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rig);
            var wheels=rig.Cast<Transform>().Where(t=>t.name.StartsWith("Wheel ")).ToArray();
            var data=new SerializedObject(platform);
            data.FindProperty("owner").objectReferenceValue=owner;data.FindProperty("model").objectReferenceValue=model;
            data.FindProperty("wheelRig").objectReferenceValue=rig;
            var entries=data.FindProperty("rollingWheels");entries.arraySize=wheels.Length;
            for(var i=0;i<wheels.Length;i++)entries.GetArrayElementAtIndex(i).objectReferenceValue=wheels[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            if(wheels.Length!=12||model.GetComponentsInChildren<Collider>().Length!=0)throw new InvalidOperationException("Expected 12 visual wheels without collisions.");
            if(signature!=EditorJsonUtility.ToJson(session))throw new InvalidOperationException("Original battle configuration changed.");
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Logs/Level2RailPlatform/PlatformChecks.txt","Level2 uses the existing blue-white 3x3 platform and three railway lanes.\n12 wheels; no added physics colliders.\nOriginal fixed route, roster and battle settings retained.\n");
            Debug.Log("Level2 railway and blue-white squad platform configured.");
        }
    }
}
