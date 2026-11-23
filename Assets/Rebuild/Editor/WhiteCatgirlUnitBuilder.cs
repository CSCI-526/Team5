using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Team5.Rebuild.Presentation;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;
using Object = UnityEngine.Object;

namespace Team5.Rebuild.Editor
{
    public static class WhiteCatgirlUnitBuilder
    {
        public const string Folder = "Assets/Rebuild/WhiteCatgirlUnit";
        public const string Visual = Folder + "/WhiteCatgirlUnitVisual.prefab";
        public const string ScenePath = "Assets/Rebuild/Scenes/WhiteCatgirlVisualTest.unity";
        public const string Assembly = "Assets/Rebuild/WhiteCatgirlAssembly/WhiteCatgirl_EditableAssembly.prefab";
        private const string Donor = "Assets/Rebuild/CatgirlFrontTest/FWLowerBody/CatgirlFWLowerBody.prefab";
        private const string DonorFolder = "Assets/Rebuild/CatgirlFrontTest/FWLowerBody";
        private static readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        private static readonly List<SpriteRenderer> upper = new List<SpriteRenderer>();
        private static Material material;

        [MenuItem("Team5/Rebuild/White Catgirl/Build animated unit from saved assembly (once)")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(Visual))
                throw new InvalidOperationException("Exit Play Mode; existing animated unit must not be overwritten.");
            Directory.CreateDirectory(Folder + "/SkinnedSprites");
            Directory.CreateDirectory("Logs/WhiteCatgirlUnit");
            var sourceHash = Hash(Assembly);
            File.Copy(Assembly, "Logs/WhiteCatgirlUnit/ApprovedAssembly.prefab", true);
            AssetDatabase.Refresh();
            bones.Clear(); upper.Clear();
            material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Rebuild/FWTest/FWWorldZSprite.mat");
            if (material == null) throw new InvalidOperationException("World-Z sprite material missing.");
            var source = PrefabUtility.LoadPrefabContents(Assembly);
            var donor = PrefabUtility.LoadPrefabContents(Donor);
            var root = new GameObject("White Catgirl Unit Visual");
            try
            {
                root.AddComponent<UnitView>();
                var driver = root.AddComponent<FWUnitVisual>();
                var billboard = Child(root.transform, "Camera Facing Visual", Vector3.zero);
                var facing = Child(billboard, "Mirror Entire Character", Vector3.zero);
                var group = billboard.gameObject.AddComponent<SortingGroup>();
                var rig = Child(facing, "White Catgirl Rig", Vector3.zero);
                var sourceParts = source.GetComponentsInChildren<SpriteRenderer>(true)
                    .Where(s => s.enabled && s.gameObject.activeSelf).ToDictionary(s => s.name);
                foreach (var part in sourceParts.Values.Where(s => !s.name.StartsWith("F_LEG_SCREEN_")))
                {
                    var t = Child(rig, part.name, source.transform.InverseTransformPoint(part.transform.position));
                    t.localRotation = Quaternion.Inverse(source.transform.rotation) * part.transform.rotation;
                    t.localScale = Divide(part.transform.lossyScale, source.transform.lossyScale);
                    var r = t.gameObject.AddComponent<SpriteRenderer>();
                    r.sprite = part.sprite; r.color = part.color; r.flipX = part.flipX; r.flipY = part.flipY;
                    r.sortingOrder = part.sortingOrder; r.sortingLayerID = part.sortingLayerID; r.sharedMaterial = material;
                    upper.Add(r);
                }
                var pelvis = upper.Single(s => s.name == "F_PELVIS");
                var body = upper.Single(s => s.name == "F_BODY");
                var face = upper.Single(s => s.name == "F_FACE");
                Bone(rig, "Skeleton", rig.position);
                Bone(bones["Skeleton"], "Pelvis", new Vector3(pelvis.bounds.center.x, pelvis.bounds.max.y, 0));
                Bone(bones["Pelvis"], "Chest", new Vector3(body.bounds.center.x, body.bounds.center.y, 0));
                Bone(bones["Chest"], "Head", new Vector3(face.bounds.center.x, face.bounds.min.y, 0));
                foreach (var side in new[] { "L", "R" })
                {
                    var sleeve = upper.Single(s => s.name == "F_SLEEVE_SCREEN_" + side);
                    var p = new Vector3(sleeve.bounds.center.x, sleeve.bounds.max.y - sleeve.bounds.size.y * .08f, 0);
                    Bone(bones["Chest"], "Arm" + side, p);
                    Bone(bones["Arm" + side], "Forearm" + side, p - Vector3.up * sleeve.bounds.size.y * .48f);
                }
                foreach (var part in upper)
                {
                    var parent = part.name.Contains("HAIR") || part.name.Contains("EAR") || part.name == "F_FACE" ? bones["Head"]
                        : part.name == "F_PELVIS" || part.name == "F_TAIL_STATIC" ? bones["Pelvis"] : bones["Chest"];
                    if (part.name.StartsWith("F_SLEEVE_SCREEN_"))
                    {
                        var side = part.name.EndsWith("_L") ? "L" : "R";
                        Skin(part, new[] { bones["Arm" + side], bones["Forearm" + side] }, true);
                    }
                    else Skin(part, new[] { parent }, false);
                }

                var originalRig = donor.GetComponentsInChildren<Animator>(true).Single().transform;
                var lower = Object.Instantiate(originalRig.gameObject).transform;
                lower.name = "Cute Girl Lower Body"; lower.SetParent(rig, false);
                lower.localPosition = Vector3.zero; lower.localRotation = Quaternion.identity; lower.localScale = Vector3.one;
                Object.DestroyImmediate(lower.GetComponent<Animator>());
                var donorIdle = AssetDatabase.LoadAssetAtPath<AnimationClip>(DonorFolder + "/CatgirlFWLowerIdle.anim");
                donorIdle.SampleAnimation(lower.gameObject, 0);
                var fw = lower.Find("FW Lower Body");
                var native = fw.GetComponentsInChildren<SpriteSkin>(true).Single(s => s.name == "F_BODY");
                var nativeRenderer = native.GetComponent<SpriteRenderer>();
                foreach (var r in lower.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    r.enabled = r == nativeRenderer;
                    var skin = r.GetComponent<SpriteSkin>();
                    if (skin != null) skin.enabled = r == nativeRenderer;
                }
                var nativeCopy = Object.Instantiate(nativeRenderer.sprite);
                nativeCopy.name = "White Catgirl Cute Girl Native Legs";
                AssetDatabase.CreateAsset(nativeCopy, Folder + "/SkinnedSprites/CuteGirlNativeLegs.asset");
                nativeRenderer.sprite = nativeCopy; nativeRenderer.sortingOrder = 2; nativeRenderer.sharedMaterial = material;
                var hips = native.boneTransforms.Where(t => t.name == "F_F_L_bone_11" || t.name == "F_F_R_bone_13").ToArray();
                var rawHip = hips.Select(h => lower.InverseTransformPoint(h.position)).Aggregate(Vector3.zero, (a,b) => a+b) / hips.Length;
                var points = Deformed(native).Select(p => lower.InverseTransformPoint(native.transform.TransformPoint(p))).ToArray();
                var rawBottom = points.Min(p => p.y);
                var artLegs = new[] { sourceParts["F_LEG_SCREEN_L"], sourceParts["F_LEG_SCREEN_R"] };
                var legTop = artLegs.Average(s => source.transform.InverseTransformPoint(new Vector3(s.bounds.center.x,s.bounds.max.y,0)).y);
                var desiredBootTop = artLegs.Average(s => source.transform.InverseTransformPoint(new Vector3(s.bounds.center.x,s.bounds.min.y+s.bounds.size.y*.25f,0)).y);
                var desiredHipX = artLegs.Average(s => source.transform.InverseTransformPoint(s.bounds.center).x);
                var sy = (legTop - desiredBootTop) / (rawHip.y - rawBottom);
                var widthReference = artLegs.Average(s => source.transform.InverseTransformPoint(new Vector3(s.bounds.center.x,s.bounds.min.y+s.bounds.size.y*.32f,0)).y);
                var sx = (legTop - widthReference) / (rawHip.y - rawBottom);
                lower.localScale = new Vector3(sx,sy,sy);
                lower.localPosition = new Vector3(desiredHipX - rawHip.x*sx, legTop - rawHip.y*sy, 0);
                foreach (var side in new[] { "L", "R" })
                {
                    var art = sourceParts["F_LEG_SCREEN_" + side];
                    var anchor = lower.Find("FW Driven Catgirl Foot " + side);
                    if (anchor == null) throw new InvalidOperationException("Missing Cute Girl foot anchor " + side);
                    var rect = art.sprite.rect;
                    rect.height *= .34f;
                    var sprite = Sprite.Create(art.sprite.texture, rect, Vector2.one*.5f, art.sprite.pixelsPerUnit,0,SpriteMeshType.FullRect);
                    var t = Child(rig, "White Catgirl Boot " + side, source.transform.InverseTransformPoint(art.transform.TransformPoint(
                        new Vector3(0, -(art.sprite.rect.height-rect.height)/art.sprite.pixelsPerUnit*.5f,0))));
                    t.localRotation = Quaternion.Inverse(source.transform.rotation)*art.transform.rotation;
                    t.localScale = Divide(art.transform.lossyScale, source.transform.lossyScale);
                    var r = t.gameObject.AddComponent<SpriteRenderer>();
                    r.sprite=sprite; r.color=art.color;r.flipX=art.flipX;r.flipY=art.flipY;
                    r.sortingOrder=art.sortingOrder;r.sharedMaterial=material;
                    Skin(r,new[]{anchor},false);
                    Object.DestroyImmediate(sprite);
                }

                var controller = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/WhiteCatgirl.controller");
                controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
                AnimatorState idleState=null,runState=null;
                foreach(var state in new[]{"Idle","Run"})
                {
                    var clip = BuildClip(state, rig, lower, hips, donorIdle);
                    var node = controller.layers[0].stateMachine.AddState(state);node.motion=clip;
                    if(state=="Idle")idleState=node;else runState=node;
                }
                controller.layers[0].stateMachine.defaultState=idleState;
                Transition(idleState,runState,true);Transition(runState,idleState,false);
                var animator=rig.gameObject.AddComponent<Animator>();
                animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                ((AnimationClip)idleState.motion).SampleAnimation(rig.gameObject,0);
                var serialized = new SerializedObject(driver);
                serialized.FindProperty("billboard").objectReferenceValue=billboard;
                serialized.FindProperty("facingRoot").objectReferenceValue=facing;
                serialized.FindProperty("front").objectReferenceValue=animator;
                serialized.FindProperty("sortingGroup").objectReferenceValue=group;
                serialized.FindProperty("worldZMaterial").objectReferenceValue=material;
                serialized.FindProperty("frontOnly").boolValue=true;
                serialized.FindProperty("artworkFacesRight").boolValue=true;
                serialized.FindProperty("worldZPriority").boolValue=true;
                serialized.FindProperty("displayTiltDegrees").floatValue=30;
                serialized.FindProperty("matchRoleHairColor").boolValue=false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                foreach(var r in lower.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>!r.enabled).ToArray())
                {
                    var unusedSkin=r.GetComponent<SpriteSkin>();if(unusedSkin!=null)Object.DestroyImmediate(unusedSkin);Object.DestroyImmediate(r);
                }
                PrefabUtility.SaveAsPrefabAsset(root,Visual);
                AssetDatabase.SaveAssets();
                if(Hash(Assembly)!=sourceHash)throw new InvalidOperationException("Approved assembly was modified.");
                File.WriteAllText("Logs/WhiteCatgirlUnit/Build.txt","Approved assembly SHA256: "+sourceHash+"\nSource unchanged.\nNative Cute Girl lower-body SpriteSkin and animation reused; new boots and upper-body artwork retained.\n");
            }
            finally { Object.DestroyImmediate(root);PrefabUtility.UnloadPrefabContents(source);PrefabUtility.UnloadPrefabContents(donor); }
            CreateTestScene();
            WhiteCatgirlAlignment.Apply();
            WhiteCatgirlAlignment.FitBootCuffArtwork();
            Debug.Log("WHITE_CATGIRL_UNIT_BUILD_OK: "+Visual);
        }

        private static Vector3 Divide(Vector3 a,Vector3 b)=>new Vector3(a.x/b.x,a.y/b.y,a.z/b.z);

        // Adjust only generated donor geometry, preserving the approved boot/upper-body bind pose.
        public static void RefineLowerOverlap()
        {
            var root=PrefabUtility.LoadPrefabContents(Visual);
            var approved=PrefabUtility.LoadPrefabContents(Assembly);
            try
            {
                var animator=root.GetComponentsInChildren<Animator>(true).Single();var rig=animator.transform;
                var idle=animator.runtimeAnimatorController.animationClips.Single(c=>c.name.EndsWith("Idle"));
                idle.SampleAnimation(rig.gameObject,0);
                var lower=rig.Find("Cute Girl Lower Body");
                var native=lower.Find("FW Lower Body").GetComponentsInChildren<SpriteSkin>(true).Single(s=>s.enabled);
                var hips=native.boneTransforms.Where(t=>t.name=="F_F_L_bone_11"||t.name=="F_F_R_bone_13").ToArray();
                var hipY=hips.Average(h=>rig.InverseTransformPoint(h.position).y);
                var hipRaw=hips.Average(h=>lower.InverseTransformPoint(h.position).y);
                var bottom=Deformed(native).Min(v=>rig.InverseTransformPoint(native.transform.TransformPoint(v)).y);
                var desired=approved.GetComponentsInChildren<SpriteRenderer>(true).Where(s=>s.name.StartsWith("F_LEG_SCREEN_")).Average(s=>approved.transform.InverseTransformPoint(new Vector3(s.bounds.center.x,s.bounds.min.y+s.bounds.size.y*.25f,0)).y);
                var scale=lower.localScale;scale.y*=(hipY-desired)/(hipY-bottom);lower.localScale=scale;
                var p=lower.localPosition;p.y=hipY-hipRaw*scale.y;lower.localPosition=p;
                foreach(var boot in rig.GetComponentsInChildren<SpriteSkin>(true).Where(s=>s.name.StartsWith("White Catgirl Boot ")))
                {
                    var sprite=boot.GetComponent<SpriteRenderer>().sprite;
                    using(var bind=new NativeArray<Matrix4x4>(boot.boneTransforms.Select(b=>b.worldToLocalMatrix*boot.transform.localToWorldMatrix).ToArray(),Allocator.Temp))sprite.SetBindPoses(bind);
                    EditorUtility.SetDirty(sprite);
                }
                // Remove disabled donor renderers, leaving the native skeleton and required animation paths.
                foreach(var r in lower.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>!r.enabled).ToArray())
                {
                    var skin=r.GetComponent<SpriteSkin>();if(skin!=null)Object.DestroyImmediate(skin);Object.DestroyImmediate(r);
                }
                PrefabUtility.SaveAsPrefabAsset(root,Visual);AssetDatabase.SaveAssets();
                Debug.Log("WHITE_CATGIRL_LOWER_OVERLAP_REFINED");
            }
            finally{PrefabUtility.UnloadPrefabContents(approved);PrefabUtility.UnloadPrefabContents(root);}
        }
        private static Transform Child(Transform p,string name,Vector3 pos){var t=new GameObject(name).transform;t.SetParent(p,false);t.localPosition=pos;return t;}
        private static void Bone(Transform p,string name,Vector3 world){var t=Child(p,name,Vector3.zero);t.position=world;bones.Add(name,t);}
        private static string Hash(string path){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(path)));}

        private static void Skin(SpriteRenderer renderer,Transform[] joints,bool sleeve)
        {
            var original=renderer.sprite;
            var sprite=Sprite.Create(original.texture,original.rect,original.pivot/original.rect.size,original.pixelsPerUnit,0,SpriteMeshType.FullRect);
            sprite.name=renderer.name;
            // A continuous sleeve mesh with interpolated elbow weights, not disconnected cutouts.
            const int rows=14,columns=4;
            var vs=new List<Vector3>();var uvs=new List<Vector2>();var ws=new List<BoneWeight>();var ids=new List<ushort>();
            var size=original.rect.size/original.pixelsPerUnit;var pivot=original.pivot/original.pixelsPerUnit;
            for(var y=0;y<=rows;y++)for(var x=0;x<=columns;x++)
            {
                var f=new Vector2((float)x/columns,(float)y/rows);
                vs.Add(new Vector3(f.x*size.x-pivot.x,f.y*size.y-pivot.y,0));
                uvs.Add((original.rect.position+Vector2.Scale(f,original.rect.size))/new Vector2(original.texture.width,original.texture.height));
                var w=sleeve?Mathf.SmoothStep(0,1,Mathf.InverseLerp(.7f,.35f,f.y)):0;
                ws.Add(new BoneWeight{boneIndex0=0,weight0=1-w,boneIndex1=sleeve?1:0,weight1=w});
                if(x<columns&&y<rows){var a=(ushort)(y*(columns+1)+x);ids.Add(a);ids.Add((ushort)(a+columns+1));ids.Add((ushort)(a+1));ids.Add((ushort)(a+1));ids.Add((ushort)(a+columns+1));ids.Add((ushort)(a+columns+2));}
            }
            sprite.SetBones(joints.Select(j=>new SpriteBone{name=j.name,parentId=-1,position=renderer.transform.InverseTransformPoint(j.position),rotation=Quaternion.identity,length=.1f}).ToArray());
            using(var vertices=new NativeArray<Vector3>(vs.ToArray(),Allocator.Temp))
            using(var texcoords=new NativeArray<Vector2>(uvs.ToArray(),Allocator.Temp))
            using(var weights=new NativeArray<BoneWeight>(ws.ToArray(),Allocator.Temp))
            using(var triangles=new NativeArray<ushort>(ids.ToArray(),Allocator.Temp))
            using(var binds=new NativeArray<Matrix4x4>(joints.Select(j=>j.worldToLocalMatrix*renderer.transform.localToWorldMatrix).ToArray(),Allocator.Temp))
            {sprite.SetVertexCount(vs.Count);sprite.SetVertexAttribute(VertexAttribute.Position,vertices);sprite.SetVertexAttribute(VertexAttribute.TexCoord0,texcoords);sprite.SetIndices(triangles);sprite.SetBindPoses(binds);sprite.SetVertexAttribute(VertexAttribute.BlendWeight,weights);}
            AssetDatabase.CreateAsset(sprite,Folder+"/SkinnedSprites/"+renderer.name+".asset");renderer.sprite=sprite;
            var skin=renderer.gameObject.AddComponent<SpriteSkin>();skin.SetRootBone(joints[0]);skin.SetBoneTransforms(joints);skin.alwaysUpdate=true;
            var data=new SerializedObject(skin);data.FindProperty("m_Bounds").boundsValue=new Bounds(Vector3.zero,Vector3.one*10);data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AnimationClip BuildClip(string state,Transform rig,Transform lower,Transform[] hips,AnimationClip donorIdle)
        {
            var source=AssetDatabase.LoadAssetAtPath<AnimationClip>(DonorFolder+"/CatgirlFWLower"+state+".anim");
            var clip=new AnimationClip{name="WhiteCatgirl"+state,frameRate=60};
            foreach(var binding in AnimationUtility.GetCurveBindings(source))
            {
                if(!binding.path.StartsWith("FW Lower Body/")&&!binding.path.StartsWith("FW Driven Catgirl Foot "))continue;
                var b=binding;b.path="Cute Girl Lower Body/"+b.path;
                AnimationUtility.SetEditorCurve(clip,b,AnimationUtility.GetEditorCurve(source,binding));
            }
            // Copy only the donor lower body. This keeps every authored new upper-body bind pose intact.
            donorIdle.SampleAnimation(lower.gameObject,0);
            var initialHip=hips.Average(h=>rig.InverseTransformPoint(h.position).y);
            var originals=bones.ToDictionary(p=>p.Key,p=>p.Value.localPosition);
            var run=state=="Run";var length=source.length;
            foreach(var bone in bones.Values)
            {
                var path=AnimationUtility.CalculateTransformPath(bone,rig);var p=originals[bone.name];
                Curve(clip,path,"m_LocalPosition.x",length,t=>p.x);
                Curve(clip,path,"m_LocalPosition.z",length,t=>p.z);
                if(bone.name=="Pelvis")Curve(clip,path,"m_LocalPosition.y",length,t=>{
                    source.SampleAnimation(lower.gameObject,t*length);
                    return p.y+hips.Average(h=>rig.InverseTransformPoint(h.position).y)-initialHip;
                });
                else Curve(clip,path,"m_LocalPosition.y",length,t=>p.y);
                var amplitude=run?(bone.name.StartsWith("Forearm")?10:bone.name.StartsWith("Arm")?12:bone.name=="Chest"?1.5f:bone.name=="Head"?.6f:0):0;
                var sign=bone.name.EndsWith("R")?-1:1;
                Curve(clip,path,"localEulerAnglesRaw.z",length,t=>sign*amplitude*Mathf.Sin(t*Mathf.PI*2));
            }
            donorIdle.SampleAnimation(lower.gameObject,0);
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,settings);
            AssetDatabase.CreateAsset(clip,Folder+"/"+clip.name+".anim");return clip;
        }
        private static void Curve(AnimationClip clip,string path,string prop,float length,Func<float,float> value)
        {
            const int count=60;var keys=new Keyframe[count+1];for(var i=0;i<=count;i++)keys[i]=new Keyframe(length*i/count,value((float)i/count));
            var curve=new AnimationCurve(keys);for(var i=0;i<keys.Length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);}
            AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),prop),curve);
        }
        private static Vector3[] Deformed(SpriteSkin skin)
        {
            var sprite=skin.GetComponent<SpriteRenderer>().sprite;var vs=sprite.GetVertexAttribute<Vector3>(VertexAttribute.Position);var weights=sprite.GetVertexAttribute<BoneWeight>(VertexAttribute.BlendWeight);var binds=sprite.GetBindPoses();var output=new Vector3[vs.Length];
            for(var i=0;i<vs.Length;i++){var w=weights[i];var indices=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};var values=new[]{w.weight0,w.weight1,w.weight2,w.weight3};for(var j=0;j<4;j++)if(values[j]>0)output[i]+=skin.transform.InverseTransformPoint((skin.boneTransforms[indices[j]].localToWorldMatrix*binds[indices[j]]).MultiplyPoint3x4(vs[i]))*values[j];}
            return output;
        }
        private static void Transition(AnimatorState a,AnimatorState b,bool moving){var t=a.AddTransition(b);t.hasExitTime=false;t.hasFixedDuration=true;t.duration=.12f;t.AddCondition(moving?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0,"IsMoving");}
        private static void CreateTestScene()
        {
            if(File.Exists(ScenePath))throw new InvalidOperationException("Test scene already exists.");
            if(!AssetDatabase.CopyAsset(CatgirlFrontPrototypeBuilder.TestScene,ScenePath))throw new IOException("Cannot copy test scene.");
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
            try{var p=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BattlePresentation>(true)).Single();var data=new SerializedObject(p);data.FindProperty("useTestUnitVisual").boolValue=true;data.FindProperty("testUnitPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<UnitView>(Visual);data.FindProperty("additionalTestVisuals").arraySize=0;data.ApplyModifiedPropertiesWithoutUndo();EditorSceneManager.SaveScene(scene);}
            finally{EditorSceneManager.CloseScene(scene,true);}
        }
    }
}
