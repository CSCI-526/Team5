using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Session;
using Team5.Rebuild.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Team5.Rebuild.Editor
{
    // One-time Edit Mode scene authoring. Existing source and expanded scenes are never overwritten.
    public static class MacroPrototypeSceneBuilder
    {
        public const string ScenePath = "Assets/Rebuild/Scenes/GridBattleMacroPrototype.unity";

        [MenuItem("Team5/Rebuild/Create Macro Grid Prototype")]
        public static void Create()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Expanded scene already exists; edit that Scene directly.");
            if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current Scene before authoring a separate expanded scene.");
            EditorSceneManager.OpenScene(PrototypeSceneBuilder.ScenePath);
            var source = UnityEngine.Object.FindFirstObjectByType<BattleSession>();
            var errors = source.ValidateConfiguration();
            if (errors.Count != 0) throw new InvalidOperationException(string.Join("\n", errors));
            var cells = source.Grid.SceneCells.Select(cell => new
            {
                Key = cell.Occupancy, cell.Kind, Material = cell.GetComponentInChildren<MeshRenderer>().sharedMaterial
            }).ToArray();
            var routeKeys = source.Route.Cells.Select(cell => cell.Occupancy).ToArray();
            var towers = source.Towers.Select((tower, i) => new
            {
                Definition = tower.CaptureDefinition(i + 1), Material = tower.GetComponentInChildren<MeshRenderer>().sharedMaterial
            }).ToArray();
            var borderRoot = source.Grid.transform.parent.Find("Decorative Border Tiles");
            var decoration = borderRoot == null ? Array.Empty<(Vector3 position, Material material)>() :
                borderRoot.GetComponentsInChildren<MeshRenderer>().Select(mesh => (position: mesh.transform.position, material: mesh.sharedMaterial)).ToArray();
            var prefabPath = AssetDatabase.GetAssetPath(UnityEngine.Object.FindFirstObjectByType<BattlePresentation>().UnitPrefab);
            if (string.IsNullOrEmpty(prefabPath)) throw new InvalidOperationException("Source scene needs a saved UnitView prefab asset.");
            var sourceCamera = source.transform.parent.GetComponentInChildren<Camera>();
            var cameraRotation = sourceCamera.transform.rotation;
            var cameraFov = sourceCamera.fieldOfView;
            var cameraBackground = sourceCamera.backgroundColor;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Macro Grid Battle Prototype");
            var grid = Child(root, "GameplayGrid").AddComponent<BattleGrid>();
            var lookup = new Dictionary<Vector3Int, BattleCell>();
            foreach (var old in cells)
            for (var z = 0; z < 3; z++)
            for (var x = 0; x < 3; x++)
            {
                var key = new Vector3Int(old.Key.x * 3 + x, old.Key.y, old.Key.z * 3 + z);
                var cell = Child(grid.gameObject, $"Small Cell ({key.x},{key.y},{key.z}) {old.Kind}").AddComponent<BattleCell>();
                cell.transform.position = new Vector3(key.x, key.y * BattleCell.LayerHeight, key.z);
                Set(cell, "kind", (int)old.Kind);
                Mesh(cell.gameObject, "Cell Mesh", PrimitiveType.Cube, Vector3.zero, BattleCell.Size, old.Material);
                lookup.Add(key, cell);
            }

            // Each stored waypoint is a real Small Cell reference, including intermediate centerline cells.
            var centers = routeKeys.Select(key => new Vector3Int(key.x * 3 + 1, key.y, key.z * 3 + 1)).ToArray();
            var routeCells = new List<BattleCell> { lookup[centers[0]] };
            for (var i = 1; i < centers.Length; i++)
            {
                var cursor = centers[i - 1];
                var delta = centers[i] - cursor;
                var direction = new Vector3Int(Math.Sign(delta.x), 0, Math.Sign(delta.z));
                while (cursor != centers[i]) { cursor += direction; routeCells.Add(lookup[cursor]); }
            }
            var route = Child(root, "SceneRoute (3 Wide)").AddComponent<SceneRoute>();
            SetReferences(route, "cells", routeCells.Cast<UnityEngine.Object>().ToArray());
            Set(route, "requiredWidth", 3);
            var towerRoot = Child(root, "Scene Towers (3x3 Footprints)");
            var towerList = new List<BattleTower>();
            foreach (var old in towers)
            {
                var definition = old.Definition;
                var tower = Child(towerRoot, $"Tower {definition.Id} (3x3)").AddComponent<BattleTower>();
                tower.transform.position = new Vector3(Mathf.RoundToInt(definition.Position.x) * 3 + 1,
                    definition.Position.y, Mathf.RoundToInt(definition.Position.z) * 3 + 1);
                Set(tower, "usesMacroFootprint", true);
                // World-distance rebalance: 4.5, deliberately not the old range times three (7.2).
                Set(tower, "range", 4.5f);
                Set(tower, "damage", definition.Damage);
                Set(tower, "attackInterval", definition.AttackInterval);
                Set(tower, "targetRule", (int)definition.TargetRule);
                Mesh(tower.gameObject, "Tower Mesh", PrimitiveType.Cube, Vector3.up * 0.6f, new Vector3(2.8f, 1.2f, 2.8f), old.Material);
                towerList.Add(tower);
            }
            var decorativeRoot = Child(root, "Decorative Border Tiles");
            decorativeRoot.layer = 2;
            foreach (var old in decoration)
            for (var z = 0; z < 3; z++)
            for (var x = 0; x < 3; x++)
            {
                var position = new Vector3(old.position.x * 3 + x, old.position.y, old.position.z * 3 + z);
                var mesh = Mesh(decorativeRoot, $"Border Tile ({position.x},{position.z})", PrimitiveType.Cube,
                    position, BattleCell.Size, old.material);
                mesh.layer = 2;
            }
            var session = Child(root, "BattleSession (Nine Independent Units)").AddComponent<BattleSession>();
            Set(session, "grid", grid);
            Set(session, "route", route);
            Set(session, "unitCount", 9);
            Set(session, "formationSize", 3);
            Set(session, "unitSpeed", 2.4f);
            Set(session, "spawnInterval", 3f);
            Set(session, "goalLives", 9);
            SetReferences(session, "towers", towerList.Cast<UnityEngine.Object>().ToArray());
            var presentation = Child(root, "Minimal Presentation (Runtime Unit Views)").AddComponent<BattlePresentation>();
            Set(presentation, "session", session);
            // Resolve the asset again after switching scenes; do not retain the old scene's loaded component reference.
            var prefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var prefab = prefabObject != null ? prefabObject.GetComponent<UnitView>() : null;
            if (prefab == null) throw new InvalidOperationException($"UnitView prefab could not be loaded: {prefabPath}");
            Set(presentation, "unitPrefab", prefab);
            var gizmos = Child(root, "Optional Debug Gizmos").AddComponent<BattleDebugGizmos>();
            Set(gizmos, "session", session);
            var light = Child(root, "Presentation Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.None;
            light.transform.rotation = Quaternion.Euler(50, -35, 0);
            var camera = Child(root, "Battle Camera (Perspective 16x9)").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = false;
            camera.fieldOfView = cameraFov;
            camera.transform.rotation = cameraRotation;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = cameraBackground;
            camera.GetUniversalAdditionalCameraData().SetRenderer(-1);
            FitCamera(camera, root);
            errors = session.ValidateConfiguration();
            if (errors.Count != 0) throw new InvalidOperationException(string.Join("\n", errors));
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Failed to save macro scene.");
            AssetDatabase.SaveAssets();
            var buildScenes = EditorBuildSettings.scenes.Where(entry => entry.path != ScenePath).ToList();
            buildScenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = buildScenes.ToArray();
            Debug.Log($"Saved {ScenePath}: {lookup.Count} Small Cells, {routeCells.Count} real route references, 3 macro towers, 9-unit formation, {decoration.Length * 9} decorative tiles.");
        }

        [MenuItem("Team5/Rebuild/Open Macro Grid Prototype")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        private static void FitCamera(Camera camera, GameObject root)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            var tanVertical = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var tanHorizontal = tanVertical * (16f / 9f);
            var distance = 0f;
            for (var z = -1; z <= 1; z += 2)
            for (var y = -1; y <= 1; y += 2)
            for (var x = -1; x <= 1; x += 2)
            {
                var offset = Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                var depth = Vector3.Dot(offset, camera.transform.forward);
                distance = Mathf.Max(distance, Mathf.Abs(Vector3.Dot(offset, camera.transform.right)) * 1.08f / tanHorizontal - depth,
                    Mathf.Abs(Vector3.Dot(offset, camera.transform.up)) * 1.08f / tanVertical - depth);
            }
            camera.transform.position = bounds.center - camera.transform.forward * (distance + 2);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = distance + bounds.size.magnitude + 20;
        }

        private static GameObject Child(GameObject parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            return child;
        }
        private static GameObject Mesh(GameObject parent, string name, PrimitiveType type, Vector3 position, Vector3 size, Material material)
        {
            var child = GameObject.CreatePrimitive(type);
            child.name = name;
            child.transform.SetParent(parent.transform, false);
            child.transform.localPosition = position;
            child.transform.localScale = size;
            UnityEngine.Object.DestroyImmediate(child.GetComponent<Collider>());
            var renderer = child.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return child;
        }
        private static void Set(UnityEngine.Object target, string field, object value)
        {
            var data = new SerializedObject(target);
            var property = data.FindProperty(field);
            if (value is bool boolean) property.boolValue = boolean;
            else if (value is int integer) property.intValue = integer;
            else if (value is float number) property.floatValue = number;
            else property.objectReferenceValue = (UnityEngine.Object)value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void SetReferences(UnityEngine.Object target, string field, UnityEngine.Object[] values)
        {
            var data = new SerializedObject(target);
            var property = data.FindProperty(field);
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
