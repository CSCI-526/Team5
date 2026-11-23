#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Team5.Rebuild.Tests
{
    public sealed class WallLadderScene1Tests
    {
        [UnityTest]public IEnumerator Scene1SquadClimbsWalksOnTopAndReturnsToGround()
        {
            var previous=SceneManager.GetActiveScene();
            var load=EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Rebuild/Scenes/1.unity",new LoadSceneParameters(LoadSceneMode.Additive));
            yield return load;var scene=SceneManager.GetSceneByPath("Assets/Rebuild/Scenes/1.unity");
            try
            {
                SceneManager.SetActiveScene(scene);
                var session=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BattleSession>(true)).Single();
                for(var i=0;i<20&&session.Simulation==null;i++)yield return null;
                Assert.That(session.ValidateConfiguration(),Is.Empty);Assert.That(session.Simulation,Is.Not.Null);
                var sim=session.Simulation;sim.SetPaused(false);
                for(var i=0;i<100&&sim.PendingSpawnCount>0;i++)session.Advance(.02f);
                var wall=session.Towers.Select(t=>t.GetComponent<WallWalkSurface>()).Single(s=>s!=null&&s.HasLadder);
                Assert.That(sim.RequestMovement(new MovementRequest(wall.Top)),Is.True);
                var climbed=false;var slots=sim.Units.Select(u=>u.SlotId).ToArray();
                for(var i=0;i<4000&&sim.IsSquadMoving;i++)
                {
                    session.Advance(.02f);
                    if(!climbed&&sim.Units.Any(u=>u.Position.y>wall.Bottom.y+.2f&&u.Position.y<wall.TopHeight-.1f))
                    {climbed=true;yield return null;yield return null;Capture(scene,"Climbing");}
                }
                Assert.That(climbed,Is.True);Assert.That(sim.IsSquadMoving,Is.False);
                Assert.That(sim.Units.Where(u=>u.IsAlive).All(u=>Mathf.Abs(u.Position.y-wall.TopHeight)<.001f),Is.True);
                Assert.That(sim.Units.Select(u=>u.SlotId),Is.EqualTo(slots));
                yield return null;yield return null;Capture(scene,"OnWall");
                var direction=wall.GetComponent<BattleTower>().Footprint.x==3?Vector3.right:Vector3.forward;
                Assert.That(sim.RequestMovement(new MovementRequest(wall.Top+direction*.3f)),Is.True);
                for(var i=0;i<4000&&sim.IsSquadMoving;i++)session.Advance(.02f);
                Assert.That(sim.IsSquadMoving,Is.False,string.Join("; ",sim.Units.Select(u=>$"{u.Id}:{u.Position}")));
                var ground=session.Route.CaptureTopCenters()[0];
                Assert.That(sim.RequestMovement(new MovementRequest(ground)),Is.True);
                for(var i=0;i<4000&&sim.IsSquadMoving;i++)session.Advance(.02f);
                if(sim.IsSquadMoving)
                {
                    var movement=typeof(BattleSimulation).GetField("continuous",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(sim);
                    var travels=(System.Collections.IDictionary)movement.GetType().GetField("travels",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(movement);
                    var report=$"Reference={sim.SquadReferencePosition}, destination={sim.SquadDestination}\n";
                    foreach(var u in sim.Units)
                    {
                        var t=travels[u.Id];report+=$"{u.Id}: {u.Position} HP={u.Hp} ";
                        if(t!=null){var next=(int)t.GetType().GetField("Next").GetValue(t);var route=(System.Collections.Generic.List<Vector3>)t.GetType().GetField("Path").GetValue(t);report+=$"next={next}/{route.Count} goal={t.GetType().GetField("Goal").GetValue(t)} path="+string.Join(" -> ",route);}
                        report+="\n";
                    }
                    File.WriteAllText("Logs/WallLadders/ReturnDebug.txt",report);
                }
                Assert.That(sim.IsSquadMoving,Is.False,string.Join("; ",sim.Units.Select(u=>$"{u.Id}:{u.Position}")));
                Assert.That(sim.Units.Where(u=>u.IsAlive).All(u=>Mathf.Abs(u.Position.y-ground.y)<.001f),Is.True);
                yield return null;Capture(scene,"ReturnedToGround");
            }
            finally{if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);if(scene.IsValid()&&scene.isLoaded)SceneManager.UnloadSceneAsync(scene);}
        }
        private static void Capture(Scene scene,string name)
        {
            var camera=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).FirstOrDefault(c=>c.enabled);
            if(camera==null||SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            var target=new RenderTexture(1280,720,24);var old=camera.targetTexture;var active=RenderTexture.active;
            var image=new Texture2D(1280,720,TextureFormat.RGBA32,false);
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();Directory.CreateDirectory("Logs/WallLadders");File.WriteAllBytes("Logs/WallLadders/"+name+".png",image.EncodeToPNG());}
            finally{camera.targetTexture=old;RenderTexture.active=active;target.Release();Object.Destroy(target);Object.Destroy(image);}
        }
    }
}
#endif
