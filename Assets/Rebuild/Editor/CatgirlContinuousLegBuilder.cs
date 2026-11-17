using System;
using System.IO;
using System.Linq;
using Team5.Rebuild.Presentation;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;
using Object = UnityEngine.Object;

namespace Team5.Rebuild.Editor
{
    // Repairs only the visual prefab. Existing bone names and all animation curves are retained.
    public static class CatgirlContinuousLegBuilder
    {
        private const string Folder = CatgirlFrontPrototypeBuilder.Folder;
        private const string Art = Folder + "/Source/ContinuousLegs";
        private const string Sprites = Folder + "/ContinuousSprites";
        [Serializable] private sealed class Registration { public Layer[] layers; }
        [Serializable] private sealed class Layer { public string name; public int[] bounds_px; }

        [MenuItem("Team5/Rebuild/Catgirl Front/Install continuous leg skins (once)")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            Directory.CreateDirectory(Folder+"/Backup"); Directory.CreateDirectory(Sprites);
            AssetDatabase.Refresh();
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(CatgirlFrontPrototypeBuilder.Visual);
            if(existing==null)throw new InvalidOperationException("Front prefab missing.");
            if(existing.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="F_LEG_CONTINUOUS_SCREEN_L"))
                throw new InvalidOperationException("Continuous leg repair already installed; existing assets are not overwritten.");
            var backup=Folder+"/Backup/CatgirlFrontUnitVisual_SplitLegs.prefab";
            if(File.Exists(backup))throw new InvalidOperationException("An earlier leg backup exists; refusing to overwrite it.");
            if(!AssetDatabase.CopyAsset(CatgirlFrontPrototypeBuilder.Visual,backup))throw new IOException("Cannot preserve split-leg prefab.");
            Directory.CreateDirectory("Logs/CatgirlFront");
            foreach(var clip in new[]{"CatgirlFrontIdle.anim","CatgirlFrontRun.anim"})
                File.Copy(Folder+"/"+clip,"Logs/CatgirlFront/BeforeContinuousLegs_"+clip,false);
            var root=PrefabUtility.LoadPrefabContents(CatgirlFrontPrototypeBuilder.Visual);
            try
            {
                var rig=root.GetComponentsInChildren<Animator>(true).Single().transform;
                var bones=rig.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t.name,t=>t);
                var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Rebuild/FWTest/FWWorldZSprite.mat");
                var oldNames=new[]{"F_THIGH_SCREEN_L","F_SHIN_SCREEN_L","F_BOOT_SCREEN_L","F_THIGH_SCREEN_R","F_SHIN_SCREEN_R","F_BOOT_SCREEN_R","F_PELVIS"};
                foreach(var name in oldNames)bones[name].gameObject.SetActive(false);
                var data=JsonUtility.FromJson<Registration>(File.ReadAllText(Art+"/registration.json"));
                foreach(var layer in data.layers)
                {
                    var isLeg=layer.name.Contains("LEG_CONTINUOUS");
                    var side=layer.name.EndsWith("_L",StringComparison.Ordinal)?"L":"R";
                    var skinBones=isLeg?new[]{bones["Thigh"+side],bones["Shin"+side],bones["Boot"+side]}:new[]{bones["Pelvis"]};
                    Create(layer,rig,skinBones,material,isLeg,side);
                }
                PrefabUtility.SaveAsPrefabAsset(root,CatgirlFrontPrototypeBuilder.Visual);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();
            Debug.Log("CATGIRL_CONTINUOUS_LEGS_OK: original prefab preserved at "+backup);
        }
        private static void Create(Layer layer,Transform rig,Transform[] bones,Material material,bool isLeg,string side)
        {
            var path=Art+"/"+layer.name+".png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Default;importer.alphaIsTransparency=true;
            importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.filterMode=FilterMode.Bilinear;
            importer.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var b=layer.bounds_px;const float ppu=635;
            if(texture.width!=b[2]-b[0] || texture.height!=b[3]-b[1])throw new InvalidDataException("Texture bounds mismatch: "+layer.name);
            var part=new GameObject(layer.name).transform;part.SetParent(rig,false);
            part.localPosition=new Vector3(((b[0]+b[2])*.5f-363)/ppu,(1053-(b[1]+b[3])*.5f)/ppu,0);
            var renderer=part.gameObject.AddComponent<SpriteRenderer>();renderer.sharedMaterial=material;
            renderer.sortingOrder=isLeg?(side=="L"?9:10):11;
            var sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),Vector2.one*.5f,ppu,0,SpriteMeshType.FullRect);
            sprite.name=layer.name;
            if(isLeg)WriteLegMesh(sprite);
            sprite.SetBones(bones.Select((bone,i)=>new SpriteBone
            {
                name=bone.name,parentId=i-1,rotation=Quaternion.identity,length=.12f,
                position=i==0?part.InverseTransformPoint(bone.position):bones[i-1].InverseTransformPoint(bone.position)
            }).ToArray());
            var bind=new NativeArray<Matrix4x4>(bones.Length,Allocator.Temp);
            try{for(var i=0;i<bones.Length;i++)bind[i]=bones[i].worldToLocalMatrix*part.localToWorldMatrix;sprite.SetBindPoses(bind);}
            finally{bind.Dispose();}
            SetWeights(sprite,bones,part,isLeg);
            AssetDatabase.CreateAsset(sprite,Sprites+"/"+layer.name+".asset");renderer.sprite=sprite;
            var skin=part.gameObject.AddComponent<SpriteSkin>();skin.SetRootBone(bones[0]);skin.SetBoneTransforms(bones);skin.alwaysUpdate=true;
            var data=new SerializedObject(skin);data.FindProperty("m_Bounds").boundsValue=new Bounds(Vector3.zero,Vector3.one*4);data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void WriteLegMesh(Sprite sprite)
        {
            // Write native serialized channels, not Sprite.OverrideGeometry's runtime override.
            // Shared strip vertices remain continuous through the knee and ankle.
            const int cols=12,rows=48;
            var count=(cols+1)*(rows+1);
            var vertices=new NativeArray<Vector3>(count,Allocator.Temp);
            var uv=new NativeArray<Vector2>(count,Allocator.Temp);
            var triangles=new NativeArray<ushort>(cols*rows*6,Allocator.Temp);
            try
            {
                var width=sprite.rect.width/sprite.pixelsPerUnit;var height=sprite.rect.height/sprite.pixelsPerUnit;
                for(var y=0;y<=rows;y++)for(var x=0;x<=cols;x++)
                {
                    var i=y*(cols+1)+x;
                    vertices[i]=new Vector3(((float)x/cols-.5f)*width,((float)y/rows-.5f)*height,0);
                    uv[i]=new Vector2((float)x/cols,(float)y/rows);
                }
                var p=0;
                for(var y=0;y<rows;y++)for(var x=0;x<cols;x++)
                {
                    var a=y*(cols+1)+x;var c=a+cols+1;
                    triangles[p++]=(ushort)a;triangles[p++]=(ushort)c;triangles[p++]=(ushort)(a+1);
                    triangles[p++]=(ushort)(a+1);triangles[p++]=(ushort)c;triangles[p++]=(ushort)(c+1);
                }
                sprite.SetVertexCount(count);
                sprite.SetVertexAttribute(VertexAttribute.Position,vertices);
                sprite.SetVertexAttribute(VertexAttribute.TexCoord0,uv);
                sprite.SetIndices(triangles);
            }
            finally{vertices.Dispose();uv.Dispose();triangles.Dispose();}
        }
        private static void SetWeights(Sprite sprite,Transform[] bones,Transform part,bool isLeg)
        {
            var positions=sprite.GetVertexAttribute<Vector3>(VertexAttribute.Position);
            var weights=new NativeArray<BoneWeight>(positions.Length,Allocator.Temp);
            try
            {
                var knee=isLeg?part.InverseTransformPoint(bones[1].position).y:0;
                var ankle=isLeg?part.InverseTransformPoint(bones[2].position).y:0;
                for(var i=0;i<weights.Length;i++)
                {
                    if(!isLeg){weights[i]=new BoneWeight{boneIndex0=0,weight0=1};continue;}
                    var y=positions[i].y;
                    var thigh=Smooth(knee-.06f,knee+.06f,y);
                    // Keep the complete boot/cuff rigid under the ankle bone. Blending
                    // through the boot shaft tears the cuff during large knee recovery poses.
                    var boot=1-Smooth(ankle+.04f,ankle+.13f,y);
                    var shin=Mathf.Max(0,1-thigh-boot);var sum=thigh+shin+boot;
                    weights[i]=new BoneWeight{boneIndex0=0,weight0=thigh/sum,boneIndex1=1,weight1=shin/sum,boneIndex2=2,weight2=boot/sum};
                }
                sprite.SetVertexAttribute(VertexAttribute.BlendWeight,weights);
            }
            finally{weights.Dispose();}
        }
        public static void RepairMeshes()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            var root=PrefabUtility.LoadPrefabContents(CatgirlFrontPrototypeBuilder.Visual);
            try
            {
                foreach(var skin in root.GetComponentsInChildren<SpriteSkin>().Where(s=>s.name.StartsWith("F_LEG_CONTINUOUS_")))
                {
                    var sprite=skin.GetComponent<SpriteRenderer>().sprite;
                    WriteLegMesh(sprite);SetWeights(sprite,skin.boneTransforms,skin.transform,true);
                    EditorUtility.SetDirty(sprite);
                    if(sprite.GetVertexCount()!=637)throw new InvalidDataException("Continuous leg mesh must have 637 serialized vertices.");
                }
                AssetDatabase.SaveAssets();
                Debug.Log("CATGIRL_LEG_MESHES_PERSISTED: 637 vertices per leg; native geometry, UVs and mixed weights.");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        private static float Smooth(float a,float b,float value)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,value));
    }
}
