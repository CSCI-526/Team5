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
