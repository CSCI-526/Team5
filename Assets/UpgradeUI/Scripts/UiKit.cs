using ReverseTD.Defense;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ReverseTD.UpgradeUI
{
    public static class UiKit
    {
        public const string CostColorTag = "#FFD93D";

        public static readonly Color PanelColor = new Color(0.11f, 0.14f, 0.21f);
        public static readonly Color PanelLineColor = new Color(0.3f, 0.36f, 0.46f);
        public static readonly Color CellColor = new Color(0.07f, 0.09f, 0.14f);
        public static readonly Color TextColor = new Color(0.92f, 0.94f, 0.97f);
        public static readonly Color MutedColor = new Color(0.62f, 0.67f, 0.75f);
        public static readonly Color GoldColor = new Color(1f, 0.88f, 0.25f);
        public static readonly Color GoldDarkColor = new Color(0.55f, 0.4f, 0.06f);
        public static readonly Color NodeGreenColor = new Color(0.18f, 0.65f, 0.22f);
        public static readonly Color NodeGreyColor = new Color(0.25f, 0.25f, 0.25f);
        public static readonly Color NodeBlueColor = new Color(0.15f, 0.35f, 0.7f);
        public static readonly Color DropColor = new Color(0.35f, 0.85f, 0.45f);
        public static readonly Color LockColor = new Color(0.86f, 0.88f, 0.92f);

        private const int RoundedTextureSize = 64;
        private const float RoundedTextureRadius = 16f;
        private const float RoundedPixelsPerUnit = 100f;

        private static Sprite rounded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            rounded = null;
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
            Image image = CreateRect(parent, name, position, size).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (radius > 0f)
            {
                Round(image, radius);
            }
            return image;
        }

        public static Image CreateCircle(Transform parent, string name, Vector2 position, float diameter, Color color)
        {
            Image image = CreateImage(parent, name, position, new Vector2(diameter, diameter), color);
            image.sprite = ShapeSprites.Circle;
            return image;
        }

        public static void Round(Image image, float radius)
        {
            image.sprite = RoundedSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = RoundedTextureRadius / radius;
        }

        public static TextMeshProUGUI CreateText(Transform parent, string name, string text, Vector2 position, Vector2 size, float fontSize,
            Color color, FontStyles style = FontStyles.Normal, float spacing = 0f)
        {
            TextMeshProUGUI label = CreateRect(parent, name, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null)
            {
                label.font = TMP_Settings.defaultFontAsset;
            }
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.fontStyle = style;
            label.characterSpacing = spacing;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.richText = true;
            label.raycastTarget = false;
            return label;
        }

        public static Button MakeButton(Image image)
        {
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        public static Button CreateButton(Transform parent, string name, Vector2 position, Vector2 size, Color color, float radius)
        {
            return MakeButton(CreateImage(parent, name, position, size, color, radius));
        }

        public static void Connect(Transform parent, RectTransform from, RectTransform to, float thickness, Color color)
        {
            var line = new GameObject("Line", typeof(RectTransform));
            line.transform.SetParent(parent, false);
            line.transform.SetAsFirstSibling();
            var connector = line.AddComponent<UILineConnector>();
            line.GetComponent<Image>().raycastTarget = false;
            connector.Connect(from, to, thickness, color);
        }

        public static RectTransform CreateLock(Transform parent, Vector2 position, float scale)
        {
            RectTransform root = CreateRect(parent, "Lock", position, new Vector2(30f, 38f) * scale);
            CreateImage(root, "Shackle Left", new Vector2(-9f, 8f) * scale, new Vector2(5f, 16f) * scale, LockColor);
            CreateImage(root, "Shackle Right", new Vector2(9f, 8f) * scale, new Vector2(5f, 16f) * scale, LockColor);
            CreateImage(root, "Shackle Top", new Vector2(0f, 15f) * scale, new Vector2(23f, 5f) * scale, LockColor, 2.5f * scale);
            CreateImage(root, "Body", new Vector2(0f, -5f) * scale, new Vector2(30f, 22f) * scale, LockColor, 4f * scale);
            CreateCircle(root, "Keyhole", new Vector2(0f, -4f) * scale, 6.5f * scale, PanelColor);
            return root;
        }

        public static RectTransform CreateCoin(Transform parent, Vector2 position, float radius)
        {
            RectTransform root = CreateRect(parent, "Coin", position, new Vector2(radius, radius) * 2f);
            CreateCircle(root, "Rim", Vector2.zero, radius * 2f, GoldDarkColor);
            CreateCircle(root, "Face", Vector2.zero, radius * 1.72f, GoldColor);
            CreateText(root, "Sign", "$", Vector2.zero, new Vector2(radius, radius) * 2f, radius * 1.15f, GoldDarkColor, FontStyles.Bold);
            return root;
        }

        private static Sprite RoundedSprite
        {
            get
            {
                if (rounded == null)
                {
                    rounded = CreateRoundedSprite();
                }
                return rounded;
            }
        }

        private static Sprite CreateRoundedSprite()
        {
            int size = RoundedTextureSize;
            float radius = RoundedTextureRadius;
            float half = size * 0.5f;
            float inner = half - radius;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "RK Rounded Panel",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - inner, 0f);
                    float dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - inner, 0f);
                    float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), RoundedPixelsPerUnit, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }
    }
}
