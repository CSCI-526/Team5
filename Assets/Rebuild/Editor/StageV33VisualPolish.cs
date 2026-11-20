using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Team5.Rebuild.Editor
{
    /// <summary>
    /// Visual-only fix for the Stage V3 maps. Does not alter a BattleCell's transform,
    /// Kind, collider, or route, nor any tower's combat configuration.
    /// Re-run this after any V3 map rebuild (which replaces the tile prefab instances).
    /// </summary>
    public static class StageV33VisualPolish
    {
        private const string RootName = "Stage V3.3 Seamless Terrain";
        private const string MaterialsFolder = "Assets/Rebuild/Materials";
        private const string BackupFolder = "Logs/StageV33VisualBackups";
        private const string SceneFolder = "Assets/Rebuild/Scenes/";
        private const float OuterSize = 200f; // Beyond the largest overview/follow frustum.
        private const float Margin = 1.5f;
        private static readonly Color WorldColor = new Color(.205f, .365f, .294f, 1f);
        private static readonly Color GrassColor = new Color(.373f, .553f, .443f, 1f);
        private static readonly Color RouteColor = new Color(.395f, .566f, .460f, 1f);
        private static readonly Color HedgeColor = new Color(.260f, .422f, .335f, 1f);
        private static readonly Color RimColor = new Color(.250f, .407f, .329f, 1f);
        private static readonly Color TowerBaseColor = new Color(.319f, .473f, .428f, 1f);

        [MenuItem("Team5/Rebuild/Polish V3 Maps - Fix Background and Camera (1-7)")]
        public static void ApplyAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before editing scenes.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var previous = EditorSceneManager.GetSceneManagerSetup();
            var fixedCount = 0;
            try
            {
                Directory.CreateDirectory(BackupFolder);
                for (var stage = 1; stage <= 7; stage++)
                {
                    var path = SceneFolder + "Level" + stage + "GridBattle.unity";
                    if (!File.Exists(path))
                    {
                        Debug.LogWarning("Stage V3.3: skipping missing scene " + path);
                        continue;
                    }
                    try
                    {
                        // Never overwrite backups; each run gets its own timestamped snapshot.
                        var copy = BackupFolder + "/Level" + stage + "GridBattle_" +
                            DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".unity";
                        File.Copy(path, copy, false);
                        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                        Polish(scene, stage);
                        EditorSceneManager.MarkSceneDirty(scene);
                        if (!EditorSceneManager.SaveScene(scene, path))
                            throw new IOException("Unable to save " + path);
                        fixedCount++;
                        Debug.Log("Stage V3.3 polished: " + path + " (backup: " + copy + ")");
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("Stage V3.3 failed on level " + stage + ": " + e);
                    }
                }
                AssetDatabase.SaveAssets();
            }
            finally
            {
                // Return to the same open scenes the user had before the editor command.
                if (previous != null && previous.Length > 0)
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
            Debug.Log("Stage V3.3: completed " + fixedCount + "/7 scenes. No gameplay rules were modified.");
        }

        // Public so it can also be called from an existing stage-authoring workflow.
        public static void Polish(Scene scene, int stage)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                throw new ArgumentException("Open the battle scene first.");
            var cells = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BattleCell>(true)).ToArray();
            if (cells.Length == 0) throw new InvalidOperationException("No BattleCells found in " + scene.name);
            var minX = cells.Min(c => c.transform.position.x);
            var maxX = cells.Max(c => c.transform.position.x);
            var minZ = cells.Min(c => c.transform.position.z);
            var maxZ = cells.Max(c => c.transform.position.z);
            var top = cells.Min(c => c.TopCenter.y);
            var center = new Vector3((minX + maxX) * .5f, top, (minZ + maxZ) * .5f);

            RemoveOutdatedBorder(scene);
            MakeSeamlessWorld(scene, center, top);
            TintTiles(cells, minX, maxX, minZ, maxZ);
            CenterCamera(scene, stage, center, minX, maxX, minZ, maxZ, top);
            Verify(scene, cells, center);
        }

        private static void RemoveOutdatedBorder(Scene scene)
        {
            // The old dressing script produced four disconnected Ground slabs around
            // the grid. Keep decorative trees/rocks, but replace those slabs with one floor.
            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                if (root.name == RootName) Object.DestroyImmediate(root);
                if (root.name != "Paint Squad Dressing") continue;
                var ground = root.transform.Find("Ground");
                if (ground != null) Object.DestroyImmediate(ground.gameObject);
            }

            // Legacy border cubes can coexist with V3 generated cells and cause the
            // gray offset slab visible in the screenshot. Remove only this known art root.
            var transforms = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            foreach (var t in transforms)
            {
                if (t == null || t.name != "Decorative Border Tiles") continue;
                if (t.GetComponentInChildren<BattleCell>(true) != null ||
                    t.GetComponentInChildren<BattleTower>(true) != null) continue;
                Object.DestroyImmediate(t.gameObject);
            }
        }

        private static void MakeSeamlessWorld(Scene scene, Vector3 center, float groundTop)
        {
            var host = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(host, scene);
            host.layer = 2; // Ignore Raycast; mouse movement still targets BattleCells.

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Continuous Grass Backdrop";
            floor.transform.SetParent(host.transform, false);
            floor.transform.position = new Vector3(center.x, groundTop - .10f, center.z);
            floor.transform.localScale = new Vector3(OuterSize, .16f, OuterSize);
            floor.layer = 2;
            var collider = floor.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            var renderer = floor.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = Material("World", WorldColor);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void TintTiles(BattleCell[] cells, float minX, float maxX, float minZ, float maxZ)
        {
            var grass = Material("Grass", GrassColor);
            var path = Material("QuietPath", RouteColor);
            var hedge = Material("Cover", HedgeColor);
            var rim = Material("Rim", RimColor);
            var towerBase = Material("TowerBase", TowerBaseColor);
            foreach (var cell in cells)
            {
                Material target;
                switch (cell.Kind)
                {
                    case CellKind.Ground: target = grass; break;
                    case CellKind.Road: target = path; break;
                    case CellKind.Obstacle:
                    {
                        var p = cell.transform.position;
                        target = p.x <= minX + .01f || p.x >= maxX - .01f ||
                            p.z <= minZ + .01f || p.z >= maxZ - .01f ? rim : hedge;
                        break;
                    }
                    case CellKind.TowerPlatform: target = towerBase; break;
                    default: continue; // Preserve START/GOAL colors and markers.
                }

                // Modify per-scene renderer overrides, never the shared prefab or material asset.
                foreach (var mesh in cell.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var count = Math.Max(1, mesh.sharedMaterials.Length);
                    var materials = Enumerable.Repeat(target, count).ToArray();
                    mesh.sharedMaterials = materials;
                    if (PrefabUtility.IsPartOfPrefabInstance(mesh))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(mesh);
                }
                foreach (var sprite in cell.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    // Don't recolor UI/goal sprites; terrain tint only.
                    if (sprite.sprite == null) continue;
                    sprite.color = cell.Kind == CellKind.Obstacle ? HedgeColor : GrassColor;
                    if (PrefabUtility.IsPartOfPrefabInstance(sprite))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(sprite);
                }
            }
        }

        private static Material Material(string name, Color tint)
        {
            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
                AssetDatabase.CreateFolder("Assets/Rebuild", "Materials");
            var path = MaterialsFolder + "/StageV33_" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader == null) throw new InvalidOperationException("No supported terrain shader found.");
                material = new Material(shader) { name = "StageV33_" + name };
                AssetDatabase.CreateAsset(material, path);
            }
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
            if (material.HasProperty("_Color")) material.SetColor("_Color", tint);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CenterCamera(Scene scene, int stage, Vector3 center,
            float minX, float maxX, float minZ, float maxZ, float top)
        {
            var cameras = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Camera>(true)).ToArray();
            var camera = cameras.FirstOrDefault(c => c.CompareTag("MainCamera")) ??
                cameras.FirstOrDefault(c => c.enabled) ?? cameras.FirstOrDefault();
            if (camera == null) throw new InvalidOperationException("Scene has no camera to frame.");

            // Fit the true playable cell bounds, using the current Game view aspect ratio.
            // The original V3 editor used a hard-coded 16:9 ratio, so other views clipped.
            var aspect = camera.aspect > .01f ? camera.aspect : 16f / 9f;
            var rotation = Quaternion.Inverse(camera.transform.rotation);
            var left = minX - .5f - Margin;
            var right = maxX + .5f + Margin;
            var bottom = minZ - .5f - Margin;
            var upper = maxZ + .5f + Margin;
            var groundCenter = new Vector3(center.x, top + .75f, center.z);
            var tanV = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f);
            tanV = Mathf.Max(.01f, tanV);
            var distance = 6f;
            var ortho = 1f;
            foreach (var x in new[] { left, right })
            foreach (var z in new[] { bottom, upper })
            foreach (var y in new[] { top - .2f, top + 2.0f })
            {
                var local = rotation * (new Vector3(x, y, z) - groundCenter);
                distance = Mathf.Max(distance,
                    Mathf.Abs(local.x) / (tanV * aspect) - local.z,
                    Mathf.Abs(local.y) / tanV - local.z);
                ortho = Mathf.Max(ortho, Mathf.Abs(local.y), Mathf.Abs(local.x) / aspect);
            }
            camera.transform.position = groundCenter - camera.transform.forward * (distance * 1.08f + 1f);
            if (camera.orthographic) camera.orthographicSize = ortho * 1.08f + .45f;
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, distance + 250f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = WorldColor;

            var follow = camera.GetComponent<Level3SquadCamera>();
            if (follow != null)
            {
                // Stage 1 and 2 are small guided maps; always show their complete layout.
                // Later stages retain gentle follow, with considerably less zoom/cropping.
                follow.enabled = stage >= 3;
                var serialized = new SerializedObject(follow);
                var zoom = serialized.FindProperty("zoomFactor");
                if (zoom != null) zoom.floatValue = 1.16f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (PrefabUtility.IsPartOfPrefabInstance(follow))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(follow);
            }
            if (PrefabUtility.IsPartOfPrefabInstance(camera.transform))
                PrefabUtility.RecordPrefabInstancePropertyModifications(camera.transform);
            if (PrefabUtility.IsPartOfPrefabInstance(camera))
                PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
        }

        private static void Verify(Scene scene, BattleCell[] cells, Vector3 center)
        {
            var floors = scene.GetRootGameObjects().Where(r => r.name == RootName).ToArray();
            if (floors.Length != 1) throw new InvalidOperationException("Missing or duplicate seamless-world root.");
            var bg = floors[0].GetComponentInChildren<MeshRenderer>(true);
            if (bg == null || bg.GetComponent<Collider>() != null)
                throw new InvalidOperationException("Backdrop missing or blocking mouse input.");
            if (Mathf.Abs(bg.transform.position.x - center.x) > .001f ||
                Mathf.Abs(bg.transform.position.z - center.z) > .001f)
                throw new InvalidOperationException("Backdrop is not aligned with the map grid.");
            if (cells.Any(c => c.transform.lossyScale != Vector3.one))
                Debug.LogWarning("Stage V3.3: this map has non-unit cell scales; check existing scene validation.");
        }
    }
}
