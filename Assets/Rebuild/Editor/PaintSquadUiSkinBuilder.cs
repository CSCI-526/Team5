using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Team5.Rebuild.Presentation.LegacyUI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Team5.Rebuild.Editor
{
    // Builds the Paint Squad UI skin used by LegacyUiKit:
    // 1. draws the 9-slice button, round button, card panel and hex paper sprites into PaintSquad/UI/Generated,
    // 2. bakes the Fredoka and Lilita One SDF fonts as static assets holding every character Team5 UI text uses,
    //    with LiberationSans SDF (the previous Team5 font) as fallback for anything else,
    // 3. writes Resources/PaintSquadUiSkin.asset that references the fonts, materials, shapes and icons.
    // Rerunnable. Works under -nographics.
    public static class PaintSquadUiSkinBuilder
    {
        public const string UiRoot = PaintSquadRoleVisualBuilder.ArtRoot + "/UI";
        public const string GeneratedFolder = UiRoot + "/Generated";
        public const string ShapesFolder = UiRoot + "/Shapes";
        public const string IconsFolder = UiRoot + "/Icons";
        public const string FontsFolder = PaintSquadRoleVisualBuilder.ArtRoot + "/Fonts";
        public const string SkinPath = "Assets/Rebuild/Resources/" + PaintSquadUiSkin.ResourcePath + ".asset";
        public const string BodyFontPath = FontsFolder + "/Fredoka SDF.asset";
        public const string HeadingFontPath = FontsFolder + "/LilitaOne SDF.asset";
        public const string FallbackFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        public const string ButtonPath = GeneratedFolder + "/PaintSquadButton.png";
        public const string ButtonCirclePath = GeneratedFolder + "/PaintSquadButtonCircle.png";
        public const string PanelPath = GeneratedFolder + "/PaintSquadPanel.png";
        public const string HexPaperPath = GeneratedFolder + "/PaintSquadHexPaper.png";

        // Printable ASCII plus the symbols Team5 UI strings use (en and em dash, bullets, arrows, check, cross,
        // star, heart, ellipsis, degree, curly quotes). Glyphs a font lacks resolve through the fallback.
        public static readonly string Characters =
            new string(Enumerable.Range(32, 95).Select(c => (char)c).ToArray()) + "–—·•×→←↑↓✓✕★♥…°‘’“” ";

        private const int ButtonPixels = 128;
        private const int Corner = 40;
        private const int ButtonLip = 12;
        private const int PanelLip = 8;

        [MenuItem("Team5/Rebuild/Paint Squad/Build UI skin")]
        public static void Build()
        {
            EnsureFolder(GeneratedFolder);
            WriteSprites();
            AssetDatabase.Refresh();
            ConfigureSprite(ButtonPath, new Vector4(Corner + 4, Corner + 4 + ButtonLip, Corner + 4, Corner + 4), TextureWrapMode.Clamp);
            ConfigureSprite(ButtonCirclePath, Vector4.zero, TextureWrapMode.Clamp);
            ConfigureSprite(PanelPath, new Vector4(Corner + 4, Corner + 4 + PanelLip, Corner + 4, Corner + 4), TextureWrapMode.Clamp);
            ConfigureSprite(HexPaperPath, Vector4.zero, TextureWrapMode.Repeat);

            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackFontPath);
            var body = BakeStatic(BodyFontPath, fallback);
            var heading = BakeStatic(HeadingFontPath, fallback);

            var skin = AssetDatabase.LoadAssetAtPath<PaintSquadUiSkin>(SkinPath);
            if (skin == null)
            {
                skin = ScriptableObject.CreateInstance<PaintSquadUiSkin>();
                AssetDatabase.CreateAsset(skin, SkinPath);
            }
            skin.bodyFont = body;
            skin.headingFont = heading;
            skin.headingOutline = Load<Material>(FontsFolder + "/LilitaOne SDF Outline.mat");
            skin.headingOutlineThin = Load<Material>(FontsFolder + "/LilitaOne SDF Outline Thin.mat");
            skin.bodyOutline = Load<Material>(FontsFolder + "/Fredoka SDF Outline.mat");
            skin.rounded = Shape("rounded32");
            skin.circle = Shape("circle");
            skin.ring = Shape("ring");
            skin.pill = Shape("pill");
            skin.ribbon = Shape("ribbon");
            skin.softShadow = Shape("soft_shadow");
            skin.glow = Shape("glow");
            skin.white = Shape("white");
            skin.button = Load<Sprite>(ButtonPath);
            skin.buttonCircle = Load<Sprite>(ButtonCirclePath);
            skin.panel = Load<Sprite>(PanelPath);
            skin.hexPaper = Load<Sprite>(HexPaperPath);
            skin.icons = AssetDatabase.FindAssets("t:Sprite", new[] { IconsFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => new PaintSquadUiSkin.NamedIcon
                {
                    name = Path.GetFileNameWithoutExtension(path).Replace("icon_", string.Empty),
                    sprite = Load<Sprite>(path)
                })
                .ToArray();
            EditorUtility.SetDirty(skin);
            AssetDatabase.SaveAssets();
            Debug.Log($"Paint Squad UI skin built: {skin.icons.Length} icons, fonts static ({body.characterTable.Count} and {heading.characterTable.Count} characters).");
        }

        public static TMP_FontAsset BakeStatic(string path, TMP_FontAsset fallback)
        {
            var font = Load<TMP_FontAsset>(path);
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            if (!font.TryAddCharacters(Characters, out var missing) && !string.IsNullOrEmpty(missing))
                Debug.Log($"{Path.GetFileName(path)} has no glyph for '{missing}'; those use the fallback font.");
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            font.fallbackFontAssetTable = fallback != null ? new List<TMP_FontAsset> { fallback } : new List<TMP_FontAsset>();
            EditorUtility.SetDirty(font);
            foreach (var texture in font.atlasTextures) if (texture != null) EditorUtility.SetDirty(texture);
            return font;
        }

        private static void WriteSprites()
        {
            File.WriteAllBytes(ButtonPath, DrawButton().EncodeToPNG());
            File.WriteAllBytes(ButtonCirclePath, DrawButtonCircle().EncodeToPNG());
            File.WriteAllBytes(PanelPath, DrawPanel().EncodeToPNG());
            File.WriteAllBytes(HexPaperPath, DrawHexPaper().EncodeToPNG());
        }

        // White glossy face (top half full white, lower half 92 percent) over a 62 percent drop edge, so a
        // tinted image gets a darker lip of its own color, like the Paint Squad pills.
        private static Texture2D DrawButton()
        {
            const int size = ButtonPixels;
            return Draw(size, size, (x, y) =>
            {
                var outer = RoundedCoverage(x, y, 0, 0, size, size, Corner);
                if (outer <= 0f) return Color.clear;
                var face = RoundedCoverage(x, y, 0, ButtonLip, size, size, Corner);
                var gloss = Mathf.Lerp(0.9f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ButtonLip + (size - ButtonLip) * 0.42f, ButtonLip + (size - ButtonLip) * 0.58f, y)));
                var value = Mathf.Lerp(0.62f, gloss, face);
                return new Color(value, value, value, outer);
            });
        }

        private static Texture2D DrawButtonCircle()
        {
            const int size = 256;
            const float lip = 16f;
            return Draw(size, size, (x, y) =>
            {
                var outer = CircleCoverage(x, y, size * 0.5f, size * 0.5f, size * 0.5f - 1f);
                if (outer <= 0f) return Color.clear;
                var face = CircleCoverage(x, y, size * 0.5f, size * 0.5f + lip * 0.5f, size * 0.5f - 1f - lip * 0.5f);
                var gloss = Mathf.Lerp(0.9f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(size * 0.44f, size * 0.6f, y)));
                var value = Mathf.Lerp(0.62f, gloss, face);
                return new Color(value, value, value, outer);
            });
        }

        // Card rim: flat white with a slightly darker, thicker bottom edge.
        private static Texture2D DrawPanel()
        {
            const int size = ButtonPixels;
            return Draw(size, size, (x, y) =>
            {
                var outer = RoundedCoverage(x, y, 0, 0, size, size, Corner);
                if (outer <= 0f) return Color.clear;
                var face = RoundedCoverage(x, y, 0, PanelLip, size, size, Corner);
                var value = Mathf.Lerp(0.8f, 1f, face);
                return new Color(value, value, value, outer);
            });
        }

        // Tileable beige hex ground (flat-top hexes, radius 60 px), like the Paint Squad map paper:
        // low contrast tiles with a small value jitter and thin darker seams.
        private static Texture2D DrawHexPaper()
        {
            const float radius = 60f;
            var period = new Vector2(radius * 3f, Mathf.Sqrt(3f) * radius);
            var width = Mathf.RoundToInt(period.x * 2f);
            var height = Mathf.RoundToInt(period.y * 2f);
            var stretch = height / (period.y * 2f);
            var apothem = radius * Mathf.Sqrt(3f) * 0.5f;
            var baseColor = LegacyUiKit.Hex(0xD3C9AB);
            var seamColor = LegacyUiKit.Hex(0xB8AD8C);
            float[] jitter = { 0f, -0.03f, 0.02f, -0.012f, -0.022f, 0.025f, -0.035f, 0.01f };
            return Draw(width, height, (px, py) =>
            {
                var point = new Vector2(px + 0.5f, (py + 0.5f) / stretch);
                var best = float.MaxValue;
                var bestId = 0;
                for (var column = -1; column <= 4; column++)
                for (var row = -1; row <= 3; row++)
                {
                    var center = new Vector2(column * radius * 1.5f, row * period.y + (column & 1) * period.y * 0.5f);
                    var q = point - center;
                    q = new Vector2(Mathf.Abs(q.x), Mathf.Abs(q.y));
                    var metric = Mathf.Max(q.y, q.x * 0.8660254f + q.y * 0.5f);
                    if (metric >= best) continue;
                    best = metric;
                    // Wrap the cell id to the texture so the jitter tiles.
                    bestId = (((column % 4) + 4) % 4) + 4 * (((row % 2) + 2) % 2);
                }
                var fill = baseColor * (1f + jitter[bestId % jitter.Length]);
                var seam = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(apothem - 2.6f, apothem - 0.8f, best));
                var color = Color.Lerp(fill, seamColor, seam);
                color.a = 1f;
                return color;
            });
        }

        private static Texture2D Draw(int width, int height, Func<int, int, Color> shade)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                pixels[y * width + x] = shade(x, y);
            texture.SetPixels(pixels);
            texture.Apply(false);
            return texture;
        }

        private static float RoundedCoverage(int x, int y, float left, float bottom, float right, float top, float radius)
        {
            var px = x + 0.5f;
            var py = y + 0.5f;
            var cx = Mathf.Clamp(px, left + radius, right - radius);
            var cy = Mathf.Clamp(py, bottom + radius, top - radius);
            var distance = new Vector2(px - cx, py - cy).magnitude;
            var inside = Mathf.Min(Mathf.Min(px - left, right - px), Mathf.Min(py - bottom, top - py));
            if (inside <= 0f) return 0f;
            return Mathf.Clamp01(radius - distance + 0.5f) * Mathf.Clamp01(inside + 0.5f);
        }

        private static float CircleCoverage(int x, int y, float cx, float cy, float radius) =>
            Mathf.Clamp01(radius - new Vector2(x + 0.5f - cx, y + 0.5f - cy).magnitude + 0.5f);

        private static void ConfigureSprite(string path, Vector4 border, TextureWrapMode wrap)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = wrap;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = border;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static Sprite Shape(string name) => Load<Sprite>($"{ShapesFolder}/{name}.png");

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException($"Missing {typeof(T).Name} at {path}.");
            return asset;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
