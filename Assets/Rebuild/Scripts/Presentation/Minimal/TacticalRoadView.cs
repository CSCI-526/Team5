using System.Collections.Generic;
using System.Linq;
using Team5.Rebuild.Session;
using UnityEngine;
using UnityEngine.Rendering;

namespace Team5.Rebuild.Presentation
{
    // Visual-only raster of the exact navigable cells. Signed distance is interpolated,
    // not quantized into tile bands. No colliders, no changes to the simulation.
    public sealed class TacticalRoadView : MonoBehaviour
    {
        private Mesh mesh;
        private Material material;
        private Texture2D field;
        public const int PixelsPerTile = 12;
        public const float DistanceSpan = 3f;
        public static TacticalRoadView Create(BattleSession session, Transform parent, int layer)
        {
            if (session == null || !session.UsesTacticalRoad || session.Grid == null) return null;
            var host = new GameObject("Tactical road surface");
            host.transform.SetParent(parent, false); host.layer = layer;
            var view = host.AddComponent<TacticalRoadView>(); view.Build(session); return view;
        }
        private void Build(BattleSession session)
        {
            var shader = Resources.Load<Shader>("TacticalRoadField");
            if (shader == null) { Debug.LogError("Missing TacticalRoadField shader.", this); return; }
            var grid = session.Grid; grid.RebuildIndex(new List<string>());
            var cells = grid.SceneCells.Where(c => session.IsMovementSurface(c) && grid.HasClearTop(c)).ToArray();
            if (cells.Length == 0) return;
            var minX = cells.Min(c => c.Occupancy.x) - 1; var maxX = cells.Max(c => c.Occupancy.x) + 1;
            var minZ = cells.Min(c => c.Occupancy.z) - 1; var maxZ = cells.Max(c => c.Occupancy.z) + 1;
            var tilesX = maxX-minX+1; var tilesZ = maxZ-minZ+1;
            var n = PixelsPerTile; var w = tilesX*n; var h = tilesZ*n;
            var mask = new bool[w*h];
            foreach (var c in cells)
            {
                var x0 = (c.Occupancy.x-minX)*n; var z0 = (c.Occupancy.z-minZ)*n;
                for (var z=0; z<n; z++) for (var x=0; x<n; x++) mask[(z0+z)*w+x0+x] = true;
            }
            var signed = SmoothDistance(mask,w,h,n,out var raw);
            var centerline = RasterCenterline(session.Route.CaptureTopCenters(), minX-.5f,minZ-.5f,w,h,n);
            var pixels = new Color32[w*h];
            for (var i=0; i<pixels.Length; i++)
            {
                var v=(byte)Mathf.RoundToInt(Mathf.Clamp01(signed[i]/(2*DistanceSpan)+.5f)*255);
                var boundary=(byte)Mathf.RoundToInt(Mathf.Clamp01(raw[i]/(2*DistanceSpan)+.5f)*255);
                var curve=(byte)Mathf.RoundToInt(Mathf.Clamp01(centerline[i]/4f)*255);
                pixels[i]=new Color32(v,boundary,curve,255);
            }
            field = new Texture2D(w,h,TextureFormat.RGBA32,false,true) { name="Road signed distance", wrapMode=TextureWrapMode.Clamp, filterMode=FilterMode.Bilinear };
            field.SetPixels32(pixels); field.Apply(false,true);
            var y=cells[0].TopCenter.y+.018f;
            var a=minX-.5f; var b=maxX+.5f; var c0=minZ-.5f; var d=maxZ+.5f;
            mesh=new Mesh { name="Continuous road gradient" };
            mesh.vertices=new[] { new Vector3(a,y,c0),new Vector3(a,y,d),new Vector3(b,y,d),new Vector3(b,y,c0) }.Select(transform.InverseTransformPoint).ToArray();
            mesh.uv=new[] { Vector2.zero,Vector2.up,Vector2.one,Vector2.right }; mesh.triangles=new[]{0,1,2,0,2,3}; mesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            material=new Material(shader) { name="Road field (runtime)",renderQueue=2995 }; material.SetTexture("_Field",field);
            var renderer=gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial=material;
            renderer.sortingOrder=short.MinValue; renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
        }
        // Round the visual spine of the road, not the authoritative navigation route.
        // Collinear grid steps collapse into one segment; quadratic fillets cut each visual bend.
        public static Vector2[] CurvedSpine(Vector3[] route)
        {
            var turns=new List<Vector2>();
            foreach(var point in route)
            {
                var p=new Vector2(point.x,point.z);
                if(turns.Count>0 && (p-turns[turns.Count-1]).sqrMagnitude<.0001f)continue;
                if(turns.Count>1)
                {
                    var a=turns[turns.Count-1]-turns[turns.Count-2];var b=p-turns[turns.Count-1];
                    if(Vector2.Dot(a.normalized,b.normalized)>.999f){turns[turns.Count-1]=p;continue;}
                }
                turns.Add(p);
            }
            if(turns.Count<3)return turns.ToArray();
            var curve=new List<Vector2>{turns[0]};
            for(var i=1;i<turns.Count-1;i++)
            {
                var p=turns[i]; var before=turns[i-1]-p;var after=turns[i+1]-p;
                var r=Mathf.Min(2.25f,Mathf.Min(before.magnitude,after.magnitude)*.46f);
                var a=p+before.normalized*r;var b=p+after.normalized*r;
                for(var j=0;j<=8;j++)
                { var t=j/8f;var u=1-t;curve.Add(u*u*a+2*u*t*p+t*t*b); }
            }
            curve.Add(turns[turns.Count-1]);return curve.ToArray();
        }
        private static float[] RasterCenterline(Vector3[] route,float x0,float z0,int w,int h,int n)
        {
            var points=CurvedSpine(route);var d=new float[w*h];for(var i=0;i<d.Length;i++)d[i]=16;
            // Only visit pixels within 4 units of each curve segment; bounded cost on WebGL.
            for(var i=1;i<points.Length;i++)
            {
                var a=points[i-1];var b=points[i];var edge=b-a;var length=edge.sqrMagnitude;
                if(length<.000001f)continue;
                int xMin=Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x,b.x)-4-x0)*n),0,w-1);
                int xMax=Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.x,b.x)+4-x0)*n),0,w-1);
                int zMin=Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.y,b.y)-4-z0)*n),0,h-1);
                int zMax=Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.y,b.y)+4-z0)*n),0,h-1);
                for(var z=zMin;z<=zMax;z++)for(var x=xMin;x<=xMax;x++)
                {
                    var offset=new Vector2(x0+(x+.5f)/n,z0+(z+.5f)/n)-a;
                    var delta=offset-edge*Mathf.Clamp01(Vector2.Dot(offset,edge)/length);
                    var index=z*w+x;d[index]=Mathf.Min(d[index],delta.sqrMagnitude);
                }
            }
            for(var i=0;i<d.Length;i++)d[i]=Mathf.Sqrt(d[i]);return d;
        }
        // Chamfer distance transform followed by three separable box passes. A roughly 0.6-tile
        // Gaussian-like smoothing radius removes sharp corners while preserving straight edges.
        public static float[] SmoothDistance(bool[] mask,int w,int h,int pixelsPerTile) => SmoothDistance(mask,w,h,pixelsPerTile,out _);
        private static float[] SmoothDistance(bool[] mask,int w,int h,int pixelsPerTile,out float[] raw)
        {
            var inside=Distance(mask,w,h,false); var outside=Distance(mask,w,h,true);
            var values=new float[mask.Length];
            for(var i=0;i<values.Length;i++) values[i]=(mask[i]?inside[i]-.5f:.5f-outside[i])/pixelsPerTile;
            raw=(float[])values.Clone();
            var scratch=new float[values.Length]; var radius=Mathf.Max(1,Mathf.RoundToInt(pixelsPerTile*.56f));
            for(var pass=0;pass<3;pass++) { Blur(values,scratch,w,h,radius,true); Blur(scratch,values,w,h,radius,false); }
            return values;
        }
        private static float[] Distance(bool[] mask,int w,int h,bool target)
        {
            var result=new float[mask.Length]; for(var i=0;i<result.Length;i++) result[i]=mask[i]==target?0:100000f;
            const float diagonal=1.41421356f;
            for(var z=0;z<h;z++) for(var x=0;x<w;x++)
            {
                var i=z*w+x; var v=result[i];
                if(x>0) v=Mathf.Min(v,result[i-1]+1);
                if(z>0) { v=Mathf.Min(v,result[i-w]+1); if(x>0) v=Mathf.Min(v,result[i-w-1]+diagonal); if(x+1<w) v=Mathf.Min(v,result[i-w+1]+diagonal); }
                result[i]=v;
            }
            for(var z=h-1;z>=0;z--) for(var x=w-1;x>=0;x--)
            {
                var i=z*w+x; var v=result[i];
                if(x+1<w) v=Mathf.Min(v,result[i+1]+1);
                if(z+1<h) { v=Mathf.Min(v,result[i+w]+1); if(x>0) v=Mathf.Min(v,result[i+w-1]+diagonal); if(x+1<w) v=Mathf.Min(v,result[i+w+1]+diagonal); }
                result[i]=v;
            }
            return result;
        }
        private static void Blur(float[] src,float[] dst,int w,int h,int r,bool horizontal)
        {
            var lines=horizontal?h:w; var length=horizontal?w:h; var step=horizontal?1:w; var lineStep=horizontal?w:1;
            for(var line=0;line<lines;line++)
            {
                var start=line*lineStep; var sum=0f;
                for(var k=-r;k<=r;k++) sum+=src[start+Mathf.Clamp(k,0,length-1)*step];
                for(var j=0;j<length;j++)
                {
                    dst[start+j*step]=sum/(2*r+1);
                    sum+=src[start+Mathf.Clamp(j+r+1,0,length-1)*step]-src[start+Mathf.Clamp(j-r,0,length-1)*step];
                }
            }
        }
        private void OnDestroy() { if(mesh!=null) Destroy(mesh); if(material!=null) Destroy(material); if(field!=null) Destroy(field); }
    }
}
