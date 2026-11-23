using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Team5.Rebuild.Editor
{
    /// <summary>Creates an editable static assembly from current, user-edited PNGs. No rig or unit logic.</summary>
    public static class WhiteCatgirlAssemblyBuilder
    {
        private const string Folder = "Assets/Rebuild/WhiteCatgirlAssembly";
        private const string PrefabPath = Folder + "/WhiteCatgirl_EditableAssembly.prefab";
        private const string Source = "Art/WhiteCatgirlFront";

        [Serializable] private sealed class Layout
        {
            public int[] root_px;
            public Layer[] layers;
            public float suggested_pixels_per_unit;
        }
        [Serializable] private sealed class Layer
        {
            public string name;
            public int order_bottom_to_top;
            public int[] bounds_px;
            public bool visible;
        }

        [MenuItem("Team5/Rebuild/White Catgirl/Create editable static assembly (once)")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before creating the assembly.");
            if (File.Exists(PrefabPath))
                throw new InvalidOperationException("Assembly already exists. Open and edit its prefab; rebuilding would overwrite manual adjustments.");
            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(Source + "/layers.json"));
            if (layout.layers == null || layout.root_px == null || layout.suggested_pixels_per_unit <= 0)
                throw new InvalidDataException("Missing assembly layout.");
            Directory.CreateDirectory(Folder + "/Parts");
            var root = new GameObject("White Catgirl - Editable Static Assembly");
            root.AddComponent<SortingGroup>();
            try
            {
                foreach (var layer in layout.layers.Where(x => x.visible).OrderBy(x => x.order_bottom_to_top))
                {
                    var target = Folder + "/Parts/" + layer.name + ".png";
                    File.Copy(Source + "/Parts/" + layer.name + ".png", target, false);
                    AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(target);
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = layout.suggested_pixels_per_unit;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.maxTextureSize = 4096;
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    settings.spriteAlignment = (int)SpriteAlignment.Center;
                    settings.spritePivot = new Vector2(.5f, .5f);
                    settings.spriteMeshType = SpriteMeshType.FullRect;
                    importer.SetTextureSettings(settings);
                    importer.SaveAndReimport();
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(target);
                    if (sprite == null) throw new InvalidDataException("Sprite import failed: " + target);
                    var child = new GameObject(layer.name);
                    child.transform.SetParent(root.transform, false);
                    // Match the current-parts preview: native size, original top-center anchor.
                    var centerX = (layer.bounds_px[0] + layer.bounds_px[2]) * .5f;
                    var centerY = layer.bounds_px[1] + sprite.rect.height * .5f;
                    child.transform.localPosition = new Vector3(
                        (centerX - layout.root_px[0]) / layout.suggested_pixels_per_unit,
                        (layout.root_px[1] - centerY) / layout.suggested_pixels_per_unit, 0);
                    var renderer = child.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprite;
                    renderer.sortingOrder = layer.order_bottom_to_top;
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            Validate();
        }

        [MenuItem("Team5/Rebuild/White Catgirl/Validate editable static assembly")]
        public static void Validate()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new InvalidDataException("Missing static prefab.");
            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(Source + "/layers.json"));
            var expected = layout.layers.Count(x => x.visible);
            var sprites = prefab.GetComponentsInChildren<SpriteRenderer>(true);
            if (sprites.Length != expected || sprites.Any(x => x.sprite == null))
                throw new InvalidDataException("Missing or unexpected sprite parts.");
            // Every component is a Transform, SpriteRenderer or SortingGroup; no rig, Animator or battle script.
            if (prefab.GetComponentsInChildren<Component>(true).Any(x =>
                !(x is Transform) && !(x is SpriteRenderer) && !(x is SortingGroup)))
                throw new InvalidDataException("Unexpected component in static assembly.");
            Directory.CreateDirectory("Logs/WhiteCatgirlAssembly");
            File.WriteAllText("Logs/WhiteCatgirlAssembly/validation.txt",
                "PASS: " + PrefabPath + "\nParts: " + expected +
                "\nComponents: Transform, SpriteRenderer and root SortingGroup only.\nNo rig, Animator, colliders or UnitView.\n");
            Debug.Log("WHITE_CATGIRL_ASSEMBLY_OK: " + PrefabPath + "; parts=" + expected);
        }
    }
}
