using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Team5.Rebuild.Editor;
using Team5.Rebuild.Presentation.LegacyUI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace Team5.Rebuild.Tests
{
    /// <summary>The Paint Squad UI skin behind LegacyUiKit: asset, static fonts, sprites and kit behavior.</summary>
    public sealed class PaintSquadUiSkinTests
    {
        private static PaintSquadUiSkin Skin => Resources.Load<PaintSquadUiSkin>(PaintSquadUiSkin.ResourcePath);

        [Test]
        public void SkinAssetReferencesFontsMaterialsShapesAndIcons()
        {
            var skin = Skin;
            Assert.That(skin, Is.Not.Null, PaintSquadUiSkinBuilder.SkinPath);
            Assert.That(AssetDatabase.GetAssetPath(skin.bodyFont), Is.EqualTo(PaintSquadUiSkinBuilder.BodyFontPath));
            Assert.That(AssetDatabase.GetAssetPath(skin.headingFont), Is.EqualTo(PaintSquadUiSkinBuilder.HeadingFontPath));
            foreach (var material in new[] { skin.headingOutline, skin.headingOutlineThin, skin.bodyOutline })
                Assert.That(material, Is.Not.Null);
            foreach (var sprite in new[] { skin.rounded, skin.button, skin.buttonCircle, skin.panel, skin.circle, skin.pill, skin.ribbon, skin.softShadow, skin.hexPaper })
                Assert.That(sprite, Is.Not.Null);
            Assert.That(skin.icons.Length, Is.GreaterThanOrEqualTo(40));
            foreach (var name in new[] { "coin", "heart", "close", "check", "arrow_left", "users" })
                Assert.That(skin.Icon(name), Is.Not.Null, name);
        }

        [Test]
        public void GeneratedSpritesAreNineSlicedWithADropEdge()
        {
            var skin = Skin;
            Assert.That(skin.button.border.y, Is.GreaterThan(skin.button.border.w), "the button's bottom slice holds the drop edge");
            Assert.That(skin.panel.border.y, Is.GreaterThan(skin.panel.border.w));
            Assert.That(skin.hexPaper.texture.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            var pixels = skin.button.texture;
            var path = AssetDatabase.GetAssetPath(pixels);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single), path);
        }

        [Test]
        public void FontsAreStaticWithEveryUiCharacterAndTheTeam5FontAsFallback()
        {
            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PaintSquadUiSkinBuilder.FallbackFontPath);
            foreach (var font in new[] { Skin.bodyFont, Skin.headingFont })
            {
                Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static), font.name);
                Assert.That(font.fallbackFontAssetTable, Does.Contain(fallback), font.name);
                for (var c = 32; c < 127; c++)
                    Assert.That(font.HasCharacter((char)c), Is.True, $"{font.name} lacks '{(char)c}'");
            }
        }

        // Every character in a string literal of the runtime UI code must render: from the static fonts or their fallback.
        [Test]
        public void EveryCharacterInUiStringsRenders()
        {
            var literal = new Regex("\"((?:[^\"\\\\]|\\\\.)*)\"");
            var characters = Directory.GetFiles("Assets/Rebuild/Scripts/Presentation", "*.cs", SearchOption.AllDirectories)
                .SelectMany(file => literal.Matches(File.ReadAllText(file)).Cast<Match>().SelectMany(m => m.Groups[1].Value))
                .Where(c => c >= 32 && c != '\\')
                .Distinct()
                .ToArray();
            Assert.That(characters.Length, Is.GreaterThan(40));
            foreach (var font in new[] { Skin.bodyFont, Skin.headingFont })
            foreach (var c in characters)
                Assert.That(font.HasCharacter(c, searchFallbacks: true, tryAddCharacter: false), Is.True,
                    $"{font.name} and its fallbacks lack '{c}' (U+{(int)c:X4})");
        }

        [Test]
        public void KitTextUsesLilitaForHeadingsFredokaForBodyAndOutlinesLightText()
        {
            var root = new GameObject("Kit Text", typeof(RectTransform));
            try
            {
                var title = LegacyUiKit.CreateText(root.transform, "Title", "TITLE", Vector2.zero, new Vector2(200, 40), 30, Color.white, FontStyles.Bold, 6);
                var body = LegacyUiKit.CreateText(root.transform, "Body", "Body", Vector2.zero, new Vector2(200, 40), 20, LegacyUiKit.Ink);
                Assert.That(title.font, Is.SameAs(Skin.headingFont));
                Assert.That(title.fontSharedMaterial, Is.SameAs(Skin.headingOutline));
                Assert.That(title.fontStyle & FontStyles.Bold, Is.EqualTo(FontStyles.Normal), "no faux bold on the SDF display font");
                Assert.That(body.font, Is.SameAs(Skin.bodyFont));
                Assert.That(body.fontSharedMaterial, Is.SameAs(Skin.bodyFont.material), "dark text on cream stays plain");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void KitButtonsAreChunkyAndKeepTheirTargetGraphic()
        {
            var root = new GameObject("Kit Buttons", typeof(RectTransform));
            try
            {
                var button = LegacyUiKit.CreateButton(root.transform, "Pill", Vector2.zero, new Vector2(200, 60), LegacyUiKit.Green, 14);
                Assert.That(button.image.sprite, Is.SameAs(Skin.button));
                Assert.That(button.image.type, Is.EqualTo(Image.Type.Sliced));
                Assert.That(button.targetGraphic, Is.SameAs(button.image));
                Assert.That(button.image.color, Is.EqualTo(LegacyUiKit.Green));
                var round = LegacyUiKit.MakeButton(LegacyUiKit.CreateCircle(root.transform, "Round", Vector2.zero, 40, LegacyUiKit.Red));
                Assert.That(round.image.sprite, Is.SameAs(Skin.buttonCircle));
                var flat = LegacyUiKit.CreateButton(root.transform, "Flat", Vector2.zero, new Vector2(56, 56), LegacyUiKit.PanelLineColor, 0);
                Assert.That(flat.image.sprite, Is.Null, "unrounded buttons keep their plain rect unless a screen opts in");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SceneSkinIsIdempotent()
        {
            var host = new GameObject("Canvas", typeof(RectTransform));
            try
            {
                var canvas = LegacyUiKit.CreateCanvas(host, 0, new Vector2(1280, 720), 0);
                var first = PaintSquadSceneSkin.EnsureBackdrop(canvas);
                var second = PaintSquadSceneSkin.EnsureBackdrop(canvas);
                Assert.That(second, Is.SameAs(first));
                Assert.That(first.transform.GetSiblingIndex(), Is.EqualTo(0));
                Assert.That(first.type, Is.EqualTo(Image.Type.Tiled));
                Assert.That(first.raycastTarget, Is.False);
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}
