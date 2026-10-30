using System;
using System.IO;
using System.Linq;
using Team5.Rebuild.Session;
using Team5.Rebuild.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Team5.Rebuild.Editor
{
    // Edit-time authoring only. Border meshes are siblings of BattleGrid, with no combat components.
    public static class DecorativeBorderSetup
    {
        [MenuItem("Team5/Rebuild/Add Decorative Border Tiles")]
        public static void AddThreeRings()
        {
            if (Application.isPlaying || SceneManager.GetActiveScene().path != PrototypeSceneBuilder.ScenePath)
                throw new InvalidOperationException("Open GridBattlePrototype.unity in Edit Mode first.");
            var session = UnityEngine.Object.FindFirstObjectByType<BattleSession>();
            var before = session.CaptureDefinition();
            var cells = session.Grid.SceneCells;
            var lowestLayer = cells.Min(cell => cell.Occupancy.y);
            var baseCells = cells.Where(cell => cell.Occupancy.y == lowestLayer).ToArray();
            var minX = baseCells.Min(cell => cell.Occupancy.x);
            var maxX = baseCells.Max(cell => cell.Occupancy.x);
            var minZ = baseCells.Min(cell => cell.Occupancy.z);
            var maxZ = baseCells.Max(cell => cell.Occupancy.z);
            var existingRoot = session.Grid.transform.parent.Find("Decorative Border Tiles");
            var root = existingRoot != null ? existingRoot.gameObject : new GameObject("Decorative Border Tiles");
            root.transform.SetParent(session.Grid.transform.parent, true);
            root.layer = 2; // Ignore Raycast, in addition to having no colliders.
            var material = BorderMaterial();
            for (var z = minZ - 3; z <= maxZ + 3; z++)
            for (var x = minX - 3; x <= maxX + 3; x++)
            {
                if (x >= minX && x <= maxX && z >= minZ && z <= maxZ) continue;
                var name = $"Border Tile ({x},{z})";
                var existing = root.transform.Find(name);
                var tile = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = name;
                tile.transform.SetParent(root.transform, false);
                tile.transform.position = new Vector3(x, lowestLayer * BattleCell.LayerHeight, z);
                tile.transform.rotation = Quaternion.identity;
                tile.transform.localScale = BattleCell.Size;
                tile.layer = 2;
                foreach (var collider in tile.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
                var renderer = tile.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            // Validate isolation before saving: no new grid occupancy or changed route/tower configuration.
            var after = session.CaptureDefinition();
            if (session.Grid.SceneCells.Length != cells.Length || !before.Waypoints.SequenceEqual(after.Waypoints) ||
                !before.Towers.Select(tower => tower.Position).SequenceEqual(after.Towers.Select(tower => tower.Position)) ||
                root.GetComponentsInChildren<BattleCell>(true).Length != 0 || root.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException("Decorative border must remain isolated from battle entities and physics.");
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(session.gameObject.scene);
            EditorSceneManager.SaveScene(session.gameObject.scene);
            Debug.Log($"Saved {root.transform.childCount} Decorative Border Tiles: three rings, no BattleCell or Collider; battle grid remains {cells.Length} cells.");
        }

        private static Material BorderMaterial()
        {
            const string path = MinimalPresentationSetup.MaterialFolder + "/DecorativeBorder.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Directory.CreateDirectory(MinimalPresentationSetup.MaterialFolder);
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Decorative Border" };
            material.SetColor("_BaseColor", new Color(0.28f, 0.37f, 0.29f));
            material.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
