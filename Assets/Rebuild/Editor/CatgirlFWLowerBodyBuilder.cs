using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Team5.Rebuild.Presentation;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;
using Object=UnityEngine.Object;

namespace Team5.Rebuild.Editor
{
    public static class CatgirlFWLowerBodyBuilder
    {
        public const string Folder="Assets/Rebuild/CatgirlFrontTest/FWLowerBody";
        public const string Visual=Folder+"/CatgirlFWLowerBody.prefab";
        private struct V {public Vector3 p;public Vector2 uv;public BoneWeight w;}
        private sealed class Foot {public Transform anchor;public int toe,heel,soleA,soleB;public string side;}
        [MenuItem("Team5/Rebuild/Catgirl Front/Build FW lower-body hybrid (once)")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            if(File.Exists(Visual))throw new InvalidOperationException("Hybrid already exists; refusing to overwrite hand edits.");
            Directory.CreateDirectory(Folder);Directory.CreateDirectory("Logs/CatgirlFWLower");AssetDatabase.Refresh();
            var root=PrefabUtility.LoadPrefabContents(CatgirlFrontPrototypeBuilder.Visual);
            var source=PrefabUtility.LoadPrefabContents("Assets/Rebuild/FWTest/FWUnitVisual.prefab");
            try
            {
                root.name="Catgirl with FW Lower Body";
                var rig=root.GetComponentsInChildren<Animator>(true).Single().transform;
                foreach(var skin in rig.GetComponentsInChildren<SpriteSkin>(true).Where(s=>s.name.StartsWith("F_LEG_CONTINUOUS_")))skin.gameObject.SetActive(false);
                var sourceFront=source.GetComponentsInChildren<Animator>(true).Single(a=>a.name=="miho_forward");
                var fw=Object.Instantiate(sourceFront.gameObject).transform;fw.name="FW Lower Body";fw.SetParent(rig,false);
                fw.localPosition=Vector3.zero;fw.localRotation=Quaternion.identity;fw.localScale=Vector3.one;
                Object.DestroyImmediate(fw.GetComponent<Animator>());
                var body=fw.GetComponentsInChildren<SpriteRenderer>(true).Single(s=>s.name=="F_BODY");
                var native=body.GetComponent<SpriteSkin>();var original=body.sprite;
                var positions=original.GetVertexAttribute<Vector3>(VertexAttribute.Position).ToArray();
                var uv=original.uv;var weights=original.GetVertexAttribute<BoneWeight>(VertexAttribute.BlendWeight).ToArray();
                var binds=original.GetBindPoses().ToArray();
                foreach(var sprite in fw.GetComponentsInChildren<SpriteRenderer>(true))
                    if(sprite!=body){sprite.enabled=false;var skin=sprite.GetComponent<SpriteSkin>();if(skin!=null)skin.enabled=false;}
                var hipL=fw.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="F_F_L_bone_11");
                var hipR=fw.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="F_F_R_bone_13");
                var rawHip=(fw.InverseTransformPoint(hipL.position)+fw.InverseTransformPoint(hipR.position))*.5f;
                var raw=positions.Select(v=>fw.InverseTransformPoint(body.transform.TransformPoint(v))).ToArray();
                var floor=raw.Min(v=>v.y);
                var ownHip=rig.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="ThighL"||t.name=="ThighR").Select(t=>rig.InverseTransformPoint(t.position)).Aggregate(Vector3.zero,(a,b)=>a+b)*.5f;
                var scale=ownHip.y/(rawHip.y-floor);
                // FW faces left; this character's art faces right. Reflect only the donor rig.
                fw.localScale=new Vector3(-scale,scale,scale);
                fw.localPosition=new Vector3(ownHip.x+rawHip.x*scale,-floor*scale,0);
                var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Rebuild/FWTest/FWWorldZSprite.mat");
                body.sprite=ClipLowerBody(original,body.transform,fw,positions,uv,weights,raw);
                body.sharedMaterial=material;body.sortingOrder=9;
                var skinData=new SerializedObject(native);skinData.FindProperty("m_Bounds").boundsValue=new Bounds(Vector3.zero,Vector3.one*20);skinData.ApplyModifiedPropertiesWithoutUndo();
                var feet=new List<Foot>();
                AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/FW_MIHO/Animation/F_IDLE01.anim").SampleAnimation(fw.gameObject,0);
                // Donor left/right exchange screen sides after the donor-only reflection.
                foreach(var pair in new[]{("R","L"),("L","R")})
                {
                    var sourceSide=pair.Item1;var artSide=pair.Item2;
                    var legIndices=Enumerable.Range(0,raw.Length).Where(i=>raw[i].y<3 && (sourceSide=="L"?raw[i].x<rawHip.x:raw[i].x>rawHip.x)).ToArray();
                    var low=legIndices.Min(i=>raw[i].y);
                    var edge=legIndices.Where(i=>raw[i].y<low+.22f).ToArray();
                    var sole=legIndices.OrderBy(i=>raw[i].y).Take(2).ToArray();
                    var anchor=new GameObject("FW Driven Catgirl Foot "+artSide).transform;anchor.SetParent(rig,false);
                    var foot=new Foot{anchor=anchor,side=artSide,toe=edge.OrderBy(i=>raw[i].x).First(),heel=edge.OrderByDescending(i=>raw[i].x).First(),soleA=sole[0],soleB=sole[1]};
                    feet.Add(foot);
                    SampleFoot(foot,rig,body,native,positions,weights,binds);
                    AddBoot(root,rig,foot,material);
                }
                var animator=rig.GetComponent<Animator>();
                var controller=AnimatorController.CreateAnimatorControllerAtPath(Folder+"/CatgirlFWLowerBody.controller");
                controller.AddParameter("IsMoving",AnimatorControllerParameterType.Bool);
                var machine=controller.layers[0].stateMachine;AnimatorState idleState=null,runState=null;
                foreach(var state in new[]{"Idle","Run"})
                {
                    var upper=AssetDatabase.LoadAssetAtPath<AnimationClip>(CatgirlFrontPrototypeBuilder.Folder+"/CatgirlFront"+state+".anim");
                    var donor=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/FW_MIHO/Animation/F_"+(state=="Idle"?"IDLE01":"RUN01")+".anim");
                    var clip=Combine(upper,donor,fw,rig,body,native,positions,weights,binds,feet);
                    var node=machine.AddState(state);node.motion=clip;if(state=="Idle")idleState=node;else runState=node;
                }
                machine.defaultState=idleState;Transition(idleState,runState,true);Transition(runState,idleState,false);
                animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
                foreach(var state in new[]{"Idle","Run"})SyncWaistCurve(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"/CatgirlFWLower"+state+".anim"),rig,state);
                // Sampling only authored new clips; reset to the hybrid Idle for a valid prefab preview.
                ((AnimationClip)idleState.motion).SampleAnimation(rig.gameObject,0);
                foreach(var sprite in fw.GetComponentsInChildren<SpriteRenderer>(true))sprite.enabled=sprite==body;
                PrefabUtility.SaveAsPrefabAsset(root,Visual);
                AssetDatabase.SaveAssets();
                File.WriteAllText("Logs/CatgirlFWLower/BuildInfo.json",JsonUtility.ToJson(new HybridInfo{donorScale=scale,nativeVertexCount=original.GetVertexCount(),clippedVertexCount=body.sprite.GetVertexCount(),nativeBoneNames=native.boneTransforms.Select(b=>b.name).ToArray()},true));
            }
            finally{PrefabUtility.UnloadPrefabContents(source);PrefabUtility.UnloadPrefabContents(root);}
            ApplyLegWidth(.85f);ApplyToTestScene();AssetDatabase.SaveAssets();Debug.Log("CATGIRL_FW_LOWER_HYBRID_BUILT: "+Visual);
        }
        [Serializable]private sealed class HybridInfo {public float donorScale;public int nativeVertexCount,clippedVertexCount;public string[] nativeBoneNames;}
        [MenuItem("Team5/Rebuild/Catgirl Front/Set hybrid leg width to 85%")]
        public static void SlimLegs()=>ApplyLegWidth(.85f);
        private static void ApplyLegWidth(float factor)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            var path=Folder+"/FWNativeLowerBody.asset";
            var backup=Folder+"/FWNativeLowerBody_BeforeSlim.asset";
            if(!File.Exists(backup)&&!AssetDatabase.CopyAsset(path,backup))throw new IOException("Could not preserve original leg mesh.");
            var original=AssetDatabase.LoadAssetAtPath<Sprite>(backup);var target=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(original==null||target==null)throw new InvalidOperationException("Build the hybrid first.");
            var positions=original.GetVertexAttribute<Vector3>(VertexAttribute.Position).ToArray();
            var weights=original.GetVertexAttribute<BoneWeight>(VertexAttribute.BlendWeight).ToArray();
            var names=original.GetBones().Select(b=>b.name).ToArray();
            var poses=original.GetBindPoses().ToArray();
            var hipL=Array.IndexOf(names,"F_F_L_bone_11");var kneeL=Array.IndexOf(names,"F_F_L_bone_12");
            var hipR=Array.IndexOf(names,"F_F_R_bone_13");var kneeR=Array.IndexOf(names,"F_F_R_bone_14");
            var bones=original.GetBones();
            Vector3 Origin(int id)=>poses[id].inverse.MultiplyPoint3x4(Vector3.zero);
            float Influence(BoneWeight w,int a,int b)
            {
                var ids=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};var values=new[]{w.weight0,w.weight1,w.weight2,w.weight3};var sum=0f;
                for(var i=0;i<4;i++)if(ids[i]==a||ids[i]==b)sum+=values[i];return sum;
            }
            for(var i=0;i<positions.Length;i++)
            {
                var p=positions[i];var left=Influence(weights[i],hipL,kneeL)>=Influence(weights[i],hipR,kneeR);
                var hip=left?hipL:hipR;var knee=left?kneeL:kneeR;
                var a=Origin(hip);var b=Origin(knee);
                if(p.y<b.y){a=b;b=poses[knee].inverse.MultiplyPoint3x4(Vector3.right*bones[knee].length);}
                var center=Mathf.LerpUnclamped(a.x,b.x,(p.y-a.y)/(b.y-a.y));
                positions[i].x=center+(p.x-center)*factor;
            }
            var data=new NativeArray<Vector3>(positions,Allocator.Temp);
            try{target.SetVertexAttribute(VertexAttribute.Position,data);EditorUtility.SetDirty(target);AssetDatabase.SaveAssets();}
            finally{data.Dispose();}
            Debug.Log("HYBRID_LEGS_SLIMMED: width="+factor+"; leg length, skeleton, boots and clothes retained.");
        }
        private static Sprite ClipLowerBody(Sprite source,Transform renderer,Transform fw,Vector3[] pos,Vector2[] uv,BoneWeight[] weights,Vector3[] raw,bool save=true)
        {
            var original=source.GetIndices().ToArray();var output=new List<V>();var indices=new List<ushort>();
            // The brown boots cover the clipped distal ends; original FW shoes are removed.
            float Y(V v)=>v.p.y+6.3449998f;
            List<V> Clip(List<V> polygon,float limit,bool above)
            {
                var list=new List<V>();
                for(var i=0;i<polygon.Count;i++)
                {
                    var a=polygon[i];var b=polygon[(i+1)%polygon.Count];var ya=Y(a);var yb=Y(b);
                    var inside=above?ya>=limit:ya<=limit;var next=above?yb>=limit:yb<=limit;
                    if(inside)list.Add(a);
                    if(inside!=next){var t=(limit-ya)/(yb-ya);list.Add(new V{p=Vector3.Lerp(a.p,b.p,t),uv=Vector2.Lerp(a.uv,b.uv,t),w=Mix(a.w,b.w,t)});}
                }
                return list;
            }
            for(var i=0;i<original.Length;i+=3)
            {
                var polygon=new List<V>();
                for(var j=0;j<3;j++){var id=original[i+j];polygon.Add(new V{p=pos[id],uv=uv[id],w=weights[id]});}
                polygon=Clip(Clip(polygon,2.8f,true),4.7f,false);
                if(polygon.Count<3)continue;
                var offset=output.Count;output.AddRange(polygon);
                for(var j=1;j<polygon.Count-1;j++){indices.Add((ushort)offset);indices.Add((ushort)(offset+j));indices.Add((ushort)(offset+j+1));}
            }
            var sprite=Sprite.Create(source.texture,source.rect,source.pivot/source.rect.size,source.pixelsPerUnit,0,SpriteMeshType.FullRect);sprite.name="FW Native Lower Body";
            sprite.SetBones(source.GetBones());
            var bind=new NativeArray<Matrix4x4>(source.GetBindPoses().ToArray(),Allocator.Temp);
            var vertices=new NativeArray<Vector3>(output.Select(v=>v.p).ToArray(),Allocator.Temp);
            var uvs=new NativeArray<Vector2>(output.Select(v=>v.uv).ToArray(),Allocator.Temp);
            var boneWeights=new NativeArray<BoneWeight>(output.Select(v=>v.w).ToArray(),Allocator.Temp);
            var triangles=new NativeArray<ushort>(indices.ToArray(),Allocator.Temp);
            try{sprite.SetVertexCount(output.Count);sprite.SetVertexAttribute(VertexAttribute.Position,vertices);sprite.SetVertexAttribute(VertexAttribute.TexCoord0,uvs);sprite.SetIndices(triangles);sprite.SetBindPoses(bind);sprite.SetVertexAttribute(VertexAttribute.BlendWeight,boneWeights);}
            finally{bind.Dispose();vertices.Dispose();uvs.Dispose();boneWeights.Dispose();triangles.Dispose();}
            if(save)AssetDatabase.CreateAsset(sprite,Folder+"/FWNativeLowerBody.asset");return sprite;
        }
        public static void RefineWaist()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            var root=PrefabUtility.LoadPrefabContents("Assets/Rebuild/FWTest/FWUnitVisual.prefab");
            Sprite copy=null;
            try
            {
                var front=root.GetComponentsInChildren<Animator>(true).Single(a=>a.name=="miho_forward").transform;
                var renderer=front.GetComponentsInChildren<SpriteRenderer>(true).Single(s=>s.name=="F_BODY");var source=renderer.sprite;
                var positions=source.GetVertexAttribute<Vector3>(VertexAttribute.Position).ToArray();var uv=source.uv;var weights=source.GetVertexAttribute<BoneWeight>(VertexAttribute.BlendWeight).ToArray();
                copy=ClipLowerBody(source,renderer.transform,front,positions,uv,weights,null,false);
                var target=AssetDatabase.LoadAssetAtPath<Sprite>(Folder+"/FWNativeLowerBody.asset");
                File.Copy(Folder+"/FWNativeLowerBody.asset","Logs/CatgirlFWLower/LowerBodyBeforeWaistClip.asset",false);
                EditorUtility.CopySerialized(copy,target);EditorUtility.SetDirty(target);AssetDatabase.SaveAssets();
                Debug.Log("HYBRID_WAIST_REFINED: native shorts removed; Catgirl clothes retained.");
            }
            finally{if(copy!=null)Object.DestroyImmediate(copy);PrefabUtility.UnloadPrefabContents(root);}
        }
        public static void SynchronizeWaist()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            var root=PrefabUtility.LoadPrefabContents(Visual);
            try
            {
                var rig=root.GetComponentsInChildren<Animator>(true).Single().transform;
                foreach(var state in new[]{"Idle","Run"})
                {
                    var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"/CatgirlFWLower"+state+".anim");
                    File.Copy(Folder+"/CatgirlFWLower"+state+".anim","Logs/CatgirlFWLower/BeforeWaistSync_"+state+"_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff")+".anim",false);
                    SyncWaistCurve(clip,rig,state);
                    EditorUtility.SetDirty(clip);
                }
                AssetDatabase.SaveAssets();Debug.Log("HYBRID_WAIST_SYNCHRONIZED: upper body follows native FW hip bob; head/arm local curves unchanged.");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        private static void SyncWaistCurve(AnimationClip clip,Transform rig,string state)
        {
            var fw=rig.Find("FW Lower Body");
            var hips=fw.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="F_F_L_bone_11"||t.name=="F_F_R_bone_13").ToArray();
            var binding=EditorCurveBinding.FloatCurve("Skeleton/Pelvis",typeof(Transform),"m_LocalPosition.y");
            var upper=AssetDatabase.LoadAssetAtPath<AnimationClip>(CatgirlFrontPrototypeBuilder.Folder+"/CatgirlFront"+state+".anim");
            var baseline=AnimationUtility.GetEditorCurve(upper,binding).Evaluate(0);
            float HipY()=>hips.Average(h=>rig.InverseTransformPoint(h.position).y);
            clip.SampleAnimation(rig.gameObject,0);var first=HipY();
            var count=Mathf.CeilToInt(clip.length*60);var keys=new Keyframe[count+1];
            for(var i=0;i<=count;i++)
            {
                var time=clip.length*i/count;clip.SampleAnimation(rig.gameObject,time);keys[i]=new Keyframe(time,baseline+HipY()-first);
            }
            AnimationUtility.SetEditorCurve(clip,binding,SmoothCurve(keys));
            foreach(var footBinding in AnimationUtility.GetCurveBindings(clip).Where(b=>b.path.StartsWith("FW Driven Catgirl Foot ",StringComparison.Ordinal)))
                AnimationUtility.SetEditorCurve(clip,footBinding,SmoothCurve(AnimationUtility.GetEditorCurve(clip,footBinding).keys));
        }
        private static AnimationCurve SmoothCurve(Keyframe[] keys)
        {
            var curve=new AnimationCurve(keys);
            for(var i=0;i<keys.Length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);}
            return curve;
        }
        private static BoneWeight Mix(BoneWeight a,BoneWeight b,float t)
        {
            var map=new Dictionary<int,float>();
            void Add(BoneWeight w,float factor)
            {
                var ids=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};var values=new[]{w.weight0,w.weight1,w.weight2,w.weight3};
                for(var i=0;i<4;i++)if(values[i]>0){map.TryGetValue(ids[i],out var old);map[ids[i]]=old+values[i]*factor;}
            }
            Add(a,1-t);Add(b,t);var list=map.OrderByDescending(k=>k.Value).Take(4).ToArray();var sum=list.Sum(k=>k.Value);
            var ids2=new int[4];var values2=new float[4];for(var i=0;i<list.Length;i++){ids2[i]=list[i].Key;values2[i]=list[i].Value/sum;}
            return new BoneWeight{boneIndex0=ids2[0],boneIndex1=ids2[1],boneIndex2=ids2[2],boneIndex3=ids2[3],weight0=values2[0],weight1=values2[1],weight2=values2[2],weight3=values2[3]};
        }
        private static void SampleFoot(Foot foot,Transform rig,SpriteRenderer body,SpriteSkin skin,Vector3[] positions,BoneWeight[] weights,Matrix4x4[] bind)
        {
            Vector3 Point(int i)
            {
                var w=weights[i];var ids=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};var values=new[]{w.weight0,w.weight1,w.weight2,w.weight3};var p=Vector3.zero;
                for(var j=0;j<4;j++)if(values[j]>0)p+=(skin.boneTransforms[ids[j]].localToWorldMatrix*bind[ids[j]]).MultiplyPoint3x4(positions[i])*values[j];
                return rig.InverseTransformPoint(p);
            }
            var center=(Point(foot.soleA)+Point(foot.soleB))*.5f;center.z=0;
            var direction=Point(foot.toe)-Point(foot.heel);
            foot.anchor.localPosition=center;foot.anchor.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg);
        }
        private static void AddBoot(GameObject root,Transform rig,Foot foot,Material material)
        {
            var original=root.GetComponentsInChildren<SpriteRenderer>(true).Single(s=>s.name=="F_BOOT_SCREEN_"+foot.side);
            var source=original.sprite;var sprite=Object.Instantiate(source);sprite.name="Catgirl Boot "+foot.side;
            var part=new GameObject("Catgirl Original Boot "+foot.side).transform;part.SetParent(rig,false);
            const float factor=.85f;
            part.localScale=new Vector3(factor,factor,1);
            part.localPosition=foot.anchor.localPosition+foot.anchor.localRotation*new Vector3(0,source.bounds.size.y*factor*.5f,0);
            part.localRotation=foot.anchor.localRotation;
            var renderer=part.gameObject.AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.sortingOrder=10;renderer.sharedMaterial=material;
            sprite.SetBones(new[]{new SpriteBone{name=foot.anchor.name,parentId=-1,position=part.InverseTransformPoint(foot.anchor.position),rotation=Quaternion.identity,length=.1f}});
            var poses=new NativeArray<Matrix4x4>(new[]{foot.anchor.worldToLocalMatrix*part.localToWorldMatrix},Allocator.Temp);
            var weights=new NativeArray<BoneWeight>(sprite.GetVertexCount(),Allocator.Temp);
            try{for(var i=0;i<weights.Length;i++)weights[i]=new BoneWeight{boneIndex0=0,weight0=1};sprite.SetBindPoses(poses);sprite.SetVertexAttribute(VertexAttribute.BlendWeight,weights);}
            finally{poses.Dispose();weights.Dispose();}
            AssetDatabase.CreateAsset(sprite,Folder+"/CatgirlOriginalBoot"+foot.side+".asset");
            var skin=part.gameObject.AddComponent<SpriteSkin>();skin.SetRootBone(foot.anchor);skin.SetBoneTransforms(new[]{foot.anchor});skin.alwaysUpdate=true;
            var data=new SerializedObject(skin);data.FindProperty("m_Bounds").boundsValue=new Bounds(Vector3.zero,Vector3.one*4);data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static AnimationClip Combine(AnimationClip upper,AnimationClip source,Transform fw,Transform rig,SpriteRenderer body,SpriteSkin skin,Vector3[] positions,BoneWeight[] weights,Matrix4x4[] bind,List<Foot> feet)
        {
            var clip=Object.Instantiate(upper);clip.name="CatgirlFWLower"+(upper.name.EndsWith("Idle")?"Idle":"Run");
            foreach(var binding in AnimationUtility.GetCurveBindings(clip).Where(b=>b.path.StartsWith("Skeleton/Pelvis/Thigh",StringComparison.Ordinal)))AnimationUtility.SetEditorCurve(clip,binding,null);
            var prefix=AnimationUtility.CalculateTransformPath(fw,rig)+"/";
            var factor=upper.length/source.length;
            foreach(var binding in AnimationUtility.GetCurveBindings(source))
            {
                if(binding.type!=typeof(Transform)||binding.path.Split('/').Any(n=>n.Contains("Arm")||n.Contains("HEAD")||n.Contains("HAIR")))continue;
                var curve=AnimationUtility.GetEditorCurve(source,binding);
                curve.keys=curve.keys.Select(k=>{k.time*=factor;k.inTangent/=factor;k.outTangent/=factor;return k;}).ToArray();
                var target=binding;target.path=prefix+binding.path;AnimationUtility.SetEditorCurve(clip,target,curve);
            }
            var count=Mathf.CeilToInt(upper.length*60);var tracks=feet.Select(f=>new[]{new List<Keyframe>(),new List<Keyframe>(),new List<Keyframe>(),new List<Keyframe>()}).ToArray();
            for(var sample=0;sample<=count;sample++)
            {
                var time=upper.length*sample/count;source.SampleAnimation(fw.gameObject,time/factor);
                for(var i=0;i<feet.Count;i++)
                {
                    var foot=feet[i];SampleFoot(foot,rig,body,skin,positions,weights,bind);
                    var p=foot.anchor.localPosition;var angle=Mathf.DeltaAngle(0,foot.anchor.localEulerAngles.z);
                    tracks[i][0].Add(new Keyframe(time,p.x));tracks[i][1].Add(new Keyframe(time,p.y));tracks[i][2].Add(new Keyframe(time,0));tracks[i][3].Add(new Keyframe(time,angle));
                }
            }
            for(var i=0;i<feet.Count;i++)for(var j=0;j<4;j++)
            {
                var property=j<3?"m_LocalPosition."+"xyz"[j]:"localEulerAnglesRaw.z";
                AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(feet[i].anchor.name,typeof(Transform),property),SmoothCurve(tracks[i][j].ToArray()));
            }
            AssetDatabase.CreateAsset(clip,Folder+"/"+clip.name+".anim");return clip;
        }
        private static void Transition(AnimatorState from,AnimatorState to,bool moving)
        {var t=from.AddTransition(to);t.hasExitTime=false;t.hasFixedDuration=true;t.duration=.12f;t.AddCondition(moving?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0,"IsMoving");}
        private static void ApplyToTestScene()
        {
            File.Copy(CatgirlFrontPrototypeBuilder.TestScene,"Logs/CatgirlFWLower/TestSceneBeforeHybrid.unity",false);
            var scene=EditorSceneManager.OpenScene(CatgirlFrontPrototypeBuilder.TestScene,OpenSceneMode.Additive);
            try
            {
                var presentation=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BattlePresentation>(true)).Single();
                var data=new SerializedObject(presentation);data.FindProperty("testUnitPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<UnitView>(Visual);data.ApplyModifiedPropertiesWithoutUndo();EditorSceneManager.SaveScene(scene);
            }
            finally{EditorSceneManager.CloseScene(scene,true);}
        }
        [Serializable] private sealed class BoneInfo
        {public string name;public Vector3 rigPosition;public Vector3 localPosition;public Quaternion localRotation;public float length;}
        [Serializable] private sealed class VertexInfo
        {public Vector3 position;public Vector2 uv;public BoneWeight weight;}
        [Serializable] private sealed class BodyInfo
        {public string name;public Vector3 rendererPosition;public Vector3 rendererScale;public Rect rect;public Vector2 pivot;public float ppu;public BoneInfo[] bones;public VertexInfo[] vertices;public ushort[] indices;}
        public static void Inspect()
        {
            Directory.CreateDirectory("Logs/CatgirlFWLower");
            var root=PrefabUtility.LoadPrefabContents("Assets/Rebuild/FWTest/FWUnitVisual.prefab");
            try
            {
                var front=root.GetComponentsInChildren<Animator>(true).Single(a=>a.name=="miho_forward").transform;
                var renderer=front.GetComponentsInChildren<SpriteRenderer>(true).Single(s=>s.name=="F_BODY");
                var sprite=renderer.sprite;var skin=renderer.GetComponent<SpriteSkin>();
                var positions=sprite.GetVertexAttribute<Vector3>(VertexAttribute.Position).ToArray();
                var uv=sprite.uv;
                var weights=sprite.GetVertexAttribute<BoneWeight>(VertexAttribute.BlendWeight).ToArray();
                var sb=sprite.GetBones();
                Debug.Log("FW_BODY_CHANNELS: positions="+positions.Length+" uv="+uv.Length+" weights="+weights.Length);
                var data=new BodyInfo{name=sprite.name,rendererPosition=renderer.transform.localPosition,rendererScale=renderer.transform.localScale,
                    rect=sprite.rect,pivot=sprite.pivot,ppu=sprite.pixelsPerUnit,vertices=new VertexInfo[positions.Length],indices=sprite.GetIndices().ToArray(),
                    bones=skin.boneTransforms.Select((b,i)=>new BoneInfo{name=b.name,rigPosition=front.InverseTransformPoint(b.position),localPosition=b.localPosition,localRotation=b.localRotation,length=sb[i].length}).ToArray()};
                for(var i=0;i<positions.Length;i++)data.vertices[i]=new VertexInfo{position=positions[i],uv=uv[i],weight=weights[i]};
                File.WriteAllText("Logs/CatgirlFWLower/BodyInfo.json",JsonUtility.ToJson(data,true));
                var texture=sprite.texture;
                var rt=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGB32);
                var old=RenderTexture.active;
                Texture2D copy=null;
                try
                {
                    Graphics.Blit(texture,rt);RenderTexture.active=rt;
                    var rect=sprite.textureRect;
                    copy=new Texture2D((int)rect.width,(int)rect.height,TextureFormat.RGBA32,false);
                    copy.ReadPixels(rect,0,0);copy.Apply();File.WriteAllBytes("Logs/CatgirlFWLower/FWBodyTexture.png",copy.EncodeToPNG());
                }
                finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);if(copy!=null)Object.DestroyImmediate(copy);}
                Debug.Log("FW_LOWER_INSPECTED: "+positions.Length+" vertices; "+skin.boneTransforms.Length+" bones; front scale="+front.localScale);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
