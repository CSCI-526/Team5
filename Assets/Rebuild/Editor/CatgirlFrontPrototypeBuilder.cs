using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Team5.Rebuild.Presentation;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;
using Object = UnityEngine.Object;

namespace Team5.Rebuild.Editor
{
    // Independent front-only art prototype. Does not rebuild or replace any FW assets.
    public static class CatgirlFrontPrototypeBuilder
    {
        public const string Folder = "Assets/Rebuild/CatgirlFrontTest";
        public const string Visual = Folder + "/CatgirlFrontUnitVisual.prefab";
        public const string TestScene = "Assets/Rebuild/Scenes/CatgirlFrontTest.unity";
        private const float Ppu = 635f;
        [Serializable] private sealed class LayerList { public Layer[] layers; }
        [Serializable] private sealed class Layer
        {
            public string name;
            public int order_bottom_to_top;
            public bool visible;
            public int[] bounds_px;
        }

        [MenuItem("Team5/Rebuild/Catgirl Front/Build prototype (once)")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (File.Exists(Visual) || File.Exists(TestScene))
                throw new InvalidOperationException("Prototype already exists. Existing assets are never overwritten by this builder.");
            Directory.CreateDirectory(Folder + "/SkinnedSprites");
            AssetDatabase.Refresh();
            var layers = JsonUtility.FromJson<LayerList>(File.ReadAllText(Folder + "/Source/front_layers.json")).layers;
            if (layers == null || layers.Length != 25) throw new InvalidDataException("Expected 25 front art layers.");
            var root = new GameObject("Catgirl Front Unit Visual");
            try
            {
                root.AddComponent<UnitView>();
                var driver = root.AddComponent<FWUnitVisual>();
                var billboard = Child(root.transform, "Camera Facing Visual", Vector3.zero);
                var facing = Child(billboard, "Mirror Entire Character", Vector3.zero);
                var group = billboard.gameObject.AddComponent<SortingGroup>();
                var rig = Child(facing, "Catgirl Front Rig", Vector3.zero);
                var bones = CreateBones(rig);
                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Rebuild/FWTest/FWWorldZSprite.mat");
                if (material == null) throw new InvalidOperationException("Existing world-Z sprite material missing.");
                foreach (var layer in layers.Where(l => l.visible)) BuildPart(layer, rig, bones, material);
                // First create bind matrices with unit scale, then apply display-only overall scale.
                facing.localScale = Vector3.one * .8f;
                var animator = rig.gameObject.AddComponent<Animator>();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.runtimeAnimatorController = BuildController(rig);
                var serialized = new SerializedObject(driver);
                Set(serialized, "billboard", billboard); Set(serialized, "facingRoot", facing);
                Set(serialized, "front", animator); Set(serialized, "back", null); Set(serialized, "sortingGroup", group);
                Set(serialized, "worldZMaterial", material);
                serialized.FindProperty("frontOnly").boolValue = true;
                serialized.FindProperty("artworkFacesRight").boolValue = true;
                serialized.FindProperty("worldZPriority").boolValue = true;
                serialized.FindProperty("displayTiltDegrees").floatValue = 30;
                serialized.FindProperty("matchRoleHairColor").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, Visual);
            }
            finally { Object.DestroyImmediate(root); }
            CreateScene();
            AssetDatabase.SaveAssets();
            Debug.Log("CATGIRL_FRONT_BUILD_OK: " + TestScene);
        }

        private static Vector3 Point(float x, float y) => new Vector3((x - 363) / Ppu, (1053 - y) / Ppu, 0);
        private static Transform Child(Transform parent, string name, Vector3 position)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false); t.localPosition = position; return t;
        }
        private static Dictionary<string, Transform> CreateBones(Transform rig)
        {
            var result = new Dictionary<string, Transform>();
            void Bone(string name, string parent, float x, float y)
            {
                var p = parent == null ? rig : result[parent];
                var bone = Child(p, name, Vector3.zero);
                bone.position = rig.TransformPoint(Point(x, y)); result.Add(name, bone);
            }
            Bone("Skeleton", null, 363, 1053);
            Bone("Pelvis", "Skeleton", 362, 776);
            Bone("Chest", "Pelvis", 380, 543);
            Bone("Head", "Chest", 385, 475);
            Bone("RearHair", "Head", 375, 255);
            Bone("FrontHair", "Head", 385, 255);
            Bone("CoatBack", "Chest", 385, 535);
            Bone("CoatL", "Chest", 300, 543); Bone("CoatR", "Chest", 460, 543);
            Bone("ArmL", "Chest", 278, 521); Bone("ForearmL", "ArmL", 231, 606); Bone("HandL", "ForearmL", 183, 680);
            Bone("ArmR", "Chest", 461, 518); Bone("ForearmR", "ArmR", 495, 608); Bone("HandR", "ForearmR", 513, 691);
            Bone("ThighL", "Pelvis", 309, 774); Bone("ShinL", "ThighL", 245, 877); Bone("BootL", "ShinL", 199, 957);
            Bone("ThighR", "Pelvis", 415, 773); Bone("ShinR", "ThighR", 461, 875); Bone("BootR", "ShinR", 502, 952);
            return result;
        }
        private static string BoneFor(string name)
        {
            switch (name)
            {
                case "F_FACE": case "F_EAR_SCREEN_L": case "F_EAR_SCREEN_R": case "F_HAIR_CLIP": return "Head";
                case "F_HAIR_FRONT": return "FrontHair";
                case "F_B_HAIR": return "RearHair";
                case "F_BODY": case "F_CHEST_DEVICE": return "Chest";
                case "F_PELVIS": return "Pelvis";
                case "F_COAT_BACK": return "CoatBack";
                case "F_COAT_SCREEN_L": return "CoatL";
                case "F_COAT_SCREEN_R": return "CoatR";
            }
            var side = name.EndsWith("_SCREEN_L", StringComparison.Ordinal) ? "L" : "R";
            if (name.Contains("UPPER_ARM")) return "Arm" + side;
            if (name.Contains("FOREARM")) return "Forearm" + side;
            if (name.Contains("HAND")) return "Hand" + side;
            if (name.Contains("THIGH")) return "Thigh" + side;
            if (name.Contains("SHIN")) return "Shin" + side;
            if (name.Contains("BOOT")) return "Boot" + side;
            throw new InvalidDataException("Unmapped part " + name);
        }
        private static void BuildPart(Layer layer, Transform rig, Dictionary<string, Transform> bones, Material material)
        {
            var png = Folder + "/Source/Parts/" + layer.name + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(png);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            // Sharpen the mip levels used by the small battle sprites and blend between levels.
            importer.mipmapEnabled = true;
            importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            importer.filterMode = FilterMode.Trilinear;
            importer.maxTextureSize = 2048;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.mipmapBias = -.65f;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
            var b = layer.bounds_px;
            if (texture.width != b[2] - b[0] || texture.height != b[3] - b[1]) throw new InvalidDataException(layer.name + " bounds mismatch");
            var transform = Child(rig, layer.name, Point((b[0] + b[2]) * .5f, (b[1] + b[3]) * .5f));
            var renderer = transform.gameObject.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = layer.order_bottom_to_top; renderer.sharedMaterial = material;
            var bone = bones[BoneFor(layer.name)];
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f, Ppu, 0, SpriteMeshType.FullRect);
            sprite.name = layer.name;
            // Cutout prototype: each part is rigidly weighted to its own articulated bone.
            // Native SpriteSkin + skinning channels; no external runtime or transform-only imitation.
            sprite.SetBones(new[] { new SpriteBone { name = bone.name, parentId = -1,
                position = transform.InverseTransformPoint(bone.position), rotation = Quaternion.identity, length = .12f } });
            var bind = new NativeArray<Matrix4x4>(1, Allocator.Temp);
            try
            {
                bind[0] = bone.worldToLocalMatrix * transform.localToWorldMatrix; sprite.SetBindPoses(bind);
            }
            finally { bind.Dispose(); }
            var weights = new NativeArray<BoneWeight>(sprite.GetVertexCount(), Allocator.Temp);
            try
            {
                for (var i = 0; i < weights.Length; i++) weights[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1 };
                sprite.SetVertexAttribute(VertexAttribute.BlendWeight, weights);
            }
            finally { weights.Dispose(); }
            AssetDatabase.CreateAsset(sprite, Folder + "/SkinnedSprites/" + layer.name + ".asset");
            renderer.sprite = sprite;
            var skin = transform.gameObject.AddComponent<SpriteSkin>();
            skin.SetRootBone(bone); skin.SetBoneTransforms(new[] { bone }); skin.alwaysUpdate = true;
            var skinData = new SerializedObject(skin);
            skinData.FindProperty("m_Bounds").boundsValue = new Bounds(Vector3.zero, Vector3.one * 4);
            skinData.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RuntimeAnimatorController BuildController(Transform rig)
        {
            var idle = MakeClip("CatgirlFrontIdle", 2.0f, rig, false);
            var run = MakeClip("CatgirlFrontRun", .64f, rig, true);
            ApplyRunArmMotion(run);
            ApplyRunLegMotion(run, rig);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/CatgirlFront.controller");
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            var machine = controller.layers[0].stateMachine;
            var a = machine.AddState("Idle"); a.motion = idle;
            var b = machine.AddState("Run"); b.motion = run; machine.defaultState = a;
            Transition(a, b, true); Transition(b, a, false);
            return controller;
        }
        private static void Transition(AnimatorState a, AnimatorState b, bool moving)
        {
            var transition = a.AddTransition(b); transition.hasExitTime = false;
            transition.hasFixedDuration = true; transition.duration = .12f;
            transition.AddCondition(moving ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, "IsMoving");
        }
        private static AnimationClip MakeClip(string name, float length, Transform rig, bool run)
        {
            var clip = new AnimationClip { name = name, frameRate = 30 };
            var bones = rig.GetComponentsInChildren<Transform>().Where(t => t != rig && t.parent != rig || t.name == "Skeleton")
                .Where(t => !t.name.StartsWith("F_", StringComparison.Ordinal)).ToArray();
            foreach (var bone in bones)
            {
                var path = AnimationUtility.CalculateTransformPath(bone, rig);
                var amplitude = 0f; var phase = 0f; var bias = 0f;
                switch (bone.name)
                {
                    case "Chest": amplitude = run ? 1.5f : .5f; break;
                    case "Head": amplitude = run ? 1f : .65f; phase = .2f; break;
                    case "RearHair": case "FrontHair": amplitude = run ? 2.5f : 1f; phase = .25f; break;
                    case "CoatBack": case "CoatL": case "CoatR": amplitude = run ? 2.5f : .7f; phase = .3f; break;
                    case "ArmL": amplitude = run ? 8f : .7f; phase = .5f; break;
                    case "ArmR": amplitude = run ? 8f : .7f; break;
                    case "ForearmL": case "ForearmR": amplitude = run ? 3f : .3f; break;
                    case "ThighL": amplitude = run ? 10f : 0; break;
                    case "ThighR": amplitude = run ? 10f : 0; phase = .5f; break;
                    case "ShinL": amplitude = run ? 4f : 0; bias = run ? 4f : 0; break;
                    case "ShinR": amplitude = run ? 4f : 0; bias = run ? -4f : 0; phase = .5f; break;
                    case "BootL": case "BootR": amplitude = run ? 2f : 0; break;
                }
                Curve(clip, path, "localEulerAnglesRaw.z", length, t => bias + amplitude * Mathf.Sin((t + phase) * Mathf.PI * 2));
                // Explicitly key every bone's position and scale in both states so transitions reset cleanly.
                var origin = bone.localPosition;
                Curve(clip,path,"m_LocalPosition.x",length,t => origin.x);
                Curve(clip,path,"m_LocalPosition.y",length,t => origin.y + (bone.name == "Pelvis" ? (run ? .012f : .004f) * (1-Mathf.Cos(t*Mathf.PI*(run?4:2))) : 0));
                Curve(clip,path,"m_LocalPosition.z",length,t => origin.z);
                Curve(clip,path,"m_LocalScale.x",length,t => 1);
                Curve(clip,path,"m_LocalScale.y",length,t => 1);
                Curve(clip,path,"m_LocalScale.z",length,t => 1);
            }
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, Folder + "/" + name + ".anim"); return clip;
        }
        private static void Curve(AnimationClip clip,string path,string property,float length,Func<float,float> value)
        {
            const int samples=32; var keys = new Keyframe[samples+1];
            for(var i=0;i<=samples;i++) keys[i]=new Keyframe(length*i/samples,value((float)i/samples));
            var curve = new AnimationCurve(keys);
            for(var i=0;i<keys.Length;i++) { AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto); AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto); }
            AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),property),curve);
        }

        [MenuItem("Team5/Rebuild/Catgirl Front/Improve Run arm swing (preserve other curves)")]
        public static void ImproveRunArms()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            const string path = Folder + "/CatgirlFrontRun.anim";
            var run = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (run == null) throw new InvalidOperationException("Build the front prototype first.");
            // Keep a byte-for-byte copy, including any hand-authored head adjustments.
            Directory.CreateDirectory("Logs/CatgirlFront");
            var backup = "Logs/CatgirlFront/RunBeforeArmSwing_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".anim";
            File.Copy(path, backup, false);
            Undo.RecordObject(run, "Improve Catgirl Run arm swing");
            ApplyRunArmMotion(run);
            EditorUtility.SetDirty(run); AssetDatabase.SaveAssets();
            Debug.Log("CATGIRL_ARM_SWING_UPDATED; backup: " + backup);
        }

        private static void ApplyRunArmMotion(AnimationClip run)
        {
            var source = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/FW_MIHO/Animation/F_RUN01.anim");
            if (source == null) throw new InvalidOperationException("FW reference Run clip missing.");
            var bindings = AnimationUtility.GetCurveBindings(source);
            AnimationCurve Reference(string suffix)
            {
                var binding = bindings.Single(b => b.type == typeof(Transform) &&
                    b.propertyName == "localEulerAnglesRaw.z" && b.path.EndsWith(suffix, StringComparison.Ordinal));
                return AnimationUtility.GetEditorCurve(source, binding);
            }
            var leftUpper = Reference("/F_Arm_L_bone_5");
            var rightUpper = Reference("/F_Arm_R_bone_8");
            var leftLower = Reference("/F_Arm_L_bone_6");
            var rightLower = Reference("/F_Arm_R_bone_9");
            float Offset(AnimationCurve curve, float phase)
            {
                var keys = curve.keys;
                var middle = (keys.Min(k => k.value) + keys.Max(k => k.value)) * .5f;
                return curve.Evaluate(phase * keys[keys.Length - 1].time) - middle;
            }
            const string chest = "Skeleton/Pelvis/Chest/";
            // Transfer only relative angular excursions/timing. FW's absolute joint angles,
            // coordinate positions, bind poses and bone orientation do not fit this skeleton.
            Curve(run, chest + "ArmL", "localEulerAnglesRaw.z", run.length, t => 8 + Offset(leftUpper, t));
            Curve(run, chest + "ArmR", "localEulerAnglesRaw.z", run.length, t => -8 + Offset(rightUpper, t));
            // This cutout's forearms hang down in bind pose. They need a large positive
            // fold toward screen right (the art's forward direction), not FW's absolute
            // angles or a small +/- rest-angle oscillation. Mirroring handles left travel.
            Curve(run, chest + "ArmL/ForearmL", "localEulerAnglesRaw.z", run.length, t => 105 + Offset(leftLower, t) * 2.7f);
            Curve(run, chest + "ArmR/ForearmR", "localEulerAnglesRaw.z", run.length, t => 80 + Offset(rightLower, t) * 1.1f);
            // Small counter-rotation keeps the separate hands from looking rigid at the cuffs.
            Curve(run, chest + "ArmL/ForearmL/HandL", "localEulerAnglesRaw.z", run.length, t => -Offset(leftLower, t) * .35f);
            Curve(run, chest + "ArmR/ForearmR/HandR", "localEulerAnglesRaw.z", run.length, t => -Offset(rightLower, t) * .35f);
        }
        [MenuItem("Team5/Rebuild/Catgirl Front/Improve Run knee cycle (preserve upper body)")]
        public static void ImproveRunLegs()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            const string path=Folder+"/CatgirlFrontRun.anim";
            var run=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(run==null)throw new InvalidOperationException("Run clip missing.");
            Directory.CreateDirectory("Logs/CatgirlFront");
            var backup="Logs/CatgirlFront/RunBeforeKneeCycle_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff")+".anim";
            File.Copy(path,backup,false);
            var root=PrefabUtility.LoadPrefabContents(Visual);
            try
            {
                Undo.RecordObject(run,"Improve Catgirl knee cycle");
                ApplyRunLegMotion(run,root.GetComponentsInChildren<Animator>(true).Single().transform);
                EditorUtility.SetDirty(run);AssetDatabase.SaveAssets();
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            Debug.Log("CATGIRL_KNEE_CYCLE_UPDATED; backup: "+backup);
        }
        private static void ApplyRunLegMotion(AnimationClip run,Transform rig)
        {
            var bones=rig.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t.name,t=>t);
            foreach(var side in new[]{"L","R"})
            {
                var thigh=bones["Thigh"+side];var shin=bones["Shin"+side];var boot=bones["Boot"+side];
                var offset=side=="L"?0:.5f;
                var restAngle=Mathf.Atan2(shin.localPosition.y,shin.localPosition.x)*Mathf.Rad2Deg;
                var neutral=-90-restAngle; // Remove the source illustration's splayed-leg stance in Run.
                float Phase(float t)=>Mathf.Repeat(t+offset,1);
                float Hip(float t)=>neutral+20*Mathf.Cos(Phase(t)*Mathf.PI*2);
                float Flex(float t)
                {
                    var p=Phase(t);
                    // Ground contact: near extension. Recovery: fold the lower leg backward.
                    return p<.5f?6+8*Mathf.Sin(p*Mathf.PI*2):6+58*Mathf.Pow(Mathf.Sin((p-.5f)*Mathf.PI*2),1.3f);
                }
                float Pitch(float t)=>8*Mathf.Cos(Phase(t)*Mathf.PI*2);
                var path="Skeleton/Pelvis/Thigh"+side;
                Curve(run,path,"localEulerAnglesRaw.z",run.length,Hip);
                Curve(run,path+"/Shin"+side,"localEulerAnglesRaw.z",run.length,t=>-Flex(t));
                Curve(run,path+"/Shin"+side+"/Boot"+side,"localEulerAnglesRaw.z",run.length,t=>Pitch(t)-Hip(t)+Flex(t));
                var pelvis=bones["Pelvis"];
                var hipY=rig.InverseTransformPoint(thigh.position).y;
                var upper=(Vector2)shin.localPosition;var lower=(Vector2)boot.localPosition;
                var heel=side=="L"?new Vector2((109-199)/635f,(957-1053)/635f):new Vector2((448-502)/635f,(952-1053)/635f);
                var toe=side=="L"?new Vector2((290-199)/635f,heel.y):new Vector2((637-502)/635f,heel.y);
                var pelvisY=AnimationUtility.GetEditorCurve(run,EditorCurveBinding.FloatCurve("Skeleton/Pelvis",typeof(Transform),"m_LocalPosition.y"));
                var restPelvisY=pelvis.localPosition.y;
                var restY=thigh.localPosition.y;
                float RotateY(Vector2 v,float degrees)
                {
                    var a=degrees*Mathf.Deg2Rad;return v.x*Mathf.Sin(a)+v.y*Mathf.Cos(a);
                }
                Curve(run,path,"m_LocalPosition.y",run.length,t=>
                {
                    var phase=Phase(t);var hip=Hip(t);var knee=-Flex(t);var pitch=Pitch(t);
                    var bob=pelvisY==null?0:pelvisY.Evaluate(t*run.length)-restPelvisY;
                    var ankleY=hipY+bob+RotateY(upper,hip)+RotateY(lower,hip+knee);
                    var footY=ankleY+Mathf.Min(RotateY(heel,pitch),RotateY(toe,pitch));
                    var recovery=phase<.5f?0:Mathf.Pow(Mathf.Sin((phase-.5f)*Mathf.PI*2),2);
                    // Visual hip correction keeps the stance foot near the ground. It changes
                    // only this leg's local pose, never the Unit root or upper-body curves.
                    return restY-footY*(1-recovery);
                });
            }
        }
        private static void CreateScene()
        {
            if (!AssetDatabase.CopyAsset("Assets/Rebuild/Scenes/1.unity", TestScene)) throw new IOException("Cannot copy Scene 1.");
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(TestScene, OpenSceneMode.Additive);
            try
            {
                var presentation = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BattlePresentation>(true)).Single();
                var data = new SerializedObject(presentation);
                Set(data,"testUnitPrefab",AssetDatabase.LoadAssetAtPath<UnitView>(Visual));
                data.FindProperty("testVisualMemberId").intValue = 111;
                data.FindProperty("useTestUnitVisual").boolValue = true;
                data.FindProperty("additionalTestVisuals").arraySize = 0;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if(previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene,true);
            }
        }
        private static void Set(SerializedObject data,string property,Object value) => data.FindProperty(property).objectReferenceValue = value;
    }
}
