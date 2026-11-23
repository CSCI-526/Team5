using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Team5.BasicTowerTrial.Presentation
{
    // Flat, code-authored silhouettes; no textures or reference-game artwork.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TavernPolygonGraphic : MaskableGraphic
    {
        [SerializeField] private Vector2[] points;
        protected override void OnEnable()
        {
            // Also repair UI objects that already existed before a script reload.
            if(!TryGetComponent<CanvasRenderer>(out _))gameObject.AddComponent<CanvasRenderer>();
            base.OnEnable();
        }
        public void SetShape(Vector2[] value)
        {
            points=(Vector2[])value.Clone();
            float area=0;
            for(var i=0;i<points.Length;i++)area+=Cross(points[i],points[(i+1)%points.Length]);
            if(area<0)System.Array.Reverse(points);
            SetVerticesDirty();
        }
        private static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();if(points==null||points.Length<3)return;
            var rect=rectTransform.rect;
            var remaining=new List<int>();
            for(var i=0;i<points.Length;i++)
            {
                var p=points[i];mesh.AddVert(new Vector3(rect.center.x+p.x*rect.width*.5f,rect.center.y+p.y*rect.height*.5f),color,Vector2.zero);remaining.Add(i);
            }
            // Ear clipping also supports the board's concave top/bottom notches.
            var safety=points.Length*points.Length;
            while(remaining.Count>2&&safety-->0)
            {
                var clipped=false;
                for(var i=0;i<remaining.Count;i++)
                {
                    var a=remaining[(i+remaining.Count-1)%remaining.Count];var b=remaining[i];var c=remaining[(i+1)%remaining.Count];
                    if(Cross(points[b]-points[a],points[c]-points[b])<=.00001f)continue;
                    var occupied=false;
                    foreach(var index in remaining)
                    {
                        if(index==a||index==b||index==c)continue;
                        var p=points[index];
                        if(Cross(points[b]-points[a],p-points[a])>=0&&Cross(points[c]-points[b],p-points[b])>=0&&Cross(points[a]-points[c],p-points[c])>=0){occupied=true;break;}
                    }
                    if(occupied)continue;
                    mesh.AddTriangle(a,b,c);remaining.RemoveAt(i);clipped=true;break;
                }
                if(!clipped)break;
            }
        }
    }
}
