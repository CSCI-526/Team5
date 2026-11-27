using Team5.Rebuild.Session;
using Team5.Rebuild.Presentation.LegacyUI;
using Team5.Rebuild.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Team5.Rebuild.Presentation
{
    // The camera rides a stable orthographic dolly. No rotation or per-unit jitter while playing.
    // Hold Tab for the whole field; tutorial, inspection and preparation retain their own framing.
    [DisallowMultipleComponent, RequireComponent(typeof(Camera)), DefaultExecutionOrder(-100)]
    public sealed class StageOverviewCamera : MonoBehaviour
    {
        [SerializeField] private Bounds battlefield;
        [SerializeField] private BattlePresentation owner;
        private Camera view;
        private float fittedAspect, overviewSize;
        private Vector3 overviewPosition, velocity;
        private Quaternion rotation;
        private float zoomVelocity;
        private BattleSimulation previousSimulation;
        private LevelOneTutorial legacyTutorial;
        private ClickMoveTutorial clickTutorial;
        public const float Pitch = 50f;
        public void Configure(Bounds bounds, BattlePresentation presentation)
        {
            battlefield=bounds; owner=presentation; view=GetComponent<Camera>(); CaptureOverview();
        }
        private void Start()
        {
            if(BattleSceneFlow.IsPreviewScene(gameObject.scene)) { enabled=false; return; }
            view=GetComponent<Camera>(); CaptureOverview();
            if(owner!=null && owner.Session!=null)
            {
                legacyTutorial=owner.Session.GetComponent<LevelOneTutorial>();
                clickTutorial=owner.Session.GetComponent<ClickMoveTutorial>();
            }
        }
        private void CaptureOverview()
        {
            Fit(view,battlefield,view.aspect); fittedAspect=view.aspect;
            overviewSize=view.orthographicSize; overviewPosition=transform.position; rotation=transform.rotation;
            velocity=Vector3.zero; zoomVelocity=0;
        }
        private void Update()
        {
            if(view==null || owner==null || owner.Session==null) return;
            if(owner.GetComponent<BattleUnitInspection>()?.OwnsCamera==true ||
               (legacyTutorial!=null && legacyTutorial.isActiveAndEnabled && legacyTutorial.IsActive))
            { velocity=Vector3.zero; zoomVelocity=0; return; }
            if(Mathf.Abs(view.aspect-fittedAspect)>.001f) CaptureOverview();
            var sim=owner.Session.Simulation;
            if(sim!=previousSimulation) { previousSimulation=sim; CaptureOverview(); }
            var overview=Keyboard.current!=null && Keyboard.current.tabKey.isPressed;
            overview|=sim==null || sim.AliveCount==0 || sim.Outcome!=BattleOutcome.Running || !sim.ContinuousMovementEnabled ||
                owner.GetComponent<SiegeRamFleet>()?.IsPlanning==true || (clickTutorial!=null && clickTutorial.IsActive);
            // At 1x-3x playback the camera remains a real-time, damped dolly; pause freezes tracking.
            if(!overview && sim.IsPaused) return;
            var aspect=Mathf.Max(.25f,view.aspect);
            var localSize=LocalSize(overviewSize,aspect);
            var targetSize=overview?overviewSize:localSize;
            var target=overviewPosition;
            if(!overview)
            {
                var focus=sim.SquadReferencePosition;
                var ahead=sim.SquadDestination-focus; ahead.y=0;
                if(sim.IsSquadMoving && ahead.sqrMagnitude>.01f) focus+=Vector3.ClampMagnitude(ahead,1.4f);
                var worldOffset=focus-battlefield.center; worldOffset.y=0;
                var cameraOffset=Quaternion.Inverse(rotation)*worldOffset;
                var maxX=Mathf.Max(0,(overviewSize-localSize)*aspect*.88f);
                var maxY=Mathf.Max(0,(overviewSize-localSize)*.80f);
                cameraOffset.x=Mathf.Clamp(cameraOffset.x,-maxX,maxX);
                cameraOffset.y=Mathf.Clamp(cameraOffset.y,-maxY,maxY);
                // A small central dead zone eliminates back-and-forth corrections while dodging.
                var current=Quaternion.Inverse(rotation)*(transform.position-overviewPosition);
                cameraOffset.x=DeadZone(current.x,cameraOffset.x,.42f);
                cameraOffset.y=DeadZone(current.y,cameraOffset.y,.32f);
                cameraOffset.x=Mathf.Clamp(cameraOffset.x,-maxX,maxX); cameraOffset.y=Mathf.Clamp(cameraOffset.y,-maxY,maxY);
                // Preserve a horizontal ground-plane dolly rather than moving up/down the world.
                target+=new Vector3(cameraOffset.x,0,cameraOffset.y/Mathf.Sin(Pitch*Mathf.Deg2Rad));
            }
            var dt=Mathf.Min(Time.unscaledDeltaTime,.08f);
            transform.rotation=rotation;
            transform.position=Vector3.SmoothDamp(transform.position,target,ref velocity,.36f,35f,dt);
            view.orthographicSize=Mathf.SmoothDamp(view.orthographicSize,targetSize,ref zoomVelocity,.30f,35f,dt);
        }
        private static float DeadZone(float current,float target,float margin)
        { return Mathf.Clamp(current,target-margin,target+margin); }
        public static float LocalSize(float overview,float aspect)
        {
            // Show roughly 25-28 world units horizontally at 16:9, with the next encounter in view.
            return Mathf.Min(overview,Mathf.Max(7.8f,12.5f/Mathf.Max(.25f,aspect)));
        }
        public static void Fit(Camera camera,Bounds bounds,float aspect)
        {
            var rotation=Quaternion.Euler(Pitch,0,0); var inverse=Quaternion.Inverse(rotation);
            float halfX=0,halfY=0;
            for(int x=-1;x<=1;x+=2) for(int y=-1;y<=1;y+=2) for(int z=-1;z<=1;z+=2)
            {
                var p=inverse*Vector3.Scale(bounds.extents,new Vector3(x,y,z));
                halfX=Mathf.Max(halfX,Mathf.Abs(p.x)); halfY=Mathf.Max(halfY,Mathf.Abs(p.y));
            }
            aspect=Mathf.Max(.25f,aspect); camera.orthographic=true;
            camera.orthographicSize=Mathf.Max(halfY/.80f,halfX/(.88f*aspect),1f);
            var distance=Mathf.Max(30f,bounds.extents.magnitude+12f);
            camera.transform.SetPositionAndRotation(bounds.center-rotation*Vector3.forward*distance,rotation);
            camera.nearClipPlane=.1f; camera.farClipPlane=Mathf.Max(200f,distance+bounds.extents.magnitude+30f);
        }
    }
}
