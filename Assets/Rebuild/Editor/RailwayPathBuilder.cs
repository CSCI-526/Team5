using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Team5.Rebuild.Session;
using Team5.Rebuild.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Team5.Rebuild.Editor
{
    public static class RailwayPathBuilder
    {
        private const string ScenePath = "Assets/Rebuild/Scenes/Level1GridBattle.unity";
        private const string Folder = "Assets/Rebuild/RailwayPath";
        private const string RootName = "Railway Path Artwork";

        [MenuItem("Team5/Rebuild/Level 1/Rebuild flat railway artwork")]
        public static void Build() => BuildScene(ScenePath, "Logs/RailwayPath");

        public static void BuildScene(string scenePath, string outputDirectory)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            AssetDatabase.Refresh();
            var straight = MaterialFor("RailStraight");
            var corner = MaterialFor("RailCorner");
            Directory.CreateDirectory(outputDirectory);
            var backup = outputDirectory + "/" + Path.GetFileNameWithoutExtension(scenePath) + "BeforeRails.unity";
            if (!File.Exists(backup)) File.Copy(scenePath, backup);
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var session = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BattleSession>(true)).Single();
            var route = session.Route;
            var signature = Signature(scene, session);
            Capture(scene, "Before", outputDirectory);
            var existing = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
            if (existing != null) Object.DestroyImmediate(existing);
            var root = new GameObject(RootName);
            var corners = 0;
            var gridErrors = new List<string>();
            session.Grid.RebuildIndex(gridErrors);
            if (gridErrors.Count > 0) throw new InvalidOperationException(string.Join("\n", gridErrors));
            for (var lane = -1; lane <= 1; lane++)
            {
              var points = LanePoints(route.Cells, lane);
              for (var i = 0; i < points.Count; i++)
              {
                var key = Team5.Rebuild.Simulation.SmallCellOccupancy.PositionToCell(points[i]);
                if (!session.Grid.TryGetCell(key, out var cell) || !cell.IsRouteSurface)
                    throw new InvalidOperationException($"Rail lane {lane} leaves the road at {key}.");
                // Keep the colored Spawn/Goal markers completely unobscured.
                if (cell.Kind != CellKind.Road) continue;
                var connections = new List<Vector3>();
                if (i > 0) connections.Add((points[i - 1] - points[i]).normalized);
                if (i + 1 < points.Count) connections.Add((points[i + 1] - points[i]).normalized);
                var curved = connections.Count == 2 && Mathf.Abs(Vector3.Dot(connections[0], connections[1])) < .01f;
                var yaw = 0f;
                if (curved)
                {
                    // Corner texture connects local -X and -Z. Rotate to match both neighbors.
                    for (var turn = 0; turn < 4; turn++)
                    {
                        var rotation = Quaternion.Euler(0, turn * 90, 0);
                        var left = rotation * Vector3.left;
                        var back = rotation * Vector3.back;
                        if ((Vector3.Dot(left, connections[0]) > .99f && Vector3.Dot(back, connections[1]) > .99f) ||
                            (Vector3.Dot(left, connections[1]) > .99f && Vector3.Dot(back, connections[0]) > .99f))
                        { yaw = turn * 90; break; }
                    }
                    corners++;
                }
                else if (connections.Count > 0 && Mathf.Abs(connections[0].x) > .5f) yaw = 90;
                var tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
                tile.name = $"Rail Lane {lane} {(curved ? "Corner" : "Straight")} {i:00}";
                Object.DestroyImmediate(tile.GetComponent<Collider>());
                tile.transform.SetParent(root.transform, false);
                tile.transform.position = cell.TopCenter + Vector3.up * .012f;
                tile.transform.rotation = Quaternion.Euler(90, yaw, 0);
                var renderer = tile.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = curved ? corner : straight;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            }
            if (signature != Signature(scene, session)) throw new InvalidOperationException("Original map, units or camera changed.");
            if (root.GetComponentsInChildren<Collider>().Length != 0) throw new InvalidOperationException("Railway artwork must have no colliders.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Capture(scene, "After", outputDirectory);
            File.WriteAllText(outputDirectory + "/Verification.txt", $"Scene: {scenePath}\nFlat texture tiles: {root.transform.childCount}\nCorner tiles: {corners}\nColliders: 0\nOriginal map/route/roster/camera signature preserved: true\n");
            Debug.Log($"Flat railway artwork built: {root.transform.childCount} tiles, {corners} corners; original scene configuration preserved.");
        }

        private static List<Vector3> LanePoints(IReadOnlyList<BattleCell> cells, int lane)
        {
            var anchors = new List<Vector3>();
            for (var i = 0; i < cells.Count; i++)
            {
                var position = cells[i].TopCenter;
                var before = i > 0 ? (position - cells[i - 1].TopCenter).normalized : (cells[1].TopCenter - position).normalized;
                var after = i + 1 < cells.Count ? (cells[i + 1].TopCenter - position).normalized : before;
                var offset = new Vector3(-before.z, 0, before.x);
                if (Vector3.Dot(before, after) < .01f) offset += new Vector3(-after.z, 0, after.x);
                var point = position + offset * lane;
                if (anchors.Count == 0 || (point - anchors[anchors.Count - 1]).sqrMagnitude > .0001f) anchors.Add(point);
            }
            var points = new List<Vector3> { anchors[0] };
            for (var i = 1; i < anchors.Count; i++)
            {
                var delta = anchors[i] - anchors[i - 1];
                if (Mathf.Abs(delta.x) > .001f && Mathf.Abs(delta.z) > .001f)
                    throw new InvalidOperationException("Offset rail path must remain orthogonal.");
                var steps = Mathf.RoundToInt(delta.magnitude);
                for (var step = 1; step <= steps; step++) points.Add(anchors[i - 1] + delta.normalized * step);
            }
            return points;
        }

        private static Material MaterialFor(string name)
        {
            var imagePath = Folder + "/" + name + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(imagePath);
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1);
            material.SetFloat("_Blend", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static string Signature(Scene scene, BattleSession session)
        {
            var cells = string.Join("|", session.Grid.SceneCells.Select(c => $"{c.Occupancy}:{c.Kind}:{c.transform.rotation}:{c.transform.lossyScale}"));
            var route = string.Join("|", session.Route.Cells.Select(c => c.GetInstanceID()));
            var actors = string.Join("|", scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BattleTower>(true)).Select(t => EditorJsonUtility.ToJson(t)));
            var cameras = string.Join("|", scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).Select(c => EditorJsonUtility.ToJson(c) + c.transform.position + c.transform.rotation));
            return cells + route + actors + cameras + EditorJsonUtility.ToJson(session);
        }

        private static void Capture(Scene scene, string name, string outputDirectory)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            var camera = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>()).First(c => c.CompareTag("MainCamera"));
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            var target = new RenderTexture(1600, 1000, 24);
            var image = new Texture2D(1600, 1000, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = target;
                camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); image.Apply();
                File.WriteAllBytes(outputDirectory + "/" + name + ".png", image.EncodeToPNG());
            }
            finally
            { camera.targetTexture = oldTarget; RenderTexture.active = oldActive; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image); }
        }
    }
}
