using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Team5.Rebuild.Editor
{
    // Renders the Paint Squad role portraits and the two Captain previews for the "Choose your squad"
    // screen to PNG sprites, then writes Resources/CharacterSetCatalogs.asset (Paint Squad and Catgirl).
    // Rerun after the role prefabs change. Needs a graphics device (do not use -nographics).
    public static class PaintSquadCharacterSetBuilder
    {
        public const string PortraitFolder = PaintSquadRoleVisualBuilder.ArtRoot + "/Portraits";
        public const string CatalogsPath = "Assets/Rebuild/Resources/CharacterSetCatalogs.asset";
        public const string CatgirlCatalogPath = "Assets/Rebuild/CatgirlFrontTest/RoleVisuals/CatgirlAllRoles.asset";
        public const string CatgirlCaptainPath = "Assets/Rebuild/CatgirlFrontTest/RoleVisuals/CatgirlCaptain.prefab";
        public const string PaintSquadPreviewPath = PortraitFolder + "/PaintSquadSelectPreviewPaintSquad.png";
        public const string CatgirlPreviewPath = PortraitFolder + "/PaintSquadSelectPreviewCatgirl.png";
        private const int PortraitPixels = 256;
        private const int PreviewPixels = 512;

        public static string PortraitPath(UnitRole role) => $"{PortraitFolder}/PaintSquad{role}Portrait.png";

        [MenuItem("Team5/Rebuild/Paint Squad/Build character set catalogs")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder(PortraitFolder);
            foreach (var role in PaintSquadRoleVisualBuilder.Roles)
                Render(PaintSquadRoleVisualBuilder.PrefabPath(role), PortraitPath(role), PortraitPixels, 16f, 200f, 1.04f);
            Render(PaintSquadRoleVisualBuilder.PrefabPath(UnitRole.Captain), PaintSquadPreviewPath, PreviewPixels, 18f, 205f, 1.08f);
            Render(CatgirlCaptainPath, CatgirlPreviewPath, PreviewPixels, 0f, 0f, 1.08f);
            AssetDatabase.Refresh();
            foreach (var path in AllSpritePaths()) ConfigureSprite(path);

            var catalogs = AssetDatabase.LoadAssetAtPath<CharacterSetCatalogs>(CatalogsPath);
            if (catalogs == null)
            {
                catalogs = ScriptableObject.CreateInstance<CharacterSetCatalogs>();
                AssetDatabase.CreateAsset(catalogs, CatalogsPath);
            }
            var data = new SerializedObject(catalogs);
            var entries = data.FindProperty("entries");
            entries.arraySize = 2;
            Fill(entries.GetArrayElementAtIndex(0), CharacterSet.PaintSquad, "Paint Squad", "Helmeted paint troopers",
                PaintSquadRoleVisualBuilder.CatalogPath, PaintSquadPreviewPath, true);
            Fill(entries.GetArrayElementAtIndex(1), CharacterSet.Catgirl, "Catgirl", "The original anime squad",
                CatgirlCatalogPath, CatgirlPreviewPath, false);
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalogs);
            AssetDatabase.SaveAssets();
            Debug.Log("PAINT_SQUAD_CHARACTER_SETS built " + CatalogsPath);
        }

        private static void Fill(SerializedProperty entry, CharacterSet set, string name, string tagline, string catalog, string preview, bool portraits)
        {
            entry.FindPropertyRelative("set").intValue = (int)set;
            entry.FindPropertyRelative("displayName").stringValue = name;
            entry.FindPropertyRelative("tagline").stringValue = tagline;
            var visuals = AssetDatabase.LoadAssetAtPath<UnitRoleVisualSet>(catalog);
            if (visuals == null) throw new InvalidOperationException("Missing role catalog " + catalog);
            entry.FindPropertyRelative("roleVisuals").objectReferenceValue = visuals;
            entry.FindPropertyRelative("captainPreview").objectReferenceValue = LoadSprite(preview);
            var list = entry.FindPropertyRelative("portraits");
            var roles = portraits ? PaintSquadRoleVisualBuilder.Roles : Array.Empty<UnitRole>();
            list.arraySize = roles.Length;
            for (var i = 0; i < roles.Length; i++)
            {
                var item = list.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("role").intValue = (int)roles[i];
                item.FindPropertyRelative("sprite").objectReferenceValue = LoadSprite(PortraitPath(roles[i]));
            }
        }

        private static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("Missing sprite " + path);
            return sprite;
        }

        private static void ConfigureSprite(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        // Renders one prefab in its Idle pose against black and against white and recovers alpha from the
        // difference, so the PNG has a clean transparent background whatever the render pipeline does with alpha.
        public static void Render(string prefabPath, string output, int pixels, float pitch, float yaw, float margin)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // 4x MSAA: with 8x the render texture reads back empty.
            var texture = new RenderTexture(pixels, pixels, 24) { antiAliasing = 4 };
            try
            {
                var light = new GameObject("Light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                light.transform.rotation = Quaternion.Euler(40, -30, 0);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.6f, .6f, .65f);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (asset == null) throw new InvalidOperationException("Missing prefab " + prefabPath);
                var unit = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
                var shadow = unit.transform.Find("Blob Shadow");
                if (shadow != null) shadow.gameObject.SetActive(false);
                var driver = unit.GetComponent<PaintSquadUnitAnimation>();
                if (driver != null)
                {
                    driver.ModelRoot.rotation = Quaternion.Euler(0, yaw, 0);
                    var idle = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(driver.CharacterAnimator.avatar))
                        .OfType<AnimationClip>().First(c => c.name == "Idle");
                    idle.SampleAnimation(driver.CharacterAnimator.gameObject, .3f);
                }

                var camera = new GameObject("Portrait Camera").AddComponent<Camera>();
                camera.orthographic = true;
                camera.transform.rotation = Quaternion.Euler(pitch, 0, 0);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.targetTexture = texture;
                camera.aspect = 1;
                camera.nearClipPlane = .01f;
                camera.farClipPlane = 100f;
                Frame(camera, unit, margin);

                var black = Capture(camera, texture, Color.black);
                var white = Capture(camera, texture, Color.white);
                var result = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false);
                var pixelsOut = new Color[black.Length];
                for (var i = 0; i < black.Length; i++)
                {
                    var b = black[i]; var w = white[i];
                    var alpha = Mathf.Clamp01(1f - ((w.r - b.r) + (w.g - b.g) + (w.b - b.b)) / 3f);
                    pixelsOut[i] = alpha < .004f ? new Color(0, 0, 0, 0)
                        : new Color(Mathf.Clamp01(b.r / alpha), Mathf.Clamp01(b.g / alpha), Mathf.Clamp01(b.b / alpha), alpha);
                }
                result.SetPixels(pixelsOut);
                result.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                File.WriteAllBytes(output, result.EncodeToPNG());
                Object.DestroyImmediate(result);
                camera.targetTexture = null;
            }
            finally
            {
                texture.Release();
                Object.DestroyImmediate(texture);
            }
        }

        private static void Frame(Camera camera, GameObject unit, float margin)
        {
            var renderers = unit.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException("Nothing to render in " + unit.name);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            // Fit the bounds' corners in camera space.
            var rotation = Quaternion.Inverse(camera.transform.rotation);
            var min = Vector3.positiveInfinity; var max = Vector3.negativeInfinity;
            for (var i = 0; i < 8; i++)
            {
                var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var local = rotation * corner;
                min = Vector3.Min(min, local); max = Vector3.Max(max, local);
            }
            var centre = camera.transform.rotation * ((min + max) * .5f);
            camera.orthographicSize = Mathf.Max(max.x - min.x, max.y - min.y) * .5f * margin;
            camera.transform.position = centre - camera.transform.forward * 20f;
        }

        private static Color[] Capture(Camera camera, RenderTexture texture, Color background)
        {
            camera.backgroundColor = background;
            camera.Render();
            var active = RenderTexture.active;
            RenderTexture.active = texture;
            var image = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            image.Apply();
            RenderTexture.active = active;
            var pixels = image.GetPixels();
            Object.DestroyImmediate(image);
            return pixels;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        public static IEnumerable<string> AllSpritePaths() =>
            PaintSquadRoleVisualBuilder.Roles.Select(PortraitPath).Concat(new[] { PaintSquadPreviewPath, CatgirlPreviewPath });
    }
}
