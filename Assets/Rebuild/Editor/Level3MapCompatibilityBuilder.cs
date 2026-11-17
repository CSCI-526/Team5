using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Team5.Rebuild.Editor.LevelAuthoring;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Team5.Rebuild.Editor
{
    public static class Level3MapCompatibilityBuilder
    {
        public const string ScenePath="Assets/Rebuild/Scenes/Level3GridBattle.unity";
        public static string TowerSignature(BattleSession session)=>string.Join("\n",session.Towers.Select((t,i)=>
            $"{i}:{t.name}:{t.transform.position.ToString("R")}:{t.transform.rotation.ToString("R")}:{t.transform.lossyScale.ToString("R")}:{JsonUtility.ToJson(t)}"));
        private static string SessionSettings(BattleSession session)=>Regex.Replace(EditorJsonUtility.ToJson(session),"\"towers\":\\[[^\\]]*\\]","\"towers\":[]");

        [MenuItem("Team5/Rebuild/Level 3/Replace towers for Map Builder compatibility")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            Directory.CreateDirectory("Logs/Level3MapCompatibility");
            var backup="Logs/Level3MapCompatibility/Before.unity";if(!File.Exists(backup))File.Copy(ScenePath,backup);
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var session=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BattleSession>(true)).Single();
            var original=session.Towers.ToArray();var before=TowerSignature(session);var settings=SessionSettings(session);
            var terrain=string.Join("|",session.Grid.SceneCells.Select(c=>$"{c.Occupancy}:{c.Kind}"));
            var route=session.Route.CaptureTopCenters();
            var replacements=new Dictionary<Object,Object>();var oldRoots=new List<GameObject>();var log=new List<string>();
            foreach(var old in original)
            {
                var source=PrefabUtility.GetCorrespondingObjectFromSource(old.gameObject);
                if(source!=null&&source.GetComponent<BattleTower>()!=null)continue;
                if(old.IsWall||old.IsMinion)throw new InvalidOperationException("A custom wall/minion needs a matching source prefab: "+old.name);
                var definition=old.CaptureDefinition(1);
                var suffix=definition.Area switch {AttackArea.PositiveX=>"PositiveX",AttackArea.NegativeX=>"NegativeX",AttackArea.PositiveZ=>"PositiveZ",AttackArea.NegativeZ=>"NegativeZ",_=>"Square"};
                var path=MapBuilderUtility.Kit+"Towers/"+(old.FootprintSize==3?"Tower_"+suffix:definition.Area==AttackArea.Square?"Tower_Attack_1x1_Square":"Tower_Attack_1x1")+".prefab";
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(prefab==null)throw new InvalidOperationException("Missing suitable existing tower prefab: "+path);
                var outer=PrefabUtility.GetOutermostPrefabInstanceRoot(old.gameObject);
                if(outer!=null&&outer!=old.gameObject)throw new InvalidOperationException("Tower is nested inside another prefab; cannot safely replace: "+old.name);
                var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
                go.name=old.gameObject.name;go.layer=old.gameObject.layer;go.tag=old.gameObject.tag;
                go.transform.SetParent(old.transform.parent,false);
                go.transform.localPosition=old.transform.localPosition;go.transform.localRotation=old.transform.localRotation;go.transform.localScale=old.transform.localScale;
                go.transform.SetSiblingIndex(old.transform.GetSiblingIndex());
                var next=go.GetComponent<BattleTower>();JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(old),next);
                next.enabled=old.enabled;go.SetActive(old.gameObject.activeSelf);
                PrefabUtility.RecordPrefabInstancePropertyModifications(go);
                PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(next);
                replacements.Add(old,next);replacements.Add(old.gameObject,go);replacements.Add(old.transform,go.transform);
                oldRoots.Add(old.gameObject);log.Add(old.name+" -> "+path);
            }
            // Repair scene script references before deleting any old tower roots.
            foreach(var component in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)))
            {
                if(component==null||oldRoots.Any(g=>component.transform.IsChildOf(g.transform)))continue;
                var data=new SerializedObject(component);var property=data.GetIterator();var changed=false;
                while(property.Next(true))
                    if(property.propertyType==SerializedPropertyType.ObjectReference&&property.objectReferenceValue!=null&&replacements.TryGetValue(property.objectReferenceValue,out var replacement))
                    {property.objectReferenceValue=replacement;changed=true;}
                if(changed)
                {
                    data.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(component);
                    if(PrefabUtility.IsPartOfPrefabInstance(component))PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                }
            }
            foreach(var root in oldRoots)Object.DestroyImmediate(root);
            if(before!=TowerSignature(session)||settings!=SessionSettings(session))throw new InvalidOperationException("Original tower combat data/order or session settings changed.");
            if(terrain!=string.Join("|",session.Grid.SceneCells.Select(c=>$"{c.Occupancy}:{c.Kind}"))||!route.SequenceEqual(session.Route.CaptureTopCenters()))
                throw new InvalidOperationException("Original terrain or route changed.");
            var draft=MapBuilderUtility.Read(scene);
            try
            {
                var errors=MapBuilderUtility.Validate(draft).Where(s=>s.StartsWith("ERROR:")).ToArray();
                if(errors.Length>0)throw new InvalidOperationException(string.Join("\n",errors));
                MapBuilderUtility.ApplyToScene(draft,scene);
                if(before!=TowerSignature(session))throw new InvalidOperationException("Map Builder round trip changed tower configuration.");
            }
            finally {Object.DestroyImmediate(draft);}
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllLines("Logs/Level3MapCompatibility/Replacements.txt",log);
            File.WriteAllText("Logs/Level3MapCompatibility/Checks.txt",$"Replaced unsupported tower roots: {oldRoots.Count}\nTower configuration, order, position and rotation preserved.\nOriginal terrain, route and battle settings preserved.\nMap Builder Read/Validate/Apply round trip passed.\n");
            Debug.Log($"Level3 Map Builder compatibility complete: replaced {oldRoots.Count} unsupported tower roots; Read/Validate/Apply passed.");
        }
    }
}
