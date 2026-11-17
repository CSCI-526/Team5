#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Team5.Rebuild.Tests
{
    public sealed class Level3CameraPlayModeTests
    {
        private const string ScenePath="Assets/Rebuild/Scenes/Level3GridBattle.unity";
        private Scene initial;
        private RenderTexture fixtureTarget;
        private void ConfigureViewport(Scene scene,LoadSceneMode mode)
        {
            if(scene.path!=ScenePath)return;
            var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).Single(c=>c.CompareTag("MainCamera"));
            fixtureTarget=new RenderTexture(1280,720,24);camera.targetTexture=fixtureTarget;
        }
        [UnityTest] public IEnumerator ZoomAndFollowStayWithinOverviewIncludingInspectionAndPlanning()
        {
            Application.runInBackground=true;Directory.CreateDirectory("Logs/Level3FollowCamera");
            initial=SceneManager.GetActiveScene();
            SceneManager.sceneLoaded+=ConfigureViewport;
            EditorSceneManager.LoadSceneInPlayMode(ScenePath,new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;yield return null;yield return null;
            var scene=SceneManager.GetSceneByPath(ScenePath);SceneManager.SetActiveScene(scene);
            var owner=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BattlePresentation>()).Single();
            var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).Single(c=>c.CompareTag("MainCamera"));
            var follow=camera.GetComponent<Level3SquadCamera>();Assert.That(follow,Is.Not.Null);
            Assert.That(follow.OverviewGroundCorners.Count,Is.EqualTo(4));
            var session=owner.Session;
            // Test the camera against the authored route without combat ending the fixture.
            foreach(var tower in session.Towers)
            {var data=new SerializedObject(tower);data.FindProperty("range").floatValue=0;data.ApplyModifiedPropertiesWithoutUndo();}
            session.StartBattle();session.enabled=false;session.AdvancePlaybackFrame(.02f);
            var sim=session.Simulation;Assert.That(sim,Is.Not.Null);
            sim.SetPaused(true);
            for(var i=0;i<45;i++){yield return null;AssertInside(camera,follow.OverviewGroundCorners);}
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(camera.fieldOfView,Is.LessThan(follow.OverviewFieldOfView*.85f),"Normal battle view zooms in.");
            AssertInside(camera,follow.OverviewGroundCorners);
            var before=camera.transform.position;Capture(camera,"StartZoom");sim.SetPaused(false);
            File.WriteAllText("Logs/Level3FollowCamera/StartView.txt",$"Overview {follow.OverviewPosition}; current {camera.transform.position}; unit {sim.Units.First(u=>u.IsAlive).Position}; projected {camera.WorldToViewportPoint(sim.Units.First(u=>u.IsAlive).Position)}; aspect {camera.aspect}");
            for(var frame=0;frame<100;frame++)
            {
                for(var tick=0;tick<10;tick++)session.AdvancePlaybackFrame(.02f);
                yield return null;AssertInside(camera,follow.OverviewGroundCorners);
            }
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(Vector3.Distance(camera.transform.position,before),Is.GreaterThan(1),"Camera follows actual squad progress.");
            AssertInside(camera,follow.OverviewGroundCorners);Capture(camera,"FollowSquad");
            // Clamp the complete view, including corner cases and a wider Game View.
            foreach(var direction in new[]{new Vector3(-1,0,-1),new Vector3(1,0,-1),new Vector3(1,0,1),new Vector3(-1,0,1)})
            {
                camera.transform.position=follow.OverviewPosition+direction*1000;
                yield return null;AssertInside(camera,follow.OverviewGroundCorners);
            }
            camera.aspect=2.4f;yield return null;AssertInside(camera,follow.OverviewGroundCorners);
            camera.ResetAspect();yield return null;
            var inspection=owner.GetComponent<BattleUnitInspection>();
            Assert.That(inspection.SelectUnit(sim.Units.First(u=>u.IsAlive).Id),Is.True);
            for(var i=0;i<40;i++){yield return null;AssertInside(camera,follow.OverviewGroundCorners);}
            Assert.That(inspection.OwnsCamera,Is.True);inspection.CloseInspection();
            var restoreDeadline=Time.realtimeSinceStartup+3;
            while(inspection.OwnsCamera&&Time.realtimeSinceStartup<restoreDeadline){yield return null;AssertInside(camera,follow.OverviewGroundCorners);}
            Assert.That(inspection.OwnsCamera,Is.False,"Inspection restores normally without fighting follow movement.");
            var fleet=owner.GetComponent<SiegeRamFleet>();
            for(var frame=0;frame<500&&!fleet.IsUnlocked;frame++)
            {for(var tick=0;tick<10;tick++)session.AdvancePlaybackFrame(.02f);yield return null;AssertInside(camera,follow.OverviewGroundCorners);}
            Assert.That(fleet.IsUnlocked,Is.True);Assert.That(fleet.TrySummon(),Is.True);
            yield return null;Assert.That(fleet.IsPlanning,Is.True);
            Assert.That(camera.fieldOfView,Is.EqualTo(follow.OverviewFieldOfView).Within(.01f));
            Assert.That(Vector3.Distance(camera.transform.position,follow.OverviewPosition),Is.LessThan(.01f));
            AssertInside(camera,follow.OverviewGroundCorners);Capture(camera,"PlanningOverview");
            fleet.CancelPlanning();
            yield return new WaitForSecondsRealtime(.5f);Assert.That(camera.fieldOfView,Is.LessThan(follow.OverviewFieldOfView*.85f));
            AssertInside(camera,follow.OverviewGroundCorners);
        }
        internal static void AssertInside(Camera camera,IReadOnlyList<Vector2> boundary)
        {
            var plane=new Plane(Vector3.up,new Vector3(0,.25f,0));
            foreach(var viewport in new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up})
            {
                var ray=camera.ViewportPointToRay(new Vector3(viewport.x,viewport.y,0));
                Assert.That(plane.Raycast(ray,out var distance),Is.True);
                var hit=ray.GetPoint(distance);var point=new Vector2(hit.x,hit.z);
                for(var edge=0;edge<boundary.Count;edge++)
                {
                    var a=boundary[edge];var delta=boundary[(edge+1)%boundary.Count]-a;
                    var inward=new Vector2(-delta.y,delta.x).normalized;
                    Assert.That(Vector2.Dot(inward,point-a),Is.GreaterThanOrEqualTo(-.015f),"Entire visible ground footprint must remain inside the original overview.");
                }
            }
        }
        private static void Capture(Camera camera,string name)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            var previous=camera.targetTexture;var active=RenderTexture.active;
            var width=previous!=null?previous.width:Screen.width;var height=previous!=null?previous.height:Screen.height;
            var target=new RenderTexture(width,height,24);var texture=new Texture2D(width,height,TextureFormat.RGBA32,false);
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();
                File.WriteAllBytes("Logs/Level3FollowCamera/"+name+".png",texture.EncodeToPNG());
            }
            finally{camera.targetTexture=previous;RenderTexture.active=active;target.Release();Object.Destroy(target);Object.Destroy(texture);}
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            SceneManager.sceneLoaded-=ConfigureViewport;
            var scene=SceneManager.GetSceneByPath(ScenePath);
            if(scene.IsValid()&&scene.isLoaded)
                foreach(var fleet in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SiegeRamFleet>()))fleet.CancelPlanning();
            Time.timeScale=1;if(initial.IsValid()&&initial.isLoaded)SceneManager.SetActiveScene(initial);
            if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
            if(fixtureTarget!=null){fixtureTarget.Release();Object.Destroy(fixtureTarget);fixtureTarget=null;}
        }
    }
}
#endif
