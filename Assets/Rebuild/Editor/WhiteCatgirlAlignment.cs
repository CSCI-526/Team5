using System;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

namespace Team5.Rebuild.Editor
{
    public static class WhiteCatgirlAlignment
    {
        private sealed class Leg
        {
            public string side;public Transform hip,anchor;public int left,right;public Vector3 targetHip,targetCuff;
            public Vector3 offset;public float angle,factor,edgeAngle;
        }
        public static void FitBootCuffArtwork()
        {
            var root=PrefabUtility.LoadPrefabContents(WhiteCatgirlUnitBuilder.Visual);
            try
            {
                var animator=root.GetComponentsInChildren<Animator>(true).Single();var rig=animator.transform;
                animator.runtimeAnimatorController.animationClips.Single(c=>c.name.EndsWith("Idle")).SampleAnimation(rig.gameObject,0);
                foreach(var side in new[]{"L","R"})
                {
                    var boot=rig.Find("White Catgirl Boot "+side).GetComponent<SpriteSkin>();
                    // The native leg's opaque ankle is offset from the clipped mesh seam.
                    // These artwork-specific offsets center each cuff on the visible ankle.
                    boot.transform.localPosition+=Vector3.right*(side=="L"?-.024f:.015f);
                    var sprite=boot.GetComponent<SpriteRenderer>().sprite;var joint=boot.boneTransforms.Single();
                    sprite.SetBones(new[]{new SpriteBone{name=joint.name,parentId=-1,position=boot.transform.InverseTransformPoint(joint.position),rotation=Quaternion.identity,length=.1f}});
                    using(var binds=new NativeArray<Matrix4x4>(new[]{joint.worldToLocalMatrix*boot.transform.localToWorldMatrix},Allocator.Temp))sprite.SetBindPoses(binds);
                    EditorUtility.SetDirty(sprite);
                }
                PrefabUtility.SaveAsPrefabAsset(root,WhiteCatgirlUnitBuilder.Visual);AssetDatabase.SaveAssets();
                Debug.Log("WHITE_CATGIRL_BOOT_CUFF_ARTWORK_FITTED");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        public static void Apply()
        {
            var source=PrefabUtility.LoadPrefabContents(WhiteCatgirlUnitBuilder.Assembly);
            var root=PrefabUtility.LoadPrefabContents(WhiteCatgirlUnitBuilder.Visual);
            try
            {
                var backup="Logs/WhiteCatgirlUnit/AlignmentBackup_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(backup);
                File.Copy(WhiteCatgirlUnitBuilder.Visual,backup+"/Visual.prefab");
                var animator=root.GetComponentsInChildren<Animator>(true).Single();var rig=animator.transform;
                var clips=animator.runtimeAnimatorController.animationClips;
                var idle=clips.Single(c=>c.name.EndsWith("Idle"));idle.SampleAnimation(rig.gameObject,0);
                var native=rig.Find("Cute Girl Lower Body/FW Lower Body").GetComponentsInChildren<SpriteSkin>(true).Single(s=>s.enabled);
                var sprite=native.GetComponent<SpriteRenderer>().sprite;
                var weights=sprite.GetVertexAttribute<BoneWeight>(VertexAttribute.BlendWeight).ToArray();
                var points=Points(native,rig);
                var legs=new System.Collections.Generic.List<Leg>();
                foreach(var side in new[]{"L","R"})
                {
                    var art=source.GetComponentsInChildren<SpriteRenderer>(true).Single(s=>s.name=="F_LEG_SCREEN_"+side);
                    // The donor has already been mirrored to face screen-right.
                    var hipName=side=="L"?"F_F_R_bone_13":"F_F_L_bone_11";
                    var kneeName=side=="L"?"F_F_R_bone_14":"F_F_L_bone_12";
                    var hipIndex=Array.FindIndex(native.boneTransforms,b=>b.name==hipName);
                    var kneeIndex=Array.FindIndex(native.boneTransforms,b=>b.name==kneeName);
                    float Influence(BoneWeight w){var ids=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};var values=new[]{w.weight0,w.weight1,w.weight2,w.weight3};var sum=0f;for(var k=0;k<4;k++)if(ids[k]==hipIndex||ids[k]==kneeIndex)sum+=values[k];return sum;}
                    var indices=Enumerable.Range(0,points.Length).Where(i=>Influence(weights[i])>.5f).ToArray();
                    var bottom=indices.Min(i=>points[i].y);var seam=indices.Where(i=>points[i].y<bottom+.006f).ToArray();
                    // Native FW skin starts below its hip pivot: keep the pivot inside
                    // the authored shorts, leaving an overlap instead of a visible butt joint.
                    var leg=new Leg{side=side,hip=native.boneTransforms[hipIndex],left=seam.OrderBy(i=>points[i].x).First(),right=seam.OrderByDescending(i=>points[i].x).First(),targetHip=ArtworkPoint(art,source.transform,.99f)+Vector3.up*.01f,targetCuff=ArtworkPoint(art,source.transform,.20f)};
                    legs.Add(leg);
                }
                foreach(var leg in legs)
                {
                    var parent=leg.hip.parent;
                    var oldPosition=leg.hip.localPosition;
                    var hipPoint=parent.InverseTransformPoint(leg.hip.position);
                    var cuffPoint=parent.InverseTransformPoint(rig.TransformPoint((points[leg.left]+points[leg.right])*.5f));
                    var newHip=parent.InverseTransformPoint(rig.TransformPoint(leg.targetHip));
                    var newCuff=parent.InverseTransformPoint(rig.TransformPoint(leg.targetCuff));
                    var b=newCuff-newHip;
                    leg.offset=newHip-oldPosition;
                    // FW's local X runs along the leg. Extend length into the boot
                    // without widening the stocking as an overall scale would do.
                    var localEnd=leg.hip.InverseTransformPoint(rig.TransformPoint((points[leg.left]+points[leg.right])*.5f));
                    var across=localEnd.y*leg.hip.localScale.y;
                    var newX=Mathf.Sqrt(Mathf.Max(.000001f,b.x*b.x+b.y*b.y-across*across))/Mathf.Abs(localEnd.x);
                    var direction=leg.hip.localRotation*new Vector3(localEnd.x*newX,across,0);
                    leg.angle=Vector2.SignedAngle(new Vector2(direction.x,direction.y),new Vector2(b.x,b.y));
                    leg.factor=newX/leg.hip.localScale.x;
                    foreach(var clip in clips)
                    {
                        var path=AnimationUtility.CalculateTransformPath(leg.hip,rig);
                        Adjust(clip,path,"m_LocalPosition.x",1,leg.offset.x,oldPosition.x);
                        Adjust(clip,path,"m_LocalPosition.y",1,leg.offset.y,oldPosition.y);
                        Adjust(clip,path,"localEulerAnglesRaw.z",1,leg.angle,leg.hip.localEulerAngles.z);
                        Adjust(clip,path,"m_LocalScale.x",leg.factor,0,leg.hip.localScale.x);
                    }
                }
                idle.SampleAnimation(rig.gameObject,0);
                points=Points(native,rig);
                foreach(var leg in legs)
                {
                    leg.anchor=rig.Find("White Catgirl Ankle "+leg.side);
                    if(leg.anchor==null){leg.anchor=new GameObject("White Catgirl Ankle "+leg.side).transform;leg.anchor.SetParent(rig,false);}
                    leg.anchor.localRotation=Quaternion.identity;
                    leg.anchor.localPosition=(points[leg.left]+points[leg.right])*.5f;
                    var edge=points[leg.right]-points[leg.left];leg.edgeAngle=Mathf.Atan2(edge.y,edge.x)*Mathf.Rad2Deg;
                    var boot=rig.Find("White Catgirl Boot "+leg.side).GetComponent<SpriteSkin>();
                    var bs=boot.GetComponent<SpriteRenderer>().sprite;
                    File.Copy(AssetDatabase.GetAssetPath(bs),backup+"/Boot"+leg.side+".asset");
                    bs.SetBones(new[]{new SpriteBone{name=leg.anchor.name,parentId=-1,position=boot.transform.InverseTransformPoint(leg.anchor.position),rotation=Quaternion.identity,length=.1f}});
                    using(var bind=new NativeArray<Matrix4x4>(new[]{leg.anchor.worldToLocalMatrix*boot.transform.localToWorldMatrix},Allocator.Temp))bs.SetBindPoses(bind);
                    boot.SetRootBone(leg.anchor);boot.SetBoneTransforms(new[]{leg.anchor});EditorUtility.SetDirty(bs);
                }
                var pelvis=rig.Find("Skeleton/Pelvis");var pelvisY=pelvis.localPosition.y;
                foreach(var clip in clips)
                {
                    File.Copy(AssetDatabase.GetAssetPath(clip),backup+"/"+clip.name+".anim");
                    clip.SampleAnimation(rig.gameObject,0);
                    var baseline=legs.Average(l=>rig.InverseTransformPoint(l.hip.position).y);
                    const int samples=60;
                    var tracks=legs.Select(l=>new[]{new Keyframe[samples+1],new Keyframe[samples+1],new Keyframe[samples+1]}).ToArray();
                    var waist=new Keyframe[samples+1];
                    for(var i=0;i<=samples;i++)
                    {
                        var time=clip.length*i/samples;clip.SampleAnimation(rig.gameObject,time);var ps=Points(native,rig);
                        waist[i]=new Keyframe(time,pelvisY+legs.Average(l=>rig.InverseTransformPoint(l.hip.position).y)-baseline);
                        for(var j=0;j<legs.Count;j++)
                        {
                            var leg=legs[j];var center=(ps[leg.left]+ps[leg.right])*.5f;var edge=ps[leg.right]-ps[leg.left];
                            tracks[j][0][i]=new Keyframe(time,center.x);tracks[j][1][i]=new Keyframe(time,center.y);
                            tracks[j][2][i]=new Keyframe(time,Mathf.DeltaAngle(leg.edgeAngle,Mathf.Atan2(edge.y,edge.x)*Mathf.Rad2Deg));
                        }
                    }
                    Set(clip,"Skeleton/Pelvis","m_LocalPosition.y",waist);
                    for(var j=0;j<legs.Count;j++)
                    {
                        Set(clip,legs[j].anchor.name,"m_LocalPosition.x",tracks[j][0]);Set(clip,legs[j].anchor.name,"m_LocalPosition.y",tracks[j][1]);Set(clip,legs[j].anchor.name,"localEulerAnglesRaw.z",tracks[j][2]);
                    }
                    EditorUtility.SetDirty(clip);
                }
                idle.SampleAnimation(rig.gameObject,0);
                PrefabUtility.SaveAsPrefabAsset(root,WhiteCatgirlUnitBuilder.Visual);AssetDatabase.SaveAssets();
                var result=legs.Select(l=>new{name=l.side,hipTarget=l.targetHip,cuffTarget=l.targetCuff,actualHip=rig.InverseTransformPoint(l.hip.position),actualCuff=l.anchor.localPosition,angleOffset=l.angle,lengthFactor=l.factor}).ToArray();
                File.WriteAllText("Logs/WhiteCatgirlUnit/AlignmentApplied.txt",string.Join("\n",result.Select(l=>$"{l.name}: hip error={Vector3.Distance(l.hipTarget,l.actualHip):F6}; cuff error={Vector2.Distance(l.cuffTarget,l.actualCuff):F6}; angle={l.angleOffset:F3}; length={l.lengthFactor:F4}")));
                Debug.Log("WHITE_CATGIRL_PER_LEG_ALIGNMENT_APPLIED");
            }
            finally{PrefabUtility.UnloadPrefabContents(source);PrefabUtility.UnloadPrefabContents(root);}
        }
        private static Vector3 ArtworkPoint(SpriteRenderer art,Transform origin,float height)
        {
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(art.sprite.texture)));
                var rect=art.sprite.rect;var pixels=texture.GetPixels32();
                var low=texture.height;var high=-1;
                for(var py=(int)rect.y;py<rect.yMax;py++)for(var px=(int)rect.x;px<rect.xMax;px++)if(pixels[py*texture.width+px].a>128){low=Mathf.Min(low,py);high=Mathf.Max(high,py);}
                if(high<low)throw new InvalidOperationException("Empty leg artwork.");
                var y=Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(low,high,height)),0,texture.height-1);
                var sum=0f;var count=0;
                for(var x=(int)rect.x;x<rect.xMax;x++)if(pixels[y*texture.width+x].a>128){sum+=x-rect.x+.5f;count++;}
                if(count==0)throw new InvalidOperationException("No leg silhouette at requested alignment row.");
                var point=new Vector3((sum/count-art.sprite.pivot.x)/art.sprite.pixelsPerUnit,(y-rect.y+.5f-art.sprite.pivot.y)/art.sprite.pixelsPerUnit,0);
                if(art.flipX)point.x=-point.x;if(art.flipY)point.y=-point.y;
                return origin.InverseTransformPoint(art.transform.TransformPoint(point));
            }
            finally{UnityEngine.Object.DestroyImmediate(texture);}
        }
        private static void Adjust(AnimationClip clip,string path,string property,float factor,float delta,float rest)
        {
            var binding=EditorCurveBinding.FloatCurve(path,typeof(Transform),property);
            var curve=AnimationUtility.GetEditorCurve(clip,binding)??AnimationCurve.Constant(0,clip.length,rest);
            curve.keys=curve.keys.Select(k=>{k.value=k.value*factor+delta;k.inTangent*=factor;k.outTangent*=factor;return k;}).ToArray();
            AnimationUtility.SetEditorCurve(clip,binding,curve);
        }
        private static void Set(AnimationClip clip,string path,string property,Keyframe[] keys)
        {
            var curve=new AnimationCurve(keys);for(var i=0;i<keys.Length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);}
            AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),property),curve);
        }
        private static Vector3[] Points(SpriteSkin skin,Transform rig)
        {
            var sprite=skin.GetComponent<SpriteRenderer>().sprite;var vs=sprite.GetVertexAttribute<Vector3>(VertexAttribute.Position);var ws=sprite.GetVertexAttribute<BoneWeight>(VertexAttribute.BlendWeight);var binds=sprite.GetBindPoses();var ps=new Vector3[vs.Length];
            for(var i=0;i<vs.Length;i++){var w=ws[i];var ids=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};var values=new[]{w.weight0,w.weight1,w.weight2,w.weight3};for(var j=0;j<4;j++)if(values[j]>0)ps[i]+=rig.InverseTransformPoint((skin.boneTransforms[ids[j]].localToWorldMatrix*binds[ids[j]]).MultiplyPoint3x4(vs[i]))*values[j];}
            return ps;
        }
        [Serializable] private sealed class Part { public string name; public Vector3 position,scale; public Quaternion rotation; public Vector3 min,max; }
        [Serializable] private sealed class Joint {public string name,path;public Vector3 position,localPosition;public Quaternion rotation;}
        [Serializable] private sealed class Report {public Part[] approved,animated;public Joint[] joints;public Vector3[] nativePoints;public BoneWeight[] weights;}
        public static void Inspect()
        {
            var source=PrefabUtility.LoadPrefabContents(WhiteCatgirlUnitBuilder.Assembly);
            var root=PrefabUtility.LoadPrefabContents(WhiteCatgirlUnitBuilder.Visual);
            try
            {
                var animator=root.GetComponentsInChildren<Animator>(true).Single();var rig=animator.transform;
                animator.runtimeAnimatorController.animationClips.Single(c=>c.name.EndsWith("Idle")).SampleAnimation(rig.gameObject,0);
                var skin=rig.Find("Cute Girl Lower Body/FW Lower Body").GetComponentsInChildren<SpriteSkin>(true).Single(s=>s.enabled);
                var sprite=skin.GetComponent<SpriteRenderer>().sprite;
                var vs=sprite.GetVertexAttribute<Vector3>(VertexAttribute.Position).ToArray();var ws=sprite.GetVertexAttribute<BoneWeight>(VertexAttribute.BlendWeight).ToArray();var binds=sprite.GetBindPoses().ToArray();
                var ps=new Vector3[vs.Length];
                for(var i=0;i<vs.Length;i++){var w=ws[i];var ids=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};var values=new[]{w.weight0,w.weight1,w.weight2,w.weight3};for(var j=0;j<4;j++)if(values[j]>0)ps[i]+=rig.InverseTransformPoint((skin.boneTransforms[ids[j]].localToWorldMatrix*binds[ids[j]]).MultiplyPoint3x4(vs[i]))*values[j];}
                Part Capture(SpriteRenderer s,Transform origin)=>new Part{name=s.name,position=origin.InverseTransformPoint(s.transform.position),scale=s.transform.lossyScale,rotation=s.transform.rotation,min=origin.InverseTransformPoint(s.bounds.min),max=origin.InverseTransformPoint(s.bounds.max)};
                var report=new Report{approved=source.GetComponentsInChildren<SpriteRenderer>().Select(s=>Capture(s,source.transform)).ToArray(),animated=root.GetComponentsInChildren<SpriteRenderer>().Where(s=>s.enabled).Select(s=>Capture(s,rig)).ToArray(),joints=skin.boneTransforms.Select(b=>new Joint{name=b.name,path=AnimationUtility.CalculateTransformPath(b,rig),position=rig.InverseTransformPoint(b.position),localPosition=b.localPosition,rotation=b.rotation}).ToArray(),nativePoints=ps,weights=ws};
                File.WriteAllText("Logs/WhiteCatgirlUnit/AlignmentBefore.json",JsonUtility.ToJson(report,true));
            }
            finally{PrefabUtility.UnloadPrefabContents(source);PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
