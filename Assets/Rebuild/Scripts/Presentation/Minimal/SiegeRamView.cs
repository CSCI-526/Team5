using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Presentation
{
    public sealed class SiegeRamView : MonoBehaviour
    {
        [SerializeField]private SpriteRenderer body;
        [SerializeField]private Material worldMaterial;
        private Material ownedMaterial,lineMaterial;
        private LineRenderer line;
        public SpriteRenderer Body=>body;
        public LineRenderer RouteLine=>line;
        public void Initialize()
        {
            ownedMaterial=new Material(worldMaterial){name="Siege Ram World Z"};body.sharedMaterial=ownedMaterial;body.flipX=true;
            var path=new GameObject("Siege Ram Route");path.transform.SetParent(transform,false);line=path.AddComponent<LineRenderer>();
            line.useWorldSpace=true;line.widthMultiplier=.11f;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            lineMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));lineMaterial.SetColor("_BaseColor",new Color(.2f,1,.8f));
            line.sharedMaterial=lineMaterial;line.positionCount=0;
        }
        public void Present(SiegeRamMotion state,Camera camera)
        {
            transform.position=state.Position;
            if(camera!=null)
            {
                body.transform.rotation=camera.transform.rotation;
                var side=Vector3.Dot(state.Heading,camera.transform.right);
                if(Mathf.Abs(side)>.1f)body.flipX=side>0;
            }
            body.sortingOrder=WorldZSceneSorting.Order(state.Position.z);
            if(ownedMaterial!=null)ownedMaterial.SetFloat("_FWFootZ",state.Position.z);
        }
        public void ShowPath(System.Collections.Generic.IReadOnlyList<Vector3> points,int next,Vector3 position)
        {
            if(line==null)return;
            line.enabled=points!=null&&points.Count>1&&next<points.Count;
            if(!line.enabled){line.positionCount=0;return;}
            next=Mathf.Max(1,next);line.positionCount=points.Count-next+1;line.SetPosition(0,position+Vector3.up*.08f);
            for(var i=next;i<points.Count;i++)line.SetPosition(i-next+1,points[i]+Vector3.up*.08f);
        }
        private void OnDestroy()
        {if(ownedMaterial!=null)Destroy(ownedMaterial);if(lineMaterial!=null)Destroy(lineMaterial);}
    }
}
