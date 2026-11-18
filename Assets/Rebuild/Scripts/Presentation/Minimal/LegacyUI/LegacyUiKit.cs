using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    // Runtime UI kit for every code-built Team5 screen. It draws in the Paint Squad Wars language:
    // slate and cream panels, chunky rounded buttons with a darker drop edge, Lilita One titles with a dark
    // outline, Fredoka body text, ribbon banners and a beige hex paper backdrop. Fonts and sprites come
    // from Resources/PaintSquadUiSkin.asset (see PaintSquad.md); without it the kit falls back to
    // procedural sprites and the TMP default font.
    public static class LegacyUiKit
    {
        // Paint Squad palette (Docs/VISUAL_STYLE_GUIDE.md in Paint Squad Wars, UIColors there).
        public static readonly Color Ink = Hex(0x2A2D3A);
        public static readonly Color InkMuted = Hex(0x7D8190);
        public static readonly Color Slate = Hex(0x3A4052);
        public static readonly Color SlateLight = Hex(0x535B70);
        public static readonly Color SlateDark = Hex(0x262A36);
        public static readonly Color ButtonSlate = Hex(0x4A5165);
        public static readonly Color CardColor = Hex(0xFBF8F1);
        public static readonly Color CardInnerColor = Hex(0xEDE7DA);
        public static readonly Color CardRimColor = Hex(0xCFC6B3);
        public static readonly Color CardLineColor = Hex(0xDCD4C4);
        public static readonly Color Gold = Hex(0xF6B12B);
        public static readonly Color GoldDark = Hex(0xC27A14);
        public static readonly Color Green = Hex(0x76BD36);
        public static readonly Color GreenDark = Hex(0x4C8A1F);
        public static readonly Color Red = Hex(0xE04A2F);
        public static readonly Color RedDark = Hex(0xA02D1C);
        public static readonly Color Blue = Hex(0x3E8EDB);
        public static readonly Color BlueDark = Hex(0x255E9F);
        public static readonly Color Grey = Hex(0xB9BDC6);
        public static readonly Color GreyDark = Hex(0x868B97);
        public static readonly Color Paper = Hex(0xCFC6A9);
        public static readonly Color DimColor = new Color(0.07f, 0.08f, 0.11f, 0.62f);

        // Original semantic names, now mapped onto the palette: dark slate panels with white text.
        public static readonly Color PanelColor = Slate;
        public static readonly Color PanelLineColor = SlateLight;
        public static readonly Color CellColor = SlateDark;
        public static readonly Color TextColor = Color.white;
        public static readonly Color MutedColor = Grey;
        public static readonly Color HighlightColor = Gold;
        public static readonly Color NodeGreenColor = Green;
        public static readonly Color NodeGreyColor = GreyDark;
        public static readonly Color NodeBlueColor = Blue;
        public static readonly Color DropColor = Hex(0x9AD65A);
        public static readonly Color LockColor = CardInnerColor;

        private const int RoundedTextureSize = 64;
        private const float RoundedTextureRadius = 16f;
        private const float RoundedPixelsPerUnit = 100f;
        // Corner radius in pixels of the skin's rounded, button and panel sprites (see PaintSquadUiSkinBuilder).
        public const float SkinCornerRadius = 40f;
        // Height in pixels of the ribbon sprite.
        private const float RibbonPixels = 128f;

        private static Sprite rounded;
        private static Sprite circle;
        private static PaintSquadUiSkin skin;
        private static bool skinLoaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            rounded = null;
            circle = null;
            skin = null;
            skinLoaded = false;
        }

        public static PaintSquadUiSkin Skin
        {
            get
            {
                if (!skinLoaded) { skin = Resources.Load<PaintSquadUiSkin>(PaintSquadUiSkin.ResourcePath); skinLoaded = true; }
                return skin;
            }
        }

        public static TMP_FontAsset BodyFont => Skin != null && Skin.bodyFont != null ? Skin.bodyFont : TMP_Settings.defaultFontAsset;
        public static TMP_FontAsset HeadingFont => Skin != null && Skin.headingFont != null ? Skin.headingFont : BodyFont;

        public static Color Hex(int rgb, float a = 1f) =>
            new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);

        public static Color Darker(Color c, float k = 0.7f) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        public static string LifeCounterText(int remaining, int limit) =>
            $": {Mathf.Clamp(remaining, 0, limit)}/{limit}";

        public static void CreateHeartIcon(Transform parent, Vector2 topLeftPosition, float scale = 1f)
        {
            var root = CreateRect(parent, "Heart Icon", topLeftPosition, new Vector2(24f, 24f));
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = topLeftPosition;
            root.localScale = new Vector3(scale, scale, 1f);
            var sprite = Icon("heart");
            if (sprite != null)
            {
                var heart = CreateImage(root, "Heart", Vector2.zero, new Vector2(30f, 30f), Color.white);
                heart.sprite = sprite;
                heart.preserveAspect = true;
                return;
            }
            var red = new Color(1f, 0.2f, 0.3f, 1f);
            var tip = CreateImage(root, "Heart Tip", new Vector2(0f, -2f), new Vector2(14f, 14f), red);
            tip.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            CreateCircle(root, "Heart Left", new Vector2(-5f, 3f), 14f, red);
            CreateCircle(root, "Heart Right", new Vector2(5f, 3f), 14f, red);
        }

        public static Canvas CreateCanvas(GameObject host, int sortingOrder, Vector2 referenceResolution, float matchWidthOrHeight)
        {
            var canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = host.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = matchWidthOrHeight;

            host.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform CreateRect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static Image CreateImage(Transform parent, string name, Vector2 position, Vector2 size, Color color, float radius = 0f)
        {
            var image = CreateRect(parent, name, position, size).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (radius > 0f) Round(image, radius);
            return image;
        }

        public static Image CreateCircle(Transform parent, string name, Vector2 position, float diameter, Color color)
        {
            var image = CreateImage(parent, name, position, new Vector2(diameter, diameter), color);
            image.sprite = CircleSprite;
            return image;
        }

        public static void Round(Image image, float radius)
        {
            var skinned = Skin != null && Skin.rounded != null;
            image.sprite = skinned ? Skin.rounded : RoundedSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = (skinned ? SkinCornerRadius : RoundedTextureRadius) / radius;
        }

        public static TextMeshProUGUI CreateText(Transform parent, string name, string text, Vector2 position, Vector2 size, float fontSize,
            Color color, FontStyles style = FontStyles.Normal, float spacing = 0f)
        {
            var label = CreateRect(parent, name, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.fontStyle = style;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.richText = true;
            label.raycastTarget = false;
            StyleText(label, (style & FontStyles.Bold) != 0, spacing);
            return label;
        }

        /// <summary>
        /// Applies the Paint Squad type: Lilita One for headings (bold requests), Fredoka for body text.
        /// Light text gets the dark outline material so it reads over the scene and on colored plates;
        /// dark text on cream cards stays plain. Call again after changing a label's color between light and dark.
        /// </summary>
        public static void StyleText(TMP_Text label, bool heading, float spacing = 0f)
        {
            if (label == null) return;
            var font = heading ? HeadingFont : BodyFont;
            if (font != null) label.font = font;
            var skinned = Skin != null && font != null && (font == Skin.headingFont || font == Skin.bodyFont);
            if (skinned)
            {
                // The SDF fonts are already heavy: faux bold would only blur them.
                label.fontStyle &= ~FontStyles.Bold;
                label.characterSpacing = heading ? Mathf.Clamp(spacing * 0.4f, 0f, 2.5f) + 0.5f : spacing * 0.5f;
                // Lilita One has a narrow space that the outline swallows.
                label.wordSpacing = heading ? 18f : 0f;
            }
            else label.characterSpacing = spacing;
            ApplyOutline(label, heading);
        }

        /// <summary>Picks the outlined or plain material for the label's font from its current color.</summary>
        public static void ApplyOutline(TMP_Text label, bool heading)
        {
            if (label == null || Skin == null || label.font == null) return;
            var light = IsLight(label.color);
            Material outline = null;
            if (label.font == Skin.headingFont) outline = label.fontSize < 22f ? Skin.headingOutlineThin : Skin.headingOutline;
            else if (label.font == Skin.bodyFont) outline = Skin.bodyOutline;
            label.fontSharedMaterial = light && outline != null ? outline : label.font.material;
        }

        /// <summary>Forces the dark outline material (colored text over the scene).</summary>
        public static void Outlined(TMP_Text label)
        {
            if (label == null || Skin == null || label.font == null) return;
            var outline = label.font == Skin.headingFont ? (label.fontSize < 22f ? Skin.headingOutlineThin : Skin.headingOutline)
                : label.font == Skin.bodyFont ? Skin.bodyOutline : null;
            if (outline != null) label.fontSharedMaterial = outline;
        }

        public static bool IsLight(Color color) => color.a > 0.05f && 0.299f * color.r + 0.587f * color.g + 0.114f * color.b > 0.55f;

        public static Button MakeButton(Image image)
        {
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            Chunky(image);
            var colors = button.colors;
            colors.highlightedColor = new Color(1.06f, 1.06f, 1.06f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f);
            colors.disabledColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colors;
            return button;
        }

        public static Button CreateButton(Transform parent, string name, Vector2 position, Vector2 size, Color color, float radius) =>
            MakeButton(CreateImage(parent, name, position, size, color, radius));

        /// <summary>Swaps a rounded or round image to the chunky button sprite: a glossy face over a darker drop edge.</summary>
        public static void Chunky(Image image)
        {
            if (image == null || Skin == null) return;
            if (Skin.button != null && (image.sprite == Skin.rounded || image.sprite == RoundedSprite) && image.sprite != null)
            {
                var radius = image.sprite == Skin.rounded ? SkinCornerRadius / image.pixelsPerUnitMultiplier
                    : RoundedTextureRadius / image.pixelsPerUnitMultiplier;
                image.sprite = Skin.button;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = SkinCornerRadius / Mathf.Max(1f, radius);
            }
            else if (Skin.buttonCircle != null && image.sprite == CircleSprite)
                image.sprite = Skin.buttonCircle;
        }

        /// <summary>Round chunky badge (squad and skill buttons): a disc with a darker drop edge.</summary>
        public static void RoundButton(Image image)
        {
            if (image == null) return;
            image.sprite = Skin != null && Skin.buttonCircle != null ? Skin.buttonCircle : CircleSprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
        }

        /// <summary>Swaps a rounded image to the card sprite, whose bottom edge is a little thicker and darker.</summary>
        public static void Panel(Image image)
        {
            if (image == null || Skin == null || Skin.panel == null || image.sprite != Skin.rounded) return;
            image.sprite = Skin.panel;
        }

        /// <summary>A Paint Squad card: a rim with a slightly thicker bottom edge and a cream face. Returns the face;
        /// its parent is the rim, which carries the name and the position.</summary>
        public static Image CreateCard(Transform parent, string name, Vector2 position, Vector2 size, float radius = 22f,
            Color? face = null, Color? rim = null, float rimWidth = 5f)
        {
            var frame = CreateImage(parent, name, position, size, rim ?? CardRimColor, radius);
            if (Skin != null && Skin.panel != null)
            {
                frame.sprite = Skin.panel;
                frame.pixelsPerUnitMultiplier = SkinCornerRadius / radius;
            }
            var inner = CreateImage(frame.transform, "Card Face", Vector2.zero, Vector2.zero, face ?? CardColor, Mathf.Max(2f, radius - rimWidth));
            inner.rectTransform.anchorMin = Vector2.zero;
            inner.rectTransform.anchorMax = Vector2.one;
            inner.rectTransform.offsetMin = new Vector2(rimWidth, rimWidth + Mathf.Round(radius * 0.25f));
            inner.rectTransform.offsetMax = new Vector2(-rimWidth, -rimWidth);
            return inner;
        }

        /// <summary>Soft drop shadow behind a rect, as a sibling placed before it.</summary>
        public static Image CreateShadow(RectTransform target, float spread = 22f, float drop = 12f, float alpha = 0.32f)
        {
            if (Skin == null || Skin.softShadow == null || target == null || target.parent == null) return null;
            var shadow = CreateImage(target.parent, target.name + " Shadow", target.anchoredPosition + new Vector2(0f, -drop),
                target.sizeDelta + Vector2.one * spread * 2f, new Color(0f, 0f, 0f, alpha));
            shadow.rectTransform.anchorMin = target.anchorMin;
            shadow.rectTransform.anchorMax = target.anchorMax;
            shadow.rectTransform.pivot = target.pivot;
            shadow.sprite = Skin.softShadow;
            shadow.type = Image.Type.Sliced;
            shadow.transform.SetSiblingIndex(target.GetSiblingIndex());
            return shadow;
        }

        /// <summary>Title ribbon with folded tails and an outlined Lilita One label named "Title".</summary>
        public static Image CreateRibbon(Transform parent, string name, string text, Vector2 position, Vector2 size, Color color, float fontSize = 0f)
        {
            var ribbon = CreateImage(parent, name, position, size, color);
            if (Skin != null && Skin.ribbon != null)
            {
                ribbon.sprite = Skin.ribbon;
                ribbon.type = Image.Type.Sliced;
                ribbon.pixelsPerUnitMultiplier = RibbonPixels / size.y;
            }
            else Round(ribbon, size.y * 0.2f);
            var label = CreateText(ribbon.transform, "Title", text, Vector2.zero, Vector2.zero, fontSize > 0f ? fontSize : size.y * 0.5f,
                Color.white, FontStyles.Bold, 2f);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(size.y * 0.7f, size.y * 0.2f);
            label.rectTransform.offsetMax = new Vector2(-size.y * 0.7f, 0f);
            return ribbon;
        }

        /// <summary>Full-rect beige hex paper (the Paint Squad map ground) with a soft vignette.</summary>
        public static Image CreateBackdrop(Transform parent, string name = "Paint Squad Backdrop")
        {
            var paper = CreateImage(parent, name, Vector2.zero, Vector2.zero, Skin != null && Skin.hexPaper != null ? Color.white : Paper);
            Stretch(paper.rectTransform);
            if (Skin != null && Skin.hexPaper != null)
            {
                paper.sprite = Skin.hexPaper;
                paper.type = Image.Type.Tiled;
                // Hex radius about 6.5 percent of the screen height, whatever the canvas reference size.
                paper.pixelsPerUnitMultiplier = HexPaperRadius / (0.065f * CanvasUnitsHeight(parent));
            }
            return paper;
        }

        // Hex radius in pixels inside the hex paper sprite.
        private const float HexPaperRadius = 60f;

        /// <summary>Height of a 16:9 screen in the canvas units of the canvas that holds 'child'.</summary>
        public static float CanvasUnitsHeight(Transform child)
        {
            var scaler = child != null ? child.GetComponentInParent<CanvasScaler>() : null;
            if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) return 1080f;
            var reference = scaler.referenceResolution;
            // Log-space blend between matching width and height, like CanvasScaler.
            var byWidth = reference.x * 9f / 16f;
            return Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(byWidth, 2f), Mathf.Log(reference.y, 2f), scaler.matchWidthOrHeight));
        }

        /// <summary>Dark translucent pill (HUD chip, tutorial bar).</summary>
        public static Image CreatePill(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var pill = CreateImage(parent, name, position, size, color);
            if (Skin != null && Skin.pill != null)
            {
                pill.sprite = Skin.pill;
                pill.type = Image.Type.Sliced;
                pill.pixelsPerUnitMultiplier = 128f / Mathf.Max(1f, size.y);
            }
            else Round(pill, size.y * 0.5f);
            return pill;
        }

        public static Sprite Icon(string name) => Skin != null ? Skin.Icon(name) : null;

        /// <summary>A Paint Squad icon sprite (file name without icon_), or null when the skin is missing.</summary>
        public static Image CreateIcon(Transform parent, string name, string icon, Vector2 position, float size)
        {
            var sprite = Icon(icon);
            if (sprite == null) return null;
            var image = CreateImage(parent, name, position, new Vector2(size, size), Color.white);
            image.sprite = sprite;
            image.preserveAspect = true;
            return image;
        }

        /// <summary>Restyles a scene-authored button: chunky sprite, face color and its labels.</summary>
        public static void SkinButton(Button button, Color face, float radius = 14f)
        {
            if (button == null) return;
            if (button.targetGraphic is Image image)
            {
                Round(image, radius);
                Chunky(image);
                image.color = face;
            }
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.06f, 1.06f, 1.06f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f);
            colors.disabledColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colors;
            var lightFace = IsLight(face) && face.r + face.g + face.b > 2.4f;
            foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
            {
                label.color = lightFace ? Ink : Color.white;
                label.fontStyle &= ~FontStyles.Bold;
                StyleText(label, true);
                // Lilita One is wider than the authored font: shrink to the authored box instead of spilling out.
                label.textWrappingMode = TextWrappingModes.NoWrap;
                if (!label.enableAutoSizing) { label.fontSizeMax = label.fontSize; label.fontSizeMin = label.fontSize * 0.4f; }
                label.enableAutoSizing = true;
                // Keep the label clear of the drop edge.
                label.margin = new Vector4(label.margin.x, label.margin.y, label.margin.z, Mathf.Max(label.margin.w, 4f));
            }
        }

        /// <summary>Restyles a scene-authored slider: pill track, gold fill and a round white knob.</summary>
        public static void SkinSlider(Slider slider)
        {
            if (slider == null || Skin == null) return;
            foreach (var image in slider.GetComponentsInChildren<Image>(true))
            {
                if (slider.handleRect != null && image.transform == slider.handleRect)
                {
                    image.sprite = Skin.buttonCircle != null ? Skin.buttonCircle : Skin.circle;
                    image.type = Image.Type.Simple;
                    image.color = Color.white;
                    image.preserveAspect = true;
                }
                else if (slider.fillRect != null && image.transform == slider.fillRect)
                {
                    image.sprite = Skin.pill;
                    image.type = Image.Type.Sliced;
                    image.pixelsPerUnitMultiplier = 128f / Mathf.Max(4f, image.rectTransform.rect.height);
                    image.color = Gold;
                }
                else
                {
                    image.sprite = Skin.pill;
                    image.type = Image.Type.Sliced;
                    image.pixelsPerUnitMultiplier = 128f / Mathf.Max(4f, image.rectTransform.rect.height);
                    image.color = new Color(Ink.r, Ink.g, Ink.b, 0.78f);
                }
            }
        }

        private static Sprite RoundedSprite => rounded != null ? rounded : (rounded = CreateRoundedSprite());
        private static Sprite CircleSprite => Skin != null && Skin.circle != null ? Skin.circle : circle != null ? circle : (circle = CreateCircleSprite());

        private static Sprite CreateRoundedSprite()
        {
            const int size = RoundedTextureSize;
            const float radius = RoundedTextureRadius;
            const float half = size * 0.5f;
            const float inner = half - radius;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "RK Rounded Panel",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - inner, 0f);
                    var dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - inner, 0f);
                    var alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), RoundedPixelsPerUnit, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        private static Sprite CreateCircleSprite()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "RK Circle",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var center = Vector2.one * (size * 0.5f);
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    var alpha = Mathf.Clamp01(size * 0.5f - distance + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size, 0, SpriteMeshType.FullRect);
        }
    }
}
