using System;
using System.IO;
using System.Linq;
using Team5.Rebuild.Editor.LevelAuthoring;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Team5.Rebuild.Editor
{
    public static class SiegeTavernBuilder
    {
        public static void Build()
        {
            if(File.Exists(SiegeTavernMode.ShopScene)||File.Exists(SiegeTavernMode.BattleScene))throw new InvalidOperationException("Prototype scenes already exist; preserve manual edits.");
            var shop=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Tavern Camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.075f,.11f);
            var controller=new GameObject("Siege Tavern").AddComponent<SiegeTavernMode>();var so=new SerializedObject(controller);so.FindProperty("shopping").boolValue=true;so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(shop,SiegeTavernMode.ShopScene);
            var battle=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var draft=ScriptableObject.CreateInstance<MapDraft>();draft.ResetGrid(24,18);
            for(var i=0;i<draft.tiles.Length;i++)draft.tiles[i]=(int)CellKind.Ground;
            var route=new System.Collections.Generic.List<Vector2Int>();
            for(var x=1;x<=10;x++)route.Add(new Vector2Int(x,13));
            for(var z=12;z>=4;z--)route.Add(new Vector2Int(10,z));
            for(var x=11;x<=22;x++)route.Add(new Vector2Int(x,4));
            foreach(var p in route)for(var dx=-1;dx<=1;dx++)for(var dz=-1;dz<=1;dz++)draft.tiles[(p.y+dz)*24+p.x+dx]=(int)CellKind.Road;
            foreach(var p in new[]{route.First(),route.Last()})for(var dx=-1;dx<=1;dx++)for(var dz=-1;dz<=1;dz++)draft.tiles[(p.y+dz)*24+p.x+dx]=(int)(p==route.First()?CellKind.Spawn:CellKind.Goal);
            draft.route=route;
            var towerPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rebuild/Prefabs/LevelBuilding/Towers/Tower_Attack_1x1_Square.prefab");
            foreach(var p in new[]{new Vector2Int(6,15),new Vector2Int(12,10),new Vector2Int(8,7),new Vector2Int(17,6)})
            {draft.tiles[p.y*24+p.x]=(int)CellKind.TowerPlatform;draft.towers.Add(new MapTowerStamp{cell=p,prefab=towerPrefab,area=AttackArea.Square,configuration="{\"range\":3.5,\"maxHp\":18,\"damage\":0.55,\"attackInterval\":1.2,\"usesMacroFootprint\":false}"});}
            var session=MapBuilderUtility.Populate(draft,battle);UnityEngine.Object.DestroyImmediate(draft);
            var host=session.transform.root.gameObject;
            foreach(var mono in host.GetComponentsInChildren<MonoBehaviour>(true).ToArray())
                if(!(mono is BattleSession)&&!(mono is BattlePresentation)&&mono.GetType().Namespace!="Team5.Rebuild.World")UnityEngine.Object.DestroyImmediate(mono);
            var data=new SerializedObject(session);data.FindProperty("autoStart").boolValue=false;data.FindProperty("squadCombat").boolValue=true;
            data.FindProperty("formationSize").intValue=3;data.FindProperty("unitCount").intValue=6;data.FindProperty("manualSquadMovement").boolValue=false;data.FindProperty("continuousSquadMovement").boolValue=false;
            data.FindProperty("initialRoster").objectReferenceValue=null;data.FindProperty("useSceneRosterOnly").boolValue=false;data.FindProperty("displayedLevelNumber").intValue=0;data.FindProperty("logEvents").boolValue=false;
            var members=data.FindProperty("members");members.arraySize=3;
            for(var i=0;i<3;i++){var m=members.GetArrayElementAtIndex(i);m.FindPropertyRelative("enabled").boolValue=true;m.FindPropertyRelative("memberId").intValue=1001+i;m.FindPropertyRelative("slotId").intValue=i;m.FindPropertyRelative("role").enumValueIndex=(int)(i==0?UnitRole.Attack:i==1?UnitRole.Tank:UnitRole.Captain);}
            data.ApplyModifiedPropertiesWithoutUndo();
            var presentation=host.GetComponentInChildren<BattlePresentation>();var view=new SerializedObject(presentation);
            var roleSet=AssetDatabase.LoadAssetAtPath<UnitRoleVisualSet>("Assets/Rebuild/CatgirlFrontTest/RoleVisuals/CatgirlAllRoles.asset");
            if(roleSet!=null)view.FindProperty("roleVisuals").objectReferenceValue=roleSet;
            view.FindProperty("unitPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<UnitView>("Assets/Rebuild/CatgirlFrontTest/FWLowerBody/CatgirlFWLowerBody.prefab");
            view.FindProperty("useTestUnitVisual").boolValue=false;view.FindProperty("additionalTestVisuals").arraySize=0;view.ApplyModifiedPropertiesWithoutUndo();
            var mode=new GameObject("Siege Tavern Battle").AddComponent<SiegeTavernMode>();var config=new SerializedObject(mode);config.FindProperty("session").objectReferenceValue=session;config.ApplyModifiedPropertiesWithoutUndo();
            var errors=session.ValidateConfiguration();if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
            EditorSceneManager.SaveScene(battle,SiegeTavernMode.BattleScene);AssetDatabase.SaveAssets();Debug.Log("SIEGE_TAVERN_BUILD_OK");
        }
    }
}
