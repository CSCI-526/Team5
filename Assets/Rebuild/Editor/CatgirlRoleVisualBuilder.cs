using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Presentation.LegacyUI;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Team5.Rebuild.Editor
{
    public static class CatgirlRoleVisualBuilder
    {
        public const string Folder="Assets/Rebuild/CatgirlFrontTest/RoleVisuals";
        private static readonly UnitRole[] Roles={UnitRole.Attack,UnitRole.Tank,UnitRole.Heal,UnitRole.Captain};
        [Serializable]private sealed class RoleInfo{public string role,prefab,hairColor;public int memberId,slotId;}
        [Serializable]private sealed class Report{public string sourceSha256;public bool sourcePreserved,mapRosterCameraPreserved;public RoleInfo[] roles;}
        public static string PathFor(UnitRole role)=>Folder+"/Catgirl"+role+".prefab";
        [MenuItem("Team5/Rebuild/Catgirl Front/Create role colors and apply to Scene 1 (once)")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            if(Roles.Any(r=>File.Exists(PathFor(r))))throw new InvalidOperationException("Role prefabs already exist; refusing to overwrite hand edits.");
            var source=CatgirlFWLowerBodyBuilder.Visual;var sourceHash=Hash(source);
            Directory.CreateDirectory(Folder);Directory.CreateDirectory("Logs/CatgirlRoles");AssetDatabase.Refresh();
            foreach(var role in Roles)
            {
                var path=PathFor(role);
                if(!AssetDatabase.CopyAsset(source,path))throw new IOException("Cannot copy hybrid for "+role);
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    root.name="Catgirl "+role;
                    var rig=root.GetComponentsInChildren<Animator>(true).Single().transform;
                    var donor=rig.Find("FW Lower Body");
                    var hair=root.GetComponentsInChildren<SpriteRenderer>(true).Where(s=>(s.name=="F_HAIR_FRONT"||s.name=="F_B_HAIR")&&!s.transform.IsChildOf(donor)).ToArray();
                    if(hair.Length!=2)throw new InvalidOperationException("Expected two independent Catgirl hair layers.");
                    foreach(var sprite in hair)sprite.color=LegacyUnitLooks.For(role).Body;
                    // SpriteRenderer tint works with native GPU skinning; no property blocks,
                    // brown-hair masks, material replacements or skin-tone changes are needed.
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            const string scenePath="Assets/Rebuild/Scenes/1.unity";
            var backup="Logs/CatgirlRoles/Scene1BeforeRoleVisuals.unity";
            File.Copy(scenePath,backup,false);
            var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Additive);
            Report report=null;
            try
            {
                var session=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BattleSession>(true)).Single();
                var signature=Signature(scene,session);
                var members=session.CaptureDefinition().Members.ToArray();
                if(members.Length!=4||!members.Select(m=>m.Unit.Role).OrderBy(r=>r).SequenceEqual(Roles.OrderBy(r=>r)))
                    throw new InvalidOperationException("Scene 1 must contain the four expected roles.");
                var presentation=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BattlePresentation>(true)).Single();
                var primary=members.Single(m=>m.Unit.Role==UnitRole.Attack);var others=members.Where(m=>m.MemberId!=primary.MemberId).ToArray();
                var data=new SerializedObject(presentation);
                data.FindProperty("useTestUnitVisual").boolValue=true;
                data.FindProperty("testVisualMemberId").intValue=primary.MemberId;
                data.FindProperty("testUnitPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<UnitView>(PathFor(primary.Unit.Role));
                var list=data.FindProperty("additionalTestVisuals");list.arraySize=others.Length;
                for(var i=0;i<others.Length;i++)
                {
                    var entry=list.GetArrayElementAtIndex(i);entry.FindPropertyRelative("memberId").intValue=others[i].MemberId;
                    entry.FindPropertyRelative("prefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<UnitView>(PathFor(others[i].Unit.Role));
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                if(signature!=Signature(scene,session))throw new InvalidOperationException("Unexpected map/roster/camera change.");
                EditorSceneManager.SaveScene(scene);
                report=new Report{sourceSha256=sourceHash,sourcePreserved=sourceHash==Hash(source),mapRosterCameraPreserved=true,
                    roles=members.Select(m=>new RoleInfo{role=m.Unit.Role.ToString(),memberId=m.MemberId,slotId=m.SlotId,prefab=PathFor(m.Unit.Role),hairColor=ColorUtility.ToHtmlStringRGB(LegacyUnitLooks.For(m.Unit.Role).Body)}).ToArray()};
                if(!report.sourcePreserved)throw new InvalidOperationException("Original hybrid must be retained unchanged.");
                File.WriteAllText("Logs/CatgirlRoles/BuildReport.json",JsonUtility.ToJson(report,true));
            }
            finally
            {
                if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene,true);
            }
            AssetDatabase.SaveAssets();Debug.Log("CATGIRL_ROLE_VISUALS_APPLIED: four copies; white-haired hybrid preserved; Scene 1 appearance only.");
        }
        private static string Hash(string path)
        {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","");}
        private static string Signature(Scene scene,BattleSession session)
        {
            var definition=session.CaptureDefinition();
            var members=string.Join("|",definition.Members.Select(m=>$"{m.MemberId}:{m.SlotId}:{m.Unit.Role}:{m.Unit.MaxHp}:{m.Unit.Speed}:{m.Unit.Damage}:{m.Unit.Range}:{m.Unit.AttackInterval}:{m.Unit.Defense}:{m.Unit.HealPerSecond}:{m.Unit.DamageType}"));
            var cells=string.Join("|",session.Grid.SceneCells.Select(c=>$"{c.Occupancy}:{c.Kind}"));
            var route=string.Join("|",session.Route.CaptureTopCenters().Select(p=>p.ToString("R")));
            var cameras=string.Join("|",scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).Select(c=>$"{c.name}:{c.transform.position.ToString("R")}:{c.transform.rotation}:{c.orthographicSize}:{c.fieldOfView}"));
            return members+"\n"+cells+"\n"+route+"\n"+cameras;
        }
    }
}
