using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Presentation.LegacyUI;
using Team5.Rebuild.Session;
using Team5.Rebuild.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
using Random=System.Random;

namespace Team5.Rebuild.Editor
{
    // V7: scenery and camera only. Retain the existing API for the stage generator.
    public static class StageV33VisualPolish
    {
        private const string RootName="Stage V3.3 Seamless Terrain";
        private const string BackupFolder="Logs/StageV7ArtBackups";
        private const string MaterialsFolder="Assets/Rebuild/Materials";
        private static readonly Color Meadow=new Color(.34f,.49f,.36f,1);
        private static readonly string[] Scenes={BattleSceneFlow.BattleScene,BattleSceneFlow.LevelTwoScene,
            BattleSceneFlow.LevelThreeScene,BattleSceneFlow.LevelFourScene,BattleSceneFlow.LevelFiveScene,
            BattleSceneFlow.LevelSixScene,BattleSceneFlow.LevelSevenScene};
        [MenuItem("Team5/Rebuild/Apply V7 Art and Dolly Camera (Keep Layouts)")]
        public static void ApplyAll()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var setup=EditorSceneManager.GetSceneManagerSetup();
            var backup=BackupFolder+"/"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            Directory.CreateDirectory(backup);
            foreach(var path in Scenes) File.Copy(path,Path.Combine(backup,Path.GetFileName(path)),false);
            int completed=0;
            try
            {
                for(var i=0;i<Scenes.Length;i++)
                {
                    var scene=EditorSceneManager.OpenScene(Scenes[i],OpenSceneMode.Single);
                    try
                    {
                        Polish(scene,i+1); EditorSceneManager.MarkSceneDirty(scene);
                        if(!EditorSceneManager.SaveScene(scene)) throw new IOException("Cannot save "+Scenes[i]);
                        completed++;
                    }
                    catch(Exception e) { Debug.LogError("V7 stage "+(i+1)+" not saved: "+e); EditorSceneManager.OpenScene(Scenes[i],OpenSceneMode.Single); }
                }
                AssetDatabase.SaveAssets();
            }
            finally { if(setup!=null && setup.Length>0) EditorSceneManager.RestoreSceneManagerSetup(setup); }
            Debug.Log($"V7 art completed {completed}/7 stages. Original scenes: {backup}");
        }
        public static void Polish(Scene scene,int stage)
        {
            var roots=scene.GetRootGameObjects();
            var session=roots.SelectMany(r=>r.GetComponentsInChildren<BattleSession>(true)).Single();
            if(!session.UsesTacticalRoad) throw new InvalidOperationException("V7 expects a tactical campaign map.");
            var cells=session.Grid.SceneCells;
            if(cells.Length==0 || cells.Select(c=>c.Occupancy.y).Distinct().Count()!=1) throw new InvalidOperationException("V7 expects flat campaign cells.");
            // Validate before edits; no gameplay state, positions, types, colliders or route are modified.
            var errors=session.ValidateConfiguration(); if(errors.Count>0) throw new InvalidOperationException(string.Join(" | ",errors));
            var y=cells[0].TopCenter.y;
            var road=cells.Where(c=>session.IsMovementSurface(c)).Select(c=>Flat(c.TopCenter)).ToArray();
            if(road.Length==0) throw new InvalidOperationException("No tactical road.");
            var meadowShader=Resources.Load<Shader>("BattlefieldMeadow");
            if(meadowShader==null) throw new InvalidOperationException("Missing BattlefieldMeadow shader.");
            var env=new[]{"Tree_A","Tree_B","Tree_C","Bush_A","Bush_B","Rock_A","Rock_B","Rock_C","GrassTuft","Flower_A","Flower_B"}
                .ToDictionary(n=>n,n=>AssetDatabase.LoadAssetAtPath<GameObject>(PaintSquadLevelDressing.PrefabPath(n)));
            if(env.Values.Any(v=>v==null)) throw new InvalidOperationException("Existing Paint Squad environment prefabs missing. Run the original Paint Squad prefab builder first.");
            foreach(var root in roots)
                if(root.name==RootName || root.name==PaintSquadLevelDressing.RootName) Object.DestroyImmediate(root);
            foreach(var t in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray())
                if(t!=null && t.name=="Decorative Border Tiles" && t.GetComponentInChildren<BattleCell>(true)==null && t.GetComponentInChildren<BattleTower>(true)==null) Object.DestroyImmediate(t.gameObject);
            // Keep cells and physics, but stop drawing hundreds of separate grass/cover squares.
            foreach(var cell in cells) foreach(var r in cell.GetComponentsInChildren<Renderer>(true))
            {
                if(r.GetComponentInParent<BattleTower>()!=null) continue;
                r.enabled=false; PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            }
            var terrain=NewRoot(scene,RootName);
            var center=new Vector3((road.Min(p=>p.x)+road.Max(p=>p.x))*.5f,y,(road.Min(p=>p.y)+road.Max(p=>p.y))*.5f);
            var floor=Primitive(terrain.transform,"Continuous meadow",PrimitiveType.Plane,center-Vector3.up*.008f,new Vector3(20,1,20),GetMaterial("Meadow",meadowShader,Meadow));
            floor.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            // Tower pads are low, non-blocking ground silhouettes; they do not alter the footprint.
            var lit=Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var padRim=GetMaterial("StoneRim",lit,new Color(.36f,.39f,.34f));
            var padTop=GetMaterial("StoneTop",lit,new Color(.62f,.60f,.49f));
            foreach(var tower in session.Towers.Where(t=>t!=null && !t.IsWall))
            {
                var p=tower.transform.position; p.y=y-.005f;
                Primitive(terrain.transform,"Foundation rim",PrimitiveType.Cylinder,p,new Vector3(1.55f,.013f,1.55f),padRim);
                Primitive(terrain.transform,"Foundation top",PrimitiveType.Cylinder,p+Vector3.up*.012f,new Vector3(1.26f,.006f,1.26f),padTop);
            }
            var route=session.Route.CaptureTopCenters();
            var definitions=session.Towers.Select((t,i)=>session.CaptureTowerDefinition(t,i+1,route)).ToArray();
            var baseline=MinionPlanner.Plan(route,definitions,stage,MinionSiteScan.Scan(session));
            var reserved=baseline.SelectMany(m=>m.Cells).Select(Flat).ToList();
            var threats=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<OpenStageThreat>(true)).Select(t=>Flat(t.transform.position));
            var protectedPoints=session.Towers.Where(t=>t!=null).Select(t=>Flat(t.transform.position)).Concat(threats).Concat(reserved).ToArray();
            var dressing=NewRoot(scene,PaintSquadLevelDressing.RootName);
            Dress(stage,y,road,protectedPoints,cells,dressing.transform,env);
            var final=MinionPlanner.Plan(route,definitions,stage,MinionSiteScan.Scan(session));
            if(!SamePlan(baseline,final))
            {
                // Scenery must not change combat support sites. Discard any props flagged as
                // clutter by the minion scanner, one group at a time, until the plan matches.
                for(var i=dressing.transform.childCount-1;i>=0 && !SamePlan(baseline,final);i--)
                { Object.DestroyImmediate(dressing.transform.GetChild(i).gameObject); final=MinionPlanner.Plan(route,definitions,stage,MinionSiteScan.Scan(session)); }
                if(!SamePlan(baseline,final)) throw new InvalidOperationException("Unable to preserve clear-ground minion sites.");
            }
            AddContactShadows(terrain.transform,dressing.transform,y,session);
            FrameBattle(scene);
            Debug.Log($"V7 level {stage}: {dressing.transform.childCount} scenery groves; {final.Count} minion groups; road and encounters retained.");
        }
        private static void AddContactShadows(Transform terrain,Transform dressing,float y,BattleSession session)
        {
            var shader=Resources.Load<Shader>("BattlefieldContactShadow");
            if(shader==null)throw new InvalidOperationException("Missing BattlefieldContactShadow shader.");
            var material=GetMaterial("ContactShadow",shader,new Color(.13f,.19f,.14f,.28f));
            foreach(Transform group in dressing) foreach(Transform prop in group)
            {
                var renderers=prop.GetComponentsInChildren<Renderer>(true); if(renderers.Length==0)continue;
                var b=renderers[0].bounds; foreach(var r in renderers)b.Encapsulate(r.bounds);
                var center=new Vector3(b.center.x+.13f*b.size.y,y+.002f,b.center.z-.08f*b.size.y);
                var scale=new Vector3(Mathf.Max(.04f,b.size.x*.145f),1,Mathf.Max(.04f,b.size.z*.13f));
                var shadow=Primitive(terrain,"Soft scenery contact",PrimitiveType.Plane,center,scale,material);
                shadow.GetComponent<Renderer>().sortingOrder=short.MinValue+2;
                GameObjectUtility.SetStaticEditorFlags(shadow,StaticEditorFlags.BatchingStatic);
            }
            foreach(var t in session.Towers.Where(t=>t!=null && !t.IsWall))
            {
                var p=t.transform.position; p.y=y+.002f;
                Primitive(terrain,"Soft tower contact",PrimitiveType.Plane,p+new Vector3(.12f,0,-.08f),new Vector3(.22f,1,.19f),material).GetComponent<Renderer>().sortingOrder=short.MinValue+2;
            }
        }
        private static bool SamePlan(List<MinionPlacement> a,List<MinionPlacement> b)
        {
            if(a.Count!=b.Count) return false;
            for(var i=0;i<a.Count;i++) if(a[i].Kind!=b[i].Kind || a[i].TowerId!=b[i].TowerId || !a[i].Cells.SequenceEqual(b[i].Cells)) return false;
            return true;
        }
        private static Vector2 Flat(Vector3 p)=>new Vector2(p.x,p.z);
        private static GameObject NewRoot(Scene scene,string name)
        { var root=new GameObject(name); SceneManager.MoveGameObjectToScene(root,scene); root.layer=2; return root; }
        private static GameObject Primitive(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(type); go.name=name; go.transform.SetParent(parent,false); go.transform.position=position; go.transform.localScale=scale; go.layer=2;
            Object.DestroyImmediate(go.GetComponent<Collider>()); var r=go.GetComponent<Renderer>(); r.sharedMaterial=material; r.shadowCastingMode=ShadowCastingMode.Off; return go;
        }
        private static Material GetMaterial(string name,Shader shader,Color color)
        {
            if(shader==null) throw new InvalidOperationException("Missing material shader.");
            if(!AssetDatabase.IsValidFolder(MaterialsFolder)) AssetDatabase.CreateFolder("Assets/Rebuild","Materials");
            var path=MaterialsFolder+"/StageV7_"+name+".mat"; var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(shader);AssetDatabase.CreateAsset(m,path);} else m.shader=shader;
            if(m.HasProperty("_Color"))m.SetColor("_Color",color);
            if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);
            if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",0);
            EditorUtility.SetDirty(m);return m;
        }
        private static float RoadDistance(Vector2 p,Vector2[] road)
        {
            var best=float.PositiveInfinity;
            foreach(var r in road) { var d=p-r; var x=Mathf.Max(0,Mathf.Abs(d.x)-.5f); var z=Mathf.Max(0,Mathf.Abs(d.y)-.5f); best=Mathf.Min(best,x*x+z*z); }
            return Mathf.Sqrt(best);
        }
        private static float PointDistance(Vector2 p,Vector2[] points)
        { var best=float.PositiveInfinity; foreach(var q in points)best=Mathf.Min(best,(p-q).sqrMagnitude);return Mathf.Sqrt(best); }
        private static float R(Random rng,float min,float max)=>Mathf.Lerp(min,max,(float)rng.NextDouble());
        private sealed class Spec { public string name; public float height, radius; public Spec(string n,float h,float r){name=n;height=h;radius=r;} }
        private static readonly Spec[] Trees={new Spec("Tree_A",2.0f,.90f),new Spec("Tree_B",2.5f,1.05f),new Spec("Tree_C",1.85f,.85f)};
        private static readonly Spec[] Bushes={new Spec("Bush_A",.48f,.42f),new Spec("Bush_B",.62f,.50f),new Spec("Rock_B",.42f,.50f),new Spec("Rock_C",.68f,.65f)};
        private static readonly Spec[] Detail={new Spec("GrassTuft",.18f,.20f),new Spec("Flower_A",.22f,.22f),new Spec("Flower_B",.20f,.20f),new Spec("Rock_A",.23f,.28f)};
        private static void Dress(int stage,float y,Vector2[] road,Vector2[] protectedPoints,BattleCell[] cells,Transform root,Dictionary<string,GameObject> env)
        {
            var rng=new Random(7319+stage*179); var occupied=new List<(Vector2 p,float radius)>(); var anchors=new List<Vector2>();
            var minX=road.Min(p=>p.x);var maxX=road.Max(p=>p.x);var minZ=road.Min(p=>p.y);var maxZ=road.Max(p=>p.y);
            // Compose groups around the route, NOT around the square map boundary. Keep front
            // silhouettes low; tall trees are mostly behind the road, framing the play area.
            var candidates=new List<Vector2>();
            for(var z=minZ-7;z<=maxZ+8;z+=4.8f) for(var x=minX-6;x<=maxX+6;x+=5.1f)
                candidates.Add(new Vector2(x+R(rng,-1.5f,1.5f),z+R(rng,-1.4f,1.4f)));
            // Cover is a physical gameplay obstacle. Give each connected interior island one
            // natural grouping, rather than a random object on each square of its footprint.
            var minCellX=cells.Min(c=>c.Occupancy.x);var maxCellX=cells.Max(c=>c.Occupancy.x);
            var minCellZ=cells.Min(c=>c.Occupancy.z);var maxCellZ=cells.Max(c=>c.Occupancy.z);
            var cover=new HashSet<Vector2Int>(cells.Where(c=>c.Kind==CellKind.Obstacle && c.Occupancy.x>minCellX && c.Occupancy.x<maxCellX && c.Occupancy.z>minCellZ && c.Occupancy.z<maxCellZ).Select(c=>new Vector2Int(c.Occupancy.x,c.Occupancy.z)));
            var coverCenters=new List<Vector2>();
            while(cover.Count>0)
            {
                var start=cover.OrderBy(p=>p.x).ThenBy(p=>p.y).First(); cover.Remove(start);var q=new Queue<Vector2Int>();q.Enqueue(start);var island=new List<Vector2Int>();
                while(q.Count>0){var p=q.Dequeue();island.Add(p);foreach(var d in new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down})if(cover.Remove(p+d))q.Enqueue(p+d);}
                if(island.Count>=2)coverCenters.Add(new Vector2(island.Average(p=>(float)p.x),island.Average(p=>(float)p.y)));
            }
            // Favour visible near-meadow clearings, then break ties with a fixed seed.
            var ordered=candidates.Select(p=>new { p, score=Mathf.Abs(RoadDistance(p,road)-4.3f)+R(rng,0,2.2f) }).OrderBy(p=>p.score).Select(p=>p.p).ToArray();
            foreach(var anchor in coverCenters.Concat(ordered))
            {
                if(anchors.Count>=Mathf.Clamp(9+stage,10,16) || RoadDistance(anchor,road)<1.75f || PointDistance(anchor,protectedPoints)<2.2f || anchors.Any(p=>Vector2.Distance(p,anchor)<3.9f))continue;
                var grove=new GameObject("Meadow grove");grove.transform.SetParent(root,false);grove.layer=2;
                var isRear=anchor.y>maxZ+1.5f || (anchor.y>(minZ+maxZ)*.5f && RoadDistance(anchor,road)>3.8f);
                // In front use small shrubs, not a wall of tall trees.
                var treeGroup=isRear && rng.NextDouble()<.80;
                var count=treeGroup?R(rng,4,7):R(rng,2,5);
                for(var i=0;i<(int)count;i++)
                {
                    var angle=R(rng,0,Mathf.PI*2);var radius=i==0?0:R(rng,.55f,1.75f);
                    var p=anchor+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
                    var spec=treeGroup && i<3?Trees[rng.Next(Trees.Length)]:Bushes[rng.Next(Bushes.Length)];
                    Place(grove.transform,p,spec,y,road,protectedPoints,occupied,env,rng);
                }
                // Understory accent near the group, not a rectangular scatter pattern.
                for(var i=0;i<3;i++)
                {
                    var p=anchor+new Vector2(R(rng,-1.8f,1.8f),R(rng,-1.6f,1.6f));
                    Place(grove.transform,p,Detail[rng.Next(Detail.Length)],y,road,protectedPoints,occupied,env,rng);
                }
                if(grove.transform.childCount==0)Object.DestroyImmediate(grove);else anchors.Add(anchor);
            }
        }
        private static void Place(Transform parent,Vector2 p,Spec spec,float y,Vector2[] road,Vector2[] protectedPoints,List<(Vector2 p,float radius)> occupied,Dictionary<string,GameObject> env,Random rng)
        {
            var height=spec.height*R(rng,.82f,1.12f);
            // Include projected tree height in the clearance so foreground props cannot cover units.
            var margin=Mathf.Max(1.35f,1.1f+height*.86f)+spec.radius;
            if(RoadDistance(p,road)<margin || PointDistance(p,protectedPoints)<2.1f+spec.radius || occupied.Any(o=>Vector2.Distance(o.p,p)<(o.radius+spec.radius)*.83f))return;
            var go=(GameObject)PrefabUtility.InstantiatePrefab(env[spec.name],parent);
            if(PrefabUtility.IsAnyPrefabInstanceRoot(go))PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            go.transform.SetPositionAndRotation(new Vector3(p.x,y,p.y),Quaternion.Euler(0,R(rng,0,360),0));
            var renderers=go.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0){Object.DestroyImmediate(go);return;}
            var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);
            var scale=Mathf.Min(height/Mathf.Max(.01f,b.size.y),spec.radius*2/Mathf.Max(.01f,Mathf.Max(b.size.x,b.size.z)));
            go.transform.localScale*=scale;b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);
            go.transform.position+=new Vector3(p.x-b.center.x,y-b.min.y,p.y-b.center.z);
            foreach(var c in go.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(c);
            foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=2;
            // Modest contact depth from the existing low-poly models; no new gameplay objects.
            occupied.Add((p,spec.radius));
        }
        public static void FrameBattle(Scene scene)
        {
            var roots=scene.GetRootGameObjects();
            var camera=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).FirstOrDefault(c=>c.CompareTag("MainCamera"));
            if(camera==null)throw new InvalidOperationException("No main camera.");
            var road=roots.SelectMany(r=>r.GetComponentsInChildren<BattleCell>(true)).Where(c=>c.IsRouteSurface).ToArray();
            if(road.Length==0)throw new InvalidOperationException("No road to frame.");
            var bounds=new Bounds(road[0].TopCenter,Vector3.one);
            foreach(var c in road)bounds.Encapsulate(new Bounds(c.TopCenter+Vector3.up*.7f,new Vector3(1,1.4f,1)));
            foreach(var t in roots.SelectMany(r=>r.GetComponentsInChildren<BattleTower>(true)))bounds.Encapsulate(new Bounds(t.transform.position+Vector3.up*1.2f,new Vector3(1.6f,3f,1.6f)));
            foreach(var t in roots.SelectMany(r=>r.GetComponentsInChildren<OpenStageThreat>(true)))bounds.Encapsulate(new Bounds(t.transform.position+Vector3.up*.5f,new Vector3(1.8f,1.5f,1.8f)));
            bounds.Expand(new Vector3(2.5f,0,2.5f));
            var follow=camera.GetComponent<Level3SquadCamera>();if(follow!=null){follow.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(follow);}
            var dolly=camera.GetComponent<StageOverviewCamera>()??camera.gameObject.AddComponent<StageOverviewCamera>();
            dolly.enabled=true;dolly.Configure(bounds,roots.SelectMany(r=>r.GetComponentsInChildren<BattlePresentation>(true)).FirstOrDefault());
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Meadow;
            EditorUtility.SetDirty(dolly);EditorUtility.SetDirty(camera);EditorUtility.SetDirty(camera.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(camera);PrefabUtility.RecordPrefabInstancePropertyModifications(camera.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(dolly);
        }
    }
}
