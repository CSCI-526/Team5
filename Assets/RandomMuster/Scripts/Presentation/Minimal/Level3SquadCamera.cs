using System.Collections.Generic;
using Team5.BasicTowerTrial.Session;
using UnityEngine;

namespace Team5.BasicTowerTrial.Presentation
{
    [DisallowMultipleComponent,RequireComponent(typeof(Camera)),DefaultExecutionOrder(1100)]
    public sealed class Level3SquadCamera : MonoBehaviour
    {
        [SerializeField] private BattlePresentation owner;
        [SerializeField,Range(1.1f,3)] private float zoomFactor=1.6f;
        [SerializeField,Min(.1f)] private float followSharpness=6;
        private Camera view;
        private Vector3 overviewPosition,overviewCenter,focusPoint;
        private Quaternion overviewRotation;
        private float overviewFov,overviewSize,groundHeight;
        private bool captured;
        private readonly List<Vector2> overview=new List<Vector2>(4);
        private readonly List<Vector2> footprint=new List<Vector2>(4);
        private List<Vector2> clipped=new List<Vector2>(12),scratch=new List<Vector2>(12);
        public IReadOnlyList<Vector2> OverviewGroundCorners=>overview.AsReadOnly();
        public Vector3 OverviewPosition=>overviewPosition;
        public float OverviewFieldOfView=>overviewFov;
        private static readonly Vector2[] ViewportCorners={Vector2.zero,Vector2.right,Vector2.one,Vector2.up};

        private void Start()
        {
            // Scene opt-in is controlled by attaching this component and binding its owner.
            if(owner==null||BattleSceneFlow.IsPreviewScene(gameObject.scene))return;
            view=GetComponent<Camera>();overviewPosition=transform.position;overviewRotation=transform.rotation;
            overviewFov=view.fieldOfView;overviewSize=view.orthographicSize;
            groundHeight=owner.Session.Route.Cells[0].TopCenter.y;
            if(!ReadFootprint(overviewPosition,overview))return;
            if(SignedArea(overview)<0)overview.Reverse();
            var plane=new Plane(Vector3.up,new Vector3(0,groundHeight,0));
            var ray=view.ViewportPointToRay(new Vector3(.5f,.5f,0));
            if(!plane.Raycast(ray,out var distance))return;
            overviewCenter=ray.GetPoint(distance);focusPoint=owner.Session.Route.Cells[0].TopCenter;captured=true;
        }
        private void Update()
        {
            if(!captured||owner==null)return;
            // Planning needs the entire original map and a stable camera for pointer projection.
            if(owner.GetComponent<SiegeRamFleet>()?.IsPlanning==true){RestoreOverview();Constrain(overviewPosition);return;}
            var inspection=owner.GetComponent<BattleUnitInspection>();
            if(inspection!=null&&inspection.OwnsCamera)return;
            var simulation=owner.Session.Simulation;var total=Vector3.zero;var count=0;
            if(simulation!=null)
                foreach(var unit in simulation.Units)if(unit.IsAlive){total+=unit.AnchorPosition;count++;}
            if(count>0)focusPoint=total/count;
            var alpha=1-Mathf.Exp(-followSharpness*Time.unscaledDeltaTime);
            transform.rotation=overviewRotation;
            if(view.orthographic)view.orthographicSize=Mathf.Lerp(view.orthographicSize,overviewSize/zoomFactor,alpha);
            else
            {
                var zoomFov=2*Mathf.Atan(Mathf.Tan(overviewFov*Mathf.Deg2Rad*.5f)/zoomFactor)*Mathf.Rad2Deg;
                view.fieldOfView=Mathf.Lerp(view.fieldOfView,zoomFov,alpha);
            }
            var offset=focusPoint-overviewCenter;offset.y=0;
            var target=overviewPosition+offset;
            Constrain(Vector3.Lerp(transform.position,target,alpha));
        }
        private void LateUpdate()
        {
            // Normal follow runs before health bars project their positions in LateUpdate.
            // Inspection owns its own LateUpdate; apply only the boundary after it finishes.
            if(captured&&owner!=null&&owner.GetComponent<BattleUnitInspection>()?.OwnsCamera==true)
                Constrain(transform.position);
        }
        private void Constrain(Vector3 desired)
        {
            // Erode the original ground quadrilateral by the current frustum footprint.
            // Clamping only the camera centre would still expose space beyond map edges.
            for(var attempt=0;attempt<24;attempt++)
            {
                if(TryClamp(desired,out var position)){transform.position=position;return;}
                // Also handle a Game View aspect-ratio change or an inspection zoom-out.
                if(view.orthographic)view.orthographicSize*=.8f;
                else view.fieldOfView=Mathf.Max(.5f,view.fieldOfView*.8f);
            }
            RestoreOverview();
        }
        private bool TryClamp(Vector3 desired,out Vector3 result)
        {
            result=desired;
            var reference=new Vector3(overviewPosition.x,desired.y,overviewPosition.z);
            if(!ReadFootprint(reference,footprint))return false;
            var span=100f;
            foreach(var p in overview)span=Mathf.Max(span,p.magnitude*4);
            foreach(var p in footprint)span=Mathf.Max(span,p.magnitude*4);
            clipped.Clear();clipped.Add(new Vector2(-span,-span));clipped.Add(new Vector2(span,-span));
            clipped.Add(new Vector2(span,span));clipped.Add(new Vector2(-span,span));
            for(var edge=0;edge<overview.Count;edge++)
            {
                var a=overview[edge];var delta=overview[(edge+1)%overview.Count]-a;
                var inward=new Vector2(-delta.y,delta.x).normalized;var minimum=float.PositiveInfinity;
                foreach(var corner in footprint)minimum=Mathf.Min(minimum,Vector2.Dot(inward,corner));
                Clip(inward,Vector2.Dot(inward,a)-minimum);
                if(clipped.Count==0)return false;
            }
            var offset=new Vector2(desired.x-reference.x,desired.z-reference.z);
            if(!Inside(clipped,offset))
            {
                var nearest=Vector2.zero;var best=float.PositiveInfinity;
                for(var i=0;i<clipped.Count;i++)
                {
                    var a=clipped[i];var edge=clipped[(i+1)%clipped.Count]-a;
                    var t=edge.sqrMagnitude>0?Mathf.Clamp01(Vector2.Dot(offset-a,edge)/edge.sqrMagnitude):0;
                    var point=a+edge*t;var distance=(point-offset).sqrMagnitude;
                    if(distance<best){best=distance;nearest=point;}
                }
                offset=nearest;
            }
            result=reference+new Vector3(offset.x,0,offset.y);return true;
        }
        private void Clip(Vector2 normal,float minimum)
        {
            scratch.Clear();var previous=clipped[clipped.Count-1];var before=Vector2.Dot(normal,previous)-minimum;
            foreach(var current in clipped)
            {
                var after=Vector2.Dot(normal,current)-minimum;
                if((before>=0)!=(after>=0))scratch.Add(Vector2.LerpUnclamped(previous,current,before/(before-after)));
                if(after>=0)scratch.Add(current);
                previous=current;before=after;
            }
            var swap=clipped;clipped=scratch;scratch=swap;
        }
        private bool ReadFootprint(Vector3 position,List<Vector2> output)
        {
            var saved=transform.position;transform.position=position;output.Clear();
            try
            {
                var plane=new Plane(Vector3.up,new Vector3(0,groundHeight,0));
                foreach(var corner in ViewportCorners)
                {
                    var ray=view.ViewportPointToRay(new Vector3(corner.x,corner.y,0));
                    if(!plane.Raycast(ray,out var distance))return false;
                    var point=ray.GetPoint(distance);output.Add(new Vector2(point.x,point.z));
                }
                return true;
            }
            finally{transform.position=saved;}
        }
        private static float SignedArea(List<Vector2> polygon)
        {var area=0f;for(var i=0;i<polygon.Count;i++){var a=polygon[i];var b=polygon[(i+1)%polygon.Count];area+=a.x*b.y-a.y*b.x;}return area*.5f;}
        private static bool Inside(List<Vector2> polygon,Vector2 point)
        {
            for(var i=0;i<polygon.Count;i++)
            {var a=polygon[i];var b=polygon[(i+1)%polygon.Count];if((b.x-a.x)*(point.y-a.y)-(b.y-a.y)*(point.x-a.x)<-.00001f)return false;}
            return true;
        }
        private void RestoreOverview()
        {transform.SetPositionAndRotation(overviewPosition,overviewRotation);view.fieldOfView=overviewFov;view.orthographicSize=overviewSize;}
        private void OnDisable(){if(captured&&view!=null)RestoreOverview();}
    }
}
