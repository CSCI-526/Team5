#if UNITY_EDITOR
using System.Collections;
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
    public sealed class Level4CameraPlayModeTests
    {
        private const string ScenePath="Assets/Rebuild/Scenes/Level4GridBattle.unity";
        private Scene initial;
        private RenderTexture target;
        private void ConfigureViewport(Scene scene,LoadSceneMode mode)
        {
            if(scene.path!=ScenePath)return;
            var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).Single(c=>c.CompareTag("MainCamera"));
            target=new RenderTexture(1280,720,24);camera.targetTexture=target;
        }
        [UnityTest] public IEnumerator Level4ZoomFollowAndInspectionRespectItsOwnOriginalView()
        {
            Application.runInBackground=true;Directory.CreateDirectory("Logs/Level4FollowCamera");
            initial=SceneManager.GetActiveScene();SceneManager.sceneLoaded+=ConfigureViewport;
            EditorSceneManager.LoadSceneInPlayMode(ScenePath,new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;yield return null;yield return null;
            var scene=SceneManager.GetSceneByPath(ScenePath);SceneManager.SetActiveScene(scene);
            var owner=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BattlePresentation>()).Single();
            var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).Single(c=>c.CompareTag("MainCamera"));
            var follow=camera.GetComponent<Level3SquadCamera>();Assert.That(follow,Is.Not.Null);
            Assert.That(follow.OverviewGroundCorners.Count,Is.EqualTo(4));
            var session=owner.Session;
            foreach(var tower in session.Towers)
            {var data=new SerializedObject(tower);data.FindProperty("range").floatValue=0;data.ApplyModifiedPropertiesWithoutUndo();}
            session.StartBattle();session.enabled=false;session.AdvancePlaybackFrame(.02f);
            var sim=session.Simulation;Assert.That(sim,Is.Not.Null);sim.SetPaused(true);
            yield return new WaitForSecondsRealtime(.7f);
            Assert.That(camera.fieldOfView,Is.LessThan(follow.OverviewFieldOfView*.85f));
            Level3CameraPlayModeTests.AssertInside(camera,follow.OverviewGroundCorners);
            var before=camera.transform.position;Capture(camera,"StartZoom");sim.SetPaused(false);
            for(var frame=0;frame<100;frame++)
            {
                for(var tick=0;tick<10;tick++)session.AdvancePlaybackFrame(.02f);
                yield return null;Level3CameraPlayModeTests.AssertInside(camera,follow.OverviewGroundCorners);
            }
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(Vector3.Distance(camera.transform.position,before),Is.GreaterThan(1));
            Capture(camera,"FollowSquad");
            foreach(var direction in new[]{new Vector3(-1,0,-1),new Vector3(1,0,-1),new Vector3(1,0,1),new Vector3(-1,0,1)})
            {
                camera.transform.position=follow.OverviewPosition+direction*1000;
                yield return null;Level3CameraPlayModeTests.AssertInside(camera,follow.OverviewGroundCorners);
            }
            camera.aspect=2.4f;yield return null;Level3CameraPlayModeTests.AssertInside(camera,follow.OverviewGroundCorners);
            camera.ResetAspect();yield return null;
            var inspection=owner.GetComponent<BattleUnitInspection>();
            Assert.That(inspection.SelectUnit(sim.Units.First(u=>u.IsAlive).Id),Is.True);
            for(var frame=0;frame<40;frame++){yield return null;Level3CameraPlayModeTests.AssertInside(camera,follow.OverviewGroundCorners);}
            inspection.CloseInspection();var deadline=Time.realtimeSinceStartup+3;
            while(inspection.OwnsCamera&&Time.realtimeSinceStartup<deadline){yield return null;Level3CameraPlayModeTests.AssertInside(camera,follow.OverviewGroundCorners);}
            Assert.That(inspection.OwnsCamera,Is.False);
            Assert.That(owner.GetComponent<SiegeRamFleet>(),Is.Null,"Adding a camera must not add Level3 siege ram gameplay to Level4.");
        }
        private void Capture(Camera camera,string name)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            var active=RenderTexture.active;var texture=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
            try
            {
                camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();
                File.WriteAllBytes("Logs/Level4FollowCamera/"+name+".png",texture.EncodeToPNG());
            }
            finally{RenderTexture.active=active;Object.Destroy(texture);}
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            SceneManager.sceneLoaded-=ConfigureViewport;Time.timeScale=1;
            var scene=SceneManager.GetSceneByPath(ScenePath);
            if(initial.IsValid()&&initial.isLoaded)SceneManager.SetActiveScene(initial);
            if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
            if(target!=null){target.Release();Object.Destroy(target);target=null;}
        }
    }
}
#endif
