using System;
using System.IO;
using System.Linq;
using Team5.Rebuild.Editor.LevelAuthoring;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Team5.Rebuild.Editor
{
    public static class MapMakerTowerVisualUpgrade
    {
        [MenuItem("Team5/Rebuild/Map Builder/Use new 3x3 tower visuals")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            Directory.CreateDirectory("Logs/MapMakerTowerVisuals/Before");
            foreach(var suffix in new[]{"NegativeX","PositiveX","NegativeZ","PositiveZ","Square"})
            {
                var path=MapBuilderUtility.Kit+"Towers/Tower_"+suffix+".prefab";
                var backup="Logs/MapMakerTowerVisuals/Before/Tower_"+suffix+".prefab";
                if(!File.Exists(backup))File.Copy(path,backup);
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var tower=root.GetComponent<BattleTower>();var configuration=JsonUtility.ToJson(tower);
                    var old=root.transform.Find("Tower Mesh");if(old!=null)old.gameObject.SetActive(false);
                    var visual=root.transform.Find("Tower Visual");
                    if(visual==null)
                    {
                        var visualPath="Assets/Rebuild/Prefabs/TowerVisuals/"+(suffix=="Square"?"SquareTower_Visual":"DirectionalTower_Visual")+".prefab";
                        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(visualPath);
                        if(prefab==null)throw new InvalidOperationException("Missing new tower visual: "+visualPath);
                        visual=((GameObject)PrefabUtility.InstantiatePrefab(prefab,root.transform)).transform;visual.name="Tower Visual";
                    }
                    visual.localPosition=Vector3.zero;visual.localScale=Vector3.one;
                    var yaw=suffix switch {"PositiveX"=>180f,"NegativeZ"=>270f,"PositiveZ"=>90f,_=>0f};
                    visual.localRotation=Quaternion.Euler(0,yaw,0);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(visual.gameObject);
                    if(configuration!=JsonUtility.ToJson(tower))throw new InvalidOperationException("Tower rules changed: "+path);
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            AssetDatabase.SaveAssets();
            var scene=EditorSceneManager.OpenScene(Level3MapCompatibilityBuilder.ScenePath,OpenSceneMode.Single);
            var session=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BattleSession>(true)).Single();
            var before=Level3MapCompatibilityBuilder.TowerSignature(session);
            foreach(var tower in session.Towers)
                if(tower.transform.Find("Tower Visual")==null)throw new InvalidOperationException("Tower still lacks new model: "+tower.name);
            var draft=MapBuilderUtility.Read(scene);
            try
            {
                var errors=MapBuilderUtility.Validate(draft).Where(s=>s.StartsWith("ERROR:")).ToArray();
                if(errors.Length>0)throw new InvalidOperationException(string.Join("\n",errors));
                MapBuilderUtility.ApplyToScene(draft,scene);
                if(before!=Level3MapCompatibilityBuilder.TowerSignature(session))throw new InvalidOperationException("Read/apply changed tower rules.");
            }
            finally{Object.DestroyImmediate(draft);}
            var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).First(c=>c.CompareTag("MainCamera"));
            if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                var old=camera.targetTexture;var active=RenderTexture.active;
                var target=new RenderTexture(1600,1000,24);var image=new Texture2D(1600,1000,TextureFormat.RGBA32,false);
                try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes("Logs/MapMakerTowerVisuals/Level3.png",image.EncodeToPNG());}
                finally{camera.targetTexture=old;RenderTexture.active=active;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(image);}
            }
            File.WriteAllText("Logs/MapMakerTowerVisuals/Checks.txt","Existing five Map Maker 3x3 tower prefabs use the new directional/square visuals.\nOld cube children disabled.\nBattleTower root components and combat settings preserved.\nLevel3 Read/Validate/Apply passed.\n");
            Debug.Log("Map Maker towers now use the new tower models; Level3 compatibility retained.");
        }
    }
}
