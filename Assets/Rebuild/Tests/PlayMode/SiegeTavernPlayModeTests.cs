#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Session;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Team5.Rebuild.Tests
{
    public sealed class SiegeTavernPlayModeTests
    {
        [UnityTest] public IEnumerator ShopDeploysIntoBattleAndEnemyBuildsAndUpgrades()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(SiegeTavernMode.ShopScene,new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;SiegeTavernMode.NewRun();
            var run=SiegeTavernMode.Run;run.Offers[0]=2;
            Assert.IsTrue(run.Buy(0));Assert.AreEqual(4,run.Deployment().Count);
            for(var frame=0;frame<4;frame++)yield return null;
            Directory.CreateDirectory("Logs/SiegeTavern");
            ScreenCapture.CaptureScreenshot("Logs/SiegeTavern/Shop.png");yield return null;yield return null;
            Object.FindFirstObjectByType<SiegeTavernMode>().BeginBattle();
            for(var i=0;i<100&&SceneManager.GetActiveScene().path!=SiegeTavernMode.BattleScene;i++)yield return null;
            yield return null;
            var mode=Object.FindFirstObjectByType<SiegeTavernMode>();
            var session=Object.FindFirstObjectByType<BattleSession>();
            Assert.IsNotNull(session);Assert.IsNotNull(session.Simulation);
            Assert.That(session.ValidateConfiguration(),Is.Empty);
            Assert.AreEqual(0,mode.EnemyBuilder.BuiltCount);
            Assert.IsTrue(session.Simulation.Towers.All(t=>t.UnderConstruction&&!t.IsAlive));
            for(var i=0;i<100&&session.Simulation.PendingSpawnCount>0;i++)session.Advance(.02f);
            Assert.AreEqual(4,session.Simulation.Units.Count);
            Assert.AreEqual(4,Object.FindObjectsByType<UnitView>(FindObjectsSortMode.None).Length);
            while(session.Simulation.ElapsedTime<8.1)
            {session.Advance(.1f);yield return null;}
            Assert.AreEqual(2,mode.EnemyBuilder.BuiltCount);
            session.Simulation.SetPaused(true);var gold=mode.EnemyBuilder.Gold;
            yield return null;yield return null;
            Assert.AreEqual(gold,mode.EnemyBuilder.Gold);
            session.Simulation.SetPaused(false);
            while(session!=null&&session.Simulation.ElapsedTime<14)
            {session.Advance(.1f);yield return null;}
            Assert.IsTrue(mode.EnemyBuilder.Levels.Any(level=>level>=2));
            yield return null;ScreenCapture.CaptureScreenshot("Logs/SiegeTavern/Battle.png");yield return null;yield return null;
            Capture(Camera.main,"Logs/SiegeTavern/BattleMap.png");
            for(var i=0;i<1500&&session!=null;i++)
            {session.Advance(.1f);yield return null;}
            Assert.AreEqual(SiegeTavernMode.ShopScene,SceneManager.GetActiveScene().path);
            Assert.IsTrue(run.Round>1||run.Lives<3);
        }
        private static void Capture(Camera camera,string path)
        {
            Assert.IsNotNull(camera);var rt=RenderTexture.GetTemporary(1280,720,24);var old=camera.targetTexture;var active=RenderTexture.active;
            var texture=new Texture2D(1280,720,TextureFormat.RGBA32,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());}
            finally{camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(texture);}
        }
    }
}
#endif
