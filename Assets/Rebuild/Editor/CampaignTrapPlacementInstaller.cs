using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Team5.Rebuild.Editor
{
    // Move the existing encounter transforms only. No terrain, tower, art or camera rebuild.
    public static class CampaignTrapPlacementInstaller
    {
        private const string CatalogPath = "Assets/Rebuild/Editor/StageV3StageCatalog.json";
        private static readonly string[] Paths = { BattleSceneFlow.LevelThreeScene, BattleSceneFlow.LevelFourScene,
            BattleSceneFlow.LevelFiveScene, BattleSceneFlow.LevelSixScene, BattleSceneFlow.LevelSevenScene };
        // Original V6/V7 positions, in catalog order. The new coordinates are read from the catalog.
        private static readonly int[][] Old = {
            new[]{22,8,14,3},
            new[]{13,9,19,11,29,10},
            new[]{21,15,14,3,16,11,23,18},
            new[]{19,9,12,9,17,3,22,15,28,17},
            new[]{15,16,23,2,12,13,8,18,19,15,21,8,27,5}
        };
        [Serializable] private sealed class Catalog { public Stage[] stages; }
        [Serializable] private sealed class Stage { public int stage; public int[] route; public Hazard[] hazards; }
        [Serializable] private sealed class Hazard { public int x,z,dx,dz; public string kind; public float radius; }

        [MenuItem("Team5/Rebuild/Apply V7.1 Trap Placements (Keep Maps)")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var data = JsonUtility.FromJson<Catalog>(File.ReadAllText(CatalogPath));
            if (data?.stages == null || !data.stages.Select(s=>s.stage).OrderBy(n=>n).SequenceEqual(Enumerable.Range(1,7)))
                throw new InvalidDataException("Expected seven campaign stages.");
            var plans = data.stages.Where(s=>s.stage>=3).OrderBy(s=>s.stage).ToArray();
            var previous = EditorSceneManager.GetSceneManagerSetup();
            var backup = "Logs/V71TrapPlacementBackups/"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            var written = new List<string>();
            try
            {
                // All five scenes must pass before saving any scene.
                foreach (var plan in plans)
                {
                    var errors=ClickMoveStageInstaller.ValidateCatalogGeometry(plan.stage);
                    if(errors.Count>0) throw new InvalidDataException(string.Join(" | ",errors));
                    Match(EditorSceneManager.OpenScene(Paths[plan.stage-3],OpenSceneMode.Single),plan);
                }
                Directory.CreateDirectory(backup);
                foreach(var path in Paths) File.Copy(path,Path.Combine(backup,Path.GetFileName(path)),false);
                foreach(var plan in plans)
                {
                    var path=Paths[plan.stage-3];
                    var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                    var match=Match(scene,plan); int moved=0;
                    for(int i=0;i<match.Length;i++)
                    {
                        var h=plan.hazards[i]; var tr=match[i].transform; var p=tr.position;
                        if(Mathf.Abs(p.x-h.x)<.001f && Mathf.Abs(p.z-h.z)<.001f)continue;
                        tr.position=new Vector3(h.x,p.y,h.z); // preserve the authored height and all stats
                        PrefabUtility.RecordPrefabInstancePropertyModifications(tr);
                        EditorUtility.SetDirty(tr); moved++;
                    }
                    if(moved>0)
                    {
                        EditorSceneManager.MarkSceneDirty(scene); written.Add(path);
                        if(!EditorSceneManager.SaveScene(scene,path))throw new IOException("Could not save "+path);
                    }
                    Debug.Log($"V7.1 Level {plan.stage}: moved {moved} hazards; {match.Length} total checked.");
                }
                Debug.Log("V7.1 trap placement completed 5/5 stages. Backups: "+backup);
            }
            catch
            {
                // Restore already-written scenes from the untouched copies if a later save fails.
                foreach(var path in written)File.Copy(Path.Combine(backup,Path.GetFileName(path)),path,true);
                if(written.Count>0)AssetDatabase.Refresh();
                // Discard the unsaved in-memory scene before restoring the previous setup.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                throw;
            }
            finally { if(previous!=null && previous.Length>0)EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }

        private static OpenStageThreat[] Match(Scene scene,Stage plan)
        {
            var roots=scene.GetRootGameObjects();
            var session=roots.SelectMany(r=>r.GetComponentsInChildren<BattleSession>(true)).Single();
            var actual=roots.SelectMany(r=>r.GetComponentsInChildren<OpenStageThreat>(true)).ToArray();
            var old=Old[plan.stage-3];
            if(session.DisplayedLevelNumber!=plan.stage || !session.UsesTacticalRoad || plan.hazards==null ||
                actual.Length!=plan.hazards.Length || old.Length!=2*actual.Length)
                throw Mismatch(scene,"unexpected session or hazard count");
            var errors=session.ValidateConfiguration();
            if(errors.Count>0)throw Mismatch(scene,string.Join(" | ",errors));
            var route=session.Route.CaptureTopCenters();
            var expected=ExpandRoute(plan.route);
            if(!route.Select(p=>new Vector2Int(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.z))).SequenceEqual(expected))
                throw Mismatch(scene,"route differs from V6/V7 catalog");
            var grid=session.Grid; grid.RebuildIndex(new List<string>());
            var road=new HashSet<Vector2Int>(grid.SceneCells.Where(c=>session.IsMovementSurface(c)&&grid.HasClearTop(c))
                .Select(c=>new Vector2Int(c.Occupancy.x,c.Occupancy.z)));
            var expectedRoad=new HashSet<Vector2Int>();
            foreach(var p in expected)for(int z=-2;z<=2;z++)for(int x=-2;x<=2;x++)expectedRoad.Add(p+new Vector2Int(x,z));
            if(!road.SetEquals(expectedRoad))throw Mismatch(scene,"walkable cells differ from V6/V7 five-wide layout");
            var match=new OpenStageThreat[actual.Length];
            for(int i=0;i<match.Length;i++)
            {
                var h=plan.hazards[i]; var kind=(OpenStageThreat.ThreatKind)Enum.Parse(typeof(OpenStageThreat.ThreatKind),h.kind);
                var candidates=actual.Where(t=>t.Kind==kind && (At(t,h.x,h.z)||At(t,old[2*i],old[2*i+1]))).ToArray();
                if(candidates.Length!=1 || match.Contains(candidates[0]))throw Mismatch(scene,$"cannot uniquely match {h.kind} at ({h.x},{h.z})");
                var threat=candidates[0]; match[i]=threat;
                if(Mathf.Abs(threat.Radius-h.radius)>.01f)throw Mismatch(scene,"hazard radius differs from validated catalog");
                var point=new Vector2Int(h.x,h.z);
                if(!road.Contains(point)||expected.Min(p=>Vector2Int.Distance(p,point))>1.01f)
                    throw Mismatch(scene,"hazard center is too far from main route");
                if(kind==OpenStageThreat.ThreatKind.Boulder)
                {
                    var dir=new Vector2Int(h.dx,h.dz);
                    if(Vector3.Distance(threat.RollDirection,new Vector3(h.dx,0,h.dz))>.01f ||
                        !road.Contains(point+2*dir)||!road.Contains(point-2*dir))throw Mismatch(scene,"boulder sweep mismatch");
                }
                else if(Coverage(road,point,h.radius)<.90f)throw Mismatch(scene,"hazard footprint mostly misses the road");
            }
            return match;
        }
        private static bool At(OpenStageThreat t,int x,int z)=>Mathf.Abs(t.transform.position.x-x)<.01f&&Mathf.Abs(t.transform.position.z-z)<.01f;
        private static List<Vector2Int> ExpandRoute(int[] route)
        {
            if(route==null||route.Length<4||route.Length%2!=0)throw new InvalidDataException("Bad route catalog.");
            var current=new Vector2Int(route[0],route[1]);var points=new List<Vector2Int>{current};
            for(int i=2;i<route.Length;i+=2)
            {
                var end=new Vector2Int(route[i],route[i+1]);
                if(end.x!=current.x&&end.y!=current.y)throw new InvalidDataException("Diagonal route.");
                var step=new Vector2Int(Math.Sign(end.x-current.x),Math.Sign(end.y-current.y));
                while(current!=end){current+=step;points.Add(current);}
            }
            return points;
        }
        // Sample the actual damage disc in world units, not the number of intersected tile centers.
        internal static float Coverage(HashSet<Vector2Int> road,Vector2Int p,float radius)
        {
            int total=0,inside=0; int extent=Mathf.CeilToInt(radius*20);
            for(int z=-extent;z<=extent;z++)for(int x=-extent;x<=extent;x++)
            {
                float dx=x*.05f,dz=z*.05f;if(dx*dx+dz*dz>radius*radius)continue;
                total++;if(road.Contains(new Vector2Int(Mathf.FloorToInt(p.x+dx+.5f),Mathf.FloorToInt(p.y+dz+.5f))))inside++;
            }
            return total>0?(float)inside/total:0;
        }
        private static InvalidOperationException Mismatch(Scene scene,string message)=>new InvalidOperationException(scene.name+": "+message+". Expected the V6/V7 layout; no unrecognized hazards will be moved.");
    }
}
