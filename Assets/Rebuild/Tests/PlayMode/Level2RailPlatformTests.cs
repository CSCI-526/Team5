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
    public sealed class Level2RailPlatformTests
    {
        // These scenes are authored for Catgirl; pin that set so the player's stored choice cannot change them.
        private CharacterSetPrefsScope characterSet;
        [SetUp] public void PinCatgirlSet() => characterSet = new CharacterSetPrefsScope(CharacterSet.Catgirl);
        [TearDown] public void RestoreCharacterSet() => characterSet?.Dispose();
        private Scene initial,opened;
        [UnityTest]public IEnumerator Level2RailsAndPlatformCarryCatgirlsWithoutChangingRouteOrSlots()
        {
            initial=SceneManager.GetActiveScene();Time.timeScale=1;
            const string path="Assets/Rebuild/Scenes/Level2GridBattle.unity";
            EditorSceneManager.LoadSceneInPlayMode(path,new LoadSceneParameters(LoadSceneMode.Additive));yield return null;yield return null;
            opened=SceneManager.GetSceneByPath(path);SceneManager.SetActiveScene(opened);
            var owner=opened.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BattlePresentation>()).Single();
            var session=owner.Session;var platform=owner.GetComponent<SquadPlatformView>();
            Assert.That(platform,Is.Not.Null);Assert.That(platform.IsPlatformMode,Is.True);
            Assert.That(session.ContinuousSquadMovement,Is.False);Assert.That(session.Route.RequiredWidth,Is.EqualTo(3));
            var rails=opened.GetRootGameObjects().Single(g=>g.name=="Railway Path Artwork");
            Assert.That(rails.GetComponentsInChildren<MeshRenderer>().Length,Is.GreaterThan(0));
            Assert.That(rails.GetComponentsInChildren<Collider>(),Is.Empty);Assert.That(platform.Model.GetComponentsInChildren<Collider>(),Is.Empty);
            var positions=session.Route.CaptureTopCenters();
            session.Simulation.SetPaused(false);session.AdvancePlaybackFrame(.08f);yield return new WaitForSecondsRealtime(.06f);yield return null;
            var living=session.Simulation.Units.Where(u=>u.IsAlive).ToArray();Assert.That(living.Length,Is.GreaterThan(0));
            var ids=living.Select(u=>u.Id).ToArray();var slots=living.Select(u=>u.SlotId).ToArray();var start=platform.RenderAnchor;
            session.AdvancePlaybackFrame(.3f);yield return new WaitForSecondsRealtime(.06f);yield return null;
            Assert.That(Vector3.Distance(start,platform.RenderAnchor),Is.GreaterThan(.02f));
            foreach(var unit in living)
            {
                Assert.That(owner.TryGetView(unit.Id,out var view),Is.True);Assert.That(view.IsRidingPlatform,Is.True);
                Assert.That(view.GetComponent<FWUnitVisual>(),Is.Not.Null);Assert.That(view.GetComponent<FWUnitVisual>().IsMoving,Is.False);
                Assert.That(Vector3.Distance(view.transform.position,platform.RenderAnchor+view.PlatformRelativeOffset+Vector3.up*SquadPlatformView.DeckHeight),Is.LessThan(.001f));
            }
            session.Simulation.SetPaused(true);yield return null;yield return null;
            var stopped=platform.RenderAnchor;yield return new WaitForSecondsRealtime(.08f);Assert.That(platform.RenderAnchor,Is.EqualTo(stopped));
            Assert.That(living.Select(u=>u.Id),Is.EqualTo(ids));Assert.That(living.Select(u=>u.SlotId),Is.EqualTo(slots));
            Assert.That(session.Route.CaptureTopCenters(),Is.EqualTo(positions));
            Capture();
        }
        private static void Capture()
        {
            Directory.CreateDirectory("Logs/Level2RailPlatform");var camera=Camera.main;var old=camera.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(1600,1000,24);var image=new Texture2D(1600,1000,TextureFormat.RGBA32,false);
            try {camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes("Logs/Level2RailPlatform/PlayMode.png",image.EncodeToPNG());}
            finally {camera.targetTexture=old;RenderTexture.active=active;target.Release();Object.Destroy(target);Object.Destroy(image);}
        }
        [UnityTearDown]public IEnumerator Close()
        {
            Time.timeScale=1;if(initial.IsValid()&&initial.isLoaded)SceneManager.SetActiveScene(initial);
            if(opened.IsValid()&&opened.isLoaded)yield return SceneManager.UnloadSceneAsync(opened);
        }
    }
}
#endif
