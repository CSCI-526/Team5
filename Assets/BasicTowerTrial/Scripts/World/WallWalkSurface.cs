using System.Collections.Generic;
using Team5.BasicTowerTrial.Simulation;
using UnityEngine;

namespace Team5.BasicTowerTrial.World
{
    [DisallowMultipleComponent,RequireComponent(typeof(BattleTower))]
    public sealed class WallWalkSurface : MonoBehaviour
    {
        [SerializeField,Min(.5f)] private float topHeight=1.14f;
        [SerializeField] private bool hasLadder;
        [SerializeField] private bool ladderOnPositiveSide;
        public bool HasLadder=>hasLadder;
        public float TopHeight=>transform.position.y+topHeight;
        private BattleTower Wall=>GetComponent<BattleTower>();
        public bool Available=>Wall.RuntimeState==null||Wall.RuntimeState.IsAlive;
        public Vector3 Top=>new Vector3(transform.position.x,TopHeight,transform.position.z);
        public Vector3 Outward=> (Wall.Footprint.x==3?Vector3.forward:Vector3.right)*(ladderOnPositiveSide?1:-1);
        public Vector3 Bottom=>transform.position+Outward*.85f;
        public Vector3 Crest=>Bottom+Vector3.up*topHeight;
        public WallSurfaceDefinition CaptureSurface(int towerId)
        {
            var cells=new List<Vector3Int>();var c=SmallCellOccupancy.PositionToCell(Top);var size=Wall.Footprint;
            for(var x=-size.x/2;x<=size.x/2;x++)for(var z=-size.y/2;z<=size.y/2;z++)cells.Add(c+new Vector3Int(x,0,z));
            return new WallSurfaceDefinition(towerId,TopHeight,cells);
        }
        public LadderDefinition CaptureLadder(int towerId)=>hasLadder?new LadderDefinition(towerId,Bottom,Crest,Top):null;
        public bool TryHit(Ray ray,out float distance,out Vector3 point)
        {
            distance=0;point=default;if(!Available)return false;
            if(!new Plane(Vector3.up,Top).Raycast(ray,out distance))return false;
            point=ray.GetPoint(distance);var size=Wall.Footprint;
            return Mathf.Abs(point.x-Top.x)<=size.x*.5f&&Mathf.Abs(point.z-Top.z)<=size.y*.5f;
        }
    }
}
