#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Presentation.LegacyUI;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Team5.Rebuild.Tests
{
    public sealed class SquadPlatformPlayModeTests
    {
        private Scene initial, opened;
        [UnityTest]
        public IEnumerator TutorialShowsVisibleGroundSwapBeforePositioningResult()
        {
            initial = SceneManager.GetActiveScene(); Time.timeScale = 1;
            typeof(LevelOneTutorial).GetField("hasPlayedTutorial", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, false);
            EditorSceneManager.LoadSceneInPlayMode("Assets/Rebuild/Scenes/Level1GridBattle.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null; yield return null;
            opened = SceneManager.GetSceneByPath("Assets/Rebuild/Scenes/Level1GridBattle.unity"); SceneManager.SetActiveScene(opened);
            var tutorial = opened.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LevelOneTutorial>()).Single();
            var owner = opened.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BattlePresentation>()).Single();
            var hud = opened.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LegacyBattleHud>()).Single();
            var session = owner.Session;
            for (var i = 0; i < 100 && tutorial.CurrentStep != 1; i++) yield return new WaitForSecondsRealtime(.02f);
            Assert.That(tutorial.CurrentStep, Is.EqualTo(1)); tutorial.Continue(); yield return null; yield return null;
            Assert.That(tutorial.CurrentStep, Is.EqualTo(2)); tutorial.Continue(); yield return null; yield return null;
            for (var i = 0; i < 500 && tutorial.CurrentStep != 3; i++)
            { session.AdvancePlaybackFrame(.02f); yield return null; }
            Assert.That(tutorial.CurrentStep, Is.EqualTo(3)); Assert.That(session.Simulation.IsPaused, Is.True);
            hud.SelectSquad(); yield return null; yield return null;
            Assert.That(hud.FormationEditor, Is.Not.Null);
            var captain = session.Simulation.Units.Single(u => u.Role == UnitRole.Captain);
            Assert.That(owner.TryGetView(captain.Id, out var view), Is.True);
            Assert.That(owner.GetComponent<SquadPlatformView>(),Is.Null); var originalSlot = captain.SlotId;
            hud.FormationEditor.Grid.HandleClick(originalSlot); hud.FormationEditor.Grid.HandleClick(7);
            Assert.That(hud.FormationEditor.ApplyAndResume(), Is.True);
            var pausedTime = session.Simulation.ElapsedTime;
            bool ResultVisible() => Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None).Any(t => t.text.Contains("POSITIONING AVOIDS DAMAGE"));
            for (var i = 0; i < 100 && !ResultVisible(); i++)
            {
                Assert.That(session.Simulation.IsPaused, Is.True);
                Assert.That(session.Simulation.ElapsedTime, Is.EqualTo(pausedTime));
                yield return new WaitForSecondsRealtime(.01f);
            }
            Assert.That(ResultVisible(), Is.True);
            Assert.That(view.IsPlatformRepositioning, Is.False, "Result hint must wait for the visual position, not just SlotId.");
            Assert.That(Vector3.Distance(view.transform.position,captain.Position), Is.LessThan(.001f));
            Assert.That(view.IsRidingPlatform,Is.False);
            Assert.That(session.Simulation.IsPaused, Is.True); Assert.That(session.Simulation.ElapsedTime, Is.EqualTo(pausedTime));
            tutorial.Skip();
        }
        [UnityTest]
        public IEnumerator Level1WalksOnGroundAndKeepsFixedFormation()
        {
            initial=SceneManager.GetActiveScene();Time.timeScale=1;
            const string path="Assets/Rebuild/Scenes/Level1GridBattle.unity";
            EditorSceneManager.LoadSceneInPlayMode(path,new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;yield return null;
            opened=SceneManager.GetSceneByPath(path);SceneManager.SetActiveScene(opened);
            foreach(var guide in opened.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<LevelOneTutorial>()))guide.Skip();
            var owner=opened.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BattlePresentation>()).Single();
            var session=owner.Session;
            Assert.That(owner.GetComponent<SquadPlatformView>(),Is.Null);
            Assert.That(opened.GetRootGameObjects().Any(g=>g.name=="Railway Path Artwork"),Is.False);
            Assert.That(session.ContinuousSquadMovement,Is.False);Assert.That(session.Route.RequiredWidth,Is.EqualTo(3));
            session.Simulation.SetPaused(false);session.AdvancePlaybackFrame(.05f);
            yield return new WaitForSecondsRealtime(.08f);
            var unit=session.Simulation.Units.First(u=>u.IsAlive);Assert.That(owner.TryGetView(unit.Id,out var view),Is.True);
            Assert.That(view.IsRidingPlatform,Is.False);Assert.That(view.GetComponent<FWUnitVisual>().IsMoving,Is.True);
            Assert.That(view.transform.position.y,Is.EqualTo(unit.Position.y).Within(.001f));
            var before=view.transform.position;var oldSlot=unit.SlotId;
            session.AdvancePlaybackFrame(.2f);yield return new WaitForSecondsRealtime(.05f);
            Assert.That(Vector3.Distance(view.transform.position,before),Is.GreaterThan(.01f));
            session.Simulation.SetPaused(true);yield return new WaitForSecondsRealtime(.06f);yield return null;
            var stopped=view.transform.position;yield return new WaitForSecondsRealtime(.05f);
            Assert.That(Vector3.Distance(view.transform.position,stopped),Is.LessThan(.001f));
            Assert.That(view.GetComponent<FWUnitVisual>().IsMoving,Is.False);
            var destination=oldSlot==7?4:7;var permutation=Enumerable.Range(0,9).ToArray();permutation[oldSlot]=destination;permutation[destination]=oldSlot;
            Assert.That(session.Simulation.TryApplyFormation(permutation),Is.True);yield return null;
            Assert.That(unit.SlotId,Is.EqualTo(destination));Assert.That(Vector3.Distance(view.transform.position,unit.Position),Is.LessThan(.001f));
            Directory.CreateDirectory("Logs/RemoveEarlyRailPlatforms");Capture();
        }
        private static void Capture()
        {
            var camera = Camera.main; var old = camera.targetTexture; var active = RenderTexture.active;
            var target = new RenderTexture(1600,1000,24); var image = new Texture2D(1600,1000,TextureFormat.RGBA32,false);
            try { camera.targetTexture=target; camera.Render(); RenderTexture.active=target; image.ReadPixels(new Rect(0,0,1600,1000),0,0); image.Apply(); File.WriteAllBytes("Logs/RemoveEarlyRailPlatforms/Level1.png",image.EncodeToPNG()); }
            finally { camera.targetTexture=old; RenderTexture.active=active; target.Release(); Object.Destroy(target); Object.Destroy(image); }
        }
        [UnityTearDown] public IEnumerator Close()
        {
            Time.timeScale=1;
            if(initial.IsValid()&&initial.isLoaded) SceneManager.SetActiveScene(initial);
            if(opened.IsValid()&&opened.isLoaded) yield return SceneManager.UnloadSceneAsync(opened);
        }
    }
}
#endif
