#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Presentation.LegacyUI;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Team5.Rebuild.Tests
{
    public sealed class SiegeRamPlayModeTests
    {
        private Scene initial;private SiegeRamFleet pointerTarget;private bool pointerHeld;private GameObject pointerReceiver;
        private readonly System.Collections.Generic.List<InputDevice> disabledMice=new System.Collections.Generic.List<InputDevice>();
        private const string ScenePath="Assets/Rebuild/Scenes/1.unity";
        // This scene is authored for Catgirl (the test reads her sprites); pin that set so the stored choice cannot change it.
        private CharacterSetPrefsScope characterSet;
        [SetUp] public void PinCatgirlSet() => characterSet = new CharacterSetPrefsScope(Team5.Rebuild.Session.CharacterSet.Catgirl);
        [TearDown] public void RestoreCharacterSet() => characterSet?.Dispose();
        [SetUp]public void Setup()
        {
            Application.runInBackground=true;
            foreach(var device in InputSystem.devices.OfType<Mouse>().Where(m=>m.enabled).ToArray()){disabledMice.Add(device);InputSystem.DisableDevice(device);}
        }
        [UnityTest]public IEnumerator SummonButtonRejectsOccupiedSpawnThenMouseDrawsIndependentVehicleRoute()
        {
            initial=SceneManager.GetActiveScene();Directory.CreateDirectory("Logs/SiegeRam");
            EditorSceneManager.LoadSceneInPlayMode(ScenePath,new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;yield return null;yield return null;
            var scene=SceneManager.GetSceneByPath(ScenePath);SceneManager.SetActiveScene(scene);
            foreach(var tutorial in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<LevelOneTutorial>()))tutorial.Skip();
            var owner=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BattlePresentation>()).Single();
            var fleet=owner.GetComponent<SiegeRamFleet>();var sim=owner.Session.Simulation;
            var gate=sim.Towers.First(t=>t.Definition.IsWoodenGate);
            Assert.That(fleet.IsRoutePassable(gate.Position),Is.True,"Routes may cross a standing wooden gate.");
            Assert.That(fleet.IsPassable(gate.Position),Is.False,"Planning through a gate must not make the intact gate physically passable.");
            var stoneWall=sim.Towers.First(t=>t.Definition.IsWall&&!t.Definition.IsWoodenGate);
            Assert.That(fleet.IsRoutePassable(stoneWall.Position),Is.False,"Stone walls still block route drawing.");
            var hud=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<LegacyBattleHud>()).Single();
            sim.SetPaused(false);Time.timeScale=1;
            yield return null;yield return null;
            var original=(RectTransform)hud.SquadButton(2).transform;
            for(var i=0;i<3;i++)
            {
                var rect=(RectTransform)fleet.VehicleButton(i).transform;
                Assert.That(rect.sizeDelta,Is.EqualTo(original.sizeDelta*1.5f));
                Assert.That(rect.anchoredPosition.y,Is.LessThan(original.anchoredPosition.y-original.sizeDelta.y));
                if(i>0)Assert.That(fleet.VehicleButton(i).interactable,Is.False);
            }
            var image=fleet.VehicleButton(0).GetComponentsInChildren<Image>().Single(i=>i.name=="Mirrored Siege Ram Icon");
            Assert.That(image.rectTransform.localScale.x,Is.LessThan(0));
            var quantity=fleet.VehicleButton(0).GetComponentsInChildren<TMPro.TMP_Text>().Single(t=>t.name=="Quantity");
            Assert.That(fleet.RemainingQuantity,Is.EqualTo(1));Assert.That(quantity.text,Is.EqualTo("x1"));
            Assert.That(fleet.SpawnOccupied(),Is.True);
            Assert.That(fleet.TrySummon(),Is.False);Assert.That(fleet.Vehicles.Count,Is.Zero);Assert.That(fleet.IsPlanning,Is.False);
            Assert.That(fleet.RemainingQuantity,Is.EqualTo(1),"Rejected summons must not consume stock.");
            Assert.That(sim.RequestMovement(new MovementRequest(sim.SquadReferencePosition+Vector3.back*3)),Is.True);
            for(var i=0;i<1000&&fleet.SpawnOccupied();i++)owner.Session.AdvancePlaybackFrame(.02f);
            Assert.That(fleet.SpawnOccupied(),Is.False,"Move the existing squad clear of the red spawn area.");
            var unitIds=sim.Units.Select(u=>u.Id).ToArray();var slots=sim.Units.Select(u=>u.SlotId).ToArray();var destination=sim.SquadDestination;
            yield return null;yield return null;
            pointerTarget=fleet;pointerHeld=false;
            var button=(RectTransform)fleet.VehicleButton(0).transform;
            Canvas.ForceUpdateCanvases();
            var buttonPoint=RectTransformUtility.WorldToScreenPoint(null,button.TransformPoint(button.rect.center));
            var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=buttonPoint},hits);
            File.WriteAllText("Logs/SiegeRam/ButtonRaycast.txt",$"screen {Screen.width}x{Screen.height}; point {buttonPoint}; enabled {fleet.VehicleButton(0).IsInteractable()}; hits "+string.Join(",",hits.Select(h=>h.gameObject.name)));
            Assert.That(hits.FirstOrDefault().gameObject?.GetComponentInParent<Button>(),Is.SameAs(fleet.VehicleButton(0)),"The summon slot must be the top UI hit.");
            Capture("NormalSlots");
            yield return Send(buttonPoint,true);yield return Send(buttonPoint,false);
            Assert.That(fleet.IsPlanning,Is.True,"Button pointer events must enter vehicle route planning.");
            Assert.That(fleet.Vehicles.Count,Is.EqualTo(1));Assert.That(Time.timeScale,Is.Zero);Assert.That(sim.IsPaused,Is.True);
            Assert.That(fleet.RemainingQuantity,Is.Zero);Assert.That(quantity.text,Is.EqualTo("x0"));
            Assert.That(image.color.r,Is.LessThan(.3f));
            Assert.That(hud.SquadButton(0).IsInteractable(),Is.False);Assert.That(hud.SpeedSlider.IsInteractable(),Is.False);
            var ram=fleet.PlanningVehicle;Assert.That(ram.Position,Is.EqualTo(fleet.SpawnPosition));
            Assert.That(fleet.VehicleRenderer(ram.Id).flipX,Is.True);Assert.That(fleet.VehicleRenderer(ram.Id).color,Is.EqualTo(Color.white));
            var soldier=owner.GetComponent<BattleUnitInspection>();Assert.That(soldier.IsInputSuppressed,Is.True);
            var hair=owner.GetComponentsInChildren<SpriteRenderer>().First(s=>s.enabled&&s.name=="F_HAIR_FRONT");
            Assert.That(Mathf.Max(hair.color.r,Mathf.Max(hair.color.g,hair.color.b)),Is.LessThan(.2f));
            Assert.That(fleet.BeginDrag(fleet.SpawnPosition+Vector3.right*10),Is.False,"Dragging cannot begin away from the ram.");
            var spawn=fleet.SpawnPosition;
            yield return Send(Camera.main.WorldToScreenPoint(spawn),true);
            yield return Send(Camera.main.WorldToScreenPoint(spawn+Vector3.back*3.2f),true);
            yield return Send(Camera.main.WorldToScreenPoint(spawn+Vector3.back*3.2f+Vector3.right*3.2f),true);
            Assert.That(fleet.Draft.Count,Is.EqualTo(3));
            Capture("Planning");yield return null;yield return null;yield return null;
            yield return Send(Camera.main.WorldToScreenPoint(spawn+Vector3.back*3.2f+Vector3.right*3.2f),false);
            Assert.That(fleet.IsPlanning,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1));Assert.That(sim.IsPaused,Is.False);
            Assert.That(soldier.IsInputSuppressed,Is.False);Assert.That(hud.SpeedSlider.IsInteractable(),Is.True);
            Assert.That(ram.Route.Count,Is.EqualTo(3));Assert.That(sim.SquadDestination,Is.EqualTo(destination));
            Assert.That(sim.Units.Select(u=>u.Id),Is.EqualTo(unitIds));Assert.That(sim.Units.Select(u=>u.SlotId),Is.EqualTo(slots));
            for(var i=0;i<400&&ram.IsMoving;i++)
            {for(var tick=0;tick<12;tick++)owner.Session.AdvancePlaybackFrame(.02f);yield return null;}
            Assert.That(ram.IsMoving,Is.False);Assert.That(Vector3.Distance(ram.Position,spawn+Vector3.back*3+Vector3.right*3),Is.LessThan(.001f));
            Capture("AfterMove");yield return null;yield return null;
            Assert.That(fleet.VehicleButton(0).interactable,Is.False);Assert.That(fleet.TrySummon(),Is.False);
            Assert.That(fleet.RemainingQuantity,Is.Zero);Assert.That(fleet.Vehicles.Count,Is.EqualTo(1));
            Assert.That(fleet.VehicleRenderer(ram.Id).GetComponentInParent<SiegeRamView>().RouteLine.enabled,Is.False,"Traveled route must disappear.");
            var target=(RectTransform)fleet.VehicleButton(0).transform;
            Assert.That(target.sizeDelta,Is.EqualTo(original.sizeDelta*1.5f));
            Assert.That(ram.Speed,Is.EqualTo(.48f).Within(.0001f));
            var beforeReplan=ram.Route.ToArray();
            fleet.ProcessPointer(Camera.main.WorldToScreenPoint(fleet.VehicleRenderer(ram.Id).bounds.center),true,true,false);
            Assert.That(fleet.IsPlanning,Is.True,"Clicking an existing ram must replan even when stock is zero.");
            Assert.That(fleet.PlanningVehicle,Is.SameAs(ram));Assert.That(sim.IsPaused,Is.True);
            fleet.CancelPlanning();
            Assert.That(fleet.Vehicles.Count,Is.EqualTo(1));Assert.That(fleet.RemainingQuantity,Is.Zero);
            Assert.That(ram.Route,Is.EqualTo(beforeReplan));Assert.That(sim.IsPaused,Is.False);
            Assert.That(fleet.TryBeginReplanningAt(Camera.main.WorldToScreenPoint(fleet.VehicleRenderer(ram.Id).bounds.center)),Is.True);
            Assert.That(fleet.BeginDrag(ram.Position),Is.True);
            Assert.That(fleet.ExtendDrag(ram.Position+Vector3.left*3),Is.True);
            Assert.That(fleet.FinishDrag(),Is.True);
            Assert.That(ram.Route[0],Is.EqualTo(ram.Position));Assert.That(ram.Route[1],Is.EqualTo(ram.Position+Vector3.left*3));
            Assert.That(fleet.RemainingQuantity,Is.Zero);Assert.That(sim.SquadDestination,Is.EqualTo(destination));
            owner.Session.AdvancePlaybackFrame(.1f);yield return null;
            var movingPosition=ram.Position;var movingRoute=ram.Route.ToArray();
            Assert.That(fleet.TryBeginReplanningAt(Camera.main.WorldToScreenPoint(fleet.VehicleRenderer(ram.Id).bounds.center)),Is.True);
            fleet.CancelPlanning();Assert.That(ram.Position,Is.EqualTo(movingPosition));Assert.That(ram.Route,Is.EqualTo(movingRoute));
            Assert.That(fleet.TryBeginReplanningAt(Camera.main.WorldToScreenPoint(fleet.VehicleRenderer(ram.Id).bounds.center)),Is.True);
            var alignedFirst=SiegeRamMotion.NextBlockCenter(movingPosition,Vector3.left);
            Assert.That(fleet.BeginDrag(movingPosition),Is.True);Assert.That(fleet.ExtendDrag(alignedFirst),Is.True);
            Assert.That(fleet.Draft[1],Is.EqualTo(alignedFirst));
            Assert.That(fleet.ExtendDrag(alignedFirst+Vector3.back*3),Is.True);
            Assert.That(fleet.FinishDrag(),Is.True);Assert.That(ram.Route[1],Is.EqualTo(alignedFirst));
            Assert.That(ram.Route[2]-ram.Route[1],Is.EqualTo(Vector3.back*3));
        }
        [UnityTest]public IEnumerator InvalidRouteAndCancelRestorePauseColorsAndReleaseSpawn()
        {
            initial=SceneManager.GetActiveScene();
            EditorSceneManager.LoadSceneInPlayMode(ScenePath,new LoadSceneParameters(LoadSceneMode.Additive));yield return null;yield return null;yield return null;
            var scene=SceneManager.GetSceneByPath(ScenePath);SceneManager.SetActiveScene(scene);
            foreach(var tutorial in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<LevelOneTutorial>()))tutorial.Skip();
            var owner=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BattlePresentation>()).Single();
            var fleet=owner.GetComponent<SiegeRamFleet>();var sim=owner.Session.Simulation;sim.SetPaused(false);Time.timeScale=1;
            Assert.That(sim.RequestMovement(new MovementRequest(sim.SquadReferencePosition+Vector3.back*3)),Is.True);
            for(var i=0;i<1000&&fleet.SpawnOccupied();i++)owner.Session.AdvancePlaybackFrame(.02f);
            yield return null;yield return null;
            sim.SetPaused(true);
            var hair=owner.GetComponentsInChildren<SpriteRenderer>().First(s=>s.enabled&&s.name=="F_HAIR_FRONT");var color=hair.color;
            Assert.That(fleet.TrySummon(),Is.True);Assert.That(fleet.TrySummon(),Is.False);
            Assert.That(fleet.BeginDrag(fleet.SpawnPosition),Is.True);
            Assert.That(fleet.ExtendDrag(fleet.SpawnPosition+Vector3.left*3),Is.False,"Leaving the map must be rejected.");
            Assert.That(fleet.FinishDrag(),Is.False);Assert.That(fleet.IsPlanning,Is.True);
            fleet.CancelPlanning();yield return null;
            Assert.That(fleet.Vehicles.Count,Is.Zero);Assert.That(fleet.IsPlanning,Is.False);Assert.That(fleet.SpawnOccupied(),Is.False);
            Assert.That(sim.IsPaused,Is.True);Assert.That(Time.timeScale,Is.EqualTo(1));Assert.That(hair.color,Is.EqualTo(color));
            Assert.That(fleet.RemainingQuantity,Is.EqualTo(1),"Cancelling removes the undeployed ram and returns its stock.");
            Assert.That(fleet.TrySummon(),Is.True);fleet.CancelPlanning();yield return null;
        }
        private IEnumerator Send(Vector2 point,bool held)
        {
            var down=held&&!pointerHeld;var up=!held&&pointerHeld;
            if(pointerTarget.IsPlanning)pointerTarget.ProcessPointer(point,down,held,up);
            else
            {
                var data=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};
                if(down)
                {
                    var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
                    pointerReceiver=hits.FirstOrDefault().gameObject;
                    if(pointerReceiver!=null)ExecuteEvents.ExecuteHierarchy(pointerReceiver,data,ExecuteEvents.pointerDownHandler);
                }
                if(up&&pointerReceiver!=null)
                {
                    ExecuteEvents.ExecuteHierarchy(pointerReceiver,data,ExecuteEvents.pointerUpHandler);
                    ExecuteEvents.ExecuteHierarchy(pointerReceiver,data,ExecuteEvents.pointerClickHandler);pointerReceiver=null;
                }
            }
            pointerHeld=held;yield return null;yield return null;
        }
        private static void Capture(string name)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            var camera=Camera.main;var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
            var canvases=Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            var settings=canvases.Select(c=>(c.renderMode,c.worldCamera,c.planeDistance)).ToArray();
            var target=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGBA32,false);
            try
            {
                camera.targetTexture=target;
                foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.2f;}
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/SiegeRam/"+name+".png",image.EncodeToPNG());
            }
            finally
            {
                for(var i=0;i<canvases.Length;i++)if(canvases[i]!=null){canvases[i].renderMode=settings[i].renderMode;canvases[i].worldCamera=settings[i].worldCamera;canvases[i].planeDistance=settings[i].planeDistance;}
                camera.targetTexture=oldTarget;RenderTexture.active=oldActive;Canvas.ForceUpdateCanvases();target.Release();Object.Destroy(target);Object.Destroy(image);
            }
        }
        [UnityTearDown]public IEnumerator Cleanup()
        {
            var scene=SceneManager.GetSceneByPath(ScenePath);
            if(scene.IsValid()&&scene.isLoaded)
            {foreach(var fleet in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SiegeRamFleet>()))fleet.CancelPlanning();}
            foreach(var device in disabledMice)if(device.added)InputSystem.EnableDevice(device);disabledMice.Clear();
            Time.timeScale=1;if(initial.IsValid()&&initial.isLoaded)SceneManager.SetActiveScene(initial);
            if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
#endif
