using System.Linq;
using Team5.Rebuild.Presentation.LegacyUI;
using Team5.Rebuild.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Team5.Rebuild.Presentation
{
    // Reuses the project's procedural toon meshes/materials; no missing texture or prefab dependencies.
    internal sealed class TacticalTrapVisual
    {
        private readonly TacticalTrapDefinition d;
        private readonly MinionMaterials art = new MinionMaterials();
        private readonly Transform root, moving, field;
        private readonly LineRenderer boundary, timer;
        private readonly Material boundaryInk, timerInk;
        private readonly Color hue;
        private readonly Vector3 center;
        private TacticalTrapState state;
        private BattleSimulation bound;
        private readonly Transform[] flames = new Transform[7];

        internal TacticalTrapVisual(Transform parent, TacticalTrapDefinition definition)
        {
            d = definition; center = d.Center;
            hue = d.Kind == TacticalTrapKind.Freeze ? new Color(.16f,.86f,1f) : d.Kind == TacticalTrapKind.Meteor ? new Color(1f,.45f,.10f) : new Color(1f,.77f,.16f);
            root = new GameObject("Tactical trap art").transform; root.SetParent(parent, false); root.position = center;
            // Ground outlines sit above the terrain but contain no physics colliders.
            boundaryInk = art.Glow(hue, null); timerInk = art.Glow(Color.white, null);
            boundary = Line("Danger boundary", boundaryInk, .075f); timer = Line("Warning countdown", timerInk, .11f);
            if (d.Kind == TacticalTrapKind.Boulder) Lane(boundary);
            else Circle(boundary, d.Radius, 1f);
            var pad = Part(root, "Runic metal base", art.Hex, art.Toon(new Color(.16f,.20f,.25f),0), Vector3.up*.045f, new Vector3(.68f,.09f,.68f));
            Part(pad, "Inset", art.Hex, art.Toon(hue,.15f), Vector3.up*.6f, new Vector3(.74f,.32f,.74f));
            if (d.Kind == TacticalTrapKind.Freeze)
            {
                for (var i=0;i<5;i++)
                {
                    var a=i*Mathf.PI*2/5;
                    var gem=Part(root,"Ice shard",art.Gem,art.Toon(i%2==0?hue:new Color(.68f,.95f,1f),.12f),
                        new Vector3(Mathf.Cos(a)*.20f,.25f,Mathf.Sin(a)*.20f),new Vector3(.15f,.48f,.15f));
                    gem.rotation=Quaternion.Euler(14*Mathf.Cos(a),i*72,14*Mathf.Sin(a));
                }
            }
            else if (d.Kind == TacticalTrapKind.Meteor)
            {
                Part(root,"Ember rune",art.Gem,art.Toon(hue,.5f),Vector3.up*.22f,new Vector3(.33f,.40f,.33f));
                for (var i=0;i<4;i++)
                {
                    var a=i*Mathf.PI*.5f;
                    Part(root,"Rune tooth",art.Gem,art.Toon(new Color(1f,.80f,.22f),.2f),new Vector3(Mathf.Cos(a)*.27f,.12f,Mathf.Sin(a)*.27f),Vector3.one*.13f);
                }
            }
            else
            {
                // Two bright arrow chevrons across the road show the direction before the rock launches.
                var side = Vector3.Cross(Vector3.up,d.Direction);
                for(var i=0;i<2;i++)
                {
                    var arrow = Line("Roll direction",art.Glow(hue,null),.10f);
                    var p=center+d.Direction*(-.65f+i*1.30f)+Vector3.up*.11f;
                    arrow.positionCount=3;
                    arrow.SetPositions(new[]{p-d.Direction*.32f+side*.30f,p+d.Direction*.18f,p-d.Direction*.32f-side*.30f});
                }
            }
            moving = new GameObject("Incoming rock").transform; moving.SetParent(root,false);
            var rockSize = d.Kind == TacticalTrapKind.Boulder ? d.Radius*2 : .72f;
            var rock = Part(moving,"Rock core",art.Ball,art.Toon(new Color(.30f,.25f,.23f),0),Vector3.zero,Vector3.one*rockSize);
            for (var i=0;i<7;i++)
            {
                var a=i*2.399963f; var y=(i%3-1)*.25f;
                var offset=new Vector3(Mathf.Cos(a)*.37f,y,Mathf.Sin(a)*.37f)*rockSize;
                var chip=Part(moving,"Rock facet",art.Gem,art.Toon(d.Kind==TacticalTrapKind.Meteor?new Color(1f,.34f,.07f):new Color(.52f,.43f,.33f),.1f),offset,Vector3.one*rockSize*.43f);
                chip.rotation=Quaternion.Euler(i*33,i*79,i*19);
            }
            field = new GameObject("Burning field").transform; field.SetParent(root,false);
            // Flat scorched decal and animated flame meshes; exact damage radius is the outline.
            if (d.Kind == TacticalTrapKind.Meteor)
            {
                var scorch=Part(field,"Scorched ground",art.Flat,art.Glow(new Color(.65f,.16f,.045f,.65f),art.Scorch),Vector3.up*.065f,new Vector3(d.Radius*2,.1f,d.Radius*2));
                for (var i=0;i<flames.Length;i++)
                {
                    var a=i*2.399963f; var r=i==0?0:d.Radius*.63f;
                    flames[i]=Part(field,"Flame",art.Gem,art.Toon(i%2==0?new Color(1f,.36f,.07f):new Color(1f,.73f,.18f),.4f),new Vector3(Mathf.Cos(a)*r,.23f,Mathf.Sin(a)*r),new Vector3(.22f,.46f,.22f));
                }
            }
            moving.gameObject.SetActive(false); field.gameObject.SetActive(false); timer.enabled=false;
        }

        private static Transform Part(Transform parent,string name,Mesh mesh,Material material,Vector3 p,Vector3 scale)
        {
            var part=MinionMaterials.Show(parent,name,mesh,material).transform; part.localPosition=p; part.localScale=scale; return part;
        }
        private LineRenderer Line(string name,Material material,float width)
        {
            var go=new GameObject(name); go.transform.SetParent(root,false);
            var line=go.AddComponent<LineRenderer>(); line.useWorldSpace=true; line.sharedMaterial=material;
            line.widthMultiplier=width; line.numCornerVertices=3; line.numCapVertices=3;
            line.shadowCastingMode=ShadowCastingMode.Off; line.receiveShadows=false; return line;
        }
        private void Circle(LineRenderer line,float radius,float portion)
        {
            const int n=48; line.positionCount=n+1;
            for(var i=0;i<=n;i++)
            {
                var a=i/(float)n*Mathf.PI*2*portion;
                line.SetPosition(i,center+new Vector3(Mathf.Cos(a)*radius,.10f,Mathf.Sin(a)*radius));
            }
        }
        private void Lane(LineRenderer line)
        {
            var side=Vector3.Cross(Vector3.up,d.Direction); var lift=Vector3.up*.10f;
            const int arc = 12;
            line.positionCount = 2 * (arc + 1) + 1;
            for (var i = 0; i <= arc; i++)
            {
                var a = i * Mathf.PI / arc;
                line.SetPosition(i, d.BoulderEnd + lift + (side * Mathf.Cos(a) + d.Direction * Mathf.Sin(a)) * d.Radius);
                line.SetPosition(arc + 1 + i, d.BoulderStart + lift + (-side * Mathf.Cos(a) - d.Direction * Mathf.Sin(a)) * d.Radius);
            }
            line.SetPosition(2 * (arc + 1), line.GetPosition(0));
        }
        internal void SetVisible(bool visible) { if(root!=null) root.gameObject.SetActive(visible); }
        internal void Refresh(BattleSimulation sim)
        {
            SetVisible(true);
            if (bound!=sim) { bound=sim; state=sim?.TacticalTraps.FirstOrDefault(t=>t.Definition.Id==d.Id); }
            var phase=state?.Phase??TacticalTrapPhase.Ready;
            var progress=state?.Progress??0;
            var ended=sim!=null&&sim.Outcome!=BattleOutcome.Running;
            var warning=!ended&&phase==TacticalTrapPhase.Warning;
            var active=!ended&&phase==TacticalTrapPhase.Active;
            var recover=phase==TacticalTrapPhase.Recovering;
            MinionMaterials.SetColor(boundaryInk, warning?Color.Lerp(hue,Color.white,Mathf.PingPong(progress*6,1)*.75f):active?hue:new Color(hue.r,hue.g,hue.b,recover ? .30f : .78f));
            boundary.widthMultiplier=(warning || active) ? .12f : .075f;
            timer.enabled=warning;
            if(warning) Circle(timer,d.Kind == TacticalTrapKind.Boulder ? .48f : d.Radius+.13f,1-progress);
            moving.gameObject.SetActive(d.Kind==TacticalTrapKind.Boulder&&(warning||active)||d.Kind==TacticalTrapKind.Meteor&&warning);
            if(moving.gameObject.activeSelf)
            {
                if(d.Kind==TacticalTrapKind.Boulder)
                {
                    moving.position=(active?state.BoulderPosition:d.BoulderStart)+Vector3.up*d.Radius;
                    moving.rotation=Quaternion.AngleAxis(active?progress*TacticalTrapDefinition.BoulderHalfLength*2/d.Radius*Mathf.Rad2Deg:0,Vector3.Cross(Vector3.up,d.Direction));
                }
                else
                {
                    moving.position=center+Vector3.up*Mathf.Lerp(6f,.4f,progress*progress);
                    moving.rotation=Quaternion.Euler(progress*200,progress*160,0);
                }
            }
            field.gameObject.SetActive(active&&d.Kind==TacticalTrapKind.Meteor);
            if(field.gameObject.activeSelf)
                for(var i=0;i<flames.Length;i++)
                {
                    var flicker=.75f+.25f*Mathf.Sin((float)sim.ElapsedTime*13+i*2.1f);
                    flames[i].localScale=new Vector3(.22f,.48f*flicker,.22f);
                }
        }
        internal void Dispose() { if(root!=null) Object.Destroy(root.gameObject); art.Dispose(); }
    }
}
