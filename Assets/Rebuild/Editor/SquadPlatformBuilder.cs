using System;
using System.IO;
using System.Linq;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Session;
using Team5.Rebuild.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Team5.Rebuild.Editor
{
    public static class SquadPlatformBuilder
    {
        private const string Folder = "Assets/Rebuild/SquadPlatform";
        public const string ScenePath = "Assets/Rebuild/Scenes/Level1GridBattle.unity";
        public const string PrefabPath = Folder + "/SquadPlatform3x3.prefab";
        [MenuItem("Team5/Rebuild/Level 1/Create squad platform")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Folder); Directory.CreateDirectory("Logs/SquadPlatform"); AssetDatabase.Refresh();
            if (File.Exists(PrefabPath)) throw new InvalidOperationException("Platform already exists; preserving hand edits.");
            var wood = Material("DeckWood", new Color(.43f, .25f, .11f));
            var plank = Material("DeckPlank", new Color(.64f, .43f, .22f));
            var iron = Material("PlatformIron", new Color(.18f, .23f, .27f), .65f);
            var bolt = Material("PlatformBolt", new Color(.55f, .62f, .65f), .7f);
            var root = new GameObject("Squad Platform 3x3");
            Add(root, "Solid block base", new Vector3(0, .225f, 0), new Vector3(3, .45f, 3), wood);
            for (var i = 0; i < 9; i++)
                Add(root, "Deck plank " + i, new Vector3((i - 4) * .3f, .475f, 0), new Vector3(.29f, .05f, 2.8f), plank);
            for (var sign = -1; sign <= 1; sign += 2)
            {
                Add(root, "Side frame X " + sign, new Vector3(sign * 1.45f, .4f, 0), new Vector3(.1f, .2f, 3), iron);
                Add(root, "Side frame Z " + sign, new Vector3(0, .4f, sign * 1.45f), new Vector3(2.8f, .2f, .1f), iron);
                for (var z = -1; z <= 1; z += 2)
                    Add(root, "Corner bolt", new Vector3(sign * 1.44f, .49f, z * 1.44f), new Vector3(.07f, .02f, .07f), bolt);
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); Object.DestroyImmediate(root);
            File.Copy(ScenePath, "Logs/SquadPlatform/Level1BeforePlatform.unity", false);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var owner = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BattlePresentation>(true)).Single();
            var session = owner.Session;
            if (session.ContinuousSquadMovement || session.Route.RequiredWidth != 3) throw new InvalidOperationException("Platform requires a fixed three-wide route.");
            var signature = EditorJsonUtility.ToJson(session);
            var view = owner.gameObject.AddComponent<SquadPlatformView>();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, owner.transform);
            instance.transform.position = session.Route.Cells[0].TopCenter;
            instance.transform.rotation = Quaternion.identity;
            var data = new SerializedObject(view);
            data.FindProperty("owner").objectReferenceValue = owner;
            data.FindProperty("model").objectReferenceValue = instance.transform;
            data.ApplyModifiedPropertiesWithoutUndo();
            if (signature != EditorJsonUtility.ToJson(session)) throw new InvalidOperationException("Session configuration changed.");
            var bounds = instance.GetComponentsInChildren<Renderer>().First().bounds;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            if (Vector3.Distance(bounds.size, new Vector3(3, BattleCell.LayerHeight, 3)) > .001f || instance.GetComponentsInChildren<Collider>().Length != 0)
                throw new InvalidOperationException("Platform must be 3 x 0.5 x 3 without physics colliders.");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            File.WriteAllText("Logs/SquadPlatform/BuildChecks.txt", "Level1: fixed-grid platform mode\nPlatform bounds: 3 x 0.5 x 3\nPhysics colliders: 0\nScene 1: continuous movement unchanged\nSession configuration unchanged\n");
            Debug.Log("Level1 3x3 squad platform created; thickness is one BattleCell (0.5).");
        }

        private static Material Material(string name, Color color, float metallic = 0)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", .2f);
            AssetDatabase.CreateAsset(material, Folder + "/" + name + ".mat"); return material;
        }

        [MenuItem("Team5/Rebuild/Level 1/Style platform blue-white and add rail wheels")]
        public static void RestyleBlueWhite()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Logs/SquadPlatform/BeforeBlueWhite");
            foreach (var name in new[] { "SquadPlatform3x3.prefab", "DeckWood.mat", "DeckPlank.mat", "PlatformIron.mat", "PlatformBolt.mat" })
            {
                var destination = "Logs/SquadPlatform/BeforeBlueWhite/" + name;
                if (!File.Exists(destination)) File.Copy(Folder + "/" + name, destination);
            }
            var blue = Recolor("DeckWood", new Color(.055f,.28f,.64f), .2f);
            var paleBlue = Recolor("DeckPlank", new Color(.55f,.78f,.96f), .15f);
            var white = Recolor("PlatformIron", new Color(.93f,.97f,1f), .3f);
            var wheelBlue = Recolor("PlatformBolt", new Color(.12f,.3f,.55f), .6f);
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var body = root.transform.Find("Raised Body");
                if (body == null)
                {
                    body = new GameObject("Raised Body").transform; body.SetParent(root.transform,false);
                    foreach (var child in root.transform.Cast<Transform>().Where(t => t != body).ToArray()) child.SetParent(body,false);
                }
                body.localPosition = Vector3.up * SquadPlatformView.BodyLift;
                foreach (var renderer in body.GetComponentsInChildren<MeshRenderer>())
                {
                    if (renderer.name.StartsWith("Deck plank "))
                    {
                        var index = int.Parse(renderer.name.Substring("Deck plank ".Length));
                        renderer.sharedMaterial = index % 3 == 1 ? white : paleBlue;
                    }
                    else if (renderer.name.StartsWith("Side frame")) renderer.sharedMaterial = white;
                    else renderer.sharedMaterial = blue;
                }
                var previous = root.transform.Find("Wheel Rig"); if (previous != null) Object.DestroyImmediate(previous.gameObject);
                var rig = new GameObject("Wheel Rig"); rig.transform.SetParent(root.transform,false);
                for (var lane = -1; lane <= 1; lane++)
                for (var end = -1; end <= 1; end += 2)
                {
                    var x = end * .95f;
                    Cylinder(rig, "Axle " + lane + " " + end, new Vector3(x,.192f,lane), new Vector3(.05f,.35f,.05f), wheelBlue);
                    for (var side = -1; side <= 1; side += 2)
                    {
                        var pivot = new GameObject($"Wheel {lane} {end} {side}"); pivot.transform.SetParent(rig.transform,false);
                        pivot.transform.localPosition = new Vector3(x,.192f,lane + side * .25f);
                        Cylinder(pivot, "Blue wheel", Vector3.zero, new Vector3(.36f,.045f,.36f), wheelBlue);
                        Cylinder(pivot, "White hub", Vector3.zero, new Vector3(.17f,.052f,.17f), white);
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var backup = "Logs/SquadPlatform/BeforeBlueWhite/Level1.unity";
            if (!File.Exists(backup)) File.Copy(ScenePath,backup);
            var scene = EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var platform = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SquadPlatformView>(true)).Single();
            var rigInstance = platform.Model.Find("Wheel Rig");
            var wheels = rigInstance.Cast<Transform>().Where(t => t.name.StartsWith("Wheel ")).ToArray();
            var serialized = new SerializedObject(platform);
            serialized.FindProperty("wheelRig").objectReferenceValue = rigInstance;
            var array = serialized.FindProperty("rollingWheels"); array.arraySize = wheels.Length;
            for (var i = 0; i < wheels.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = wheels[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (wheels.Length != 12 || platform.Model.GetComponentsInChildren<Collider>().Length != 0)
                throw new InvalidOperationException("Expected 12 visual wheels and no collision bodies.");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            File.WriteAllText("Logs/SquadPlatform/BlueWhiteBuild.txt", "Blue/white body; body thickness 0.5; lift 0.32; deck height 0.82; 12 wheels aligned with 3 tracks; no colliders.\n");
            Debug.Log("Blue-white raised platform and 12 rail-aligned wheels created.");
        }

        private static Material Recolor(string name, Color color, float metallic)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/" + name + ".mat");
            material.SetColor("_BaseColor",color); material.SetFloat("_Metallic",metallic); material.SetFloat("_Smoothness",.35f);
            EditorUtility.SetDirty(material); return material;
        }

        private static void Cylinder(GameObject root, string name, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cylinder); part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>()); part.transform.SetParent(root.transform,false);
            part.transform.localPosition = position; part.transform.localScale = scale; part.transform.localRotation = Quaternion.Euler(90,0,0);
            part.GetComponent<Renderer>().sharedMaterial = material;
        }
        private static void Add(GameObject root, string name, Vector3 position, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>()); part.transform.SetParent(root.transform, false);
            part.transform.localPosition = position; part.transform.localScale = size;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
