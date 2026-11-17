#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Simulation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Team5.Rebuild.Tests
{
    public sealed class Level3SiegeRamPlayModeTests
    {
        private const string Path="Assets/Rebuild/Scenes/Level3GridBattle.unity";
        private Scene initial;
        [UnityTest] public IEnumerator GateStopUnlocksSupportStartAndRamBreaksGateThenSquadResumes()
        {
            Application.runInBackground=true;Directory.CreateDirectory("Logs/Level3SiegeRam");
            initial=SceneManager.GetActiveScene();
            EditorSceneManager.LoadSceneInPlayMode(Path,new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;yield return null;yield return null;
            var scene=SceneManager.GetSceneByPath(Path);SceneManager.SetActiveScene(scene);
            var owner=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BattlePresentation>()).Single();
            var session=owner.Session;
            // Isolate the gate mechanic from combat difficulty without saving the test scene.
            foreach(var tower in session.Towers.Where(t=>!t.IsWall))
            {var data=new SerializedObject(tower);data.FindProperty("range").floatValue=0;data.ApplyModifiedPropertiesWithoutUndo();}
            var roster=new SerializedObject(session);roster.FindProperty("initialRoster").objectReferenceValue=null;
            var members=roster.FindProperty("members");members.arraySize=6;
            var roles=new[]{UnitRole.Captain,UnitRole.Attack,UnitRole.Tank,UnitRole.Heal,UnitRole.Sniper,UnitRole.Demolisher};
            var slots=new[]{4,0,1,2,3,5};
            for(var index=0;index<roles.Length;index++)
            {
                var member=members.GetArrayElementAtIndex(index);
                member.FindPropertyRelative("enabled").boolValue=true;member.FindPropertyRelative("memberId").intValue=901+index;
                member.FindPropertyRelative("slotId").intValue=slots[index];member.FindPropertyRelative("role").intValue=(int)roles[index];
                member.FindPropertyRelative("spawnTime").floatValue=0;
            }
            roster.ApplyModifiedPropertiesWithoutUndo();
            session.StartBattle();
            yield return null;yield return null;yield return null;
            session.enabled=false;var sim=session.Simulation;Assert.That(sim,Is.Not.Null);
            var fleet=owner.GetComponent<SiegeRamFleet>();Assert.That(fleet,Is.Not.Null);
            Assert.That(fleet.SpawnPosition,Is.EqualTo(new Vector3(7,.25f,10)));
            Assert.That(fleet.SpawnPosition,Is.Not.EqualTo(session.Route.Cells[0].TopCenter));
            Assert.That(fleet.IsUnlocked,Is.False);Assert.That(fleet.VehicleButton(0).gameObject.activeSelf,Is.False);
            Assert.That(fleet.TrySummon(),Is.False);
            sim.SetPaused(true);
            for(var i=0;i<6;i++){session.AdvancePlaybackFrame(.1f);yield return null;}
            Assert.That(fleet.IsUnlocked,Is.False,"Pausing must not be mistaken for a gate stop.");sim.SetPaused(false);
            Assert.That(fleet.SpawnOccupied(),Is.False,"The main squad start does not occupy the separate support start.");
            // A high render rate produces multiple Update calls with no FixedUpdate.
            // These presentation-only frames used to reset the 0.4 battle-second timer.
            for(var frame=0;frame<500&&!fleet.IsUnlocked&&sim.Outcome==BattleOutcome.Running;frame++)
            {for(var tick=0;tick<10;tick++)session.AdvancePlaybackFrame(.02f);yield return null;yield return null;yield return null;}
            var gate=sim.Towers.Single(t=>t.Definition.IsWoodenGate);
            Assert.That(fleet.IsUnlocked,Is.True,"Six-member gate stop must unlock even with presentation-only frames between battle ticks.");
            Assert.That(gate.IsAlive,Is.True);Assert.That(fleet.VehicleButton(0).interactable,Is.True);
            var stopped=sim.Units.First(u=>u.IsAlive).Position;
            for(var i=0;i<10;i++)session.AdvancePlaybackFrame(.1f);
            Assert.That(sim.Units.First(u=>u.IsAlive).Position,Is.EqualTo(stopped));
            Assert.That(Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Any(t=>t.text.Contains("WOODEN GATE BLOCKS")),Is.True);
            yield return null;
            var guide=Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Single(t=>t.name=="Gate Instructions");
            guide.ForceMeshUpdate();Assert.That(guide.textInfo.characterCount,Is.GreaterThan(50));
            var center=RectTransformUtility.WorldToScreenPoint(null,guide.rectTransform.TransformPoint(guide.rectTransform.rect.center));
            Assert.That(center.x,Is.InRange(1f,Screen.width-1f));Assert.That(center.y,Is.InRange(1f,Screen.height-1f));
            Capture("GateGuide");
            fleet.VehicleButton(0).onClick.Invoke();
            Assert.That(fleet.IsPlanning,Is.True);Assert.That(sim.IsPaused,Is.True);Assert.That(Time.timeScale,Is.Zero);
            Assert.That(fleet.PlanningVehicle.Position,Is.EqualTo(fleet.SpawnPosition));
            Assert.That(fleet.RemainingQuantity,Is.Zero);
            Assert.That(fleet.IsRoutePassable(gate.Position),Is.True);Assert.That(fleet.IsPassable(gate.Position),Is.False);
            Assert.That(fleet.BeginDrag(fleet.SpawnPosition),Is.True);
            fleet.ExtendDrag(fleet.SpawnPosition+Vector3.left*3);
            Assert.That(fleet.Draft.Count,Is.EqualTo(1),"Level3 cannot draw left.");
            Assert.That(fleet.FinishDrag(),Is.False);Assert.That(fleet.IsPlanning,Is.True);
            Assert.That(fleet.BeginDrag(fleet.SpawnPosition),Is.True);
            fleet.ExtendDrag(fleet.SpawnPosition+Vector3.forward*3);
            Assert.That(fleet.Draft.Count,Is.EqualTo(1),"Level3 cannot draw vertically.");
            Assert.That(fleet.BeginDrag(fleet.SpawnPosition),Is.True);
            Assert.That(fleet.ExtendDrag(new Vector3(22,.25f,13)),Is.True,"Vertical mouse drift should project onto the rightward route through the gate.");
            Assert.That(fleet.Draft.All(p=>Mathf.Abs(p.z-fleet.SpawnPosition.z)<.001f),Is.True);
            for(var i=1;i<fleet.Draft.Count;i++)Assert.That(fleet.Draft[i].x,Is.GreaterThan(fleet.Draft[i-1].x));
            Capture("Planning");
            Assert.That(fleet.FinishDrag(),Is.True);Assert.That(sim.IsPaused,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1));
            var ram=fleet.Vehicles.Single();var hp=gate.Hp;var damaged=false;
            for(var frame=0;frame<700&&gate.IsAlive;frame++)
            {for(var tick=0;tick<20;tick++)session.AdvancePlaybackFrame(.02f);yield return null;if(gate.Hp<hp)damaged=true;}
            Assert.That(damaged,Is.True);Assert.That(gate.IsAlive,Is.False,"Ram contact must destroy the gate.");
            for(var i=0;i<10;i++){session.AdvancePlaybackFrame(.05f);yield return null;}
            Assert.That(sim.Units.First(u=>u.IsAlive).Position,Is.Not.EqualTo(stopped),"Squad must resume its existing route once the gate disappears.");
            Assert.That(fleet.VehicleButton(0).interactable,Is.False);Capture("GateDestroyed");
            session.StartBattle();yield return null;
            Assert.That(fleet.IsUnlocked,Is.False);Assert.That(fleet.RemainingQuantity,Is.EqualTo(1));Assert.That(fleet.Vehicles.Count,Is.Zero);
        }
        private static void Capture(string name)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            var camera=Camera.main;var oldTarget=camera.targetTexture;var active=RenderTexture.active;
            var canvases=Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            var settings=canvases.Select(c=>(c.renderMode,c.worldCamera,c.planeDistance)).ToArray();
            var target=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGBA32,false);
            try
            {
                camera.targetTexture=target;
                foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.2f;}
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/Level3SiegeRam/"+name+".png",image.EncodeToPNG());
            }
            finally
            {
                for(var i=0;i<canvases.Length;i++)if(canvases[i]!=null){canvases[i].renderMode=settings[i].renderMode;canvases[i].worldCamera=settings[i].worldCamera;canvases[i].planeDistance=settings[i].planeDistance;}
                camera.targetTexture=oldTarget;RenderTexture.active=active;Canvas.ForceUpdateCanvases();target.Release();Object.Destroy(target);Object.Destroy(image);
            }
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            var scene=SceneManager.GetSceneByPath(Path);
            if(scene.IsValid()&&scene.isLoaded)
                foreach(var fleet in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SiegeRamFleet>()))fleet.CancelPlanning();
            Time.timeScale=1;if(initial.IsValid()&&initial.isLoaded)SceneManager.SetActiveScene(initial);
            if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
#endif
