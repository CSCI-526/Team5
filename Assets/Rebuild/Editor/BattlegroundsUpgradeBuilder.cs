using System;
using System.IO;
using Team5.Rebuild.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Team5.Rebuild.Editor
{
    public static class BattlegroundsUpgradeBuilder
    {
        [MenuItem("Tools/Team5/Create Battlegrounds Upgrade Scene")]
        public static void Build()
        {
            if(File.Exists(BattlegroundsUpgradeScene.ScenePath))throw new InvalidOperationException("Scene already exists; preserve manual edits.");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var host=new GameObject("Battlegrounds Upgrade");host.AddComponent<BattlegroundsUpgradeScene>();
            var camera=new GameObject("Tavern UI Camera").AddComponent<Camera>();camera.transform.SetParent(host.transform);
            camera.tag="MainCamera";camera.transform.localPosition=new Vector3(0,0,-10);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.14f,.09f,.08f);
            camera.orthographic=true;camera.orthographicSize=450;camera.nearClipPlane=.1f;camera.farClipPlane=100;
            camera.gameObject.AddComponent<AudioListener>();
            EditorSceneManager.SaveScene(scene,BattlegroundsUpgradeScene.ScenePath);
            AssetDatabase.SaveAssets();Debug.Log("BATTLEGROUNDS_UPGRADE_CREATED");
        }
    }
}
