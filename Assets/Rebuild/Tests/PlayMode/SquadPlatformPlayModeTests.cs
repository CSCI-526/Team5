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
        public IEnumerator TutorialWaitsForVisibleSwapBeforeShowingPositioningResult()
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
            var platform = owner.GetComponent<SquadPlatformView>(); var originalSlot = captain.SlotId;
            hud.FormationEditor.Grid.HandleClick(originalSlot); hud.FormationEditor.Grid.HandleClick(7);
            Assert.That(hud.FormationEditor.ApplyAndResume(), Is.True);
            var pausedTime = session.Simulation.ElapsedTime; var platformPosition = platform.RenderAnchor;
            bool ResultVisible() => Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None).Any(t => t.text.Contains("POSITIONING AVOIDS DAMAGE"));
            for (var i = 0; i < 100 && !ResultVisible(); i++)
            {
                Assert.That(session.Simulation.IsPaused, Is.True);
                Assert.That(session.Simulation.ElapsedTime, Is.EqualTo(pausedTime));
                yield return new WaitForSecondsRealtime(.01f);
            }
            Assert.That(ResultVisible(), Is.True);
            Assert.That(view.IsPlatformRepositioning, Is.False, "Result hint must wait for the visual position, not just SlotId.");
            Assert.That(Vector3.Distance(view.PlatformRelativeOffset,captain.FormationOffset), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(platform.RenderAnchor,platformPosition), Is.LessThan(.0001f));
            Assert.That(session.Simulation.IsPaused, Is.True); Assert.That(session.Simulation.ElapsedTime, Is.EqualTo(pausedTime));
            tutorial.Skip();
        }
        [UnityTest]
        public IEnumerator Level1PlatformCarriesSquadAndSwapsOnlyWithinDeck()
        {
            initial = SceneManager.GetActiveScene(); Time.timeScale = 1;
            EditorSceneManager.LoadSceneInPlayMode("Assets/Rebuild/Scenes/Level1GridBattle.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null; yield return null;
            opened = SceneManager.GetSceneByPath("Assets/Rebuild/Scenes/Level1GridBattle.unity"); SceneManager.SetActiveScene(opened);
            foreach (var guide in opened.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LevelOneTutorial>())) guide.Skip();
            var owner = opened.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BattlePresentation>()).Single();
            var session = owner.Session;
            // Test-only two-passenger fixture; never saved to the authored scene.
            var fixture = new SerializedObject(session);
            fixture.FindProperty("initialRoster").objectReferenceValue = null;
            var members = fixture.FindProperty("members"); members.arraySize = 2;
            for (var i = 0; i < 2; i++)
            {
                var member = members.GetArrayElementAtIndex(i);
                member.FindPropertyRelative("enabled").boolValue = true;
                member.FindPropertyRelative("memberId").intValue = 110 + i;
                member.FindPropertyRelative("slotId").intValue = i == 0 ? 4 : 3;
                member.FindPropertyRelative("role").intValue = (int)(i == 0 ? UnitRole.Captain : UnitRole.Attack);
                member.FindPropertyRelative("spawnTime").floatValue = 0;
            }
            fixture.ApplyModifiedPropertiesWithoutUndo();
            session.StartBattle();
            foreach (var guide in opened.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LevelOneTutorial>())) guide.Skip();
            session.Simulation.SetPaused(false); session.AdvancePlaybackFrame(.05f);
            yield return null; yield return null;
            var platform = owner.GetComponent<SquadPlatformView>();
            Assert.That(platform.IsPlatformMode, Is.True); Assert.That(session.ContinuousSquadMovement, Is.False);
            Assert.That(platform.Model.GetComponentsInChildren<Collider>(), Is.Empty);
            var body = platform.Model.Find("Raised Body");
            var bounds = body.GetComponentsInChildren<Renderer>().First().bounds;
            foreach (var renderer in body.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            Assert.That(Vector3.Distance(bounds.size, new Vector3(3,SquadPlatformView.DeckThickness,3)), Is.LessThan(.001f));
            Assert.That(bounds.min.y-platform.RenderAnchor.y,Is.EqualTo(SquadPlatformView.BodyLift).Within(.001f));
            var wheelRig = platform.Model.Find("Wheel Rig");
            Assert.That(wheelRig, Is.Not.Null);
            var wheels = wheelRig.Cast<Transform>().Where(t => t.name.StartsWith("Wheel ")).ToArray();
            Assert.That(wheels.Length, Is.EqualTo(12), "Two axles on each of three tracks; one wheel on each rail.");
            foreach(var wheel in wheels)
                Assert.That(wheel.localPosition.y-SquadPlatformView.WheelRadius,Is.EqualTo(.012f).Within(.001f));
            Assert.That(owner.TryGetView(110, out var captain), Is.True);
            Assert.That(owner.TryGetView(111, out var attack), Is.True);
            var character = captain.GetComponent<FWUnitVisual>();
            Assert.That(character, Is.Not.Null); Assert.That(attack.GetComponent<FWUnitVisual>(), Is.Not.Null);
            Assert.That(captain.transform.Find("Body"), Is.Null, "The old block soldier must not overlap the Catgirl.");
            void AssertOnDeck(UnitView view)
            {
                Assert.That(view.IsRidingPlatform, Is.True);
                Assert.That(Vector3.Distance(view.transform.position, platform.RenderAnchor + view.PlatformRelativeOffset + Vector3.up*SquadPlatformView.DeckHeight), Is.LessThan(.001f));
                Assert.That(Mathf.Abs(view.PlatformRelativeOffset.x), Is.LessThanOrEqualTo(1.001f));
                Assert.That(Mathf.Abs(view.PlatformRelativeOffset.z), Is.LessThanOrEqualTo(1.001f));
            }
            AssertOnDeck(captain); AssertOnDeck(attack);
            var start = platform.RenderAnchor;
            var wheelRotation = wheels[0].localRotation;
            session.AdvancePlaybackFrame(.5f); yield return new WaitForSecondsRealtime(.06f); yield return null;
            Assert.That(Vector3.Distance(platform.RenderAnchor,start), Is.GreaterThan(.1f));
            Assert.That(Quaternion.Angle(wheels[0].localRotation,wheelRotation),Is.GreaterThan(.1f));
            AssertOnDeck(captain); AssertOnDeck(attack); Assert.That(captain.IsPlatformRepositioning, Is.False);
            Assert.That(character.IsMoving,Is.False,"Riding the platform must keep the character in Idle.");
            session.Simulation.SetPaused(true); yield return new WaitForSecondsRealtime(.06f);
            var stopped = platform.RenderAnchor; var pausedWheel = wheels[0].localRotation; yield return new WaitForSecondsRealtime(.08f);
            Assert.That(platform.RenderAnchor, Is.EqualTo(stopped));
            Assert.That(Quaternion.Angle(wheels[0].localRotation,pausedWheel),Is.LessThan(.001f));
            var oldOffset = captain.PlatformRelativeOffset;
            var slots = Enumerable.Range(0,9).ToArray(); slots[0] = 4; slots[4] = 0;
            Assert.That(session.Simulation.TryApplyFormation(slots), Is.True);
            Assert.That(captain.State.SlotId, Is.Zero);
            Assert.That(captain.PlatformRelativeOffset, Is.EqualTo(oldOffset), "Changing slots must not instantly teleport the displayed passenger.");
            session.Simulation.SetPaused(false);
            yield return null; yield return null;
            Assert.That(character.IsMoving,Is.True,"Walking between platform slots must play Run.");
            yield return new WaitForSecondsRealtime(.5f); yield return null;
            Assert.That(captain.IsPlatformRepositioning, Is.False); AssertOnDeck(captain); AssertOnDeck(attack);
            Assert.That(character.IsMoving,Is.False);
            Directory.CreateDirectory("Logs/SquadPlatform"); Capture();
            // Scene 1 keeps its free-squad movement mode and has no platform component.
            EditorSceneManager.LoadSceneInPlayMode("Assets/Rebuild/Scenes/1.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            var free = SceneManager.GetSceneByPath("Assets/Rebuild/Scenes/1.unity");
            Assert.That(free.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SquadPlatformView>()).Any(), Is.False);
            Assert.That(free.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BattleSession>()).Single().ContinuousSquadMovement, Is.True);
            yield return SceneManager.UnloadSceneAsync(free);
        }
        private static void Capture()
        {
            var camera = Camera.main; var old = camera.targetTexture; var active = RenderTexture.active;
            var target = new RenderTexture(1600,1000,24); var image = new Texture2D(1600,1000,TextureFormat.RGBA32,false);
            try { camera.targetTexture=target; camera.Render(); RenderTexture.active=target; image.ReadPixels(new Rect(0,0,1600,1000),0,0); image.Apply(); File.WriteAllBytes("Logs/SquadPlatform/Platform.png",image.EncodeToPNG()); }
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
