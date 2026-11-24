using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Team5.Rebuild.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace Team5.Rebuild.Editor
{
    // Dresses Level1GridBattle..Level7GridBattle with Paint Squad environment props (trees, bushes, rocks,
    // grass, flowers, a few camp props) under one scene root, "Paint Squad Dressing".
    // Props are presentation only: no colliders, Ignore Raycast layer, batching static, no shadows.
    // They stand only on decorative ground outside every BattleCell, and tall props are rejected where the
    // battle camera would see them over the playfield. Placement is seeded by the scene name, so a rerun
    // gives the same result; rerunning replaces the root.
    public static class PaintSquadLevelDressing
    {
        public const string RootName = "Paint Squad Dressing";
        public const string PrefabFolder = PaintSquadRoleVisualBuilder.ArtRoot + "/Environment";
        private const string ModelFolder = PaintSquadRoleVisualBuilder.ArtRoot + "/Models/Environment";
        private const string BorderMaterialPath = "Assets/Rebuild/Materials/Minimal/DecorativeBorder.mat";
        private const string BorderRootName = "Decorative Border Tiles";
        public const int IgnoreRaycastLayer = 2;
        // Footprints may touch a BattleCell edge but never overlap it by more than this.
        public const float FootprintTolerance = .01f;
        // Width of the generated ground ring for levels that have no decorative border tiles.
        private const int GeneratedRing = 12;
        // Extra clearance around the playfield for what a prop may cover on screen (unit heads, health bars).
        private const float ViewMargin = .6f;

        public static readonly string[] Levels = Enumerable.Range(1, 7).Select(i => $"Level{i}GridBattle").ToArray();
        public static string ScenePath(string level) => $"Assets/Rebuild/Scenes/{level}.unity";
        public static string PrefabPath(string model) => $"{PrefabFolder}/PaintSquad{model}.prefab";

        // Model name and the height it is scaled to (a Paint Squad soldier is about 1 unit tall).
        private static readonly (string model, float height)[] Props =
        {
            ("Tree_A", 2.6f), ("Tree_B", 3.2f), ("Tree_C", 2.4f), ("Bush_A", .6f), ("Bush_B", .75f),
            ("Rock_A", .3f), ("Rock_B", .5f), ("Rock_C", .8f), ("GrassTuft", .35f), ("Flower_A", .3f),
            ("Flower_B", .3f), ("Fence", .7f), ("Crate", .55f), ("Barrel", .6f), ("Log", .4f), ("Tent", 1.4f),
        };

        private static readonly (string, float)[] Edge = { ("GrassTuft", .45f), ("Flower_A", .2f), ("Flower_B", .15f), ("Rock_A", .2f) };
        private static readonly (string, float)[] Near =
        {
            ("Bush_A", .22f), ("Bush_B", .18f), ("Rock_B", .18f), ("GrassTuft", .2f), ("Flower_A", .08f),
            ("Crate", .07f), ("Barrel", .07f),
        };
        private static readonly (string, float)[] Far =
        {
            ("Tree_A", .28f), ("Tree_B", .24f), ("Tree_C", .2f), ("Bush_A", .08f), ("Bush_B", .06f),
            ("Rock_C", .06f), ("Log", .03f), ("Tent", .02f), ("Fence", .015f), ("Crate", .015f),
        };
        // Lower substitutes tried, in order, when a prop is rejected for covering the playfield.
        private static readonly string[] Fallbacks = { "Bush_B", "Bush_A", "Rock_B", "GrassTuft", "Rock_A" };

        [MenuItem("Team5/Rebuild/Paint Squad/Dress levels")]
        public static void DressLevels()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var setup = Application.isBatchMode ? null : EditorSceneManager.GetSceneManagerSetup();
            BuildPrefabs();
            var report = new List<string>();
            foreach (var level in Levels)
            {
                var scene = EditorSceneManager.OpenScene(ScenePath(level), OpenSceneMode.Single);
                var counts = Dress(scene);
                EditorSceneManager.SaveScene(scene);
                report.Add($"{level}: {counts.Sum(c => c.Value)} props ({string.Join(", ", counts.OrderBy(c => c.Key).Select(c => $"{c.Key} {c.Value}"))})");
            }
            Debug.Log("Paint Squad dressing\n" + string.Join("\n", report));
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }

        public static void BuildPrefabs()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder(PaintSquadRoleVisualBuilder.ArtRoot, "Environment");
            foreach (var (model, height) in Props)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelFolder}/{model}.fbx");
                if (source == null) throw new InvalidOperationException("Missing environment model " + model);
                var root = new GameObject("PaintSquad" + model);
                try
                {
                    var art = (GameObject)PrefabUtility.InstantiatePrefab(source);
                    art.transform.SetParent(root.transform, false);
                    var bounds = Bounds(art);
                    art.transform.localScale = Vector3.one * (height / Mathf.Max(.01f, bounds.size.y));
                    MakePresentationOnly(root);
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(model));
                }
                finally { Object.DestroyImmediate(root); }
            }
            AssetDatabase.SaveAssets();
        }

        private static void MakePresentationOnly(GameObject root)
        {
            foreach (var collider in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = IgnoreRaycastLayer;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            }
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }

        private static Bounds Bounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        private struct Tile { public int x, z; public float top; }

        // Dresses one open scene and returns prop counts per model.
        public static Dictionary<string, int> Dress(Scene scene)
        {
            foreach (var old in scene.GetRootGameObjects().Where(g => g.name == RootName).ToArray()) Object.DestroyImmediate(old);
            var cells = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BattleCell>(true)).ToArray();
            if (cells.Length == 0) throw new InvalidOperationException(scene.name + " has no BattleCell");
            var occupied = new HashSet<Vector2Int>(cells.Select(c => new Vector2Int(c.Occupancy.x, c.Occupancy.z)));
            int minX = occupied.Min(c => c.x), maxX = occupied.Max(c => c.x), minZ = occupied.Min(c => c.y), maxZ = occupied.Max(c => c.y);
            var cellTop = cells.Max(c => c.TopCenter.y);
            var camera = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true))
                .OrderByDescending(c => c.name.StartsWith("Battle Camera")).First();

            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            var tiles = BorderTiles(scene, occupied);
            if (tiles.Count == 0)
            {
                // Map Builder levels have an empty border: lay a flat decorative ground ring first.
                BuildGround(root.transform, minX, maxX, minZ, maxZ, cellTop);
                for (var x = minX - GeneratedRing; x <= maxX + GeneratedRing; x++)
                for (var z = minZ - GeneratedRing; z <= maxZ + GeneratedRing; z++)
                    if (!occupied.Contains(new Vector2Int(x, z))) tiles.Add(new Tile { x = x, z = z, top = cellTop });
            }
            tiles = tiles.Where(t => Visible(camera, new Vector3(t.x, t.top, t.z))).OrderBy(t => t.x).ThenBy(t => t.z).ToList();

            var prefabs = Props.ToDictionary(p => p.model, p => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(p.model)));
            var random = new Random(Seed(scene.name));
            var counts = new Dictionary<string, int>();
            var playfield = Rect.MinMaxRect(minX - .5f - ViewMargin, minZ - .5f - ViewMargin, maxX + .5f + ViewMargin, maxZ + .5f + ViewMargin);
            foreach (var tile in tiles)
            {
                var distance = Mathf.Max(minX - tile.x, tile.x - maxX, minZ - tile.z, tile.z - maxZ);
                var (table, chance) = distance <= 1 ? (Edge, 0.0) : distance == 2 ? (Near, .12) : (Far, .28);
                // Draw every random value up front so one rejection does not shift the rest of the layout.
                var roll = random.NextDouble();
                var pick = Pick(table, random.NextDouble());
                var jitter = new Vector3((float)random.NextDouble() - .5f, 0, (float)random.NextDouble() - .5f) * .6f;
                var yaw = (float)random.NextDouble() * 360;
                var scale = .85f + (float)random.NextDouble() * .3f;
                if (roll > chance) continue;
                if (pick == "Tent" && distance < 4) pick = "Tree_A";
                var position = new Vector3(tile.x, tile.top, tile.z) + jitter;
                var height = Props.First(p => p.model == pick).height;
                foreach (var model in new[] { pick }.Concat(Fallbacks.Where(f => Props.First(p => p.model == f).height < height)))
                {
                    var prop = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[model], root.transform);
                    prop.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
                    prop.transform.localScale = Vector3.one * scale;
                    var bounds = Bounds(prop);
                    if (OverlapsCell(bounds, occupied) || Covers(camera, bounds, playfield, cellTop))
                    {
                        Object.DestroyImmediate(prop);
                        continue;
                    }
                    counts[model] = counts.TryGetValue(model, out var n) ? n + 1 : 1;
                    break;
                }
            }
            MakePresentationOnly(root);
            EditorSceneManager.MarkSceneDirty(scene);
            return counts;
        }

        private static List<Tile> BorderTiles(Scene scene, HashSet<Vector2Int> occupied)
        {
            var result = new List<Tile>();
            foreach (var go in scene.GetRootGameObjects())
            foreach (var border in go.GetComponentsInChildren<Transform>(true).Where(t => t.name == BorderRootName))
            foreach (Transform tile in border)
            {
                var key = new Vector2Int(Mathf.RoundToInt(tile.position.x), Mathf.RoundToInt(tile.position.z));
                if (occupied.Contains(key)) continue;
                result.Add(new Tile { x = key.x, z = key.y, top = tile.position.y + tile.lossyScale.y * .5f });
            }
            return result;
        }

        private static void BuildGround(Transform root, int minX, int maxX, int minZ, int maxZ, float top)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(BorderMaterialPath);
            var ground = new GameObject("Ground").transform;
            ground.SetParent(root, false);
            float x0 = minX - .5f, x1 = maxX + .5f, z0 = minZ - .5f, z1 = maxZ + .5f, r = GeneratedRing;
            void Slab(string name, float ax, float bx, float az, float bz)
            {
                var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(slab.GetComponent<Collider>());
                slab.name = name;
                slab.transform.SetParent(ground, false);
                slab.transform.position = new Vector3((ax + bx) / 2, top - .25f, (az + bz) / 2);
                slab.transform.localScale = new Vector3(bx - ax, .5f, bz - az);
                var renderer = slab.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            Slab("Ground South", x0 - r, x1 + r, z0 - r, z0);
            Slab("Ground North", x0 - r, x1 + r, z1, z1 + r);
            Slab("Ground West", x0 - r, x0, z0, z1);
            Slab("Ground East", x1, x1 + r, z0, z1);
        }

        private static string Pick((string model, float weight)[] table, double roll)
        {
            var total = table.Sum(t => t.weight);
            var at = roll * total;
            foreach (var (model, weight) in table)
            {
                if (at < weight) return model;
                at -= weight;
            }
            return table[table.Length - 1].model;
        }

        private static int Seed(string name)
        {
            unchecked
            {
                var hash = (int)2166136261;
                foreach (var c in name) hash = (hash ^ c) * 16777619;
                return hash;
            }
        }

        private static bool Visible(Camera camera, Vector3 point)
        {
            var v = camera.WorldToViewportPoint(point);
            return v.z > 0 && v.x > -.08f && v.x < 1.08f && v.y > -.08f && v.y < 1.12f;
        }

        // True when the XZ footprint overlaps a BattleCell square by more than the tolerance.
        public static bool OverlapsCell(Bounds bounds, HashSet<Vector2Int> occupied)
        {
            for (var x = Mathf.FloorToInt(bounds.min.x); x <= Mathf.CeilToInt(bounds.max.x); x++)
            for (var z = Mathf.FloorToInt(bounds.min.z); z <= Mathf.CeilToInt(bounds.max.z); z++)
                if (occupied.Contains(new Vector2Int(x, z)) &&
                    bounds.max.x > x - .5f + FootprintTolerance && bounds.min.x < x + .5f - FootprintTolerance &&
                    bounds.max.z > z - .5f + FootprintTolerance && bounds.min.z < z + .5f - FootprintTolerance)
                    return true;
            return false;
        }

        // True when, seen from the battle camera, the prop is drawn over the playfield: any bounds corner
        // that is above the ground lands, along its view ray, inside the playfield (plus margin).
        private static bool Covers(Camera camera, Bounds bounds, Rect playfield, float ground)
        {
            var origin = camera.transform.position;
            for (var i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x, (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                if (corner.y <= ground + .02f) continue;
                var direction = corner - origin;
                if (direction.y >= 0) return true;
                var hit = origin + direction * ((ground - origin.y) / direction.y);
                if (playfield.Contains(new Vector2(hit.x, hit.z))) return true;
            }
            return false;
        }

        // Batch entry: Unity -batchmode -projectPath ~/Team5 -executeMethod Team5.Rebuild.Editor.PaintSquadLevelDressing.DressLevelsBatch -quit
        public static void DressLevelsBatch() => DressLevels();
    }
}
