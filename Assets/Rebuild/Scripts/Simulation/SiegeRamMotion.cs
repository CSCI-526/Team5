using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Team5.Rebuild.Simulation
{
    // Independent vehicle motion. Never rewrites a soldier's MemberId, SlotId or squad route.
    public sealed class SiegeRamMotion
    {
        public const float RouteStep=3;
        private readonly List<Vector3> route=new List<Vector3>();
        public ReadOnlyCollection<Vector3> Route {get;}
        public int Id {get;}
        public Vector3 Position {get;private set;}
        public Vector3 Heading {get;private set;}=Vector3.right;
        public int NextWaypoint {get;private set;}
        public float Speed {get;}
        public bool IsMoving=>NextWaypoint<route.Count;
        public SiegeRamMotion(int id,Vector3 position,float speed)
        {
            if(!float.IsFinite(speed)||speed<=0)throw new ArgumentOutOfRangeException(nameof(speed));
            Id=id;Position=position;Speed=speed;Route=route.AsReadOnly();
        }
        // Small-cell centers are integers; blocks 0..2, 3..5, ... have centers 1, 4, ... .
        public static Vector3 NextBlockCenter(Vector3 start,Vector3 direction)
        {
            var center=new Vector3(Mathf.Floor((start.x+.5f)/RouteStep)*RouteStep+1,start.y,
                Mathf.Floor((start.z+.5f)/RouteStep)*RouteStep+1);
            return center+RouteStep*(Mathf.Abs(direction.x)>=Mathf.Abs(direction.z)
                ?Vector3.right*Mathf.Sign(direction.x):Vector3.forward*Mathf.Sign(direction.z));
        }
        public static bool ExtendRouteDraft(List<Vector3> draft, Vector3 cursor, Func<Vector3, bool> passable)
        {
            float Snap(float n)=>Mathf.Sign(n)*Mathf.Floor((Mathf.Abs(n)+.001f)/SiegeRamMotion.RouteStep)*SiegeRamMotion.RouteStep;
            var start=draft[0];
            var fromStart=cursor-start;fromStart.y=0;
            // Returning to the starting block erases the alignment connector as well.
            if(draft.Count>1&&Mathf.Floor((cursor.x+.5f)/3)==Mathf.Floor((start.x+.5f)/3)&&
                Mathf.Floor((cursor.z+.5f)/3)==Mathf.Floor((start.z+.5f)/3))draft.RemoveRange(1,draft.Count-1);
            if(draft.Count==1)
            {
                if(fromStart.sqrMagnitude<.000001f)return true;
                var first=SiegeRamMotion.NextBlockCenter(start,fromStart);
                var alongX=Mathf.Abs(fromStart.x)>=Mathf.Abs(fromStart.z);
                var reached=alongX?Mathf.Abs(fromStart.x)+.001f>=Mathf.Abs(first.x-start.x):Mathf.Abs(fromStart.z)+.001f>=Mathf.Abs(first.z-start.z);
                if(!reached)return true;
                var samples=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(start,first)*5));
                for(var sample=1;sample<=samples;sample++)
                    if(!passable(Vector3.Lerp(start,first,(float)sample/samples))) return false;
                draft.Add(first);
            }
            var anchor=draft[1];
            var target=anchor+new Vector3(Snap(cursor.x-anchor.x),0,Snap(cursor.z-anchor.z));
            for(var i=0;i<512;i++)
            {
                var previous=draft[draft.Count-1];var delta=target-previous;if(delta.sqrMagnitude<.0001f)break;
                var next=previous+SiegeRamMotion.RouteStep*(Mathf.Abs(delta.x)>=Mathf.Abs(delta.z)?Vector3.right*Mathf.Sign(delta.x):Vector3.forward*Mathf.Sign(delta.z));
                if(draft.Count>2&&(next-draft[draft.Count-2]).sqrMagnitude<.0001f){draft.RemoveAt(draft.Count-1);continue;}
                var valid=true;
                for(var sample=1;sample<=15;sample++)if(!passable(Vector3.Lerp(previous,next,sample/15f))){valid=false;break;}
                if(!valid) return false;
                draft.Add(next);
            }
            return (target - draft[draft.Count - 1]).sqrMagnitude <= .0001f;
        }

        public bool TrySetRoute(IReadOnlyList<Vector3> points,Func<Vector3,bool> passable,bool alignFirstToBlockCenter=false)
        {
            if(points==null||points.Count<2||passable==null||(points[0]-Position).sqrMagnitude>.0001f)return false;
            for(var i=0;i<points.Count;i++)
            {
                var p=points[i];
                if(!float.IsFinite(p.x)||!float.IsFinite(p.y)||!float.IsFinite(p.z)||Mathf.Abs(p.y-Position.y)>.001f)return false;
                if(i==0){if(!passable(p))return false;continue;}
                var delta=p-points[i-1];var length=Mathf.Abs(delta.x)+Mathf.Abs(delta.z);
                if(i==1&&alignFirstToBlockCenter)
                {
                    var aligned=false;
                    foreach(var direction in new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back})
                        if((p-NextBlockCenter(Position,direction)).sqrMagnitude<.000001f){aligned=true;break;}
                    if(!aligned)return false;
                }
                else if((Mathf.Abs(delta.x)>.001f&&Mathf.Abs(delta.z)>.001f)||length<RouteStep-.001f||Mathf.Abs(length/RouteStep-Mathf.Round(length/RouteStep))>.001f)return false;
                for(var j=1;j<=Mathf.CeilToInt(length*5);j++)
                    if(!passable(Vector3.Lerp(points[i-1],p,(float)j/Mathf.CeilToInt(length*5))))return false;
            }
            route.Clear();for(var i=0;i<points.Count;i++)route.Add(points[i]);NextWaypoint=1;return true;
        }
        public void Advance(float deltaTime,Func<Vector3,bool> passable,Action<Vector3> onBlocked=null)
        {
            if(!float.IsFinite(deltaTime)||deltaTime<=0||passable==null)return;
            var remaining=deltaTime*Speed;
            while(IsMoving&&remaining>.000001f)
            {
                var delta=route[NextWaypoint]-Position;var distance=delta.magnitude;
                if(distance<.00001f){Position=route[NextWaypoint++];continue;}
                var step=Mathf.Min(remaining,Mathf.Min(distance,.15f));var next=Position+delta/distance*step;
                if(!passable(next)){Heading=delta/distance;onBlocked?.Invoke(next);return;}
                Heading=delta/distance;Position=next;remaining-=step;
                if(step>=distance-.00001f){Position=route[NextWaypoint++];}
            }
        }
    }
}
