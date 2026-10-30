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
using UnityEngine.SceneManagement;

namespace Team5.Rebuild.Editor
{
    // Authoring utility only. Never called by Play Mode or a runtime assembly.
    public static class PrototypeSceneBuilder
    {
        public const string ScenePath = "Assets/Rebuild/Scenes/GridBattlePrototype.unity";

        [MenuItem("Team5/Rebuild/Open Grid Battle Prototype")]
        public static void OpenPrototype()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) CreatePrototype();
            else EditorSceneManager.OpenScene(ScenePath);
            FrameScene();
        }

        // Used by the initial batch authoring run. Existing work is never regenerated.
        public static void CreatePrototype()
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Prototype already exists; edit its Scene directly.");
            Directory.CreateDirectory("Assets/Rebuild/Scenes");
            Directory.CreateDirectory("Assets/Rebuild/Prefabs");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Grid Battle Prototype");
            var grid = Child(root, "BattleGrid").AddComponent<BattleGrid>();
            var map = new Dictionary<Vector2Int, BattleCell>();
            var routeCoordinates = new List<Vector2Int>();
            for (var x = 0; x <= 4; x++) routeCoordinates.Add(new Vector2Int(x, 2));
            for (var z = 3; z <= 5; z++) routeCoordinates.Add(new Vector2Int(4, z));
            for (var x = 5; x <= 8; x++) routeCoordinates.Add(new Vector2Int(x, 5));
            for (var z = 4; z >= 2; z--) routeCoordinates.Add(new Vector2Int(8, z));
            for (var x = 9; x <= 11; x++) routeCoordinates.Add(new Vector2Int(x, 2));
            var platforms = new[] { new Vector2Int(2, 3), new Vector2Int(6, 4), new Vector2Int(10, 3), new Vector2Int(1, 5) };
            for (var z = 0; z < 8; z++)
            for (var x = 0; x < 12; x++)
            {
                var coordinate = new Vector2Int(x, z);
                var kind = routeCoordinates.Contains(coordinate) ? CellKind.Road : CellKind.Ground;
                if (coordinate == routeCoordinates[0]) kind = CellKind.Spawn;
                if (coordinate == routeCoordinates[routeCoordinates.Count - 1]) kind = CellKind.Goal;
                if (platforms.Contains(coordinate)) kind = CellKind.TowerPlatform;
                map.Add(coordinate, CreateCell(grid.gameObject, x, 0, z, kind));
            }
            CreateCell(grid.gameObject, 2, 1, 6, CellKind.Ground);
            CreateCell(grid.gameObject, 2, 2, 6, CellKind.Obstacle);
            CreateCell(grid.gameObject, 3, 1, 6, CellKind.Obstacle);
            CreateCell(grid.gameObject, 10, 1, 6, CellKind.Obstacle);
            var route = Child(root, "SceneRoute").AddComponent<SceneRoute>();
            SetReferences(route, "cells", routeCoordinates.Select(value => (UnityEngine.Object)map[value]).ToArray());
            var towerRoot = Child(root, "Scene Towers");
            var towerList = new List<BattleTower>();
            for (var i = 0; i < 3; i++)
            {
                var tower = Child(towerRoot, $"Tower {i + 1}").AddComponent<BattleTower>();
                tower.transform.position = map[platforms[i]].TopCenter;
                towerList.Add(tower);
            }
            var session = Child(root, "BattleSession").AddComponent<BattleSession>();
            var sessionData = new SerializedObject(session);
            sessionData.FindProperty("grid").objectReferenceValue = grid;
            sessionData.FindProperty("route").objectReferenceValue = route;
            sessionData.ApplyModifiedPropertiesWithoutUndo();
            SetReferences(session, "towers", towerList.Cast<UnityEngine.Object>().ToArray());
            var debug = Child(root, "Optional Debug Gizmos").AddComponent<BattleDebugGizmos>();
            var debugData = new SerializedObject(debug);
            debugData.FindProperty("session").objectReferenceValue = session;
            debugData.ApplyModifiedPropertiesWithoutUndo();
            // A plain camera makes Game View Gizmos usable; combat does not reference it.
            var camera = Child(root, "Debug Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(5.5f, 15, -7.5f);
            camera.transform.LookAt(new Vector3(5.5f, 0, 3.5f));
            camera.orthographic = true;
            camera.orthographicSize = 7.8f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.1f, 0.14f);
            camera.farClipPlane = 100;
            var errors = session.ValidateConfiguration();
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Failed to save prototype Scene.");
            AssetDatabase.SaveAssets();
            var buildScenes = EditorBuildSettings.scenes.Where(value => value.path != ScenePath).ToList();
            buildScenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = buildScenes.ToArray();
            Debug.Log($"Created {ScenePath}: {grid.SceneCells.Length} static cells, {route.Cells.Count} route references, {towerList.Count} towers.");
            VerifySavedScene();
        }

        public static void VerifySavedScene()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var session = UnityEngine.Object.FindFirstObjectByType<BattleSession>();
            var errors = session.ValidateConfiguration();
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            if (session.Grid.SceneCells.Length != 100 || session.Towers.Count != 3)
                throw new InvalidOperationException("Unexpected saved Scene contents.");
            Debug.Log("Saved Scene reopened and validated successfully.");
        }

        private static void FrameScene()
        {
            SceneView.lastActiveSceneView?.LookAt(new Vector3(5.5f, 0, 3.5f), Quaternion.Euler(55, 0, 0), 12);
        }
        private static GameObject Child(GameObject parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            return child;
        }
        private static BattleCell CreateCell(GameObject parent, int x, int layer, int z, CellKind kind)
        {
            var cell = Child(parent, $"Cell ({x},{layer},{z}) {kind}").AddComponent<BattleCell>();
            cell.transform.position = new Vector3(x, layer * BattleCell.LayerHeight, z);
            var data = new SerializedObject(cell);
            data.FindProperty("kind").enumValueIndex = (int)kind;
            data.ApplyModifiedPropertiesWithoutUndo();
            return cell;
        }
        private static void SetReferences(UnityEngine.Object target, string field, UnityEngine.Object[] values)
        {
            var data = new SerializedObject(target);
            var list = data.FindProperty(field);
            list.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
