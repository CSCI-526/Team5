using System.Collections.Generic;
using System.Linq;
using Team5.BasicTowerTrial.Session;
using Team5.BasicTowerTrial.Simulation;
using Team5.BasicTowerTrial.World;
using Team5.BasicTowerTrial.Presentation.LegacyUI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Team5.BasicTowerTrial.Presentation
{
    [DisallowMultipleComponent,DefaultExecutionOrder(900)]
    public sealed partial class SiegeRamFleet : MonoBehaviour
    {
        [SerializeField]private BattlePresentation owner;
        [SerializeField]private Sprite icon;
        [SerializeField]private SiegeRamView vehiclePrefab;
        [SerializeField,Min(.1f)]private float moveSpeed=.48f;
        [SerializeField,Range(.1f,.49f)]private float radius=.42f;
        [SerializeField,Min(0),Tooltip("Number of siege rams available at the start of each battle.")]private int initialQuantity=1;
        [SerializeField,Min(.01f)]private float gateDamage=1;
        [SerializeField,Min(.05f)]private float gateAttackInterval=1;
        private readonly Dictionary<int,float> attackCooldowns=new Dictionary<int,float>();
        private int remainingQuantity;
        private Image ramIcon;private TMP_Text quantityLabel;
        private readonly List<SiegeRamMotion> vehicles=new List<SiegeRamMotion>();
        private readonly Dictionary<int,SiegeRamView> views=new Dictionary<int,SiegeRamView>();
        private readonly Button[] buttons=new Button[3];
        private readonly List<Vector3> draft=new List<Vector3>();
        private readonly Dictionary<SpriteRenderer,Color> spriteColors=new Dictionary<SpriteRenderer,Color>();
        private readonly Dictionary<Renderer,(MaterialPropertyBlock saved,MaterialPropertyBlock dim)> meshColors=new Dictionary<Renderer,(MaterialPropertyBlock,MaterialPropertyBlock)>();
        private readonly List<(CanvasGroup group,float alpha,bool interactable,bool blocks)> uiGroups=new List<(CanvasGroup,float,bool,bool)>();
        private BattleSession session;private BattleSimulation simulation,planningSimulation;
        private LegacyBattleHud hud;private BattleUnitInspection inspection;
        private SiegeRamMotion planningVehicle;private bool dragging,validCursor,wasPaused,oldSuppressed,planningNewSummon;
        private float oldTimeScale,toastUntil;private double sampledTime;
        private GameObject overlay;private TMP_Text hint,toast;private int nextId=1,openingPointerFrame=-1;
        public bool IsPlanning=>planningVehicle!=null;
        public int RemainingQuantity=>remainingQuantity;
        public IReadOnlyList<SiegeRamMotion> Vehicles=>vehicles;
        public SiegeRamMotion PlanningVehicle=>planningVehicle;
        internal bool HideVehicleHud { get; set; }
        internal Sprite PlanningIcon => icon;
        public IReadOnlyList<Vector3> Draft=>draft;
        public Button VehicleButton(int index)=>buttons[index];
        public SpriteRenderer VehicleRenderer(int id)=>views.TryGetValue(id,out var view)?view.Body:null;
        public Vector3 SpawnPosition=>supportSpawnCenter!=null?supportSpawnCenter.TopCenter:
            session!=null&&session.Route.Cells.Count>0?session.Route.Cells[0].TopCenter:Vector3.zero;

        private void OnEnable()
        {
            if(!Application.isPlaying||owner==null||BattleSceneFlow.IsPreviewScene(gameObject.scene))return;
            session=owner.Session;session.SimulationCreated+=OnSimulation;OnSimulation(session.Simulation);
            EnsureButtons();
        }
        private void EnsureButtons()
        {
            if(buttons[0]!=null)return;
            hud=Object.FindObjectsByType<LegacyBattleHud>(FindObjectsSortMode.None).FirstOrDefault(h=>h.Session==session);
            if(hud==null||hud.SquadButton(2)==null)return;
            var reference=(RectTransform)hud.SquadButton(2).transform;
            var scale=reference.sizeDelta.x/56f;var size=reference.sizeDelta*1.5f;
            var position=reference.anchoredPosition+Vector2.down*(reference.sizeDelta.y+12*scale);
            for(var i=0;i<3;i++)
            {
                var button=LegacyUiKit.CreateButton(reference.parent,"Siege Vehicle Slot "+(i+1),Vector2.zero,size,LegacyUiKit.PanelLineColor,0);
                var rect=(RectTransform)button.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
                rect.anchoredPosition=position+Vector2.down*i*(size.y+12*scale);button.interactable=false;buttons[i]=button;
                if(i==0)
                {
                    var image=LegacyUiKit.CreateImage(rect,"Mirrored Siege Ram Icon",Vector2.zero,size*.92f,Color.white);
                    image.sprite=icon;image.preserveAspect=true;image.raycastTarget=false;image.rectTransform.localScale=new Vector3(-1,1,1);
                    ramIcon=image;
                    var badge=LegacyUiKit.CreateImage(rect,"Siege Ram Quantity Badge",Vector2.zero,Vector2.one*29*scale,new Color(.1f,.14f,.2f,1));
                    badge.raycastTarget=false;
                    badge.rectTransform.anchorMin=badge.rectTransform.anchorMax=badge.rectTransform.pivot=Vector2.one;
                    badge.rectTransform.anchoredPosition=new Vector2(-2,-2)*scale;
                    quantityLabel=LegacyUiKit.CreateText(badge.transform,"Quantity","",Vector2.zero,Vector2.one*29*scale,16*scale,Color.white,FontStyles.Bold);
                    quantityLabel.raycastTarget=false;
                    button.onClick.AddListener(()=>TrySummon());
                }
            }
            toast=LegacyUiKit.CreateText(reference.parent,"Siege Ram Feedback","",new Vector2(0,-275)*scale,new Vector2(740,48)*scale,16*scale,LegacyUiKit.HighlightColor,FontStyles.Bold);
            toast.raycastTarget=false;
            RefreshQuantity();
        }
        private void RefreshQuantity()
        {
            foreach(var button in buttons)if(button!=null)button.gameObject.SetActive(IsUnlocked&&!HideVehicleHud);
            if(quantityLabel!=null)quantityLabel.text="x"+remainingQuantity;
            if(ramIcon!=null)ramIcon.color=RamIconColor();
            if(buttons[0]!=null)buttons[0].interactable=IsUnlocked&&remainingQuantity>0&&!IsPlanning&&simulation!=null&&simulation.Outcome==BattleOutcome.Running&&(hud==null||!hud.TutorialOwnsPause);
        }
        private void OnSimulation(BattleSimulation next)
        {
            CancelPlanning();foreach(var view in views.Values)if(view!=null)Destroy(view.gameObject);
            views.Clear();vehicles.Clear();attackCooldowns.Clear();simulation=next;sampledTime=next?.ElapsedTime??0;
            ResetGateGuide();remainingQuantity=Mathf.Max(0,initialQuantity);RefreshQuantity();
        }
        private void Update()
        {
            if(session==null)return;
            EnsureButtons();
            RefreshQuantity();
            if(toast!=null&&Time.unscaledTime>toastUntil)toast.text="";
            if(simulation!=session.Simulation)OnSimulation(session.Simulation);
            if(simulation==null)return;
            var delta=(float)(simulation.ElapsedTime-sampledTime);sampledTime=simulation.ElapsedTime;
            UpdateGateGuide(delta);
            if(!IsPlanning&&simulation.Outcome==BattleOutcome.Running&&delta>0)
                AdvanceVehicles(delta);
            if(IsPlanning)
            {
                if(simulation!=planningSimulation||simulation.Outcome!=BattleOutcome.Running){CancelPlanning();return;}
                ReadPlanningInput();
            }
            foreach(var vehicle in vehicles)
            {
                var view=views[vehicle.Id];view.Present(vehicle,Camera.main);
                if(vehicle!=planningVehicle)view.ShowPath(vehicle.Route,vehicle.NextWaypoint,vehicle.Position);
            }
        }
        private void LateUpdate(){if(IsPlanning)ApplyDimming();}
        private void AdvanceVehicles(float delta)
        {
            // Use battle time and short steps so attack cadence follows pause and playback speed.
            while(delta>.000001f)
            {
                var step=Mathf.Min(delta,.05f);delta-=step;
                foreach(var vehicle in vehicles)
                {
                    attackCooldowns.TryGetValue(vehicle.Id,out var cooldown);
                    attackCooldowns[vehicle.Id]=Mathf.Max(0,cooldown-step);
                    vehicle.Advance(step,IsPassable,probe=>
                    {
                        if(attackCooldowns[vehicle.Id]>.000001f)return;
                        if(simulation.TryRamWoodenGate(-vehicle.Id,probe,radius,gateDamage))
                            attackCooldowns[vehicle.Id]=Mathf.Max(.05f,gateAttackInterval);
                    });
                }
            }
        }
        private void Say(string message)
        {if(toast!=null){toast.text=message;toastUntil=Time.unscaledTime+3;}if(hint!=null)hint.text=message+"  |  Esc: cancel";}
        public bool SpawnOccupied()
        {
            if(session==null||simulation==null)return true;
            foreach(var unit in simulation.Units)if(unit.IsAlive&&TouchesSpawn(unit.Position,unit.CollisionRadius))return true;
            return vehicles.Any(v=>TouchesSpawn(v.Position,radius));
        }
        private bool TouchesSpawn(Vector3 point,float bodyRadius)
        {
            foreach(var cell in session.Grid.SceneCells)
            {
                if(!IsSupportSpawn(cell)||SmallCellOccupancy.PositionToCell(point).y!=cell.Occupancy.y)continue;
                var x=Mathf.Max(Mathf.Abs(point.x-cell.TopCenter.x)-.5f,0);
                var z=Mathf.Max(Mathf.Abs(point.z-cell.TopCenter.z)-.5f,0);
                if(x*x+z*z<=bodyRadius*bodyRadius)return true;
            }
            return false;
        }
        public bool TrySummon()
        {
            if(!IsUnlocked||IsPlanning||session==null||simulation==null||simulation.Outcome!=BattleOutcome.Running||vehiclePrefab==null)return false;
            if(remainingQuantity<=0){Say("No siege rams remaining.");return false;}
            if(simulation.PendingSpawnCount>0){Say("Wait for the squad to deploy.");return false;}
            if(hud!=null&&hud.TutorialOwnsPause){Say("Finish the current guide first.");return false;}
            if(SpawnOccupied()){Say("Spawn occupied. Move units away first.");return false;}
            if(!IsPassable(SpawnPosition)){Say("Spawn is blocked.");return false;}
            var vehicle = new SiegeRamMotion(nextId, SpawnPosition, moveSpeed);
            RegisterVehicle(vehicle);
            BeginPlanning(vehicle);return true;
        }

        public bool CanUsePlannedRoute(IReadOnlyList<Vector3> points) =>
            vehiclePrefab != null && IsUnlocked && new SiegeRamMotion(0, SpawnPosition, moveSpeed)
                .TrySetRoute(points, IsRoutePassable, true);

        public bool TryLaunchPlannedRoute(IReadOnlyList<Vector3> points)
        {
            if (simulation == null || simulation.Outcome != BattleOutcome.Running || simulation.IsPaused ||
                (simulation.PendingSpawnCount > 0 && !simulation.InitialDeploymentHeld) || IsPlanning || remainingQuantity <= 0 || SpawnOccupied() ||
                vehiclePrefab == null || !IsUnlocked) return false;
            var vehicle = new SiegeRamMotion(nextId, SpawnPosition, moveSpeed);
            if (!vehicle.TrySetRoute(points, IsRoutePassable, true)) return false;
            RegisterVehicle(vehicle);
            return true;
        }

        private void RegisterVehicle(SiegeRamMotion vehicle)
        {
            ClearGateGuide();
            nextId++;
            vehicles.Add(vehicle);
            var view=Instantiate(vehiclePrefab,transform);view.name="Siege Ram "+vehicle.Id;view.Initialize();view.Present(vehicle,Camera.main);views.Add(vehicle.Id,view);
            remainingQuantity--;RefreshQuantity();
            if(toast!=null)toast.text="";toastUntil=0;
        }
        public bool TryBeginReplanningAt(Vector2 screenPoint)
        {
            if(IsPlanning||session==null||simulation==null||simulation.Outcome!=BattleOutcome.Running||
                (hud!=null&&hud.TutorialOwnsPause))return false;
            var control=owner.GetComponent<BattleUnitInspection>();
            if(control!=null&&control.IsInputSuppressed)return false;
            if(EventSystem.current!=null)
            {
                var hits=new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=screenPoint},hits);
                if(hits.Count>0)return false;
            }
            var camera=Camera.main;if(camera==null)return false;
            var ray=camera.ScreenPointToRay(screenPoint);SiegeRamMotion selected=null;var nearest=float.PositiveInfinity;
            foreach(var vehicle in vehicles)
            {
                var body=views[vehicle.Id].Body;
                if(body==null||!body.enabled||!body.gameObject.activeInHierarchy)continue;
                var plane=new Plane(body.transform.forward,body.transform.position);
                if(!plane.Raycast(ray,out var distance)||distance>=nearest)continue;
                var local=body.transform.InverseTransformPoint(ray.GetPoint(distance));var bounds=body.sprite.bounds;
                if(local.x<bounds.min.x||local.x>bounds.max.x||local.y<bounds.min.y||local.y>bounds.max.y)continue;
                selected=vehicle;nearest=distance;
            }
            if(selected==null)return false;
            BeginPlanning(selected,false);openingPointerFrame=Time.frameCount;return true;
        }
        private void BeginPlanning(SiegeRamMotion vehicle,bool newSummon=true)
        {
            ClearGateGuide();
            inspection=owner.GetComponent<BattleUnitInspection>();inspection?.CloseImmediately();
            SquadRouteInput.ForSession(session)?.Cancel();
            planningSimulation=simulation;wasPaused=simulation.IsPaused;oldTimeScale=Time.timeScale;
            oldSuppressed=inspection!=null&&inspection.IsInputSuppressed;
            simulation.SetPaused(true);Time.timeScale=0;if(inspection!=null)inspection.IsInputSuppressed=true;
            planningVehicle=vehicle;planningNewSummon=newSummon;dragging=false;validCursor=false;draft.Clear();
            var protectedView=views[vehicle.Id];
            protectedView.ShowPath(null,0,vehicle.Position);
            foreach(var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if(renderer==protectedView.Body||renderer==protectedView.RouteLine)continue;
                var cell=renderer.GetComponentInParent<BattleCell>();if(cell!=null&&IsSupportSpawn(cell))continue;
                if(renderer is SpriteRenderer sprite){spriteColors[sprite]=sprite.color;continue;}
                var saved=new MaterialPropertyBlock();renderer.GetPropertyBlock(saved);var dim=new MaterialPropertyBlock();renderer.GetPropertyBlock(dim);
                var color=saved.HasColor("_BaseColor")?saved.GetColor("_BaseColor"):
                    saved.HasColor("_Color")?saved.GetColor("_Color"):
                    renderer.sharedMaterial!=null&&renderer.sharedMaterial.HasProperty("_BaseColor")?renderer.sharedMaterial.GetColor("_BaseColor"):Color.white;
                color.r*=.18f;color.g*=.18f;color.b*=.18f;dim.SetColor("_BaseColor",color);dim.SetColor("_Color",color);meshColors[renderer]=(saved,dim);
            }
            foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                var group=canvas.GetComponent<CanvasGroup>();if(group==null)group=canvas.gameObject.AddComponent<CanvasGroup>();
                uiGroups.Add((group,group.alpha,group.interactable,group.blocksRaycasts));group.alpha*=.18f;group.interactable=false;group.blocksRaycasts=false;
            }
            overlay=new GameObject("Siege Ram Route Planning",typeof(RectTransform));LegacyUiKit.CreateCanvas(overlay,2100,new Vector2(1280,720),.5f);
            var blocker=LegacyUiKit.CreateImage(overlay.transform,"Block other interactions",Vector2.zero,Vector2.zero,Color.clear);LegacyUiKit.Stretch(blocker.rectTransform);blocker.raycastTarget=true;
            var instructions=session.DisplayedLevelNumber==3
                ? "Drag the siege ram to the right with the mouse to draw a path and break through the wooden gate."
                : "Drag to the next 3x3 center, then in 3-cell steps. Release to move.  |  Esc: cancel";
            hint=LegacyUiKit.CreateText(overlay.transform,"Instructions",instructions,new Vector2(0,310),new Vector2(1220,65),19,LegacyUiKit.HighlightColor,FontStyles.Bold);
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(null);
            ApplyDimming();
        }
        private void ApplyDimming()
        {
            foreach(var pair in spriteColors)if(pair.Key!=null){var c=pair.Value;c.r*=.18f;c.g*=.18f;c.b*=.18f;pair.Key.color=c;}
            foreach(var pair in meshColors)if(pair.Key!=null)pair.Key.SetPropertyBlock(pair.Value.dim);
        }
        // Routes may target the far side of a gate; movement still waits for its destruction.
        public bool IsRoutePassable(Vector3 point)=>IsPassable(point,true);
        public bool IsPassable(Vector3 point)=>IsPassable(point,false);
        private bool IsPassable(Vector3 point,bool allowWoodenGates)
        {
            if(session==null||simulation==null||!float.IsFinite(point.x)||!float.IsFinite(point.z))return false;
            if(Mathf.Abs(point.y-SpawnPosition.y)>.01f)return false;
            var key=SmallCellOccupancy.PositionToCell(point);
            for(var z=Mathf.FloorToInt(point.z-radius+.5f);z<=Mathf.FloorToInt(point.z+radius+.5f);z++)
            for(var x=Mathf.FloorToInt(point.x-radius+.5f);x<=Mathf.FloorToInt(point.x+radius+.5f);x++)
            {
                var dx=Mathf.Max(Mathf.Abs(point.x-x)-.5f,0);var dz=Mathf.Max(Mathf.Abs(point.z-z)-.5f,0);
                if(dx*dx+dz*dz>radius*radius)continue;
                var cellKey=new Vector3Int(x,key.y,z);
                if(!session.Grid.TryGetCell(cellKey,out var cell)||!session.Grid.HasClearTop(cell)||cell.Kind==CellKind.Obstacle||cell.Kind==CellKind.TowerPlatform)return false;
                foreach(var id in simulation.GetCellOccupancy(cellKey).TowerIds)
                {
                    var tower=simulation.Towers.FirstOrDefault(t=>t.Id==id);
                    if(!allowWoodenGates||tower==null||!tower.Definition.IsWoodenGate)return false;
                }
            }
            return true;
        }
        public bool BeginDrag(Vector3 point)
        {
            if(!IsPlanning)return false;var delta=point-planningVehicle.Position;delta.y=0;
            if(delta.sqrMagnitude>1.5f*1.5f)return false;
            dragging=true;validCursor=true;draft.Clear();draft.Add(planningVehicle.Position);return true;
        }
        public bool ExtendDrag(Vector3 cursor)
        {
            if(!IsPlanning||!dragging)return false;
            var start = draft[0];
            if (session.DisplayedLevelNumber == 3)
                cursor = new Vector3(Mathf.Max(cursor.x, start.x), start.y, start.z);
            validCursor = SiegeRamMotion.ExtendRouteDraft(draft, cursor, IsRoutePassable);
            if (!validCursor) Say("Blocked. Draw around stone walls, towers and obstacles.");
            views[planningVehicle.Id].ShowPath(draft, 1, start);
            return validCursor;
        }

        public bool FinishDrag()
        {
            if(!IsPlanning||!dragging)return false;dragging=false;
            if(session.DisplayedLevelNumber==3)
                for(var i=1;i<draft.Count;i++)
                    if(draft[i].x<=draft[i-1].x+.001f||Mathf.Abs(draft[i].z-draft[i-1].z)>.001f)
                    {Say("Draw a straight path to the right.");return false;}
            if(!validCursor||!planningVehicle.TrySetRoute(draft,IsRoutePassable,true))
            {Say(session.DisplayedLevelNumber==3?"Draw a clear path to the right in 3-cell steps.":"Draw a clear route to the next 3x3 center, then use 3-cell steps.");return false;}
            EndPlanning();
            if(session.DisplayedLevelNumber==3)ShowGateGuide(replanReminder:true);
            return true;
        }
        private void ReadPlanningInput()
        {
            if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){CancelPlanning();return;}
            var mouse=Mouse.current;if(mouse==null)return;
            ProcessPointer(mouse.position.ReadValue(),mouse.leftButton.wasPressedThisFrame,mouse.leftButton.isPressed,mouse.leftButton.wasReleasedThisFrame);
        }
        // Screen-space pointer boundary is shared by mouse input and deterministic UI tests.
        public void ProcessPointer(Vector2 screenPoint,bool pressed,bool held,bool released)
        {
            if(!IsPlanning){if(pressed)TryBeginReplanningAt(screenPoint);return;}
            if(Time.frameCount==openingPointerFrame)return;
            var camera=Camera.main;if(!IsPlanning||camera==null)return;
            var ray=camera.ScreenPointToRay(screenPoint);var plane=new Plane(Vector3.up,planningVehicle.Position);
            if(!plane.Raycast(ray,out var distance))return;var point=ray.GetPoint(distance);
            if(pressed)
            {
                if(!BeginDrag(point)&&views[planningVehicle.Id].Body.bounds.IntersectRay(ray))BeginDrag(planningVehicle.Position);
            }
            if(dragging&&(held||released))ExtendDrag(point);
            if(dragging&&released)FinishDrag();
        }
        private void EndPlanning()
        {
            var vehicle=planningVehicle;
            planningVehicle=null;dragging=false;Time.timeScale=oldTimeScale;
            if(planningSimulation!=null&&planningSimulation.Outcome==BattleOutcome.Running)planningSimulation.SetPaused(wasPaused);
            if(inspection!=null)inspection.IsInputSuppressed=oldSuppressed;
            foreach(var pair in spriteColors)if(pair.Key!=null)pair.Key.color=pair.Value;spriteColors.Clear();
            foreach(var pair in meshColors)if(pair.Key!=null)pair.Key.SetPropertyBlock(pair.Value.saved);meshColors.Clear();
            foreach(var entry in uiGroups)if(entry.group!=null){entry.group.alpha=entry.alpha;entry.group.interactable=entry.interactable;entry.group.blocksRaycasts=entry.blocks;}uiGroups.Clear();
            if(overlay!=null){overlay.SetActive(false);Destroy(overlay);overlay=null;}hint=null;planningSimulation=null;draft.Clear();
            if(vehicle!=null&&views.TryGetValue(vehicle.Id,out var view))view.ShowPath(vehicle.Route,vehicle.NextWaypoint,vehicle.Position);
            RefreshQuantity();
        }
        public void CancelPlanning()
        {
            if(!IsPlanning)return;
            var id=planningVehicle.Id;var remove=planningNewSummon;EndPlanning();
            if(!remove)return;
            vehicles.RemoveAll(v=>v.Id==id);
            if(views.TryGetValue(id,out var view)){views.Remove(id);if(view!=null)Destroy(view.gameObject);}
            attackCooldowns.Remove(id);
            remainingQuantity++;RefreshQuantity();
        }
        private void OnDisable()
        {
            ClearGateGuide();
            CancelPlanning();if(session!=null)session.SimulationCreated-=OnSimulation;
            foreach(var view in views.Values)if(view!=null)Destroy(view.gameObject);views.Clear();vehicles.Clear();attackCooldowns.Clear();
            foreach(var button in buttons)if(button!=null)Destroy(button.gameObject);if(toast!=null)Destroy(toast.gameObject);
        }
    }
}
