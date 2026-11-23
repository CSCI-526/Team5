using System;
using System.IO;
using System.Linq;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Team5.Rebuild.Editor
{
    public static class WallLadderBuilder
    {
        private const string Folder="Assets/Rebuild/WallLadders";
        [MenuItem("Team5/Rebuild/Walls/Add ladder and walkable tops to Scene 1")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            Directory.CreateDirectory(Folder);Directory.CreateDirectory("Logs/WallLadders");AssetDatabase.Refresh();
            var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/LadderMetal.mat");
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.SetColor("_BaseColor",new Color(.72f,.48f,.12f));material.SetFloat("_Metallic",.4f);AssetDatabase.CreateAsset(material,Folder+"/LadderMetal.mat");}
            var baseWall=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rebuild/Prefabs/LevelBuilding/Towers/Wall_3x1_X.prefab");
            var prefabPath=Folder+"/LadderWall_3x1.prefab";
            if(!File.Exists(prefabPath))
            {
                var model=UnityEngine.Object.Instantiate(baseWall);model.name="Ladder Wall 3x1";
                try{Configure(model.GetComponent<BattleTower>(),true,false,material);PrefabUtility.SaveAsPrefabAsset(model,prefabPath);}
                finally{UnityEngine.Object.DestroyImmediate(model);}
            }
            const string path="Assets/Rebuild/Scenes/1.unity";
            File.Copy(path,"Logs/WallLadders/Scene1Before_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")+".unity");
            var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
            var session=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BattleSession>(true)).Single();
            var errors=session.ValidateConfiguration();if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
            var walls=session.Towers.Where(w=>w.IsWall&&!w.IsWoodenGate&&!w.IsMinion).ToArray();
            if(walls.Length==0)throw new InvalidOperationException("Scene 1 has no stone walls.");
            var start=session.Route.CaptureTopCenters()[0];BattleTower selected=null;var positive=false;
            // Leave space above the elevated sprites in the existing camera framing.
            // Prefer an interior segment instead of the extreme map edge.
            var minZ=session.Grid.SceneCells.Min(c=>c.transform.position.z);var maxZ=session.Grid.SceneCells.Max(c=>c.transform.position.z);
            foreach(var wall in walls.OrderBy(w=>w.transform.position.z>minZ+2&&w.transform.position.z<maxZ-2?0:1).ThenBy(w=>(w.transform.position-start).sqrMagnitude))
            {
                foreach(var side in new[]{false,true})
                {
                    var normal=(wall.Footprint.x==3?Vector3.forward:Vector3.right)*(side?1:-1);
                    var foot=wall.transform.position+normal*.85f;var key=SmallCellOccupancy.PositionToCell(foot);
                    if(!session.Grid.TryGetCell(key,out var cell)||!(cell.IsRouteSurface||cell.Kind==CellKind.Ground))continue;
                    if(session.Towers.Any(other=>other!=wall&&Mathf.Abs(foot.x-other.transform.position.x)<other.Footprint.x*.5f+.2f&&Mathf.Abs(foot.z-other.transform.position.z)<other.Footprint.y*.5f+.2f))continue;
                    selected=wall;positive=side;break;
                }
                if(selected!=null)break;
            }
            if(selected==null)throw new InvalidOperationException("No clear ground approach for the ladder.");
            foreach(var wall in walls)Configure(wall,wall==selected,wall==selected&&positive,material);
            errors=session.ValidateConfiguration();if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
            var definition=session.CaptureDefinition();
            if(definition.Ladders.Count!=1||definition.WallSurfaces.Count!=walls.Length)throw new InvalidOperationException("Missing wall traversal definitions.");
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            File.WriteAllText("Logs/WallLadders/Scene1Build.txt",$"Wall tops: {walls.Length}\nLadder wall: {selected.name} at {selected.transform.position}\nApproach: {selected.GetComponent<WallWalkSurface>().Bottom}\nTop: {selected.GetComponent<WallWalkSurface>().Top}\n");
            Debug.Log("WALL_LADDER_SCENE1_BUILD_OK");
        }
        private static void Configure(BattleTower wall,bool ladder,bool positive,Material material)
        {
            var surface=wall.GetComponent<WallWalkSurface>()??wall.gameObject.AddComponent<WallWalkSurface>();
            var oldLadder=wall.transform.Find("Climb Ladder");
            var height=wall.GetComponentsInChildren<MeshRenderer>(true).Where(r=>oldLadder==null||!r.transform.IsChildOf(oldLadder))
                .Max(r=>r.bounds.max.y)-wall.transform.position.y;
            var data=new SerializedObject(surface);data.FindProperty("topHeight").floatValue=height;data.FindProperty("hasLadder").boolValue=ladder;data.FindProperty("ladderOnPositiveSide").boolValue=positive;data.ApplyModifiedPropertiesWithoutUndo();
            if(!ladder){if(oldLadder!=null)UnityEngine.Object.DestroyImmediate(oldLadder.gameObject);return;}
            if(wall.transform.Find("Climb Ladder")!=null)return;
            var group=new GameObject("Climb Ladder").transform;group.SetParent(wall.transform,false);
            var outward=wall.Footprint.x==3?Vector3.forward:Vector3.right;outward*=positive?1:-1;
            var tangent=Vector3.Cross(Vector3.up,outward);
            void Bar(string name,Vector3 position,Vector3 size)
            {
                var bar=GameObject.CreatePrimitive(PrimitiveType.Cube);bar.name=name;bar.transform.SetParent(group,false);
                bar.transform.position=wall.transform.position+position;bar.transform.rotation=Quaternion.LookRotation(outward,Vector3.up);
                bar.transform.localScale=size;bar.GetComponent<MeshRenderer>().sharedMaterial=material;
                UnityEngine.Object.DestroyImmediate(bar.GetComponent<Collider>());
            }
            foreach(var sign in new[]{-1,1})Bar("Ladder Rail",outward*.68f+tangent*(sign*.25f)+Vector3.up*((height+.15f)*.5f),new Vector3(.075f,height+.15f,.075f));
            var steps=Mathf.Max(4,Mathf.CeilToInt(height/.18f));for(var i=0;i<=steps;i++)Bar("Ladder Rung",outward*.68f+Vector3.up*(.06f+(height-.06f)*i/steps),new Vector3(.56f,.05f,.06f));
        }
    }
}
