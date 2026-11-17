using System;
using System.IO;
using System.Linq;
using Team5.Rebuild.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Team5.Rebuild.Editor
{
    public static class SiegeRamBuilder
    {
        public const string Folder="Assets/Rebuild/SiegeRam";
        public const string Visual=Folder+"/SiegeRam2D.prefab";
        [MenuItem("Team5/Rebuild/Siege Ram/Build and enable in Scene 1 (once)")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            if(File.Exists(Visual))throw new InvalidOperationException("Siege Ram already built; refusing to overwrite assets.");
            AssetDatabase.Refresh();
            var icon=Import(Folder+"/SiegeRamIconOriginal.png",false);
            var sprite=Import(Folder+"/SiegeRamTransparent.png",true);
            var root=new GameObject("Siege Ram 2D");
            try
            {
                var view=root.AddComponent<SiegeRamView>();
                var body=new GameObject("Mirrored Vehicle Sprite").AddComponent<SpriteRenderer>();body.transform.SetParent(root.transform,false);
                body.sprite=sprite;body.flipX=true;
                var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Rebuild/FWTest/FWWorldZSprite.mat");body.sharedMaterial=material;
                var data=new SerializedObject(view);data.FindProperty("body").objectReferenceValue=body;data.FindProperty("worldMaterial").objectReferenceValue=material;data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,Visual);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
            Directory.CreateDirectory("Logs/SiegeRam");const string path="Assets/Rebuild/Scenes/1.unity";
            File.Copy(path,"Logs/SiegeRam/Scene1BeforeSiegeRam.unity",false);
            var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                var owner=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BattlePresentation>(true)).Single();
                if(owner.GetComponent<SiegeRamFleet>()!=null)throw new InvalidOperationException("Scene already has a ram controller.");
                var controller=owner.gameObject.AddComponent<SiegeRamFleet>();var data=new SerializedObject(controller);
                data.FindProperty("owner").objectReferenceValue=owner;data.FindProperty("icon").objectReferenceValue=icon;
                data.FindProperty("vehiclePrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<SiegeRamView>(Visual);
                data.ApplyModifiedPropertiesWithoutUndo();EditorSceneManager.SaveScene(scene);
            }
            finally{EditorSceneManager.CloseScene(scene,true);}
            AssetDatabase.SaveAssets();Debug.Log("SIEGE_RAM_BUILT: Scene 1 summoning slots and independent route planning enabled.");
        }
        private static Sprite Import(string path,bool world)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.alphaIsTransparency=true;importer.mipmapEnabled=world;
            importer.npotScale=TextureImporterNPOTScale.None;importer.wrapMode=TextureWrapMode.Clamp;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.filterMode=world?FilterMode.Trilinear:FilterMode.Bilinear;
            importer.maxTextureSize=2048;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;
            if(world)
            {
                var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
                try
                {
                    texture.LoadImage(File.ReadAllBytes(path));var pixels=texture.GetPixels32();var bottom=0;
                    for(var y=0;y<texture.height;y++){if(Enumerable.Range(0,texture.width).Any(x=>pixels[y*texture.width+x].a>64)){bottom=y;break;}}
                    settings.spritePixelsPerUnit=texture.width/2.2f;settings.spriteAlignment=(int)SpriteAlignment.Custom;
                    settings.spritePivot=new Vector2(.5f,(float)bottom/texture.height);
                }
                finally{UnityEngine.Object.DestroyImmediate(texture);}
            }
            importer.SetTextureSettings(settings);importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
