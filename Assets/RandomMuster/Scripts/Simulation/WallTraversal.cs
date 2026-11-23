using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Team5.BasicTowerTrial.Simulation
{
    public sealed class WallSurfaceDefinition
    {
        public int TowerId { get; }
        public float Height { get; }
        public ReadOnlyCollection<Vector3Int> Cells { get; }
        public WallSurfaceDefinition(int towerId,float height,IEnumerable<Vector3Int> cells)
        { TowerId=towerId;Height=height;Cells=new List<Vector3Int>(cells).AsReadOnly(); }
    }
    public sealed class LadderDefinition
    {
        public int TowerId { get; }
        public Vector3 Bottom { get; }
        public Vector3 Crest { get; }
        public Vector3 Top { get; }
        public LadderDefinition(int towerId,Vector3 bottom,Vector3 crest,Vector3 top)
        {
            if(!BattleNumbers.Finite(bottom)||!BattleNumbers.Finite(crest)||!BattleNumbers.Finite(top)||crest.y<=bottom.y+.1f||Mathf.Abs(top.y-crest.y)>.001f)
                throw new ArgumentException("A ladder needs a vertical climb followed by a level landing.");
            TowerId=towerId;Bottom=bottom;Crest=crest;Top=top;
        }
        private static float On(Vector3 p,Vector3 a,Vector3 b)
        {var d=b-a;var t=Vector3.Dot(p-a,d)/d.sqrMagnitude;return t>=-.0001f&&t<=1.0001f&&(a+d*t-p).sqrMagnitude<.000001f?Mathf.Clamp01(t):-1;}
        public bool ContainsSegment(Vector3 a,Vector3 b)=>
            On(a,Bottom,Crest)>=0&&On(b,Bottom,Crest)>=0||On(a,Crest,Top)>=0&&On(b,Crest,Top)>=0;
        public List<Vector3> Between(Vector3 a,Vector3 b)
        {
            float Progress(Vector3 p){var t=On(p,Bottom,Crest);if(t>=0)return t;var u=On(p,Crest,Top);return u<0?-1:1+u;}
            var x=Progress(a);var y=Progress(b);if(x<0||y<0)return null;
            var path=new List<Vector3>{a};if(x<1&&y>1||y<1&&x>1)path.Add(Crest);path.Add(b);return path;
        }
    }
}
