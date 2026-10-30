using System;
using System.IO;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Session;
using Team5.Rebuild.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Team5.Rebuild.Editor
{
    // Editor-only authoring: add mesh children to the existing Scene entities, never clone the map.
    public static class MinimalPresentationSetup
    {
        public const string UnitPrefabPath = "Assets/Rebuild/Prefabs/MinimalUnitView.prefab";
        public const string MaterialFolder = "Assets/Rebuild/Materials/Minimal";

        [MenuItem("Team5/Rebuild/Apply Minimal Presentation")]
        public static void ApplyToOpenPrototype()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring the Scene.");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != PrototypeSceneBuilder.ScenePath)
                throw new InvalidOperationException("Open GridBattlePrototype.unity before applying presentation.");
            Apply(UnityEngine.Object.FindFirstObjectByType<BattleSession>());
        }

        public static void ApplyToSavedPrototype()
        {
            EditorSceneManager.OpenScene(PrototypeSceneBuilder.ScenePath);
            Apply(UnityEngine.Object.FindFirstObjectByType<BattleSession>());
        }

        private static void Apply(BattleSession session)
        {
            if (session == null) throw new InvalidOperationException("No BattleSession in prototype Scene.");
            var errors = session.ValidateConfiguration();
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory("Assets/Rebuild/Prefabs");
            AssetDatabase.Refresh();
            var materials = new Material[6];
            foreach (CellKind kind in Enum.GetValues(typeof(CellKind)))
                materials[(int)kind] = MaterialAsset(kind.ToString(), CellColor(kind));
            var towerMaterial = MaterialAsset("Tower", new Color(0.24f, 0.48f, 0.9f));
            foreach (var cell in session.Grid.SceneCells)
                MeshChild(cell.transform, "Cell Mesh", PrimitiveType.Cube, Vector3.zero, BattleCell.Size, materials[(int)cell.Kind]);
            foreach (var tower in session.Towers)
                MeshChild(tower.transform, "Tower Mesh", PrimitiveType.Cylinder, Vector3.up * 0.5f, new Vector3(0.55f, 0.5f, 0.55f), towerMaterial);

            var prefab = AssetDatabase.LoadAssetAtPath<UnitView>(UnitPrefabPath);
            if (prefab == null)
            {
                UpdateSquareUnitPrefab();
                prefab = AssetDatabase.LoadAssetAtPath<UnitView>(UnitPrefabPath);
            }
            var presentation = session.transform.parent.GetComponentInChildren<BattlePresentation>(true);
            if (presentation == null)
            {
                var root = new GameObject("Minimal Presentation (Runtime Unit Views)");
                root.transform.SetParent(session.transform.parent, false);
                presentation = root.AddComponent<BattlePresentation>();
            }
            var data = new SerializedObject(presentation);
            data.FindProperty("session").objectReferenceValue = session;
            data.FindProperty("unitPrefab").objectReferenceValue = prefab;
            data.ApplyModifiedPropertiesWithoutUndo();

            var camera = session.transform.parent.GetComponentInChildren<Camera>(true);
            camera.name = "Battle Camera";
            camera.transform.position = new Vector3(5.5f, 15, -7.5f);
            camera.transform.LookAt(new Vector3(5.5f, 0, 3.5f));
            camera.orthographic = true;
            camera.orthographicSize = 6.6f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100;
            camera.GetUniversalAdditionalCameraData().SetRenderer(-1); // Existing URP Universal 3D Renderer.
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var lightTransform = session.transform.parent.Find("Presentation Light");
            var lightObject = lightTransform != null ? lightTransform.gameObject : new GameObject("Presentation Light");
            lightObject.transform.SetParent(session.transform.parent, false);
            lightObject.transform.rotation = Quaternion.Euler(50, -35, 0);
            var light = lightObject.GetComponent<Light>();
            if (light == null) light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.None;
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(session.gameObject.scene);
            EditorSceneManager.SaveScene(session.gameObject.scene);
            Debug.Log("Minimal Presentation saved: static mesh children and square unit view prefab are ready.");
        }

        [MenuItem("Team5/Rebuild/Update Square Unit View")]
        public static void UpdateSquareUnitPrefab()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before editing the unit prefab.");
            Directory.CreateDirectory(MaterialFolder);
            var materialPath = MaterialFolder + "/UnitSquare.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "UnitSquare" };
                material.SetColor("_BaseColor", Color.white);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            var exists = AssetDatabase.LoadAssetAtPath<GameObject>(UnitPrefabPath) != null;
            var root = exists ? PrefabUtility.LoadPrefabContents(UnitPrefabPath) : new GameObject("MinimalUnitView");
            try
            {
                if (root.GetComponent<UnitView>() == null) root.AddComponent<UnitView>();
                // Preserve the prefab root/GUID and original child IDs, so all Scene references survive.
                var capsule = root.transform.Find("Capsule Mesh");
                if (capsule != null)
                {
                    capsule.name = "Outline";
                    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    capsule.GetComponent<MeshFilter>().sharedMesh = cube.GetComponent<MeshFilter>().sharedMesh;
                    UnityEngine.Object.DestroyImmediate(cube);
                }
                MeshChild(root.transform, "Outline", PrimitiveType.Cube, Vector3.up * 0.13f, new Vector3(0.8f, 0.26f, 0.8f), material);
                MeshChild(root.transform, "Body", PrimitiveType.Cube, Vector3.up * 0.26f, new Vector3(0.68f, 0.008f, 0.68f), material);
                MeshChild(root.transform, "Mark Diamond", PrimitiveType.Cube, Vector3.up * 0.268f, new Vector3(0.26f, 0.004f, 0.26f), material);
                root.transform.Find("Mark Diamond").localRotation = Quaternion.Euler(0, 45, 0);
                MeshChild(root.transform, "Mark Plate", PrimitiveType.Cube, Vector3.up * 0.268f, new Vector3(0.32f, 0.004f, 0.32f), material);
                MeshChild(root.transform, "Mark Across", PrimitiveType.Cube, Vector3.up * 0.268f, new Vector3(0.44f, 0.004f, 0.14f), material);
                MeshChild(root.transform, "Mark Upright", PrimitiveType.Cube, Vector3.up * 0.269f, new Vector3(0.14f, 0.004f, 0.44f), material);
                root.GetComponent<UnitView>().ApplyAppearance(Team5.Rebuild.Simulation.UnitRole.Attack);
                PrefabUtility.SaveAsPrefabAsset(root, UnitPrefabPath);
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root); else UnityEngine.Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Square unit view prefab saved.");
        }

        private static void MeshChild(Transform parent, string name, PrimitiveType primitive, Vector3 position, Vector3 scale, Material material)
        {
            var existing = parent.Find(name);
            var mesh = existing != null ? existing.gameObject : GameObject.CreatePrimitive(primitive);
            mesh.name = name;
            mesh.transform.SetParent(parent, false);
            mesh.transform.localPosition = position;
            mesh.transform.localRotation = Quaternion.identity;
            mesh.transform.localScale = scale;
            // Views never interact with physics or decide collision/combat outcomes.
            foreach (var collider in mesh.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
            var renderer = mesh.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static Material MaterialAsset(string name, Color color)
        {
            var path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("The existing URP Lit shader is unavailable.");
            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Color CellColor(CellKind kind)
        {
            switch (kind)
            {
                case CellKind.Road: return new Color(0.65f, 0.54f, 0.35f);
                case CellKind.Spawn: return new Color(0.9f, 0.25f, 0.25f);
                case CellKind.Goal: return new Color(0.2f, 0.75f, 0.9f);
                case CellKind.TowerPlatform: return new Color(0.35f, 0.4f, 0.65f);
                case CellKind.Obstacle: return new Color(0.35f, 0.32f, 0.3f);
                default: return new Color(0.38f, 0.53f, 0.39f);
            }
        }
    }
}
